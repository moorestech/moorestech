# Task 6 shared grip offset report

## Status

DONE. The craft grip now uses one shared diagonal offset token with one matching `translate(offset, offset)`. The retained measured tokens are `--craft-grip-size: 8.74px`, `--craft-grip-inset: 6.98px`, and `--craft-grip-offset: 0.4px`.

## RED

The shared E2E contract was extended before production CSS changed. It required the authored offset token and computed shared transform:

```text
authoredGripOffset: ".4px"
transform: "matrix(1, 0, 0, 1, 0.4, 0.4)"
```

The focused recipe test failed as intended because the unmodified grip returned `authoredGripOffset: ""` and `transform: "none"`. This failure established that the contract covers the new permitted geometry degree of freedom.

## Iterations

Each row uses a fresh production build and a 1635x922 CSS viewport / dSF2 capture (3270x1844 PNG). Grip metrics are the production comparator's selected mask.

| iteration | changed decision | size / inset / offset | bbox | right / bottom gap | median delta | result |
| --- | --- | --- | --- | --- | ---: | --- |
| baseline | no offset | 9.2 / 7 / 0 | 23x22 | 20 / 20 | 0 | existing RED state |
| 1 | shared offset only | 9.2 / 7 / 0.4 | 23x22 | 19 / 19 | 0 | rejected: width remains one pixel wide |
| 2 | measured frontier pair, offset held | 8.74 / 6.98 / 0.4 | **22x22** | **19 / 19** | **0** | retained |

Iteration 2 selects the already-recorded `8.74 / 6.98` pair from the settled 29x18 frontier, where it is a `22x22` / `20,20` state before the offset. This is one documented measured-pair selection rather than independent size or inset tuning; the newly authorized offset stays fixed at `0.4px`.

## GREEN and contract

The one allowed source change is the root token plus `.craft::after { transform: translate(var(--craft-grip-offset), var(--craft-grip-offset)); }`. No separate x/y value, width/height split, gradient, shadow, additional pseudo-element, consumer override, clip-path change, panel color, or tab path changed.

The shared E2E assertion records the browser values for all consumers:

```text
authored size/inset/offset: 8.74px / 6.98px / .4px
computed width/height:     8.73438px / 8.73438px
computed right/bottom:     6.98 / 6.98
transform:                 matrix(1, 0, 0, 1, 0.4, 0.4)
```

The final fresh comparator is **13/13 PASS**. Its strict grip measurements are `22x22`, right gap `19`, bottom gap `19`, and face median `rgb(132 133 149)` with maximum delta `0`. The face remains one solid clipped triangle with `backgroundImage: none` and `boxShadow: none`.

## Verification

```text
# RED: focused recipe contract failed with missing offset token and transform.
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/recipe.spec.ts --grep "正本のヘッダ装飾"

# GREEN: same focused contract passed after the permitted implementation.
# PASS: 1/1

pnpm build
# PASS: tsc -b && vite build

CAPTURE_VIEWPORT_W=1635 CAPTURE_VIEWPORT_H=922 \
  CAPTURE_OUT=/tmp/webui-craft-grip-offset-0.4-s8.74-i6.98.png \
  pnpm exec tsx e2e/capture-eval.ts

/tmp/webui-craft-qa-venv/bin/python e2e/craft-chrome/compare.py \
  --ref ../../docs/webui-parity/reference-player-inventory-3270x1844.png \
  --cur /tmp/webui-craft-grip-offset-0.4-s8.74-i6.98.png \
  --out /tmp/webui-craft-grip-offset-0.4-s8.74-i6.98-compare
# PASS: 13/13

pnpm exec playwright test --config e2e/playwright.config.ts \
  e2e/tests/recipe.spec.ts \
  e2e/tests/modeHud/operation-mode-hud.spec.ts \
  e2e/tests/research.spec.ts
# PASS: 12/12

pnpm exec tsc -p e2e/tsconfig.json --noEmit
# PASS
```

At offset implementation completion, `GamePanel/style.module.css` remained exactly 200 lines and `craftChromeAssertions.ts` was 90 lines. The implementation diff contained only the allowed shared token, diagonal transform, selected measured frontier pair, shared contract, and durable evidence.

## Review fix round 1 — transform-aware overlap contract

The offset implementation exposed a test-helper defect: `expectCraftGrip` used computed `right`, `bottom`, `width`, and `height`, but ignored the pseudo-element's computed transform. Its overlap rectangle was therefore 0.4px up-left of the painted grip.

### RED → GREEN

- RED replaced the broad overlap span with a 0.25px visible boundary button. Its `right` and `bottom` positions put it outside the helper's old rectangle but inside only after the shared `0.4px` diagonal translate. Before the fix, the contract correctly failed with `overlaps: false` while expecting `true`.
- GREEN parses `getComputedStyle(element, "::after").transform` through `DOMMatrixReadOnly` and adds the matrix `e`/`f` translation to all four grip-box coordinates. The boundary test then passes, proving detection uses the painted position rather than the pre-transform position.

The source style geometry did not change. Fresh production capture still reports `22x22`, gaps `19/19`, grip face delta `0`, and comparator **13/13 PASS**. Shared central, PlacementModeHud, and ResearchDetailPane tests pass **12/12**; E2E TypeScript compile passes. `craftChromeAssertions.ts` remains 93 lines and `GamePanel/style.module.css` remains 200 lines.

## Review fix round 2 — padding-box origin

The absolute pseudo-element is positioned from the frame padding box, not its border edge. The helper now subtracts computed `borderRightWidth` and `borderBottomWidth` before composing the untransformed box, then applies `DOMMatrixReadOnly` `e`/`f` to all four edges. RED used a 0.25px button at `right/bottom: 5.7px`, which the border-edge helper falsely overlapped; GREEN correctly returns no overlap. The transform-sensitive probe remains at `6.7px` / `0.25px` and correctly overlaps the painted grip. Final shared suites: **13/13**; fresh comparator **13/13**; build and E2E TypeScript compile pass.

## Debug fix round 3 — numeric custom-property contract

Dev preserved the authored offset as `0.4px`, while the production minifier serialized it as `.4px`; all geometry fields already matched. The shared assertion now parses the custom property with `Number.parseFloat` and asserts numeric `0.4`, while retaining stable size/inset strings and the computed transform assertion. The dev RED reproduces the string-only mismatch; prod and dev shared suites both pass **13/13** after the minimal helper-only fix. CSS source is unchanged.
