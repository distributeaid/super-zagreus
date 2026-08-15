# CI & coverage

CI runs on every pull request (and on pushes to `main` to refresh the baseline).
It runs both test suites with coverage and enforces two gates. The same checks run
locally through root `yarn` scripts — no drift between "what you see" and "what CI enforces".

## Gates

- **Patch coverage** — changed lines must be ≥ `patchMin` (default **99%**). This is the
  primary signal: new code ships with tests.
- **Total floors** — each suite's overall line coverage must stay ≥ its floor
  (`webTotalMin`, `apiTotalMin`), seeded from the measured baseline.

All three thresholds live in [`coverage.config.json`](../coverage.config.json). Change them there.

## Run it locally

```bash
yarn coverage        # run both suites with coverage → apps/*/coverage/lcov.info
git fetch origin main
yarn coverage:diff   # patch gate + floors; writes coverage-diff.json + coverage-report.md
```

`coverage-diff.json` is the machine-readable result an agent reads to self-check before
finishing — identical to what the CI gate decides. Exit code is non-zero if any gate fails.

## Adjusting thresholds

Edit `coverage.config.json`. Raising floors as coverage climbs is a manual, deliberate step.
