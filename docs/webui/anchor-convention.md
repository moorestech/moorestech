# Web UI tutorial anchor convention

`data-tutorial-anchor` identifies a stable game-facing UI target for tutorial highlighting. Phase A5 defines the contract and typed attribute helper; the DOM registry and duplicate detection are Phase C4.

## Naming

- Use lowercase ASCII in `<screen-or-domain>.<element>` form.
- Separate words inside a segment with `-`: `inventory.close-button`.
- Name the element by its player-facing role, not its component, CSS class, DOM position, or current copy.
- Keep an ID stable across refactors. Renaming is a tutorial contract migration.
- An ID must resolve to at most one mounted element. Repeated list rows need a stable identity suffix defined by their domain contract before they become tutorial targets.

Use the typed helper:

```tsx
import { tutorialAnchor } from "@/shared/tutorialAnchor";

<button {...tutorialAnchor("inventory.close-button")}>...</button>
```

## Why this is separate from `data-testid`

`data-testid` belongs to automated tests and may change when test structure changes. `data-tutorial-anchor` belongs to the game/tutorial protocol: Unity content refers to it, runtime observers track its DOM lifecycle, and duplicate or missing IDs are diagnosable contract failures. Sharing the attributes would couple production tutorial content to test implementation details and allow harmless test refactors to break gameplay.

## Ownership and validation

Feature components only attach the attribute. They must not implement observers, overlays, or acknowledgements. The Phase C4 registry will own lookup, mount/unmount tracking, uniqueness checks, visibility resolution, and acknowledgements described in `design/tutorial-web-redesign.md`.
