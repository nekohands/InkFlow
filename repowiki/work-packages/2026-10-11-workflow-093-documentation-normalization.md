# Workflow 0.93.0 documentation normalization

Audience: Agent-only.
Agent read instruction: Read this page when resuming the 2026-10-11 workflow adoption or checking documentation conformance; then read the current Wiki index, profile, and full adoption record.
Status: Accepted
ID: init-2026-10-11
Owner: InkFlow maintainers
Authorization: user-specified `project-delivery-init` with continuation request; documentation-only scope
Record mode: FULL — cross-set documentation migration and entry-gate normalization
Parent/root index: [Work packages](README.md); [RepoWiki](../README.md)

## Sources

- [Repository instructions](../../AGENTS.md)
- [Full adoption record](../../docs/delivery/adoption-2026-10-02.md)
- [Workflow profile](../../docs/delivery/PROJECT_WORKFLOW.md)
- [Docs index](../../docs/README.md)
- [RepoWiki index](../README.md)

## Objective and boundary

Make the repository conform to workflow 0.93.0 before any new application
package is admitted. Preserve the accepted 5.81 implementation/evidence and
the user-owned `.workbuddy-ai/` boundary.

In scope: RepoWiki Agent metadata and source citations, hierarchical Wiki
catalog/metadata, docs root and populated-directory indexes, profile/adoption
state, repository instruction pointers, and local link/structure checks.

Non-goals: application source, tests, dependencies, migrations, runtime,
architecture decisions, new product behavior, or selection of the next feature.

## Acceptance traceability

- `DOC-1`: one profile remains under `docs/delivery/PROJECT_WORKFLOW.md`; no
  second profile or parallel Agent authority is introduced.
- `DOC-2`: all 31 RepoWiki Markdown pages have `Audience: Agent-only` and an
  explicit `Agent read instruction`; topic pages have Sources/Evidence; the
  generated catalog and metadata are current.
- `DOC-3`: `docs/README.md` and every populated docs directory index its actual
  maintained pages, while docs remain capability/delivery output.
- `DOC-4`: 5.81 Accepted SHA/evidence, current/history records, blockers, and
  `.workbuddy-ai/` disposition remain preserved; no application file changes.
- `DOC-5`: RepoWiki generator check, generator self-test, and docs/RepoWiki
  local-link check pass.
- `DOC-6`: post-normalization conformance is `COMPLIANT`; next state is
  `Intake required`, not automatic application implementation.

## Migration and evidence

- Before: `NORMALIZATION_REQUIRED` because the existing Wiki pages lacked the
  0.93.0 Agent metadata/source/catalog requirements and docs lacked hierarchical
  root/directory indexes.
- Map and dispositions: the full table and retained/excluded reasons are in
  [the adoption record](../../docs/delivery/adoption-2026-10-02.md).
- Executed: `node .../generate-repowiki-index.js --root repowiki --write`;
  generated 31 pages and `repowiki/meta/repowiki-metadata.json`.
- `DOC-5` evidence: catalog `--check` PASS; `test-repowiki-index.js` PASS;
  local Markdown-link check PASS; `git diff --check` PASS (Git only emitted
  existing LF/CRLF normalization warnings).
- Application Build/Test/Runtime/CI: `N/A` for this init-only documentation
  package; no application implementation was authorized or changed.

## Result

`RepoWiki sync: PASS`. Workflow conformance is `COMPLIANT` as of 2026-10-11.
No active application work package exists; a fresh standard or `grillme` intake
must define the next package before implementation.
