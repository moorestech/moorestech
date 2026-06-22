---
name: mooreseditor-block-connectors
description: "Use when editing or QAing any moorestech block's placement offset, inputConnects, outputConnects, connectorGuid, direction, flow capacity, or tank index in mooreseditor while checking the result in Unity. Trigger on requests like \"ブロックの接続を調整\", \"inputConnects/outputConnectsを追加\", \"Fluid/Gear/Inventory connectorを編集\", \"BlockSetupで見ながらoffsetを直す\", \"録画からブロック編集手順を抽象化\", even if the user only says the connector is misaligned or the block does not connect."
---

# mooreseditor-block-connectors

mooreseditor の `blocks` データで任意ブロックの offset / inputConnects / outputConnects を編集し、Unity の `BlockSetup` シーンで見た目と接続位置を確認する。

## Inputs to Establish

Before editing, identify these values from the user request, the current mooreseditor selection, or the master data:

- Target block: `name` or `blockGuid`.
- Field scope: block `offset`, `inputConnects`, `outputConnects`, or a combination.
- Connector side and type: `Input` or `Output`; `Fluid`, `Gear`, `Inventory`, or the schema's enum value.
- Connector values: `connectorGuid`, `offset.x/y/z`, `directions`, `connectOption.flowCapacity`, `connectTankIndex`.
- Expected Unity check: what should visually line up or connect after the edit.

If the user provides only a recording, extract the concrete edits first, then generalize them into these inputs. Do not hard-code recorded values into future edits.

## Default Workflow

1. Confirm the workspace with `pwd`; this project uses git worktrees heavily.
2. Open Unity on `moorestech_client/Assets/Scenes/Other/BlockSetup.unity`.
3. Open `mooreseditor`, stay on the `Editor` tab, and display the `blocks` table.
4. Locate the target block row by `name` or `blockGuid`; use the row's `Edit` button.
5. Edit block-level `offset` only when the whole block needs to move relative to its setup position.
6. For connector work, open `inputConnects` or `outputConnects` with its `Edit` button.
7. Add a connector with `Add Item` only when no suitable connector entry exists; otherwise edit the existing row.
8. Set `connectType` before editing connector-specific fields because the visible fields can depend on the type.
9. Fill `connectorGuid`, connector `offset`, `directions`, `connectOption`, and `connectTankIndex` from the established inputs.
10. Switch back to Unity and inspect the block in `BlockSetup`; rotate/pan the view and test the intended alignment.
11. Return to mooreseditor, revise fields, and repeat until Unity shows the expected position.
12. Save with `Cmd+S` in mooreseditor after the visual check passes.

Do not directly edit Unity scene, prefab, or ScriptableObject YAML files while doing this workflow. Use mooreseditor for master data and Unity Editor/uloop routes for Unity-serialized assets.

## Connector Editing Rules

- Treat block `offset` and connector `offset` as separate layers. Block `offset` moves the block; connector `offset` moves the connection point relative to the block.
- Prefer editing the minimal field that explains the mismatch. If only the pipe port is wrong, change connector offset, not block offset.
- For `Fluid` connectors, verify `connectTankIndex` and `connectOption.flowCapacity`; they are not visible alignment fields but affect runtime behavior.
- For copied connector GUIDs, paste the full GUID and then visually verify the displayed short form did not refer to the wrong row.
- Keep input and output connector entries distinct. Do not mirror values between them unless the block design requires symmetric ports.

## Recording Analysis

When using Record & Replay output as the source:

1. Read `events.jsonl`; do not rely on the raw accessibility tree alone because mooreseditor tables are huge.
2. Extract only useful events first:

```bash
jq -r 'select(.kind|test("mouse|keyboard|window.changed|session")) | [.id,.timestamp,.kind,(.app.name // ""),(.window.title // ""),(.mouse.target.title // ""),(.keyboard.keyEquivalent // ""),((.keyboard.modifiers // [])|join("+"))] | @tsv' events.jsonl
```

3. Inspect `keyboard.text_input` targets to recover edited `X`, `Y`, `Z`, GUID, and enum values.
4. Map clicks on `Edit inputConnects`, `Edit outputConnects`, `Add Item`, and `クリップボードの内容を追加` into semantic steps.
5. Report uncertain row identity explicitly when the recording only captured a generic `AXButton Edit`.
6. If event IDs and timestamps appear out of order, use timestamps for the narrative and IDs only as evidence anchors.

## Validation

- Confirm that Unity was checked after each meaningful offset or connector change.
- Confirm that mooreseditor was saved with `Cmd+S`.
- If `.cs` files were not changed, Unity compile is not required by the project rule.
- If code, YAML schema, or JSON master files are changed while supporting the edit, follow the relevant project skill and run the required compile/validation.
- Before finishing, run `git diff -- .codex/skills/mooreseditor-block-connectors/SKILL.md` or inspect the skill directly to catch stale recorded constants.

## Gotchas

- Record & Replay logs can contain enormous `AX` table dumps. Filter by event kind and keyboard target before summarizing.
- A generic `Edit` button in the log does not prove which block row was edited; use surrounding table state or ask for the intended block if row identity matters.
- `keyboard.text_input` values are append-style traces. Use the target's placeholder/value and nearby `Cmd+A`/delete events to determine the final field value.
- mooreseditor UI updates can reorder element indices. Never build the workflow around AX element numbers from one recording.
- Unity scene navigation keystrokes in a recording are QA context, not data edits. Do not convert them into master data changes.
