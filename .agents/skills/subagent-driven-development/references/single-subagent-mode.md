# 単一subagent実装モードの詳細（規模ゲート未満）

SKILL.md「単一subagent実装モード」の手順詳細・継続再派遣・fix経路・復旧。裁定の出所は ADR 0053 と `.decisions/2026-09-08-SDD閾値未満は本体インラインでなく単一opus-subagentで実装する.md`、`.decisions/2026-09-09-SDD単一モードの継続上限と途中終了ステータスとfix経路.md`。

## 手順の補足

- **`scripts/sdd-workspace` は隔離worktree側をcwdにして実行する。** このスクリプトは `git rev-parse --show-toplevel` でcwd基準にツリーを解決する。本体ワーキングツリーで実行すると報告ファイルがworktree外を指し、契約の「作業ディレクトリの外で編集しない」と正面衝突する
- **同名の `single-report.md` が既に在れば派遣前に削除する。** worktree再利用（隔離の例外1）で前計画の `Task N: done` 行が残ると、復旧の一次ソースが汚染される
- **フォアグラウンドで派遣する。** バックグラウンド派遣は孤児化して止まる事故があった
- `scripts/task-brief` は使わず、計画ファイル全体の絶対パスをブリーフとして直渡しする（`[PLAN_FILE_ABS]`）。実装範囲は `[TASK_RANGE]` で列挙する（末尾の最終レビュー・PR作成タスクは含めない）
- **計画ファイル全体を渡すため、派遣プロンプトには `single-implementer-prompt.md` のヘッダ無効化文言を必ず含める**（冒頭の `> **For agentic workers:**` / `> **For the controller session:**` ブロックと末尾の最終レビュー・PR作成タスクはコントローラー向けであり、subagentはSDDスキルを起動せず・subagentを派遣せず・PRも作成しない）。これが無いと subagent が平文の命令形ヘッダを自分への指示と読み、入れ子のSDD起動やPR作成という取り消せない副作用が起きる
- 報告ファイルは `<workspace>/single-report.md` 固定。subagentがタスク完了ごとに `Task N: done <sha7> — <1行要約>` を追記する。継続派遣は同じ報告ファイルへ**追記**させる（Writeで置き換えさせない。1体目の done 行が消えると完了範囲の確定が壊れる）
- **計画のチェックボックス（`- [ ]`）は誰も更新しない。** 進捗の正は報告ファイルの `Task N: done` 行と台帳の `Single-subagent:` 行であり、復旧もこの2つだけを読む。チェックボックスの未更新を未完了の根拠にしないこと
- DONE の網羅突合: 報告ファイルの `Task N: done` 行が `[TASK_RANGE]` の全番号を覆い、`git log <base>..HEAD --oneline` の `Task N:` コミットと一致することを確認する。欠けていれば継続再派遣する。タスクレビュアーは派遣しない

## 継続再派遣（途中失敗時）

継続再派遣の対象は**途中終了（ステータス `PARTIAL` — コンテキスト枯渇・一部タスクのみ完了）だけ**であり、**上限は2回**である。`NEEDS_CONTEXT` は完了タスク数に関わらず不足コンテキストを回答して再派遣し、**この2回には数えない**。`BLOCKED` は SKILL.md「Implementerのステータス対応」の1〜4に従い、原因が計画自体の誤り（4）なら**継続回数に関わらず即座に人間へエスカレーションする**（継続2回やSDD本体への切替を先に消化しない）。

継続の手順: 報告ファイルの `Task N: done <sha7>` 行と `git log <base>..HEAD --oneline` で完了タスクを確定し、残りタスクだけを `[TASK_RANGE]` に列挙した継続subagentを同じテンプレで派遣する（報告ファイルは同じものへ**追記**させる。`## コンテキスト` の書き方は `single-implementer-prompt.md` 末尾）。台帳に `Single-subagent: continuation #k from Task N base <sha7>` を追記する。**`k` は1始まりで、この台帳行の本数がそのまま継続回数である** — `NEEDS_CONTEXT` への回答再派遣と DONE_WITH_CONCERNS の fix 派遣はこの行を書かない（＝数えない）。したがって compaction 後は `continuation #` 行を数えるだけで残り回数が確定する。

2回の継続でも終わらなければ規模誤判定とみなし、残りタスクをSDD本体（タスクごと派遣＋タスクレビュー）へ切り替える。台帳には切替行を逐語で1行書く: `Single-subagent: switched to SDD per-task at Task N (continuations 2)`。この行があれば復旧時に「単一モードは終了しており、以降はSDD本体の `Task N: complete` 行を読む」と機械的に判定できる。

## DONE_WITH_CONCERNS の fix 経路

懸念対応の前に DONE と同じ網羅突合を行う。未完了タスクがあれば継続再派遣する（懸念対応はその後）。網羅していれば懸念を読み、正しさ・スコープに関するものなら最終レビュー所見と同じ経路（単一fix subagent・opus）で直してから最終レビューへ（継続派遣ではないので上限2回には数えない）。単なる所見ならメモして最終レビューへ。

このモードにはタスクレビュー報告ファイルが存在しないため、**fix 派遣に渡す入力は「報告ファイル（`<workspace>/single-report.md`）の絶対パス」＋「そのうち懸念セクションを読め」の1行＋`implementer-contract.md` の絶対パス**とする（既存規約と同じく懸念本文をコントローラーが転記しない。懸念は implementer が報告ファイルへ書き切っており、そこが正本である）。fix subagentは同じ報告ファイルへ fix 報告（テスト結果込み）を追記する。

## 台帳とcompaction後の復旧

コントローラーが書くのは次の行だけ（タスク別の完了は subagent が報告ファイルへ書く）:

- 派遣時 `Single-subagent: dispatched base <sha7> report <path>`
- 継続時 `Single-subagent: continuation #k from Task N base <sha7>`
- 切替時 `Single-subagent: switched to SDD per-task at Task N (continuations 2)`
- 完了時 `Single-subagent: complete (commits <base7>..<head7>)`

復旧順は **台帳 → 元subagentの生存確認（ListAgents） → 報告ファイル → `git log`**。台帳に `dispatched` があって `complete` が無ければ、subagentが走っているか途中終了している — **継続派遣の前に ListAgents 等で元subagentの生存を確認し、生きていれば結果を待つ**（compaction後も subagent は生存しており、死亡と決めつけて再派遣し同一worktreeを二重編集した実事故がある）。不在または報告済みなら、報告ファイルの `Task N: done` 行と `git log` で完了タスクを確定する。完了タスクが `[TASK_RANGE]` を全て覆っていれば再派遣せず、完了行を台帳に補記して最終ブランチ全体レビューへ進む。覆っていなければ継続再派遣する。

## ワークフロー例

```
You: 実装4タスク・5ファイル → 単一subagent。worktreeを作成し事前計画レビューを通しました。

[sdd-workspace で報告パスを確保、BASE=ab12cd3 を台帳に記帳]
[single-implementer-prompt.md で model: opus をフォアグラウンド派遣]

Implementer:
  - Task 1〜4 を順に実装、タスクごとにコミット（4コミット）
  - 報告ファイルに Task N: done <sha> を4行追記
  - 12/12 tests passing、自己レビュー: 問題なし
  - ステータス: DONE

[報告ファイルの Task 1〜4: done 行が git log と一致することを確認]
[台帳に complete 行を記帳]
[moores-code-review を Skill ツールで実行 → 所見2件 → 単一fix subagent(opus)へ]
[pr-create で PR 作成]
```
