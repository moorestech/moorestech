# Web UI uGUI Visual Parity Scorecard

## Capture Conditions

- Renderer: Playwright Chromium
- Viewport: 1284×725 CSS px
- Device scale factor: 2
- Output: 2568×1450 px
- Reference: `reference.png` copied without resizing from `/Users/katsumi/Desktop/スクショ/inventory.png`
- State: demo inventory open, first craft recipe selected
- Background: fixed bright world-like gradient for transparency inspection
- Full render: `final.png`
- Equal-scale crops: `crops/*.png`

Mock item names, icons, and quantities are excluded from scoring. Panel transparency, frame structure, corners, line count, label position, typography treatment, and ornaments are included.

Both `reference.png` and `final.png` are 2568×1450. The numbered detail references remain the authoritative source for local ornament structure because they are the user-provided crops of those regions.

## Measured Contracts

| Feature | Reference measurement | Web measurement | Evidence |
|---|---|---|---|
| Panel frame | 8px combined outer/inner bands; square corner | 4px border + 4px dark inset + 1px light inset; `border-radius: 0` | `visualParity.spec.ts`, `craft-panel.png` |
| Header bands | upper ≈8px; lower ≈8–12px; fading ends | upper 8px with 2 gradient bands; lower 10px with 2 gradient bands; transparent endpoints | `inventory-panel.png`, `recipe-list-panel.png` |
| Hotbar label gap | 8px; label and slot centers within 3px | 8px CSS gap; Playwright asserts center delta ≤3px | `visualParity.spec.ts`, `hotbar.png` |
| Stepped triangle | ≈104×103px high-brightness bbox | 52×52 CSS px at dSF2 = 104×104 output px; six tonal steps | `visualParity.spec.ts`, `craft-panel.png` |
| Divider emblem | centered diamond with inward daggers | 17×17 CSS px bordered diamond; Playwright asserts panel-center delta ≤3px | `visualParity.spec.ts`, `divider-ornament.png` |
| Craft selection frame | ≥5 visible layers; right caps ≈8–10% width | 2px cyan core, two inset bands, two outer bands/glow; 34px right L caps on 322px crop width = 10.6% | `visualParity.spec.ts`, `recipe-selection.png` |
| Forbidden preview | no independent center object | no `craftPreview` DOM; Playwright asserts count 0 | `recipe.spec.ts`, `craft-panel.png` |

## Results

| Axis | Score | Evidence | Result |
|---|---:|---|---|
| A. Transparency and non-destructive guard | 15/15 | `final.png`, `crops/craft-panel.png`: the injected background remains continuously visible through all three panels; shadows are short pixel offsets. The former center preview DOM is absent and the middle field contains only the backdrop. | PASS |
| B. Inventory heading rules | 12/12 | `crops/inventory-panel.png`: independent upper and lower multi-band horizontal rules fade toward both ends; no rounded heading card exists. | PASS |
| C. CRAFT RECIPE heading rules | 12/12 | `crops/recipe-list-panel.png`: the title is bounded by separate layered fading rules rather than a uniform single separator. | PASS |
| D. Center/craft frame layers | 12/12 | `crops/craft-panel.png`: square outer border, inset inner line, dark inner band, and short hard shadow are all independently visible. | PASS |
| E. Hotbar labels and slots | 12/12 | `crops/hotbar.png`: every number is outside and above its slot, horizontally center-aligned, with an open gap. Labels no longer form an internal top-left notch. | PASS |
| F. Lower-right stepped triangle | 10/10 | `crops/craft-panel.png`: a six-step, multi-tone downward triangle is anchored inside the center panel's lower-right corner. | PASS |
| G. Dagger and diamond divider | 12/12 | `crops/divider-ornament.png`: tapered inward line groups, dagger heads, and a layered center diamond form one centered ornament. | PASS |
| H. Selected craft frame | 12/12 | `crops/recipe-selection.png`: square dark outer edge, blue band, cyan core, inset navy lines, outer glow, and brighter right-side L corners are visible. | PASS |
| I. Typography and existence parity | 3/3 | `final.png`: text retains gray antialiasing and stepped shadow. No preview card, character-derived object, rounded icon card, internal hotbar tab, or uniform separator remains. | PASS |

**Total: 100/100**

All MUST axes pass. The first render was not accepted: QA found the stepped triangle attached to the recipe selection frame and an overly strong item-slot selection glow. The triangle was moved to the center panel's lower-right corner, and item selection was reduced while the five-layer treatment remained on the craft selection frame.

## Automated Verification

- `pnpm build`
- `pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/visualParity.spec.ts`
- `pnpm exec playwright test --config e2e/playwright.config.ts e2e/tests/recipe.spec.ts e2e/tests/hotbar.spec.ts`

The complete unit and E2E suites are run again during final repository QA.
