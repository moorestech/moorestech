# Issue #1101 横長画面HUD配置 レビュー記録 (2026-07-30)

## 対象
- base: `79ff7564382f03e59b0efc94c3bc1f77fb73ce52` / reviewed head: `ffb0624936c476b85eeccc03a4dd7cb54af011e6`
- ブランチ: `sakastudio/ui` / PR: なし（Issue #1101）
- context要約 — ゴール: 横長画面でチャレンジHUDを実画面右上へ配置して罫線を短縮し、スキット会話帯と操作ボタンも実画面端へ追従 / 非目標: HUD内容・操作契約・1280×720基準の要素寸法の変更 / 許容トレードオフ: 後方互換性と将来拡張性は考慮せず、現行の画面端契約を優先 / 制約: Unity YAMLを直接編集せず、Web UI正本・E2E・実画面目視を同期

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | 最終diffでconfirmed 0、比較演算子・コメント長・region・schema・event候補も0 |
| tests / flow reviewer | 0 | 2432×786のE2Eが旧stage中央配置でREDになり、新しい実viewport配置でGREENになることを確認 |
| holistic reviewer | 0 | Warning 1件として画面端距離の負値を許すテストを検出し、上下端の下限assertion追加後に解消 |
| architecture reviewer | 0 | logical viewport overlayはstage拡縮を維持しつつ画面端HUDだけを実viewportへ広げる責務分離になっている |
| Codex外部監査 | スキップ | ローカルCLIがhook読込時の不正UTF-8環境値でpanicし、独立監査を完走できなかった |
| Fable全般相当 | 0 | 専用Fable枠の代わりに独立holistic reviewerで俯瞰確認し、検出したWarningを修正 |
| post-check 2系統 | 0 | rationale喪失なし。最終コメント規約候補0 |

## 適用した修正
- logical viewport overlayを追加し、チャレンジ・配置/削除・スキットHUDを実画面端へ追従（実装 / architecture） → 適用コミット `ffb0624936c476b85eeccc03a4dd7cb54af011e6`
- チャレンジHUDを右24px・上24px・幅520pxへ変更し、配置HUDをその左へ退避（ユーザー要望 / tests-flow） → 適用コミット `ffb0624936c476b85eeccc03a4dd7cb54af011e6`
- スキット操作ボタンを高コントラスト化し、2432×786で会話帯・操作ボタン・HUD端距離を固定するE2Eを追加（Issue #1101 / holistic） → 適用コミット `ffb0624936c476b85eeccc03a4dd7cb54af011e6`
- 画面端距離へ0以上のassertionを追加し、全E2Eで判明した旧配置期待値2件を新契約へ同期（holistic / QA） → 適用コミット `ffb0624936c476b85eeccc03a4dd7cb54af011e6`

## 設計判断（AskUserQuestion裁定）
- なし。Portal/fixed案はHUDごとの独自拡縮が必要になり、「1280基準寸法を維持して画面端だけ実viewportへ追従」という既存契約に一致しないため、代替候補ではなく不採用とした。

## 破棄した指摘
- なし。

## 事後結果（マージ後追記可）
- なし。

## メタ
- セッションID: primary Codex workspace session / independent reviewers: `review_tests_flow`, `review_holistic`, `review_architecture`
- スキップ系統: Codex外部監査（CLI runtime panic）、専用Fable実行枠（独立holistic reviewerで代替）
- suppressed: 0件
- 最終検証: lint成功、unit 388/388、E2E 121/121、production build成功、2432×786の4状態を目視合格
