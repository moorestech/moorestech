# ポーズメニューのボタンはPanelActionButtonへ寄せる

決定: ポーズメニューのトップ（セーブ・セーブして終了・設定・バグ報告）と子画面の「戻る」ボタンを、素の Mantine Button から webui-design §8.6 の PanelActionButton へ寄せる。webui-design SKILL.md の「PauseMenuPanel に素の Mantine Button が残っている」という負債の記述も直す。

棄却案:
- 戻るボタンの variant="default" だけを外し、素の Mantine Button のままにする
- plan のコードどおりにして、戻るボタンのグレー面を受け入れる

理由: ユーザー裁定 2026-09-24。Task 4 のレビューで、plan のコードが「前例として引用しない負債」（素の Mantine Button）を新しいファイルへ広げ、コードベースで初めての variant="default" を持ち込むと指摘された。質問「ボタンの見た目」→「既存語彙へ寄せる」。

リンク: [[2026-09-24-ポーズメニューは4ボタンのトップから設定とバグ報告の子画面へ階層化する]]
