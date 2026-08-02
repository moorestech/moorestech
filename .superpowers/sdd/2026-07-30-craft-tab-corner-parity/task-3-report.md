# Task 3 Report: shared craft grip

## Outcome

Replaced the shared craft-frame `::after` decoration with a 9 CSS px, single-color triangle inset by 7 CSS px. The shared contract now verifies RecipeViewer, PlacementModeHud, and ResearchDetailPane, including the absence of visible content overlap.

## TDD evidence

### RED

Command (from `moorestech_web/webui`):

```bash
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/recipe.spec.ts e2e/tests/modeHud/operation-mode-hud.spec.ts e2e/tests/research.spec.ts
```

Output: `3 failed, 8 passed (14.9s)`. Each failure came from `expectCraftGrip` and recorded the existing 24px width, 18px height, 8px inset, linear-gradient background image, and inset box shadow. PlacementModeHud and ResearchDetailPane also reported `overlaps: true`; RecipeViewer reported `overlaps: false` with the old larger grip. This is the expected pre-fix gradient/24×18px failure.

### GREEN

Command (from `moorestech_web/webui`):

```bash
pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/recipe.spec.ts e2e/tests/modeHud/operation-mode-hud.spec.ts e2e/tests/research.spec.ts
wc -l src/shared/ui/GamePanel/style.module.css
```

Output: `11 passed (12.5s)` and `200 src/shared/ui/GamePanel/style.module.css`.

An initial GREEN run exposed a real PlacementModeHud overlap after the pseudo-element conversion (`overlaps: true`). The craft padding was expanded only on its right and bottom edges to reserve the 9px inset grip, then the full focused suite passed.

## Changed files

- `moorestech_web/webui/e2e/support/craftChromeAssertions.ts` — shared computed-style and non-overlap assertion.
- `moorestech_web/webui/e2e/support/operationHudAssertions.ts` — adopts the shared contract and removes gradient-specific duplicate assertions.
- `moorestech_web/webui/e2e/tests/recipe.spec.ts` — checks the selected central craft frame.
- `moorestech_web/webui/e2e/tests/research.spec.ts` — checks the selected research detail craft frame.
- `moorestech_web/webui/src/app/tokens.css` — adds the three craft-grip tokens immediately after `--panel-edge-fade`.
- `moorestech_web/webui/src/shared/ui/GamePanel/style.module.css` — uses a clipped single-color triangle, removes gradient and shadow, reserves the craft-frame corner, and is reduced to 200 lines.

## Line counts

```text
49  e2e/support/craftChromeAssertions.ts
97  e2e/support/operationHudAssertions.ts
135 e2e/tests/recipe.spec.ts
121 e2e/tests/research.spec.ts
160 src/app/tokens.css
200 src/shared/ui/GamePanel/style.module.css
```

## Self-review

- `git diff --check` completed with exit code 0.
- The helper asserts exact content, 9×9 size, 7px inset, triangle clip path, `rgba(146, 148, 167, 0.98)`, no background image, no shadow, and no content overlap.
- The obsolete linear-gradient assertion was removed from the placement HUD helper; its remaining animation and typography contract is unchanged.
- The default panel, skit, and `bottomDeco` declaration values were not modified.
- The support directory contains six files, within the ten-file limit; all changed source files are at or below 200 lines.

## Concerns

No remaining concerns. The craft variant's right/bottom padding is intentionally larger to keep its shared grip outside all measured content rectangles; focused E2E coverage verifies RecipeViewer, PlacementModeHud, and ResearchDetailPane.
