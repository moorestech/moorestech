# Web UI 移行 実装セッション計画 (2026-06-14)

**親 TODO**: `docs/cef-webui-migration-todo.md` / **監査**: `docs/ui-completeness-reaudit-plan.md`
**今セッションのスコープ**: 小機能4本 (INV-2 / COM-2 / COM-3 / INV-6) + SubInventory 土台 (INV-4 + BLK-1)
**動画方式**: Playwright ブラウザ録画 (`video:'on'`)。CEF (INFRA-1) は破損のため in-Unity 録画は不可。
**テスト acceptance**: 各機能 = vitest 単体 + Playwright e2e (mock host 相手) + 録画。C# Topic/Action は実装 + `uloop compile` で検証 (live Unity e2e は CEF 前提のためスコープ外)。

## 実行モデル
- **コントローラ (メイン)** が共有ファイルを直列所有: `protocol.ts` / `payloadTypes.ts` / `App.tsx` / `e2e/mock-host/*` / `playwright.config.ts` / C# 全 Topic/Action + `WebUiGameBinder.cs` / `InventoryTopic.cs`。
- **サブエージェント** は各機能の新規 web ファイル (component / css / logic / 単体テスト / e2e spec) のみ作成。`pnpm vitest run <file>` で自己検証。build/e2e/uloop は実行しない (1 worktree で競合するため)。
- 統合フェーズでコントローラが `pnpm build` → `pnpm vitest run` 全件 → `pnpm test:e2e` (録画) → `uloop compile` を一括実行。

## 共有契約 (コントローラが先に確定)

### payloadTypes.ts 追加
- `PlayerInventoryData` に `selectedHotbar: number` を追加 (INV-2)
- `ModalRequest = { id; title; message; buttonText; variant: "confirm"|"error" }`、`ModalData = { modal: ModalRequest | null }` (COM-2)
- `ProgressData = { visible: boolean; progress: number; label: string | null }` (COM-3)
- `FluidSlotData = { fluidId: number; amount: number; capacity: number; name: string }` (INV-6)
- `BlockSlotRef = { area: "block" | "main" | "hotbar" | "grab"; slot: number }` (INV-4)
- `BlockInventoryData = { open; blockType; identifier; blockName; itemSlots: SlotData[]; fluidSlots: FluidSlotData[]; progress: number|null }` (INV-4/BLK-1)

### protocol.ts 追加
- Topics: `blockInventory: "block_inventory.current"`, `modal: "ui.modal"`, `progress: "ui.progress"`
- Actions: `inventory.select_hotbar { index }`、`ui.modal.respond { id; result }`、`block_inventory.move_item { from: BlockSlotRef; to: BlockSlotRef; count }`

## タスク (各 = impl + 単体 + e2e spec、acceptance は統合フェーズ)

### Wave 1 (並列・相互独立)
- **T-COM2 Modal**: `features/modal/` ModalHost。`ui.modal` topic 購読、modal!=null で表示 (uGUI OneButtonModal = title/message/1ボタン + 背景クリックキャンセル準拠)。ボタン/背景 → `ui.modal.respond`。
- **T-COM3 Progress**: `features/progress/` ProgressBar。`ui.progress` 購読、visible 時に 0..1 フィル + label (uGUI ProgressBarView = scrollbar.size 準拠の横バー)。
- **T-INV4 BlockInventory+Chest**: `features/blockInventory/` BlockInventoryPanel + `blockType→component` 静的レジストリ + ChestInventory (uGUI ChestBlockInventoryView = IChestParam.ItemSlotCount 個の grid)。`block_inventory.current` 購読、open 時表示。item スロット grab 操作 → `block_inventory.move_item`。

### Wave 2 (Wave1 の成果に依存)
- **T-INV6 Fluid**: `shared/ui/FluidSlot/` + `ProgressArrow/` (uGUI FluidSlotView = 液体アイコン + amount/capacity N0 + tooltip、ProgressArrowView = slider 0..1)。BlockInventoryPanel レジストリに "tank" エントリを追加し fluidSlots + progress を描画 (INV4 の panel に依存)。
- **T-INV2 Hotbar 選択**: 既存 `features/inventory/InventoryPanel` を編集。`selectedHotbar` でハイライト、スロットクリック/1-9キー/ホイール → `inventory.select_hotbar` (uGUI HotBarView 準拠: 循環選択)。

## C# (コントローラ実装 + uloop compile)
- `InventoryTopic`: `selectedHotbar` を snapshot に追加 + `SelectHotbarActionHandler` (`inventory.select_hotbar`)。
- `ModalTopic` + `ModalRespondActionHandler`。
- `ProgressTopic`。
- `BlockInventoryTopic` (SubInventoryState の open/close + item list を Topic 化) + `BlockMoveItemActionHandler`。
- `WebUiGameBinder.Bind()` に全登録。

## レビュー
各機能: 実装エージェント自己レビュー + 統合後に spec/quality レビューエージェント。最後に全体レビュー。
</content>
</invoke>
