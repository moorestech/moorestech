# 列車はFで車両インベントリを開きEで乗車し、インタラクトはGameScreen限定にする

出所: ユーザー裁定 2026-08-30（パリティ検査の2問）
- Q「列車車両は左クリック=開く・E=乗車の2操作を持つ。Fに統合するとどうするか」→ 選択「F＝車両インベントリを開く、乗車はE維持」
- Q「InteractControllerをGameScreenState駆動にすると建築/破壊モード中の採掘・ハイライトは消える。よいか」→ 選択「GameScreenだけでよい」

## 決定
- 列車車両のインタラクトは2アクション: F＝車両インベントリを開く、E＝乗車。tooltipは「[F] 車両インベントリを開く / [E] 乗車」の2行
- EはKeyCode直書きをやめInputSystemの`Playable/Ride`アクションとして正式化する
- インタラクト（選定・ハイライト・tooltip・F/E）はGameScreenStateでのみ駆動する。建築・破壊・デバッグ各モード中は対象選定もハイライトも行わない

## 棄却案
- F＝乗車、車両インベントリは左クリック維持（列車だけ左クリック例外が残る）
- F＝乗車、車両インベントリを開く操作は廃止
- 建築・破壊モード中もInteractControllerを駆動（設置ゴーストtooltipと並ぶ）

## ADR
- docs/adr/0046-interact-key-unifies-open-ride-and-mining.md
