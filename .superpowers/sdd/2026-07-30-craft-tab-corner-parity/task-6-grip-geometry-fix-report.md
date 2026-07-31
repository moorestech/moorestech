# Task 6 grip geometry fix report

## Status

DONE_WITH_CONCERNS. The allowed single size and inset tokens cannot emit the authoritative detector geometry at this renderer scale. The best accepted state is restored unchanged: `--craft-grip-size: 9.2px` and `--craft-grip-inset: 7px`.

## RED

Fresh production build, 1635x922 CSS viewport capture (3270x1844 output), and comparator reproduced the stated issue:

| metric | reference | current |
| --- | ---: | ---: |
| detected bbox | 22x22 | 23x22 |
| right gap | 19 | 20 |
| bottom gap | 19 | 20 |
| face median | rgb(132 133 149) | rgb(132 133 149) |

The initial comparator was 13/13 because its geometry tolerance is one pixel. This task uses the stricter exact target above.

## One-token iteration evidence

Only `--craft-grip-size` changed during size trials, with inset fixed at `7px`. Every row was rebuilt, captured, and compared.

| size token | detected bbox | gaps | comparator |
| --- | --- | --- | --- |
| 8.70–8.79px, each 0.01px | 22x21 | 20/20 | 13/13 |
| 8.80px | 23x22 | 20/20 | 13/13 |
| 9.10px | 23x22 | 20/20 | 13/13 |
| 9.19px | 23x22 | 20/20 | 13/13 |
| 9.20px | 23x22 | 20/20 | 13/13 |

Only `--craft-grip-inset` changed during inset trials, with the closest-height size `8.80px` fixed.

| inset token | detected bbox | gaps | comparator |
| --- | --- | --- | --- |
| 7.00px | 23x22 | 20/20 | 13/13 |
| 6.99px, 6.90px, 6.80px | 22x21 | 20/20 | 13/13 |
| 6.69px | 21x21 | 20/20 | 13/13 |
| 6.68–6.63px, each 0.01px | 21x20 | 20/20 | 12/13, rejected |
| 6.62–6.50px | 20x20 | 20/20 | 12/13, rejected |

The exact target is therefore unavailable over the observed consecutive 0.01px transition intervals: the size raster jumps from 22x21 to 23x22, while lower inset values shrink the component and the detector's right/bottom edges remain at 20px gaps. Adding independent width/height or x/y tokens would violate the settled scope, so no such token was added.

## GREEN and contracts

The final restored token pair remains the contract's measured value: authored `9.2px`, Chromium computed width/height `9.1875px`, `right: 7`, and `bottom: 7`. The shared pseudo-element continues to have exactly one single-color triangle, `backgroundImage: none`, and `boxShadow: none`.

Final fresh comparator result: 13/13 PASS. It reports `23x22`, gaps `20/20`, and grip face median `rgb(132 133 149)` with max delta 0.

## Commands and results

```text
cd moorestech_web/webui
pnpm build
# PASS: tsc -b && vite build

CAPTURE_VIEWPORT_W=1635 CAPTURE_VIEWPORT_H=922 \
  CAPTURE_OUT=/tmp/webui-craft-current-task6-final.png \
  pnpm exec tsx e2e/capture-eval.ts
# PASS: 3270x1844 capture

/tmp/webui-craft-qa-venv/bin/python e2e/craft-chrome/compare.py \
  --ref ../../docs/webui-parity/reference-player-inventory-3270x1844.png \
  --cur /tmp/webui-craft-current-task6-final.png \
  --out /tmp/webui-craft-chrome-task6-final
# PASS: 13/13

pnpm exec playwright test --config e2e/playwright.config.ts \
  e2e/tests/recipe.spec.ts \
  e2e/tests/modeHud/operation-mode-hud.spec.ts \
  e2e/tests/research.spec.ts
# PASS: 12/12; central, PlacementModeHud, and ResearchDetailPane contracts covered

pnpm exec tsc -p e2e/tsconfig.json --noEmit
# PASS
```

## Changed files and self-review

- `docs/webui-parity/iteration-log.md`: durable iteration evidence and final concern.
- This report: RED/GREEN evidence for the task handoff.

No source token or E2E assertion change is retained because the measured best candidate is the already-authored `9.2px`/`7px` pair and its expected computed values already exactly matched. Reviewed the final diff for scope: no tab, panel-color, pseudo-element structure, gradient, shadow, consumer CSS, C#, Unity YAML, or meta change is present. `git diff --check` passes.

## Concern

The strict exact bbox and gap target is blocked by Chromium raster quantization plus the existing frame-boundary detector behavior under the allowed two-token model. The tolerated visual comparator and all shared-consumer contracts remain green; resolving exact geometry needs a user-approved expansion of the geometry model or detector constraints.
