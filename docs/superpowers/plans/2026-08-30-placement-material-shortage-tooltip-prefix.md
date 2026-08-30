# 設置素材不足ツールチップ「アイテム不足：」接頭辞 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 設置ゴーストのカーソルツールチップの不足表示を「アイテム不足： 素材名 所持/必要」書式にし、電線・チェーン・レール不足の行も同書式へ統一する。

**Architecture:** ツールチップ文言は `Localization/localization.csv` の key+params 契約で持ち、C#（`ConstructionMaterialShortageLine` 等）はキーとパラメータを渡すだけ、描画はWeb側（`CursorTooltip`）。CSV の文言変更に加え、レビュー Critical C1 の裁定（2026-08-30）により電線・チェーン・レールの専用3キーを削除し、C#の配線を実アイテム名＋所持/必要へ合流させる。

**Tech Stack:** localization.csv → Mooresmaster生成 `LocalizationKeys`（Unity, force-recompile必要）／webui `scripts/generate-localization-keys.mjs`（`npm run gen:i18n`）。

## Requirements

- `ui.tooltip.placeMaterialShortage` を ja `アイテム不足： {p0} {p1}/{p2}` / en・Source `Missing item: {p0} {p1}/{p2}` / de `Fehlender Gegenstand: {p0} {p1}/{p2}` にする（受け入れ: 鉄板3所持・10必要で「アイテム不足： 鉄板 3/10」）
- `ui.tooltip.placeWireNoWireItem` / `placeGearChainNoItem` / `placeRailNotEnoughRailItem` の3キーを**削除**し、電線・歯車チェーン・レールの不足行も `ui.tooltip.placeMaterialShortage` へ合流させる（受け入れ: 銅のワイヤー0所持・1必要で「アイテム不足： 銅のワイヤー 0/1」、レールは補強棒材・鉄板の2行）
- 不足素材が1件も算出できないときは `PlaceWireFailed` / `PlaceGearChainFailed` / `PlaceRailFailed` の1行へ落とし、無言の失敗にしない
- 個数表記（所持/必要）は全系統で維持する
- やらないこと: ビルドメニュー側ツールチップ（ADR 0041）の文言、見出し行の追加、トースト通知側の語彙統一

> **非目標の変更（2026-08-30 ユーザー裁定）:** 当初の非目標「キー名・パラメータ数・C#/Web の配線は変えない」および「電線等3行は個数なし」は、レビュー Critical C1（3行の名前が接続ツール名でありアイテム名と一致しない）への裁定で明示的に上書きされた。配線変更を伴う案Aを採る。

## Global Constraints

- 作業は `moores-wt new feature/placement-material-shortage-tooltip-prefix` で切った新規worktreeで行い、PR作成直後に `moores-wt rm` で畳む
- localization.csv 変更後は webui `npm run gen:i18n` を実行し、生成物差分をコミットに含める（3キー削除により差分が出る）。Unity側は `uloop compile --project-path ./moorestech_client --force-recompile`（[[localization-csv-needs-force-recompile]]）
- 接続ツールの必要素材はサーバー共有の `ConnectToolCostCalculator`、不足の突き合わせは `ConstructionCostShortageCalculator` を再利用し、判定の重複定義を作らない

---

### Task 1: localization.csv の4キー文言変更と回帰確認

**Files:**
- Modify: `Localization/localization.csv:222,225,234,237`
- Test（既存・回帰確認のみ）: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Util/ConstructionMaterialShortageReporterTest.cs`, `moorestech_web/webui/src/shared/tooltip/CursorTooltip.test.ts`

**Interfaces:**
- Consumes: 既存キー `LocalizationKeys.Ui.Tooltip.PlaceMaterialShortage`（params: 名前, 所持, 必要）
- Produces: なし（文言のみ）

- [x] **Step 1: CSV の4行を書き換える**

```csv
ui.tooltip.placeMaterialShortage,Missing item: {p0} {p1}/{p2},Missing item: {p0} {p1}/{p2},アイテム不足： {p0} {p1}/{p2},Fehlender Gegenstand: {p0} {p1}/{p2}
ui.tooltip.placeWireNoWireItem,Missing item: Wire,Missing item: Wire,アイテム不足： 電線,Fehlender Gegenstand: Kabel
ui.tooltip.placeGearChainNoItem,Missing item: Chain,Missing item: Chain,アイテム不足： チェーン,Fehlender Gegenstand: Kette
ui.tooltip.placeRailNotEnoughRailItem,Missing item: Rail,Missing item: Rail,アイテム不足： レール,Fehlender Gegenstand: Gleis
```

（列は `key,Source,english,japanese,german`。カンマを含む値は無いのでクォート不要。「アイテム不足：」の後は半角スペース1つ）

- [x] **Step 2: webui 生成物を再生成し差分が無いことを確認**

Run: `cd moorestech_web/webui && npm run gen:i18n && git status --short src/shared/i18n/generated/`
Expected: 出力なし（キー追加が無いため生成物は不変）

- [x] **Step 3: webui テスト**

Run: `cd moorestech_web/webui && npx vitest run src/shared/tooltip/CursorTooltip.test.ts`
Expected: PASS（テストはキーで辞書を差し替えており文言に依存しない）

- [x] **Step 4: Unity force-recompile とサーバー/クライアントテスト**

Run: `uloop compile --project-path ./moorestech_client --force-recompile`
Expected: errors 0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ConstructionMaterialShortageReporterTest"`
Expected: 全PASS

- [x] **Step 5: 実表示確認（unityプレイ録画テスト・任意）** — 文言のみの変更のため省略（既存EditModeテスト・vitestで代替）

素材を持たない状態で機械をゴースト設置し、カーソルツールチップに「アイテム不足： <素材名> 0/N」が出ることをスクリーンショットで確認する（`uloop-screenshot`）。

- [x] **Step 6: コミット**

```bash
git add Localization/localization.csv
git commit -m "feat: 設置素材不足ツールチップに「アイテム不足：」接頭辞を付け電線/チェーン/レール不足も統一 (ADR 0047)"
```

### Task 1b: 接続ツール不足行の実アイテム名合流（レビューC1裁定・追加）

**Files:**
- Add: `moorestech_client/.../PlaceSystem/Util/ConnectToolMaterialShortageCalculator.cs`
- Modify: `PlaceSystem/Util/ConstructionCostShortageCalculator.cs`（ItemId基準の突き合わせ／`ToShortages` 抽出）, `ConstructionMaterialShortageLine.cs`（`ToLines` と落とし先キー）, `Feedback/PlacementFeedback.cs`（`AddLines`）
- Modify: 電線（`ElectricWireFeedbackLines` / `ElectricWirePlacementFailureTooltipKey` / `ElectricWireExtendMode` / `AutoConnectNoticeLines` / `ElectricWireAutoConnectPreview` / `ElectricWireAutoConnectVirtualInventory` / `ElectricWireAutoConnectToolSelector`）
- Modify: 歯車チェーン（`GearChainPlacementFailureTooltipKey` / `GearChainPoleExtendPreviewCalculator` / 両Mode）
- Modify: レール（`TrainRailPlacementFailureTooltipKey` / `TrainRailConnectPreviewCalculator` / `TrainRailConnectSystem`）
- Modify: `Localization/localization.csv`（3キー削除）, `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`
- Test: `Client.Tests/PlaceSystem/Util/ConnectToolMaterialShortageCalculatorTest.cs`（新規）と既存6テストの追随

- [x] **Step 1: 不足算出の共有ロジックを置く**（`ConnectToolCostCalculator` + 所持集計 + `CalculateRequirements` の再利用）
- [x] **Step 2: 3系統の呼び出し側を不足素材の行群へ変える**（素材不足以外の失敗理由は従来どおり理由キー1行）
- [x] **Step 3: CSV の3キー削除と `npm run gen:i18n`**
- [x] **Step 4: テスト追随＋新規テスト、compile と EditMode テスト**

### Task 2: 全ブランチレビュー（必須・省略不可）

- [ ] 必ず最後に moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）
- [ ] pr-create で PR 作成 → `moores-wt rm` で worktree を畳む → `bd close` 対象タスクを閉じる

---

## 配置と前例

- 文言変更は CSV のみ。前例: `ui.tooltip.placeWireNoWireItem` 等の既存キーが同じ形で C#（key+params）→Web描画。書式をWeb側でなくCSV文言で持つのは `.decisions/2026-08-19-カーソルツールチップの書式はWeb側が持つ.md` と矛盾しない（Webは辞書の文字列をそのまま描画する）。
- 新規パターン: なし。

## 判断記録（ADR）

- 設計: `docs/adr/0047-placement-material-shortage-tooltip-prefix.md` / `.decisions/2026-08-30-設置素材不足tooltipはアイテム不足接頭辞を付け所持必要を維持する.md`
- 独語訳 `Fehlender Gegenstand:`、英語 `Missing item:` の語選択 — 出所: agent前提（既存 de 列の語彙 Kabel/Kette/Gleis を流用）
- 電線等3行の実アイテム名＋個数への合流 — 出所: ユーザー裁定 2026-08-30（レビュー Critical C1）。`.decisions/2026-08-30-接続ツールの不足行も実アイテム名と個数で出す.md`
