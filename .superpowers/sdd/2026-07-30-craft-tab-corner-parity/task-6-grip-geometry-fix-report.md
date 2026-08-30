# Task 6 grip geometry fix report

## Status

DONE_WITH_CONCERNS. Round 4 completes the stated target-facing frontier: 29 distinct size buckets times 18 inset transition buckets contains no exact target. Every pair now waits 400ms after its token mutation before DOM read and screenshot. This remains a bounded result, not an arbitrary CSS-domain claim. The best accepted state remains unchanged: `--craft-grip-size: 9.2px` and `--craft-grip-inset: 7px`.

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

The one-dimensional size interval has no exact target. Round 2 below corrects this further: two-token combinations can produce 22x22, but no measured target-facing pair produces both 19px gaps. Adding independent width/height or x/y tokens would violate the settled scope, so no such token was added.

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

The strict exact bbox and gap target has no match in the measured two-token frontier under Chromium rasterization. The tolerated visual comparator and all shared-consumer contracts remain green. For the sampled frontier, a single shared pseudo-element translate/offset is the smallest candidate extra degree of freedom to evaluate with approval; this is not a claim that it succeeds for every inset value or for the full CSS domain.

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

## Review fix round 2 — two-token frontier

Round 1's one-dimensional blocker was insufficient because inset changes the raw bbox. This round therefore paired the target-relevant size and inset quantization frontiers before making a conclusion.

### Measured frontier and candidate result

- Size representatives: `8.70`, `8.71`, `8.72`, `8.74`, `8.75`, `8.77`, `8.79`, `8.80`. They are the eight distinct Chromium 1/64px widths spanning the 22-pixel transition: `8.68750`, `8.70312`, `8.71875`, `8.73438`, `8.75000`, `8.76562`, `8.78125`, and `8.79688px`.
- Inset representatives: `7.00`, `6.99`, `6.69`, `6.68`, `6.62`, and `6.50px`, the observed transition buckets from the initial 7.00→6.50 direction. The 48 combinations were all recaptured at 3270x1844 after the harness' 400ms settling delay.
- The 48-row frontier had no `22x22`/`19,19` result. 22x22 appeared in adjacent inset bands, so 18 such pairs (`8.74–8.80px` with the measured `6.98..6.82px` bands) were independently recaptured under the same settling condition. All 18 were exactly `22x22` but had gaps `20/20`.
- The 0/66 evidence is superseded by the captured-DOM 0/522 manifest in review fix round 3.

### Reproducible component inventory

`measure/measure_grip_frontier.py --captures /tmp` emits 49 TSV lines: header plus every settled frontier capture. Each row records the representative size token, exact computed width, inset token, calculated DOM pseudo-element rect, every raw color-mask component, `touches_frame`, min-size eligibility, selected component, post-filter bbox, and gaps.

Representative rows demonstrate the complete component pattern:

| token pair | computed width | selected raw/post-filter | gaps | nonselected raw component |
| --- | ---: | --- | --- | --- |
| 8.70 / 7.00 | 8.68750px | `(2029,1398)-(2050,1418)`, 248px, 22x21 | 20/20 | `(1991,1359)-(2060,1428)`, 139px |
| 8.74 / 6.98 | 8.73438px | `(2029,1397)-(2050,1418)`, 253px, 22x22 | 20/20 | `(1991,1359)-(2060,1428)`, 139px |
| 8.80 / 7.00 | 8.79688px | `(2028,1397)-(2050,1418)`, 255px, 23x22 | 20/20 | `(1991,1359)-(2060,1428)`, 139px |
| 8.80 / 6.50 | 8.79688px | `(2031,1399)-(2050,1418)`, 210px, 20x20 | 20/20 | `(1991,1359)-(2060,1428)`, 139px |

Every true grip is `touch=0`, meets the 5px minimum, and is selected. Every nonselected component is retained in the TSV with the same flags, making the detector decision independently auditable. The DOM box is calculated from Playwright's measured stable central panel rect `[604.2839,166.7115,1034.9882,719.7614]`; the 6.68px inset row is included rather than inferred or omitted.

### Round 2 command and commit

```text
# Fresh build, then 48 settled runtime captures for the 8x6 frontier.
pnpm build

/tmp/webui-craft-qa-venv/bin/python \
  .superpowers/sdd/2026-07-30-craft-tab-corner-parity/measure/measure_grip_frontier.py \
  --captures /tmp > /tmp/task6-grip-frontier.tsv
# 49 lines; exact 22x22/19,19 candidates: 0
```

Round 2 commit: `docs: record grip two-token frontier blocker`.

## Review fix round 3 — captured-DOM manifest

- Audited all remaining distinct widths `8.8125px` through `9.1875px` together with the prior width frontier, giving 29 browser-measured size buckets × 18 target-facing inset buckets = **522** settled 3270x1844 captures.
- `measure/task-6-grip-frontier-manifest.tsv` is the durable audit artifact. Every row has exact token pair; browser-read width/height/right/bottom; browser-read panel/pseudo rect; capture filename/SHA256; raw all-component inventory; selected bbox; and gaps. The capture script writes the raw browser data, and the analyzer reads that manifest through a repository-relative comparator import.
- Audit result: all 522 image hashes revalidated; all DOM columns are populated; exact `22x22` with gaps `19/19` is **0/522**. No token candidate exists in this stated frontier, so no CSS/assertion/comparator change is retained.
- This establishes the blocker only over the 29×18 target-facing bucket set; it does not call the entire CSS decimal domain exhaustive. Round 3 commit: `docs: record grip captured-dom manifest`.

## Review fix round 4 — per-pair settled reproduction

- Corrected the capture timing: token mutation, then a 400ms wait, then browser DOM read and screenshot now occur for **every** size/inset pair. The initial page-level wait remains setup-only and is not used as pair settlement evidence.
- The committed `measure/rebuild_grip_frontier_audit.sh` is the one-command regeneration path. It builds the Web UI, writes its raw browser manifest to `/tmp/task6-grip-frontier-raw.tsv`, captures all 522 PNGs, and re-analyses those files into the committed TSV. It recreates every temporary capture artifact when prior `/tmp/task6-grip*` files are absent; PNGs remain intentionally uncommitted. Its `WEBUI_CRAFT_PYTHON` override selects the normal NumPy/Pillow QA interpreter (default: `/tmp/webui-craft-qa-venv/bin/python`).

```text
sh .superpowers/sdd/2026-07-30-craft-tab-corner-parity/measure/rebuild_grip_frontier_audit.sh
```
- Round 5 re-run result: before any image analysis, the analyzer recomputes each PNG SHA256 and fails on a raw-manifest mismatch. All 522 rows passed that enforced check and contain captured browser dimensions and rects; exact selected `22x22` plus `19/19` gaps remains **0/522**. The manifest is LF-delimited so `git diff --check` passes.
- This is bounded evidence only for the sampled 29×18 target-facing frontier. In this sample, a single shared pseudo-element translate/offset is the smallest extra geometry candidate to evaluate if approved; it is not a universal inset-space or CSS-domain assertion.
