# codex委譲モード（Claudeが実装をcodexへ任せる）

人間が「実装はcodexに任せて、テストとレビューはこっちで」と指示したときのモード。実装を書くのはcodex（ユーザー全体スキル `~/.agents/skills/codex-implement` のラッパー。既定 `gpt-5.6-sol`）で、Claude本体は実装コードを書かない。規模ゲートの判定（単一subagent／SDD本体）は、codexへ渡す進め方の指示に置き換わる。

## 役割分担

| 担い手 | やること |
|---|---|
| codex | 実装。計画の残りの実装タスクを**1回の依頼でまとめて**受け、自分でSDDを回す（タスクごとの派遣はcodex側の中で行う） |
| Claude本体 | worktree隔離・事前計画レビュー・codexへの依頼・コンパイル／テスト・コミット・タスクレビュー（subagent）・裁定の取りまとめ・実機確認・最終レビュー・PR |

## 依頼の出し方

- **Claudeはタスクを1つずつ渡さない。** 「subagent-driven-development スキルで `<plan>` の Task N から実装して。実装パートのみ。Task X（実機確認）・最終レビュー・PR作成はやらない」と1回だけ依頼する。依頼文にplanの中身を転記しない（codexが自分で読む）。
- 呼び出しは `node ~/.agents/skills/codex-implement/scripts/codex-implement.mjs --cd <worktree> --task "..." < /dev/null`。**`< /dev/null` は必須**で、無いと `Reading additional input from stdin...` で止まる。
- 実行は `run_in_background: true` で起動し、**Claudeはそのままターンを終えて完了通知を待つ**。前景でのポーリングループ、途中経過の覗き見、sleepでの待機はしない（コンテキストの浪費になる。2026-09-24 ユーザー指摘）。
- セッションID（stderrの `--session <UUID>`）を台帳に控える。

## サンドボックスの制約（依頼文に必ず書く）

codexは `workspace-write` サンドボックスで動くので、次はできない。できない前提で依頼文に「やらないこと」と書き、Claudeが引き取る。

- **git commit / add**: linked worktreeの `.git` 共通ディレクトリが `--cd` の外にあるため失敗する。コミットはClaudeが行う。
- **uloop / Unity**: 起動もコンパイルもできない。`.cs` のコンパイルとUnityテストはClaudeが回す。
- **ポートの待受**（Playwrightのmock-host、HTTPテスト）: `listen EPERM` で失敗する。e2eとHTTP系テストはClaudeが回す。
- **`--cd` 外や読み取り専用の場所への書き込み**（例: symlink先の `.agents/skills/`、Beads DB）: codexにはパッチファイルを残させ、Claudeが `git apply` する。
- **報告ファイル名**: `.superpowers/sdd/` には他計画のtrackedファイル（`task-4-report.md` 等）が混ざっている。codexには固有接頭辞（例: `pmh-`）付きの名前で書かせる。上書きされたら `git checkout --` で戻す。

## codexが戻ったらClaudeがやること

1. `git status` / `git diff` を全件見る。Unityが自動で書き換えるファイル（`_CompileRequester.cs`・`.moorestech-external-revisions.json` 等）は戻す。
2. **200行制限の抜け道を探す。** codexは制限に収めるために空行を詰めたり、クラスの説明コメントを削ったり、メソッドを式形式に畳んだり、`ws.close(); return;` のように2文を1行へ詰めたりすることがある。ちょうど200行も違反（規約は200行未満）。見つけたら、レビューで差し戻して正しく分割させる。
3. webuiの `e2e/`（mock-host・spec）は `pnpm tsc -b` の対象外で、分割時のimport消し忘れはmock-hostの起動時にReferenceErrorで初めて出る。e2eに触る依頼では完了条件に `pnpm exec tsc -p e2e/tsconfig.json --noEmit` を入れる。
4. コンパイル → 影響ドメイン全域のテスト（狭いフィルタだけにしない）→ PlayMode遷移テスト → webuiの vitest / tsc / lint / e2e を自分で回す。どれもバックグラウンド起動＋完了通知待ち。失敗したら、変更前のbaseでも落ちるかを確かめてから原因を判断する。
5. 通ったらClaudeがタスク単位でコミットし、`scripts/review-package` でdiffを作ってタスクレビュアーsubagentへ渡す（通常のSDDと同じ）。実装者の報告は、codexの最終出力とClaudeの検証結果を1ファイルにまとめて渡す。
6. レビュー所見の修正もcodexへ依頼する（所見ファイルのパスを渡すだけ。所見を転記しない）。planが義務付けた内容と衝突する所見や、仕様の穴は人間の裁定に回し、裁定の結果を要件ファイルにまとめてcodexへ渡す。

## 台帳

通常の台帳の行に、codexのセッションIDと「committed by controller」を足す。例: `Task 2: codex session <uuid>; committed <sha7> (base <sha7>); review pending` → レビュー後に `Task 2: complete (...)`。
