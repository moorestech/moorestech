# Issue #1101 横長画面HUD配置 R2 レビュー記録 (2026-07-30)

## 対象
- base: `50090a5f2c876e7013a5382296bf54ec419c254b` / reviewed head: `c22782f283b862795db72620d85e3c77fddcb784`
- ブランチ: `sakastudio/ui` / PR: #1102
- context要約 — ゴール: チャレンジHUDを実viewport左上、罫線を約3分の1、Placementを実viewport右上、ヴィネットを実viewport四辺へ配置 / 非目標: HUD内容・スキット操作・通信契約の変更 / 許容トレードオフ: 旧右上配置との互換を捨ててユーザー指定の左右配置を優先 / 制約: 固定長トークン、Web UI正本3コピー同期、1280×720と2432×786の自動・目視QA
- 初回記録: [R1](2026-07-30-issue-1101-wide-screen-hud.md)

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---:|---|
| 決定論チェック | 0 | 最終diffでconfirmed・比較演算子・コメント長・region・schema・event候補すべて0 |
| precedent / user-intent / SSOT / Fable全般相当 | 0 | 左右端アンカー、本文幅と罫線幅の分離、viewportヴィネットがユーザー意図と正本へ一致 |
| test-mutation / flow reviewer | 0 | 左右逆転、stageヴィネット復帰、罫線未短縮のmutationをE2Eが検出 |
| architecture / duplication reviewer | 3→0 | aria属性の視覚フック流用、ヴィネット無名値、端距離assert重複を修正 |
| Codex外部監査 | Medium 1 / Low 2→0 | 横長時の本文・Placement幅assertを追加。罫線クラスとヴィネットトークン化はarchitecture指摘と一致 |
| post-check 2系統 | 0 | rationale喪失なし。コメント規約候補0 |

## 適用した修正
- チャレンジHUDを左24pxへ移し、本文520pxを維持したまま罫線だけ176pxへ短縮（ユーザー指示 / user-intent） → 適用コミット `c22782f283b862795db72620d85e3c77fddcb784`
- Placement HUDを右24pxへ独立配置し、ヴィネットをstageから実viewportへ移動（ユーザー指示 / architecture） → 適用コミット `c22782f283b862795db72620d85e3c77fddcb784`
- 罫線専用クラス、ヴィネット幾何・色トークン、実viewport上端helperへ責務を明示（architecture / Codex） → 適用コミット `c22782f283b862795db72620d85e3c77fddcb784`
- 2432×786で左右端距離・本文520px・罫線176px・Placement 288px・ヴィネット所有者を固定し、PR画像4枚を再撮影（test-mutation / Codex） → 適用コミット `c22782f283b862795db72620d85e3c77fddcb784`

## 設計判断（AskUserQuestion裁定）
- Q: チャレンジHUDとPlacement HUDをどの画面端へ配置するか。/ 裁定: ユーザー指示によりチャレンジは左端、Placementは右端。罫線は従来の約3分の1。/ 適用: `c22782f283b862795db72620d85e3c77fddcb784`
- Q: ヴィネットの所有者。/ 裁定: ユーザー指示により1280 stageではなく実viewport四辺へ追従。/ 適用: `c22782f283b862795db72620d85e3c77fddcb784`

## 破棄した指摘
- なし。

## 事後結果（マージ後追記可）
- なし。

## メタ
- セッションID: root / Codex外部監査 `019fb2af-feca-7d12-ba8c-5170b4325204`
- スキップ系統: 専用Fableモデル枠は利用不可のため、独立holistic reviewerで同観点を実行
- suppressed: 0件
- 最終検証: lint成功、unit 388/388、E2E 123/123、production build成功、1280×720の22状態と2432×786の4状態を目視合格
