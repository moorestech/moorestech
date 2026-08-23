# 操作ヒントの正はC#のUIStateに置きtopicで配る

## 決定
「この画面でどのキーが何をするか」のSSOTは各`IUIState`実装（C#）に置く。各stateがキー名＋翻訳キーの一覧を宣言し、UiStateTopic経由でWeb UIへ配る。Web UIは受け取った配列を左下へ描画するだけで、画面名からヒント内容を再導出しない。

出所: ユーザー裁定 2026-08-23 原文「常に左下に出てる操作方法の表記が全体的に正しくなかったりヌケモレがある感じだから調べて補正したい」→ 選択「C#のUIStateが宣言しtopicで配る（推奨）」

## 棄却案
- **Web側にuiState→ヒントの静的テーブルを持つ**: C#に触れず実装は軽いが、遷移判定を書くコードと文言が再び離れる。今回`KeyControlDescription`が腐った構造（`Tab/ECS`誤記・PlaceBlockのQ/E逆・ResearchTreeはSetText欠落）をそのまま場所を変えて再現するため棄却。
- **マスタデータに持つ**: キー割当の正はコード側にしか無いため、正とデータが別repoへ分裂する。

## 理由
遷移を判定する`if`とヒント宣言が同一ファイルに並ぶため、遷移を触ればヒントも視界に入る。ドリフトの構造的原因を消す。

## リンク
- 実装対象: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/*.cs`, `Client.WebUiHost/Game/Topics/UiStateTopic.cs`, `moorestech_web/webui/src/features/**`
- 退役対象: `Client.Game/InGame/UI/KeyControl/KeyControlDescription.cs`
