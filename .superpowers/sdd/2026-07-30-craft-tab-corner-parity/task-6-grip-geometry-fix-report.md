# Task 6 grip geometry fix report

## Status

DONE_WITH_CONCERNS. Round 1 corrects the original unqualified unavailable claim: the measured plan interval cannot emit the authoritative detector geometry, but this is not asserted as an exhaustive proof over every possible CSS size. The best accepted state remains unchanged: `--craft-grip-size: 9.2px` and `--craft-grip-inset: 7px`.

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

The observed size interval has no exact target: the size raster jumps from 22x21 to 23x22, while lower inset values shrink the component and the detector's right/bottom edges remain at 20px gaps. Adding independent width/height or x/y tokens would violate the settled scope, so no such token was added. Round 1 below verifies that this is raw renderer behavior, not a comparator false exclusion.

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

The strict exact bbox and gap target is blocked in the measured single-size interval by Chromium rasterization under the allowed two-token model. The tolerated visual comparator and all shared-consumer contracts remain green; resolving exact geometry needs a user-approved expansion of the geometry model.

## Review fix round 1 — detector investigation

### Raw mask, post-filter, and DOM evidence

No CSS source was changed before this diagnosis. For each image, the raw grip color mask is the comparator mask (`channel spread <35`, mean 70–190), and components use the production comparator's radius-one connectivity.

| image / tokens | raw grip bbox, count | `touches_frame` | post-filter bbox | result |
| --- | --- | --- | --- | --- |
| reference | `(2030,1364)-(2051,1385)`, 304 | false | 22x22 | target |
| current 9.20/7.00 | `(2028,1397)-(2050,1418)`, 274 | false | 23x22 | source mismatch |
| inset 6.99 | `(2029,1398)-(2050,1418)`, 252 | false | 22x21 | raw transition |
| inset 6.90 | `(2029,1398)-(2050,1418)`, 252 | false | 22x21 | raw transition |
| inset 6.80 | `(2029,1398)-(2050,1418)`, 232 | false | 22x21 | raw transition |
| inset 6.69 | `(2030,1398)-(2050,1418)`, 231 | false | 21x21 | raw transition |
| inset 6.68 | `(2030,1399)-(2050,1418)`, 230 | false | 21x20 | rejected 12/13 |

Every selected grip passes the post-filter unchanged. A separate 70x70 current mask component is also not frame-marked, but its count (139) is below the grip count and therefore it is never selected. `touches_frame` does not discard the true grip and no comparator change is warranted.

Playwright's central craft panel rect was `[604.2839,166.7115,1034.9882,719.7614]` CSS px. Its calculated pseudo-element boxes demonstrate the source movement before image detection: `9.2/7` has `9.1875px` width/height and `[1018.8007,703.5739,1027.9882,712.7614]`; `8.8/6.99` has `8.79688px` and `[1019.2013,703.9745,1027.9982,712.7714]`; `8.8/6.69` has `[1019.5013,704.2745,1028.2982,713.0714]`. Thus the inset moves the DOM box, while antialiasing changes the raw thresholded shape before filtering.

### Formerly unmeasured size interval

Chromium computed-style measurement reduced the missing tokens to 21 unique 1/64px widths. Each was runtime-captured after a fresh production build and passed `compare.py` 13/13 with raw/post-filter `23x22`, gaps `20/20`:

| unique representatives | computed widths |
| --- | --- |
| 8.82, 8.83, 8.85, 8.86, 8.88 | 8.8125–8.875px |
| 8.91, 8.93, 8.94, 8.96, 8.97, 8.99 | 8.90625–8.98438px |
| 9.00, 9.02, 9.04, 9.05, 9.07 | 9.00000–9.0625px |
| 9.11, 9.13, 9.15, 9.16, 9.18 | 9.10938–9.17188px |

The unmeasured aliases `8.81`, `8.84`, `8.87`, `8.89`, `8.92`, `8.95`, `8.98`, `9.01`, `9.03`, `9.06`, `9.08`, `9.09`, `9.12`, `9.14`, and `9.17` have the same computed width as an adjacent listed representative, so no distinct renderer input remained. Combined with the prior 8.70–8.80 and 9.10/9.19/9.20 captures, the measured bounded interval is 8.70–9.20px; it is deliberately not called exhaustive.

### Commands and GREEN

```text
pnpm build
# PASS

# Playwright runtime probes: read calculated panel/pseudo boxes and 1/64px width groups.
# 21 unique computed-width screenshots were then captured at 1635x922 CSS px / dSF2.

/tmp/webui-craft-qa-venv/bin/python e2e/craft-chrome/compare.py \
  --ref ../../docs/webui-parity/reference-player-inventory-3270x1844.png \
  --cur ../../docs/webui-parity/reference-player-inventory-3270x1844.png
# PASS 13/13 self

/tmp/webui-craft-qa-venv/bin/python e2e/craft-chrome/compare.py \
  --ref ../../docs/webui-parity/reference-player-inventory-3270x1844.png \
  --cur /tmp/webui-craft-current-task6-final.png
# PASS 13/13 current
```

Round 1 retained no source modification: raw renderer evidence, not `compare.py`, is the blocker. The commit for this documentation correction is `docs: record grip renderer-stage blocker`.
