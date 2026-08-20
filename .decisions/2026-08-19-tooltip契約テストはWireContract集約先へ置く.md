# tooltip の wire 契約テストは WireContractC2Test へ集約する

2026-08-19 ユーザー裁定（moores-code-review Step 7 の AskUserQuestion）。

## 決定

`TooltipPresentationCarriesOnlyVisibilityKeyAndParams`（`TooltipPresentation` の public フィールドが `Visible` / `TextKey` / `TextParams` の3つだけであることを検証する構造テスト）は、`Client.Tests/Mining/MiningFocusStateTest.cs` ではなく `Client.Tests/WebUi/WireContractC2Test.cs` に置く。同ファイルには既に `TooltipMatchesFixture` があり、tooltip の wire 契約はここへ集約される。

plan `2026-08-19-webui-cursor-tooltip-and-text-selection.md` の Task 3 Step 1 は `MiningFocusStateTest` への追加を明示指定していたが、本裁定でこれを上書きする。

## 棄却した案

- **専用ファイル `Client.Tests/WebUi/TooltipContractTest.cs` を新設する**: 責務分離は最も明確だが、既存の集約先があるのに新規ファイルを増やす必要がない。
- **テスト自体を削除する**: `WireContractC2Test` の fixture ゴールデン比較（`JToken.DeepEquals`・余剰フィールドも検出）で足りるという判断。構造の明示的なガードを残す方を採った。
- **現状維持（planの指定どおり `MiningFocusStateTest` に置く）**: `TooltipPresentation` にフィールドを1つ足すだけで無関係な採掘テストが赤くなり、原因が読めずガードごと緩められるリスクがある。

## 理由

`MiningFocusStateTest` は `MoorestechServerDIContainerGenerator` を起動する重量級フィクスチャで、tooltip の wire 形状ガードとは責務が別。同居させると (1) フィールド追加で無関係なテストが赤くなる、(2) 重量 Setup が落ちると wire 退行ガードまで同時に沈黙する。

## リンク

- [[2026-08-19-カーソルツールチップの書式はWeb側が持つ]]
- ADR: `docs/adr/0019-webui-cursor-tooltip-typography-owned-by-web.md`
