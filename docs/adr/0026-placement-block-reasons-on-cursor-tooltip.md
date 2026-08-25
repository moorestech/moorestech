---
status: accepted
---

# 設置不可理由はカーソルツールチップに集約し、世界空間の文字ラベルは廃止する

設置プレビュー中、置けないセル・ドラッグは赤ゴーストになるだけで理由が無く、電線系だけが世界空間TextMeshProラベル（ブロック付近・ワイヤー中間点）で理由とコストを出していた。設置不可理由（地形干渉・既存ブロック重複・素材不足・電線不足・距離超過）と設置案内（電線コスト・接続範囲外）を**全PlaceSystemからカーソルツールチップ（topic `ui.tooltip`）へ集約**し、世界空間ラベルは撤去する。プレイヤーが「何が足りないか」をカーソル横で読める状態にするため。

出所: ユーザー裁定 2026-08-21 原文「設置時に何が足りないか表示したい。マウスカーソルの部分に」→ 選択「建設コスト不足 + 設置不可の全理由」（[[.decisions/2026-08-21-設置不可の全理由をカーソルtooltipに表示する.md]]）

## 決定

1. **対象理由**: 建設コスト不足だけでなく設置不可の全理由。  
   出所: ユーザー裁定 2026-08-21 選択「建設コスト不足 + 設置不可の全理由」
2. **対象システム**: 通常設置・ベルト・レール・列車・ギアチェーンポール・電線ツール・BP貼り付けの全PlaceSystem。「理由→カーソルツールチップ」の共通基盤を作り各PlaceSystemから理由をプッシュする。  
   出所: ユーザー裁定 2026-08-21 選択「全PlaceSystemを対象にし、理由表示の基盤を共通化」
3. **素材不足の文言**: 不足素材のみ「素材名 所持/必要」。必要はドラッグ全セル（地形干渉等で既に不可のセルは除く）分の総数。  
   出所: ユーザー裁定 2026-08-21 選択「不足素材のみ「素材名 所持/必要」、必要は今回の設置全セル分」
4. **複数理由**: 成立分を全て行で並べる（1理由のみではない）。  
   出所: ユーザー裁定 2026-08-21 選択「成立している理由を全部行で並べる」
5. **プレビューが出ないケース**: 距離超過は「遠すぎます」を出す。照準が何にも当たっていないときは無表示。  
   出所: ユーザー裁定 2026-08-21 選択「遠すぎるときだけ「遠すぎます」を出す」
6. **世界空間ラベルの撤去**: 通常設置の自動接続ラベル（コスト・拒否・案内）は全てツールチップへ移設し世界ラベル廃止。電線ツールもコスト・拒否理由はツールチップへ移設、ゴースト上の電柱名ラベルは削除。ワイヤー線の半透明描画は残す。  
   出所: ユーザー裁定 2026-08-21 選択「全部カーソルtooltipへ移設（世界ラベル廃止）」／原文「コスト、拒否理由はtooltip、電柱名は消す」
7. **wire契約**: `ui.tooltip` を `lines[{textKey,textParams}]` の行配列へ拡張。既存の単一行呼び出し（採掘・クラフト・削除）も1要素配列へ一括更新し、契約スキーマ・WireContractテスト・mock-hostを同時更新（後方互換なし）。  
   出所: ユーザー裁定 2026-08-21 選択「行配列へ拡張: lines[{textKey,textParams}]」

## Considered Options（実提示分のみ）

- 建設コスト不足のみ表示／建設コストを常時表示し不足行を強調 → 却下（決定1）
- CommonBlockPlaceSystemのみ対応し他は後続 → 却下（決定2）
- 「あとN個」差分表記／全素材列挙＋不足行色分け → 却下（決定3）
- 優先順位で最上位1理由だけ → 却下（決定4）
- 距離超過も無表示のまま → 却下（決定5）
- 電線ラベルはコストのみ世界に残す／世界ラベル維持＋tooltip併記／電線ツール現状維持／電柱名もtooltipへ → 却下（決定6）
- 契約無変更で改行連結文字列を{p0}パススルー → 却下（決定7。「生の表示文字列を受け付けない」契約理念に反する）

## agent前提（ユーザー未裁定・実装で従う設計）

- **理由の型**: クライアント側に `PlacementBlockReason`（種別enum＋任意パラメータ。素材不足は素材ItemId・所持・必要を持つ）と、1フレーム分の理由集合を表す `PlacementFeedback`（不可理由＋設置案内の行列）を置く。各PlaceSystemは `ManualUpdate` 内で自システムの既存判定結果（`PlaceInfo.Placeable` を落とした原因）から理由を組み立て、共通の `PlacementFeedbackTooltipPresenter` へ毎フレームプッシュする。Presenterはフレーム先頭でHide→理由があればShowの前例（`DeleteObjectService`）に従い、`TooltipPresentation` の同値比較で変化通知を抑える。  
  根拠: AGENTS.md「判断は具体側で行い、基盤には`SetHoge(値)`でプッシュ」／`ManualUpdate()`駆動の既存形。
- **行の順序**: 地形干渉・重複 → 距離 → 素材不足（素材ごと1行） → 電線不足 → 設置案内（電線コスト・接続範囲外）の固定順。プレビュー図の文言・順序は裁定対象外。
- **セルの選び方**: セルローカルな理由（地形干渉・重複）はカーソル下セル（無ければ末尾セル。`ElectricWireAutoConnectPreview` のcursorIndex解決と同じ）。ドラッグ全体の理由（素材・電線）は全セル集計。
- **ローカライズ**: 理由文は `ui.tooltip.place*` キーを `Localization/localization.csv` に追加し `LocalizationKeys.Ui.Tooltip.*` 経由で渡す。`ElectricWirePlacementFailureText` のハードコード日本語はキーへ置換し、同クラスは「reason→LocalizationKey」へ役割変更。素材名は `ContentLocalizationKeys.ItemName` で解決した文字列を `{p0}` に入れる（`MiningFocusState.ShowRecommendMiningTools` と同じ）。
- **サーバー限定理由（未解放）**: クライアントは未解放ブロックを選べない（ビルドメニューが解放済みのみ列挙）ため、クライアント側の理由には含めない。サーバーの集約通知（`ui.notification.placeBlock*`）は現状維持。
- **BP貼り付け**: 重複セルは従来どおり送信前に除外し、全セルが重複で置けないときのみ「設置位置が埋まっています」を出す。部分重複の案内行は出さない。
- **Web側**: `CursorTooltip.tsx` は `lines` を1行ずつ `translateExternalKey` で解決し縦に並べる。書式（18px/padding/max-width）はADR0019どおりWeb側トークンのまま。行数増による高さはclampで画面内に収める。

## Consequences

- `AutoConnectWirePreviewRenderer` からラベル機能を削除（ワイヤー線描画のみ残る）。`ElectricWireExtendPreviewObject._costLabel`・`ElectricWirePoleGhostPart._nameLabel` は削除。
- `ConstructionCostPreviewCalculator` は「賄えるセル数」に加え「素材ごとの所持/必要」を返す形へ拡張（またはそれを返す姉妹メソッドを追加）。
- 契約変更のため WireContractC2Test・`bridge/contract/schemas/ui.ts`・`e2e/mock-host`・`CursorTooltip.test.ts` を一括更新。
- 関連: [[0019-webui-cursor-tooltip-typography-owned-by-web.md]]（書式はWeb側）、[[.decisions/2026-08-05-通常設置の自動接続ゲートは維持し拒否理由をtooltip表示する.md]]、[[.decisions/2026-08-14-手掘り不可と道具不足は別文言にする.md]]（理由種別ごとに別キー）。
