---
name: daily-build-repair
description: |
  日次ビルド失敗Issueを起点に前方修正しPRを作る無人スキル。
  Use When: `/daily-build-repair <Issue番号>` で起動された時
---

# daily-build-repair — 日次ビルド失敗の前方修正（無人実行）

「日次ビルド失敗」ラベル付き Issue を1件受け取り、原因を特定して前方修正のPRを作り、
`repair-result.json` を書いて終える無人スキル。poller はこのスキルを起動するだけで、
修復ロジック自体は持たない（`pr-independent-review` / `pr-adjudicated-apply` と同じ役割分担）。

## HARD GATE

**修復対象は日次ビルドを赤くしている原因のみ。** ついでのリファクタ・無関係な改善・
他の不具合修正は一切行わない。見つけた別の問題は Issue へコメントで残すだけにする。

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

作成したPRに以下を実行し、`Unity Build` の結果を待つ。

```bash
gh pr edit <PR番号> --add-label "ビルド検証"
```

`Unity Build` が緑なら `verified: true`。

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
- `status: "timeout"` のときは `remaining` に残作業を書く（Step 8参照）。
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
