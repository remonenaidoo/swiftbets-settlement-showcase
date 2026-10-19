# swiftbets-settlement

[![ci](https://github.com/remonenaidoo/swiftbets-settlement-showcase/actions/workflows/ci.yml/badge.svg)](https://github.com/remonenaidoo/swiftbets-settlement-showcase/actions/workflows/ci.yml)

Settlement for SwiftBets: indexes open legs by fixture, evaluates legs when results arrive (priority-gated by result version), counts resolved legs with token-guarded Lua counters in Redis, and settles coupons in a second stage off Kafka. A reconciler repairs Redis/SQL divergence and raises stuck coupons; unprocessable messages park on an inbound DLQ while the partition keeps flowing.

## Hosts

- `SwiftBets.Settlement.Worker`: consumers, reconciler and the operator refresh command.
- `SwiftBets.Settlement.Migrator`: one-shot DbUp migrator.

## Data and events

- **Owns:** SQL Server `SbSettlement` (leg index, results, evaluations, settlements, outbox, inbox) and Redis (progress counters and token sets).
- **Events:** Consumes `placement.coupon-placed`, `offer.result-published`, `settlement.leg-evaluated`; produces `settlement.leg-evaluated`, `settlement.coupon-settled`, `settlement.stuck-coupon`.

## Layout

Clean Architecture, enforced by project references and `*.ArchitectureTests`:

```
src/*.Domain          pure domain, no references
src/*.Application     use cases and ports; depends on Domain and contracts only
src/*.Infrastructure  adapters (Dapper + embedded .sql, Kafka, Redis); implements Application ports
src/*.Api | *.Worker  composition root: observability, error envelope, health, metrics
src/*.Migrator        DbUp scripts under Migrations/, run once before the host starts
```

Every host exposes `/health/live`, `/health/ready` (checks its real dependencies), `/metrics` (Prometheus), logs compact JSON with correlation ids, and exports traces over OTLP.

## Build and test

```bash
scripts/fetch-shared-packages.sh .   # the shared packages, from public releases
dotnet test SwiftBets.Settlement.slnx
```

Integration tests use Testcontainers and need Docker.

## Part of SwiftBets

SwiftBets is a sportsbook and casino platform built as independent .NET services. It runs live at [swiftbets.remonenaidoo.com](https://swiftbets.remonenaidoo.com). These repositories show a selection of it: [Wallet](https://github.com/remonenaidoo/swiftbets-wallet-showcase) · [Originals](https://github.com/remonenaidoo/swiftbets-originals-showcase) · [Steward](https://github.com/remonenaidoo/swiftbets-steward-showcase) · [Building blocks](https://github.com/remonenaidoo/swiftbets-building-blocks-showcase) · [Contracts](https://github.com/remonenaidoo/swiftbets-contracts-showcase).

## License

MIT
