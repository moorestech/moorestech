# シャドー台帳 — pr-independent-review

独立レビューのverdictと人間の実マージ判断を突き合わせ、見逃し率を実測するための帳簿。
「あなたの実判断」「一致」列は人間が後で記入する。追記型・行の書き換え禁止（記入列を除く）。

`head` 列はレビューしたHEADのshort SHA（先頭7桁）。同じPRを別headで再レビューした行を区別するため空欄にしない。
`縮退` 列は「なし（5系統フル実行）／縮退内容（codex不在等）／スタブ」のいずれか。
verdictを額面どおり見逃し率へ数えてよいかの判別に使うため空欄にしない。
台帳はverdict比較の粒度までとし、`一致` 列が不一致になったPRのみ突き合わせセッションが `records/pr-<番号>.md`（または `-rN`）へ caught / missed / false-positive の欠陥単位内訳を追記する（人間は確認のみ・見逃し率は missed / human-confirmed で集計）。

| 日付 | PR | head | verdict | 新形 | suppressed | 縮退 | あなたの実判断 | 一致 |
|---|---|---|---|---|---|---|---|---|
| 2026-07-27 | #1041 | 未記録 | 未測定（スタブ） | 3 | 0（未収集） | スタブ（Step 6未実行・配管スモークテスト） |  |  |
| 2026-08-02 | #1116 | 2bf849b | Critical差し戻し | 0 | 6 | fable指定不可でprecedent-alignment/Fable全般をopus実行 |  |  |
| 2026-08-02 | #1111 | 80935cb | Critical差し戻し | 20 | 6 | fable指定不可でprecedent-alignment/Fable全般をopus実行・novelty gate測定器修正2件 |  |  |
