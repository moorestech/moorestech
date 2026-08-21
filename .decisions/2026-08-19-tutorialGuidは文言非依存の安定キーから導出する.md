# tutorialGuidは文言非依存の安定キーから導出する

決定: generate_challenges.py の tutorialGuid を「表示文言＋tutorialParam内容」からのuuid5導出ではなく、文言に依存しない安定キー（チャレンジ識別＋チュートリアル枠）からの導出へ本PR内で変更する。LEGACY_TUTORIAL_GUIDS は一回限りの移行表として閉じる。

棄却案:
- 別タスクへ送る
- 現状維持（文言を直すたびにLEGACY表へ手で追記し続ける）

理由: ユーザー裁定 2026-08-19（AskUserQuestion）。pinText等を1文字直すだけでGUIDが変わり `challengeTutorial.<guid>.text` の既訳が無言で孤児化する構造を今のうちに断つ。challenges.json再生成とlocalization.csvのキー付け替えを伴う。
