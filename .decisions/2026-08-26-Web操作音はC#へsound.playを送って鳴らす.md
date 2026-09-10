# Web操作音はWeb→C#へ「鳴らせ」アクションを送って鳴らす

- 日付: 2026-08-26
- 決定: Web UIの操作音（研究ノード選択・詳細ペイン閉じ・研究ボタン等）は actionContract に `sound.play {type}` を1本足し、C#側 SoundEffectManager が再生する
- 棄却案: (a) Webview内で `<audio>` を直接再生 (b) 操作音はWeb・結果音はC#の併用
- 理由: 音声資産・音量設定・将来のミキサーをUnity側へ一本化する。WebSocket 1ホップのレイテンシは許容
- 出所: ユーザー裁定 2026-08-26 質問「Web UIの操作音はどこで鳴らしますか」→ 選択「Web→C#へ『鳴らせ』を送る」
- リンク: SoundEffectManager.cs / actionContract.ts
