# 0045. 設置素材不足ツールチップに「アイテム不足：」接頭辞を付ける

日付: 2026-08-30
状態: 採択

## Context

設置ゴーストのカーソルツールチップは、不足素材を `ConstructionMaterialShortageReporter` → `ConstructionMaterialShortageLine` → `ui.tooltip.placeMaterialShortage`（`{p0} {p1}/{p2}`）で `鉄板 3/10` の行として出す（裁定 2026-08-21）。行頭に不足であることを示す語が無く、数字だけでは「足りない」と読めない。
同系の不足表示として `ui.tooltip.placeWireNoWireItem`（電線が足りません）/ `placeGearChainNoItem`（チェーンが足りません）/ `placeRailNotEnoughRailItem`（レールが足りません）がある。文言はすべて localization.csv（キー＋パラメータ、書式はWeb側描画）。

## Decision

- **不足素材行を「アイテム不足： 素材名 所持/必要」にする。** 行ごとに接頭辞を付け、所持/必要の数値は維持する。
  出所: ユーザー裁定 2026-08-30 原文「機械を設置するときのアイテム不足ツールチップで、アイテム不足：　具体的なアイテム名　と出すようにしたい」→ 選択「行ごとに接頭辞＋所持/必要を維持」（プレビュー「アイテム不足： 鉄板 3/10 / アイテム不足： 歯車 0/5」）
  棄却案: ①個数を消す ②見出し1行＋素材行 ③1行カンマ連結
- **電線・チェーン・レール不足の行も「アイテム不足： 電線」等へ統一する。**
  出所: ユーザー裁定 2026-08-30 選択「電線・チェーン・レールも揃える」
  棄却案: 機械設置の素材不足だけ変更
- 実装は localization.csv の当該4キーの文言変更のみで、キー名・パラメータ構成・C#/Web の配線は変えない（agent前提: 契約 key+params を保つ最小変更。英語は `Missing item: {p0} {p1}/{p2}` / `Missing item: Wire` 等、独語も同型で更新）。
- 電線等の3行は個数を持たないため接頭辞＋アイテム名のみとする（agent前提: 既存行に個数パラメータが無い帰結。個数付与は本ADRの範囲外）。

## Consequences

- localization.csv 変更のため webui の `localizationKeys.ts` 再生成と force-recompile が必要（キー追加は無いので生成物差分は出ない見込み。CI の生成チェックで確認）。
- 文言のみの変更で、`ConstructionMaterialShortageReporterTest` / `CursorTooltip.test.ts` はキー・パラメータで検証しているため影響しない。
