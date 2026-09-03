# インタラクトはIInteractableを対象側に持たせ単一コントローラで駆動する

出所: ユーザー裁定 2026-08-30 原文「インタラクトという共通概念を作ってロジックを共通化する」→ 選択「IInteractableを対象側に持たせる」

## 決定
- ブロック(openableのみ)・列車車両・mapObject・露頭の各GameObjectがIInteractable（ハイライトON/OFF・tooltip文言・単押し/長押し種別・実行）を実装する
- 単一のInteractControllerをGameScreenStateからManualUpdateで駆動し、対象選定(2m・照準優先・角度最小)・ハイライト・tooltip・F入力を一箇所で行う
- 採掘FSMは長押し種別の実行内側に残す。基盤は「開く/採掘」を知らない

## 棄却案
- Controller側でヒットしたコンポーネント型をswitchし既存サービスを呼ぶ（種別追加のたびにswitchが伸びる）

## ADR
- docs/adr/0046-interact-key-unifies-open-ride-and-mining.md
