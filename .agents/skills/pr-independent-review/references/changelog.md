# 規則の由来（pr-independent-review）

SKILL.md 本文の規則がどの事故・裁定から生まれたかの台帳。本文には規則だけを置き、根拠はここに残す。
通常のレビュー実行では読まない。規則を緩めたい・疑わしいと思ったときに引く。

| 本文の規則 | 由来 |
| --- | --- |
| `$CANON` を SHA ピンの使い捨て worktree にする | 2026-08-05 裁定で正典 tree 導入。2026-08-19 裁定で共有 worktree の毎回 reset を SHA ピンへ変更（並列レビューが共有 canon を reset し合い、実行中に物差しが差し替わった） |
| `$CANON` を「この SKILL.md が置かれている tree」にしない | 2026-08-05 実測。メイン worktree は他セッションが実装作業中で、レビュー実行中にブランチが切り替わった |
| `$ORIGIN` で `gh pr checkout` を実行しない | 2026-08-05 に実際に発生。cwd の worktree のブランチが切り替わり、メイン worktree が他セッションの作業ブランチから引き剥がされた |
| PR ごとに worktree を1つ作り使い回さない | ユーザー裁定 2026-08-05。並行レビューの奪い合いで、修正作業中のツリーを次のレビューが `reset --hard` で消した |
| MERGED PR の BASE_REF は `<mergeCommit>^1` | PR #1041 実測。`origin/<base>` を使うと merge-base が HEAD 自身になり、patch 空・novelty 全空・exit 0 の沈黙故障で verdict が「自動マージ可」に化けた |
| `<mergeCommit>` は `.mergeCommit.oid` の SHA へ展開する | `gh pr view --json mergeCommit` はオブジェクトを返す。そのまま渡すと `unknown revision` |
| patch 生成のフラグ省略禁止 | ユーザー側 git 設定（quotepath / color / ext-diff / textconv / renames）が patch を静かに痩せさせ、決定論チェックとレンズの判定が外れる |
| プレイテストシナリオの `.cs` を patch から除外 | ユーザー裁定 2026-08-16 / PR #1137-F12。使い捨ての操作台本をプロダクトコードの規約で裁かない |
| 外部リビジョンピンの差分は指摘対象にしない | ユーザー裁定 2026-08-16 / PR #1127-F06。兄弟クローンの HEAD へ追随するだけの機械的更新 |
| PR 内で新設・変更された ADR は `[agent前提]` へ降格 | 免責ロンダリング事故の再演経路を塞ぐ。独立セッションからは PR 内 ADR のユーザー承認の実在を検証できない |
| 見逃しの記録粒度は verdict 比較のまま、内訳は reconcile が追記 | ユーザー裁定 2026-08-02 |
| verdict 一致でも reconcile を省かない | PR #1095 で verdict 一致のまま missed 17件 |
| 改善キューの closed 根拠は実 diff バックテストまで | 2026-08-02 PR #1095 改善で合成 fixture の緑だけを closed 根拠にした前科 |
| reconcile の入力は人間コメントの `commit_id` に紐づける | PR #1095 で人間指摘の commit と自動レビュー時の head が食い違い、実装形まで別物だった |
| ダイジェストの閲覧は裁定サイトへ一本化 | [[2026-08-23-レビューダイジェストの公開は裁定サイトへ一本化する]] |
| 無人起動は cmux 対話モード、終了合図は `session-done.marker` | ADR 0023。2026-08-20 までは `claude -p`。対話モードは「ターン終了＝プロセス終了」の合図を持たない |
| 中止時は `abort.json` を書く | 書かずに終わると poller が自壊と誤認し、fail-closed を押し切って resume する |
| `$RUNDIR` を `/tmp` に置かない・消さない | OS に掃除される。中間生成物は reconcile のフォレンジック・リプレイの入力そのもの。裁定サイトも `$RUNDIR` を配信実体として読む |
| 記録類はセッションが commit しない | `.dev-hooks/logs-sync.mjs` が Stop/SessionEnd で add→commit→pull --rebase→push まで行う。旧版の「正典 tree へ書き込むが commit しない」は記録先を `$LOGS` へ分離する前の記述 |
| `which codex` を使わない | 封じ込め PATH では失敗し、10本連続で偽縮退した |
