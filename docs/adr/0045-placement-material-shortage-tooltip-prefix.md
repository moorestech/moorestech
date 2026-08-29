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
- **電線・チェーン・レール不足の行も同じ「アイテム不足：」書式へ統一する。**（当初は「アイテム不足： 電線」等のツール名表示。下の裁定で実アイテム名へ改めた）
  出所: ユーザー裁定 2026-08-30 選択「電線・チェーン・レールも揃える」
  棄却案: 機械設置の素材不足だけ変更
- **電線・チェーン・レールの専用3キーを廃し、`ui.tooltip.placeMaterialShortage` へ合流させる。** 不足行に出す名前は接続ツール名ではなく実際に不足しているアイテム名とし、所持/必要も伴わせる（「アイテム不足： 銅のワイヤー 0/1」「アイテム不足： 補強棒材 4/12」）。C#側の配線変更を伴ってよい。
  出所: ユーザー裁定 2026-08-30（レビュー Critical C1 への裁定）→ 選択「実アイテム名＋個数に合流（推奨）」。[[2026-08-30-接続ツールの不足行も実アイテム名と個数で出す]]
  棄却案: ①3行を「電線の素材が足りません」等の従来型の文へ戻す ②接頭辞を残しツール名だと文言で明示する（「素材不足： 電線（接続ツール）」）
  理由: `ui.tooltip.placeWireNoWireItem` 等の「電線 / チェーン / レール」は buildMenu.json の `connectTools[].name`（ツール名）であり、実際に不足するアイテム（銅のワイヤー / 鉄のワイヤー / 補強棒材＋鉄板）と一致しない。接頭辞の付与で「文」から「アイテムの名指し」へ意味が変わったため、名前のSSOTをアイテムマスタ1本へ寄せる。
- 不足素材が1件も算出できないとき（接続ツールがマスタに無い等）は無言にせず、系統ごとの汎用文言 `PlaceWireFailed` / `PlaceGearChainFailed` / `PlaceRailFailed` の1行へ落とす。
- 本ADR初版の「キー名・パラメータ構成・C#/Web の配線は変えない」「電線等の3行は個数を持たない」という前提（いずれもagent前提）は、上記のユーザー裁定で明示的に上書きされた。

### 不足系文言の統一対象キー一覧（次回の掃引範囲）

「アイテム不足：」語彙へ統一済みのキーと、旧語彙のまま残るキーを固定する。次に接頭辞・語彙を触るときはこの一覧を掃引範囲とする。

- 統一済み: `ui.tooltip.placeMaterialShortage`（設置ツールチップの唯一の不足行。電線・チェーン・レールもここへ合流）
- 未統一（本ADRの範囲外・旧語彙）: `ui.buildMenu.materialShortageTitle` / `ui.buildMenu.materialShortageLine`（ADR 0041側）、`ui.notification.placeBlockCostShortage` / `placeBlockWireShortage` / `electricWireExtendNoWireItem` / `railEditNotEnoughRailItem`（トースト通知）、`ui.tooltip.craftCannotByItemShortage`、`ui.research.missingItems`

## Consequences

- localization.csv 変更のため webui の `localizationKeys.ts` 再生成と force-recompile が必要。3キー削除により生成物にも差分が出る。
- 接続ツールの不足素材算出は `ConnectToolMaterialShortageCalculator`（クライアント`PlaceSystem/Util`）へ集約し、必要数はサーバー共有の `ConnectToolCostCalculator`、突き合わせは `ConstructionCostShortageCalculator` を再利用する。判定の重複定義は作らない。
- レールは必要アイテムが2種のため、不足行が複数行になりうる。
- 削除した3キーを参照していた C# テストとプレイテストシナリオは新しいキー構成へ追随させた。
