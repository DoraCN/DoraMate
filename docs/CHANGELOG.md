# Changelog

## [0.10.0] - 2026-07-06

### Added
- **Advanced Arrow coverage for C# bindings**: DoraNode and DoraOperator now cover Union, FixedSizeBinary, Duration, and Interval types across schema validation, assertions/projectors, contract samples, and regression tests.
- **Release package frontend hosting**: LocalAgent now serves the bundled `frontend/` directory from the release ZIP, with SPA fallback to `index.html`.
- **DoraMate layout sidecar flow**: Example docs now use `test.yml` plus `test.yml.layout.json` instead of the removed `xydataflow.yml` file.

### Changed
- **Version bump**: All active release components are aligned to v0.10.0, including LocalAgent, Frontend, DoraNode, DoraOperator, and DoraMate.Templates.
- **C# NuGet release posture**: `DoraMate.DoraNode`, `DoraMate.DoraOperator`, and `DoraMate.Templates` are source/version-ready for v0.10.0 packaging. Public nuget.org publication remains a separate publish step that requires `NUGET_API_KEY`; the last confirmed public SDK packages are v0.9.0.
- **Release scripts**: `build-release.ps1`, `package-zip.ps1`, and `package-installer.nsi` now use the current LocalAgent output path and handle the current PowerShell/Trunk `NO_COLOR` environment behavior.

### Fixed
- **ZIP startup experience**: The ZIP package now includes `bin/doramate-localagent.exe` from the actual `doramate-localagent/target/release/` output and serves the Web UI at `http://127.0.0.1:52100`.
- **Example build preservation**: Imported Dora YAML `build` commands are preserved by the frontend converter so LocalAgent-generated runtime YAML keeps C# and Rust build steps.

### Validation
- Local release gate passed on 2026-07-06: 20/20 rounds, 100% run/status/stop success, zero residual failures.
- Release build passed with `pwsh ./scripts/build-release.ps1 -SkipTests`.
- ZIP extraction smoke passed: `/api/health` returned `status=ok`, version `0.10.0`; `/` and the frontend JS asset returned HTTP 200 from the bundled LocalAgent.

## [0.9.0] - 2026-05-29

### Added
- **LocalAgent residual diagnosis**: New `/api/diagnose` endpoint, `--cleanup`/`--diagnose`/`--force-kill` CLI modes, and startup self-check for port conflicts and residual processes.
- **C# SDK productization**: NuGet packages (`DoraMate.DoraNode`, `DoraMate.DoraOperator`) and `dotnet new` templates (`dora-node`, `dora-operator`).
- **E2E regression test suite**: Pester 5 test framework with 12 test scenarios across P0/P1/P2 priorities. Integrated into PR gate and nightly CI.
- **Package scripts**: `build-release.ps1` one-click build, `package-zip.ps1` ZIP distribution, NSIS installer script.
- **GitHub Release workflow**: Automatic build and packaging on `v*` tag push.
- **Trend summary with gate-green readiness**: `summarize-local-runtime-trends.ps1` now includes a `gate_green_readiness` assessment block.
- **E2E P0 pre-check in standard release gate**: Release gate workflow now runs E2E P0 tests before executing smoke rounds.

### Changed
- **Version bump**: All components synced to v0.9.0 (LocalAgent, Frontend, C# SDK).
- **Standard release gate CI**: Added `e2e-precheck` job and trend-summary generation step.
- **Multi-dataflow smoke**: Baseline profile covers 3 samples; standard profile covers 5.

### Fixed
- Standard release gate stabilized at 100% pass rate across 3 consecutive 50-round runs (190 cumulative rounds, zero failures).

### Infrastructure
- `VERSION` file introduced as single source of truth for version numbers.
- `scripts/start-doramate.cmd` and `scripts/stop-doramate.cmd` for end-user launcher.

## [0.2.0] - 2026-04-18

### Added
- Initial open-flow state machine and YAML converter for `run / status / stop` lifecycle.
- LocalAgent HTTP API and WebSocket status/log streaming.
- PR gate with unit tests and live smoke (3 rounds).
- Standard release gate (20-50 rounds, manual trigger).
- Multi-dataflow smoke runner (baseline profile).
- Trend summary generator.
- C# Dora node/operator bindings (DoraNode, DoraOperator).
- Sample dataflows: csharp-dataflow, csharp-multi-node, csharp-async-node, csharp-arrow-node, csharp-operator.
- Frontend (Leptos CSR WASM) with visual dataflow editor.
- CLI diagnosis tool (`doramate-localagent --diagnose`).

### Infrastructure
- CI/CD: PR gate, standard release gate, E2E nightly, multi-dataflow smoke, trend summary.
- Scripts: release gate runner, smoke tester, trend summarizer.
