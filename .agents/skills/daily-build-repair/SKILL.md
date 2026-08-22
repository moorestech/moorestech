---
name: daily-build-repair
description: |
  日次ビルド失敗Issueを起点に前方修正しPRを作る無人スキル。
  Use When: `/daily-build-repair <Issue番号>` で起動された時
hooks:
  # 無人実行の関所。スキル発動中だけ有効（repo横断のsettings.jsonに置くと開発者の通常セッションまで巻き込む）
  # Gate for unattended runs; active only while this skill runs, unlike a repo-wide settings.json hook
  PreToolUse:
    - matcher: "AskUserQuestion"
      hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py ask"
  Stop:
    - hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py stop repair"
---

# daily-build-repair — 日次ビルド失敗の前方修正（無人実行）

「日次ビルド失敗」ラベル付き Issue を1件受け取り、原因を特定して前方修正のPRを作り、
`repair-result.json` を書いて終える無人スキル。poller はこのスキルを起動するだけで、
修復ロジック自体は持たない（`pr-independent-review` / `pr-adjudicated-apply` と同じ役割分担）。

## HARD GATE

**修復対象は日次ビルドを赤くしている原因のみ。** ついでのリファクタ・無関係な改善・
他の不具合修正は一切行わない。見つけた別の問題は Issue へコメントで残すだけにする。

## 最重要: 無人起動でも「repair-result.json で終える」

このスキルは poller から `launch_claude` 経由で cmux ワークスペース上の**対話モード** claude で
フォアグラウンド起動される（`pr-adjudicated-apply` と同形）。対話モードではターンを終えても
プロセスは消えない。**poller は `repair-result.json` の存在とプロセス生存で監視している**だけなので、
ターンを閉じただけでは何も終わらない。

- **ターンを終える前に必ず `repair-result.json` を書くこと。** 「後で確認します」と述べて
  ターンを閉じてはいけない。スケジュールされた再開はこの実行環境に存在しない
- Step 6 のビルド待ちのように結果が返るまで数分〜数十分かかる処理は、**同一ターン内で
  ブロッキングして待つ**。待つこと自体がこのスキルの仕事である
- 終了は `repair-result.json` を書いた直後だけ。書く前に終わる終わり方は、成功・失敗
  いずれの意図であってもバグである
- リトライは**1回のみ**（`pr-adjudicated-apply` の `MAX_APPLY_RETRY=1` と同じ思想）。
  プロセスが死んで `repair-result.json` も無ければ、poller が新しいセッションで
  1回だけ作り直す。それ以上の救済は無い

## Issue本文の形（Plan Aが起票する形式）

対象Issueの本文は先頭に `<!-- daily-build-issue -->` マーカーがあり、以下の構造を持つ:

- `- run: <URL>` / `- head: \`<sha>\`` / `- 前回グリーン: \`<sha>\`` または `（成功記録なし）`
- `## 失敗ジョブ:` 見出しの下に `- [<ジョブ名>](<URL>)` が並び、**各ジョブ行の直下に
  コードブロックでログ末尾4000文字が埋め込まれている**。多くの場合、Issue本文だけで
  エラー行を読める。ログURLを辿るのは本文の抜粋で原因を特定できないときの手段。
- `## 容疑者PR:` 見出しの下に `- #<番号> <タイトル>` が並ぶ（前回グリーンからの差分PR一覧。
  どれが怪しいか当たりを付ける材料であり、revert 対象ではない）
- 末尾に「前方修正で対応します。bisect は行わない（ADR 0028）。」の趣旨の一文がある

## Step 1: 対象の読み取り

```bash
gh issue view <N> --json title,body,labels
```

本文をそのまま読み、まず `## 失敗ジョブ:` 直下のログ抜粋からエラー行を特定する。
抜粋だけで原因が特定できない場合のみ、ジョブURLから `gh run view <run-id> --log-failed`
で該当ジョブのログ全体を辿る。

## Step 2: 作業場所の用意

```bash
moores-wt new fix/daily-build-<N> --no-editor
```

**メインワークツリーで作業しない**（`CLAUDE.local.md` の裁定）。以降の全作業は
この使い捨てworktree内で行う。

## Step 3: 前方修正

現在の master に対して直接直す。**bisect は行わない**。`## 容疑者PR:` の一覧は
「どのPRの変更が怪しいか当たりを付ける材料」であって、そのPRを revert するためのもの
ではない。エラーが指すファイルを直接直すのが基本。HARD GATE のとおり、修復対象は
ビルドを赤くしている原因のみに限定する。

## Step 4: ローカル検証

`.cs` を触ったら `uloop compile` を通す。関連するテストがあれば実行する。

## Step 5: PR作成

`pr-create` スキルでPRを作る。本文に `Fixes #<N>` と、何が壊れていて何を直したかを書く。

**PRの自動マージは絶対に行わない。** `gh` コマンドでPRをマージする操作は一切実行しない。

## Step 6: ビルド検証

既に「ビルド検証」ラベルが付いている場合は `--add-label` の再実行ではイベントが発火しない。
一度 `--remove-label` してから `--add-label` し直す（remove→addのトグル）。

```bash
gh pr edit <PR番号> --remove-label "ビルド検証" 2>/dev/null; gh pr edit <PR番号> --add-label "ビルド検証"
```

ラベル付与だけでは緑/赤を判定できない。`gh pr checks <PR番号> --watch` は「ビルド検証」
ラベル発火のrunがまだ登録されていない間は空振りしうるため使わない。代わりに以下の手順で
runを特定してからポーリングする。

`build.yml` は `pull_request: types: [labeled]` で発火するため、無関係なラベル付与
（poller が付ける `独立レビュー:実行中` 等）でも空run（`conclusion: skipped`）が生成されうる。
`--limit 1` はこの空runを掴む恐れがあるため、複数件取得して `skipped` を除外する。
# `build.yml` triggers on `pull_request: types: [labeled]`, so any unrelated label
# (e.g. poller's `独立レビュー:実行中`) also spawns a run that completes as `skipped`.
# Fetch several runs and discard `skipped` ones instead of trusting `--limit 1`.

```bash
# run登録待ち: ラベル発火のrunがGitHub Actions側に現れるまで数十秒〜数分かかることがある
# Wait for the run to register; the label-triggered run can take up to a few minutes to appear
gh run list --workflow="Unity Build" --branch <ブランチ名> --limit 10 \
  --json databaseId,status,conclusion,event \
  --jq '[.[] | select(.conclusion != "skipped")][0]'
```

`databaseId` が取れたら、その run の `status` が `completed` になるまでポーリングで待つ。

```bash
gh run watch <databaseId> --exit-status
```

`Unity Build` は4ジョブ並列で1時間前後かかりうる。**同一ターン内でブロッキングして待つ**
（「最重要」節参照）。ポーリング中に深夜枠（09:00 JST）の停止指示（`## 停止指示`）が
届いた場合は、待機を打ち切りStep 8の停止手順へ移る。

- `conclusion == "success"` なら `verified: true`
- `conclusion` が `failure`/`cancelled` 等なら `verified: false`（Step 7参照）

## Step 7: 結果の書き出し

`repair-result.json` を `$PR_REVIEW_RUNDIR_BASE/issue-<N>/repair-result.json`
（`PR_REVIEW_RUNDIR_BASE` 既定値: `~/hermes-agent/data/repos/moorestech_logs/harness/pr-independent-review/runs/`）
へ書く。**`state/` は poller の内部状態専用であり、成果物はそこに置かない。**

スキーマ:

```json
{"status": "success|failure|timeout", "pr_number": 1234, "verified": true, "summary": "...", "remaining": "..."}
```

- `status: "success"` は `verified: true`（Step 6でビルド検証ラベルにより緑を確認済み）
  のときだけ許される。
- `status: "failure"` は次のいずれかで使う。`summary` に何が起きたかを書き、`remaining` に
  残作業を書く:
  - 原因を特定できず修正に至らなかった
  - 修正したが `uloop compile` 等のローカル検証が通らなかった
  - PRは作れたが Step 6 のビルド検証が赤のまま（`verified: false`）だった
- `status: "timeout"` は深夜枠の打ち切りによる中断で使う（Step 8参照）。`failure` との違いは、
  `timeout` は翌朝 `remaining` を元にセッションを継続できる中断であり、`failure` は
  このIssueに対する試行を打ち切る終端であること。
- **このファイルを書くまでセッションを終えない。**

poller（Task 3/4）はこのファイルの出現をフェーズ完了の合図にする。

## Step 8: 停止指示を受けたとき

poller から `## 停止指示` を含むプロンプトで停止指示を受けたら、その時点で
「どこまで調べたか・何を直しかけたか・残作業」を以下でIssueへ投稿する。

```bash
gh issue comment <N> --body "..."
```

投稿後、`repair-result.json` に `status: "timeout"` と `remaining`（残作業の要約）を
書いてから終了する。
