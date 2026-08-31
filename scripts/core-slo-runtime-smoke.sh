#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_SLO_SMOKE_BASE_URL:-http://localhost:8080}}"
probe_count="${INKFLOW_SLO_PROBE_COUNT:-5}"
max_time="${INKFLOW_SLO_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_SLO_CURL_BIN:-curl}"
evidence_directory="${RUNNER_TEMP:-${TMPDIR:-/tmp}}"
if [[ -n "${INKFLOW_SLO_EVIDENCE_FILE:-}" ]]; then
  evidence_file="$INKFLOW_SLO_EVIDENCE_FILE"
else
  mkdir -p -- "$evidence_directory"
  evidence_file="$(mktemp "$evidence_directory/inkflow-core-slo-evidence.XXXXXX.json")"
fi
probe_dir=""
output_tmp=""

fail() {
  printf 'core-slo-runtime-smoke: %s\n' "$1" >&2
  exit 1
}

latency_target_for_surface() {
  case "$1" in
    public_api|developer_api)
      printf '750\n'
      ;;
    legado_api|reader)
      printf '1000\n'
      ;;
    *)
      fail "unknown Core SLO surface: $1"
      ;;
  esac
}

case "$base_url" in
  http://*|https://*) ;;
  *) fail 'base URL must use http or https' ;;
esac

case "$base_url" in
  *[[:space:]#?]*) fail 'base URL must not contain whitespace, a fragment, or a query' ;;
esac

base_url="${base_url%/}"
case "$base_url" in
  http://|https://) fail 'base URL must include a host' ;;
esac

if ! [[ "$probe_count" =~ ^[1-9][0-9]*$ ]] || (( probe_count > 20 )); then
  fail 'INKFLOW_SLO_PROBE_COUNT must be an integer from 1 to 20'
fi

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_SLO_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! command -v "$curl_bin" >/dev/null 2>&1; then
  fail "curl executable not found: $curl_bin"
fi

if [[ -z "$evidence_file" || "$evidence_file" == */ ]]; then
  fail 'INKFLOW_SLO_EVIDENCE_FILE must be a non-empty file path'
fi

output_parent="$(dirname -- "$evidence_file")"
mkdir -p -- "$output_parent"

cleanup() {
  local status=$?
  set +e
  if [[ -n "$probe_dir" ]]; then
    rm -rf -- "$probe_dir"
  fi
  if [[ -n "$output_tmp" ]]; then
    rm -f -- "$output_tmp"
  fi
  exit "$status"
}
trap cleanup EXIT

probe_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-slo-probe.XXXXXX")"
output_tmp="$(mktemp "$output_parent/.inkflow-core-slo-evidence.XXXXXX")"

probe_surface() {
  local surface="$1"
  local path="$2"
  local expected_status="$3"
  local attempt result status duration sorted rank p95_seconds p95_milliseconds target
  local warmup_result error_file
  local -a durations=()

  printf 'Core SLO probe: %s (%s, expected HTTP %s, %s request(s))\n' \
    "$surface" "$path" "$expected_status" "$probe_count"

  # A freshly started application may spend its first request warming JIT,
  # database pools and serializers. Keep that deployment artifact out of the
  # steady-state synthetic window while still validating its status code.
  error_file="$probe_dir/$surface-warmup.err"
  if ! warmup_result="$("$curl_bin" \
    --silent --show-error \
    --output /dev/null \
    --write-out $'%{http_code}\t%{time_total}' \
    --connect-timeout "$max_time" \
    --max-time "$max_time" \
    -- "$base_url$path" 2>"$error_file")"; then
    fail "$surface warm-up request failed (transport or timeout)"
  fi
  if [[ "$warmup_result" != *$'\t'* ]]; then
    fail "$surface warm-up request returned malformed curl timing output"
  fi
  status="${warmup_result%%$'\t'*}"
  if [[ "$status" != "$expected_status" ]]; then
    fail "$surface warm-up request returned HTTP $status; expected HTTP $expected_status"
  fi

  for ((attempt = 1; attempt <= probe_count; attempt++)); do
    error_file="$probe_dir/${surface}-${attempt}.err"
    if ! result="$("$curl_bin" \
      --silent --show-error \
      --output /dev/null \
      --write-out $'%{http_code}\t%{time_total}' \
      --connect-timeout "$max_time" \
      --max-time "$max_time" \
      -- "$base_url$path" 2>"$error_file")"; then
      fail "$surface request $attempt failed (transport or timeout)"
    fi

    if [[ "$result" != *$'\t'* ]]; then
      fail "$surface request $attempt returned malformed curl timing output"
    fi

    status="${result%%$'\t'*}"
    duration="${result#*$'\t'}"
    if [[ "$status" != "$expected_status" ]]; then
      fail "$surface request $attempt returned HTTP $status; expected HTTP $expected_status"
    fi
    if ! [[ "$duration" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
      fail "$surface request $attempt returned invalid timing output"
    fi

    durations+=("$duration")
  done

  sorted="$(printf '%s\n' "${durations[@]}" | sort -n)"
  rank=$(( (probe_count * 95 + 99) / 100 ))
  p95_seconds="$(printf '%s\n' "$sorted" | awk -v rank="$rank" 'NR == rank { print; exit }')"
  if [[ -z "$p95_seconds" ]]; then
    fail "$surface did not produce a p95 timing sample"
  fi
  p95_milliseconds="$(awk -v value="$p95_seconds" 'BEGIN { printf "%.3f", value * 1000 }')"
  target="$(latency_target_for_surface "$surface")"
  if ! awk -v measured="$p95_milliseconds" -v target="$target" \
    'BEGIN { exit !(measured >= 0 && measured <= target) }'; then
    fail "$surface p95 ${p95_milliseconds}ms exceeds Core SLO target ${target}ms"
  fi

  PROBE_P95_MILLISECONDS="$p95_milliseconds"
  printf 'Core SLO probe: %s PASS (p95=%sms, target<=%sms)\n' \
    "$surface" "$p95_milliseconds" "$target"
}

window_start="$(date -u +'%Y-%m-%dT%H:%M:%S.%3NZ')"

# These routes are deliberately fixed and bounded. The empty Legado query must
# not discover or fetch a real source, and the Developer route intentionally
# proves the unauthenticated authorization boundary with a good 401 response.
probe_surface public_api '/api/v1/books' 200
public_api_p95="$PROBE_P95_MILLISECONDS"
probe_surface legado_api '/api/legado/v1/search?q=' 200
legado_api_p95="$PROBE_P95_MILLISECONDS"
probe_surface developer_api '/api/developer/v1/books' 401
developer_api_p95="$PROBE_P95_MILLISECONDS"
probe_surface reader '/reader' 200
reader_p95="$PROBE_P95_MILLISECONDS"

window_end="$(date -u +'%Y-%m-%dT%H:%M:%S.%3NZ')"

printf '{\n' > "$output_tmp"
printf '  "schemaVersion": 1,\n' >> "$output_tmp"
printf '  "evidenceSource": "ci-core-slo-runtime-smoke",\n' >> "$output_tmp"
printf '  "windowStart": "%s",\n' "$window_start" >> "$output_tmp"
printf '  "windowEnd": "%s",\n' "$window_end" >> "$output_tmp"
printf '  "surfaces": {\n' >> "$output_tmp"
printf '    "public_api": {"requestCount": %s, "serverErrorCount": 0, "durationSampleCount": %s, "p95LatencyMilliseconds": %s},\n' \
  "$probe_count" "$probe_count" "$public_api_p95" >> "$output_tmp"
printf '    "legado_api": {"requestCount": %s, "serverErrorCount": 0, "durationSampleCount": %s, "p95LatencyMilliseconds": %s},\n' \
  "$probe_count" "$probe_count" "$legado_api_p95" >> "$output_tmp"
printf '    "developer_api": {"requestCount": %s, "serverErrorCount": 0, "durationSampleCount": %s, "p95LatencyMilliseconds": %s},\n' \
  "$probe_count" "$probe_count" "$developer_api_p95" >> "$output_tmp"
printf '    "reader": {"requestCount": %s, "serverErrorCount": 0, "durationSampleCount": %s, "p95LatencyMilliseconds": %s}\n' \
  "$probe_count" "$probe_count" "$reader_p95" >> "$output_tmp"
printf '  }\n}\n' >> "$output_tmp"

mv -f -- "$output_tmp" "$evidence_file"
output_tmp=""
printf 'core-slo-runtime-smoke: PASS (evidence=%s)\n' "$evidence_file"
