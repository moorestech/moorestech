# タスク: brainstorming の実走（圧縮版リグレッション r1）

以下を実行してください:

Skill({skill: "brainstorming", args: "建設メニュー中のスポイト機能を実装したい。ミドルクリック"})

このタスクは実在ユーザーの設計依頼を無人で再現するものです。設計対話でユーザーの回答・承認が必要な場面では、以下の「既知のユーザー回答」を採用して自走してください（AskUserQuestion を出す代わりにこの回答を使ってよい）:

- スポイトの有効範囲: 配置モード（PlaceBlock）中と通常プレイ（GameScreen）中の両方で有効にする
- ピック時にブロックの向き（回転）もコピーする
- 設計提示・spec レビュー・プランの承認: すべて「承認」
- 上記に無い選択が必要になったら: 既存コードベースのパターンに最も整合する案を自分で選ぶ

スコープ: brainstorming の設計対話 → spec 作成 → writing-plans のプラン作成まで。プランの実行には進まないこと。spec / plan の git commit はスキルの手順どおり行ってよい（隔離 worktree で安全）。

処理が終了したら:

1. /Users/katsumi/moorestech/.mso/live-trial/20260708-163041-eyedropper-compress-r1/out/status.json に完了マーカーを 1 ファイルだけ Write する。内容:
   {"status": "PASS|FAIL|ERROR", "spec_path": "<絶対パス or null>", "plan_path": "<絶対パス or null>"}
2. 「DONE: <status>」と 1 行だけ報告する。

制約:
- 途中で人間に質問せず最後まで自走すること。
- skill の手順に忠実に従い、人手の追加判断・省略をしないこと。
- out/ には status.json 以外を書かないこと。
