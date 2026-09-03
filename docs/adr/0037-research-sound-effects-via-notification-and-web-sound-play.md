# 0037 序盤原始研究のSE: 結果音は通知イベント・Web操作音は sound.play アクション

- Status: Accepted
- Date: 2026-08-27
- 関連: `.decisions/2026-08-26-Web操作音はC#へsound.playを送って鳴らす.md` / `2026-08-26-sound.playの引数は用途名enumにする.md` / `2026-08-26-研究結果音は通知イベント経由で鳴らす.md` / `2026-08-26-同一SE種別は短時間クールダウンで重複再生を抑える.md` / `2026-08-26-研究SEのクリップはエージェントがフリー素材で用意する.md` / `2026-08-27-sound.playは共通UI部品にも今回入れる.md`

## Context

依頼原文「序盤原始研究だけでもSE入れたいから入れるところを洗い出して」「Web操作音も出したい」。
現状のSEは `Client.Game/InGame/SoundEffect/SoundEffectManager.cs` の enum 5種（採掘・設置）だけで、UI音は無い。研究画面は Web UI（`moorestech_web/webui/src/features/research/`）に移行済みで、uGUI側 `ResearchTreeElement` は描画停止。Web側に音声再生機構・音声資産は無い。

## Decision

### 1. 再生経路（出所: ユーザー裁定 2026-08-26 質問「Web UIの操作音はどこで鳴らしますか」→ 選択「Web→C#へ『鳴らせ』を送る」）
Web操作音は `actionContract.ts` に `sound.play { type }` を1本足し、C#側 `SoundEffectManager` が再生する。Webview内 `<audio>` 再生は採らない。棄却案: Webview内直接再生／操作音Web・結果音C#の併用。

### 2. 語彙（出所: ユーザー裁定 2026-08-26 質問「sound.playの引数は何を指す語彙にしますか」→ 選択「用途名（セマンティック）」）
引数は用途名で、C# `SoundEffectType` enum と同名の文字列。クリップ割当は Unity 側 `[SerializeField]` のみ。棄却案: クリップ名直指定。
enumの追加名（agent前提）: `UiSelect` `UiClose` `UiConfirm` `UiDenied` `UiOpen` `ResearchComplete` `Unlock` `ChallengeComplete`。

### 3. 結果音の経路（出所: ユーザー裁定 2026-08-26 質問「結果音はC#側のどこで鳴らしますか」→ 選択「通知イベント経由」）
サーバー通知イベント（`NotificationService.EventTag`）を購読し messageId 別に鳴らす: `achievement.researchCompleted`→ResearchComplete、`achievement.unlocked*`→Unlock、`achievement.challengeCompleted`→ChallengeComplete。`ResearchCompleteActionHandler` では鳴らさない。棄却案: アクションハンドラ経由。
agent前提（配置細目）: 質問時のプレビューでは `NotificationTopic.OnNotification` を挙げたが、同クラスは Web リレー（Client.WebUiHost）のため、購読者は Client.Game の `NotificationSoundEffectPlayer` として独立させる（前例 `ChallengeManager` の `SubscribeEventResponse`）。区別キーは category（3種とも Achievement）でなく messageId。

### 4. 連続通知の抑制（出所: ユーザー裁定 2026-08-26 質問「アンロック通知が同時に3件以上来る場合」→ 選択「同一種別は短時間内でクールダウン」）
`SoundEffectManager` に同一 `SoundEffectType` のクールダウンを入れる。秒数（agent前提: 0.1秒）。既存の採掘・設置音にも同じ抑制がかかることは提示済み。棄却案: 全部鳴らす／解放音を割り当てない。

### 5. 画面開閉・否定音（出所: ユーザー裁定 2026-08-26 同日質問群）
- 研究画面開閉は `ResearchTreeState.OnEnter/OnExit` で UiOpen/UiClose（棄却案: 今回は付けない）
- 素材不足で研究ボタン disabled のクリックは UiDenied。disabled `<button>` は click が来ないため、包み要素の `onPointerDown` で `!interactable && !completed` のとき送る（棄却案: 出さない）

### 6. Web側の送信元（出所: ユーザー裁定 2026-08-27 質問「sound.playアクションの適用範囲は」→ 選択「今回で共通ボタン/パネル部品にも入れる」）
研究画面の4操作（ノード選択→UiSelect／詳細ペイン閉じ→UiClose／研究ボタン→UiConfirm／不足クリック→UiDenied）に加え、`shared/ui` の `IconButton`・`PanelActionButton`・`ModeSwitch` の onClick に UiSelect を仕込み全画面へ一括適用する。棄却案: research/ 配下のみ。
agent前提: `sound.play` の送信は fire-and-forget で失敗トーストを出さない（`shouldToastFailure` から除外）。

### 7. クリップ（出所: ユーザー裁定 2026-08-26 質問「SEクリップは誰が用意しますか」→ 選択「エージェントがフリー素材を探して入れる」／2026-08-27 質問「ライセンス条件は」→ 選択「CC-BYも可（クレジット記載）」）
CC0 または CC-BY のフリー素材を `moorestech_client/Assets/Asset/Common/SoundEffect/` へ入れ、入手元URL・作者・ライセンスを同ディレクトリの `CREDITS.md`（agent前提: 新設）に記録する。棄却案: 既存mp3の仮割当／ユーザー先行用意／CC0のみ。

## 呼び出し側の線引き
Web・UIステート・通知トピックは「用途名を渡す」ことだけを知る。クリップ選択・クールダウン・音量は `SoundEffectManager` 内部。Webはクリップ名・ファイル構成を知らない。

## Consequences
- `SoundEffectManager` は enum→clip 辞書に8種追加＋クールダウン。200行規約内に収める
- `actionContract.ts` / `Client.WebUiHost/Game/Actions/` に `sound.play` ハンドラ新設（既存 `IActionHandler` 同型）
- `NotificationTopic` が SE を鳴らす副作用を持つ。テストは通知→SE種別の対応表を検証
- Prefab の AudioClip 配線は `uloop execute-dynamic-code` 経由（手編集禁止）
