# Migration checklist from upstream dora/apis

This repository re-implements upstream `apis/csharp` behavior by depending on upstream Dora source at build time.

## Upstream paths used directly

- `apis/c/node`
- `apis/c/operator`
- `apis/rust/node`
- `apis/rust/operator/types`

## Not copied into this repo

- Upstream full `apis/csharp` implementation (we re-write locally under `src/`).
- Upstream Rust workspace internals under `libraries/*` (resolved from the vendored `third_party/dora` snapshot).

## Parity targets

1. DoraNode API parity (event loop, sync/async read, Arrow bridge).
2. DoraOperator API parity (native ABI bridge + send output helpers).
3. examples parity under `samples/`.
4. smoke parity via scripts.
