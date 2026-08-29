# 設置素材不足ツールチップ「アイテム不足：」接頭辞 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 設置ゴーストのカーソルツールチップの不足表示を「アイテム不足： 素材名 所持/必要」書式にし、電線・チェーン・レール不足の行も同書式へ統一する。

**Architecture:** ツールチップ文言は `Localization/localization.csv` の key+params 契約で持ち、C#（`ConstructionMaterialShortageLine` 等）はキーとパラメータを渡すだけ、描画はWeb側（`CursorTooltip`）。本変更は CSV の4キーの文言のみを書き換え、キー名・パラメータ構成・配線は一切変えない。

**Tech Stack:** localization.csv → Mooresmaster生成 `LocalizationKeys`（Unity, force-recompile必要）／webui `scripts/generate-localization-keys.mjs`（`npm run gen:i18n`）。

## Requirements

- `ui.tooltip.placeMaterialShortage` を ja `アイテム不足： {p0} {p1}/{p2}` / en・Source `Missing item: {p0} {p1}/{p2}` / de `Fehlender Gegenstand: {p0} {p1}/{p2}` にする（受け入れ: 鉄板3所持・10必要で「アイテム不足： 鉄板 3/10」）
- `ui.tooltip.placeWireNoWireItem` → ja `アイテム不足： 電線` / en `Missing item: Wire` / de `Fehlender Gegenstand: Kabel`
- `ui.tooltip.placeGearChainNoItem` → ja `アイテム不足： チェーン` / en `Missing item: Chain` / de `Fehlender Gegenstand: Kette`
- `ui.tooltip.placeRailNotEnoughRailItem` → ja `アイテム不足： レール` / en `Missing item: Rail` / de `Fehlender Gegenstand: Gleis`
- キー名・パラメータ数・C#/Web の配線は変えない。個数表記（所持/必要）は維持する
- やらないこと: ビルドメニュー側ツールチップ（ADR 0041）の文言、電線等への個数付与、見出し行の追加

## Global Constraints

- 作業は `moores-wt new feature/placement-material-shortage-tooltip-prefix` で切った新規worktreeで行い、PR作成直後に `moores-wt rm` で畳む
- localization.csv 変更後は webui `npm run gen:i18n` を実行し、生成物差分が無いことを確認（キー追加は無い）。Unity側は `uloop compile --project-path ./moorestech_client --force-recompile`（[[localization-csv-needs-force-recompile]]）
- コメント規約・200行制約は本変更に該当コード変更なし

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
git commit -m "feat: 設置素材不足ツールチップに「アイテム不足：」接頭辞を付け電線/チェーン/レール不足も統一 (ADR 0045)"
```

### Task 2: 全ブランチレビュー（必須・省略不可）

- [ ] 必ず最後に moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）
- [ ] pr-create で PR 作成 → `moores-wt rm` で worktree を畳む → `bd close` 対象タスクを閉じる

---

## 配置と前例

- 文言変更は CSV のみ。前例: `ui.tooltip.placeWireNoWireItem` 等の既存キーが同じ形で C#（key+params）→Web描画。書式をWeb側でなくCSV文言で持つのは `.decisions/2026-08-19-カーソルツールチップの書式はWeb側が持つ.md` と矛盾しない（Webは辞書の文字列をそのまま描画する）。
- 新規パターン: なし。

## 判断記録（ADR）

- 設計: `docs/adr/0045-placement-material-shortage-tooltip-prefix.md` / `.decisions/2026-08-30-設置素材不足tooltipはアイテム不足接頭辞を付け所持必要を維持する.md`
- 独語訳 `Fehlender Gegenstand:`、英語 `Missing item:` の語選択 — 出所: agent前提（既存 de 列の語彙 Kabel/Kette/Gleis を流用）
- 電線等3行は個数なしの接頭辞＋名前のみ — 出所: agent前提（既存行にパラメータが無い帰結、ADR 0045 記載）
