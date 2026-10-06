# Decisions

Scope: accepted architecture decisions (ADR). One decision per file; new work
that changes an accepted direction must add an ADR before implementation.

Parent/root index: [../README.md](../README.md)
Human/authoritative home: [docs/adr/](../../docs/adr/) — Chinese records `0001`–`0028`.

## Reading guide

- Foundation: `0001` modular monolith, `0002` content/source model, `0003`
  Legado compatibility, `0004` infrastructure.
- Source/health/DSL: `0005` health parameters, `0015` credential owner scope,
  `0023` bounded pre-requests, `0027` source lifecycle disable.
- Messaging: `0016` PostgreSQL outbox relay, `0017` inbox polling, `0018`
  failure policy, `0019` `crawler.task.created` handler, `0020` dead-letter
  operations observation.
- Content: `0022` canonical read reselection; Reading/product: `0006`, `0024`,
  `0025`, `0026`, `0028` reader collection/package access boundary.
- Commercial/observability/ops: `0009`, `0010`–`0013` Core SLO, `0012` OTel
  collector baseline, `0014` audit retention, `0021` collection run control and
  book packages, `0007`/`0008` private library boundary.

Current ADR list is the directory listing of [docs/adr/](../../docs/adr/);
this index only groups them for navigation and does not restate their content.
