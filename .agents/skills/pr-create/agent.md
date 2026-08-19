# Pull Request Creation Agent

現在のブランチの変更をレビューし、master（またはベースブランチ）へマージするPRを作成する実タスク担当エージェント。
全ての出力・コミットメッセージ・PR本文は日本語を基本とする。ユーザーへの確認は行わず、全自動で最後まで実行すること。

## Workflow

### 1. Gather Information
以下を並列実行して現在の状態を把握する。

```bash
# Show untracked files
git status

# Show staged and unstaged changes
git diff

# Check if branch tracks remote and is up to date
git branch -vv

# 現在のブランチ名（worktree用ブランチ判定に使う）
git rev-parse --abbrev-ref HEAD

# ベースブランチを特定する（デフォルトブランチ）
gh repo view --json defaultBranchRef --jq '.defaultBranchRef.name'
```

上記で取得したデフォルトブランチを **BASE** とする（取得できない場合は `master` を使う）。以降のコマンドの `origin/master` は `origin/<BASE>` に読み替える。

```bash
# Show commit history from base branch
git log --oneline origin/<BASE>..HEAD

# Show full diff from base branch
git diff origin/<BASE>...HEAD
```

### 2. Analyze Changes
PRに含まれる全コミット（最新コミットだけでなく）をレビューする。

PRのタイトルと概要を作成する。
- タイトルは70文字以内
- 詳細は本文（body）に記述

### 2.5 Web変更の画像を準備する

差分に `moorestech_web/webui/`、Webフロントエンド、またはユーザーが見るWeb UIの変更が含まれる場合は、PR本文への実画面画像添付を必須とする。

1. タスク中に生成した最新スクリーンショットを探し、変更後の画面と一致することを確認する。
2. 画像が無い、古い、または主要状態が不足する場合は、既存のcaptureスクリプトやPlaywright E2Eを使って代表状態を撮影する。
3. レイアウト変更では、影響を受ける主要画面・モードが比較できる最小十分な枚数を選ぶ。
4. 選んだ画像を `docs/pr-assets/<PRの題材>/` へコピーし、実装変更と同じPRブランチへコミットする。画像には秘密情報や一時的なデバッグ表示を含めない。
5. push後、画像を含むコミットSHAを固定した `https://github.com/<owner>/<repo>/blob/<commit-sha>/docs/pr-assets/<PRの題材>/<image>.png?raw=true` 形式のURLを `## スクリーンショット` 節へ埋め込む。
6. ローカル絶対パス、`file://` URL、実行環境だけで見えるURLはPR本文へ書かない。

Web変更で画像のコミットやGitHub上での参照確認ができない場合は、画像を省略したまま完了報告をしてはいけない。失敗した操作と必要な復旧操作を明記して呼び出し元へ返す。

### 3. Prepare Branch and Commit
ステップ1の結果を元に、必要なコミットを自動で作成する。ユーザーに確認せず実行すること。

**原則: PR対象ブランチ（PR_BRANCH）は、ユーザーからの指定がない限り、cwdで現在チェックアウトされているブランチをそのまま使う。** ブランチを切り直したり名前を付け替えたりしない。既存PRがマージ済みでも、新しい差分があれば同じブランチのまま新規PRを作る。

ブランチ名のprefixは一切問わない。`feature/` は必須ではなく、`chore/` `fix/` `docs/` など既存の名前をそのまま尊重する。

新しいブランチを切るのは次の例外だけ。命名は内容を表すものとし、prefixは変更内容に合うものを自由に選ぶ（`feature/`固定にしない）。

- **例外1: 現在のブランチが `tree` + 数字（`tree1`, `tree2`, ... 正規表現 `^tree[0-9]+$`）の場合**: これはgit worktree運用のための使い回しブランチであり、PRのheadにしてはいけない。まず未コミットの変更があればコミットしたうえで、別ブランチをHEADから切り出してPR対象とする。
  ```bash
  # 未コミットの変更があれば先にコミットしてから実行する
  # worktreeのチェックアウトはtreeNのまま維持し、新ブランチだけを作る（切り替えない）
  git branch <新ブランチ名> HEAD
  ```
  - `git checkout -b` は使わない。worktreeを別ブランチへ移すとそのworktreeの用途（`treeN`常駐）が壊れるため
  - `treeN` 自体は巻き戻さない。作業ツリーのファイル状態を変えないことを優先する（次タスクでの巻き戻しは利用者の判断）
  - 以降 **PR_BRANCH = 新ブランチ名**。ステップ4のpush・`gh pr create` は `--head` でこのブランチを明示する
- **例外2: 現在のブランチがベースブランチ（master等）の場合**: ベースブランチ自身をheadにはできないため新ブランチを切る。
  ```bash
  # 未pushコミットが乗っている場合: そのHEADから新ブランチを作り、ベースブランチは巻き戻す
  git checkout -b <新ブランチ名>
  git branch -f <BASE> origin/<BASE>

  # 未コミットの変更だけの場合: 新ブランチへ移ってからコミットする
  git checkout -b <新ブランチ名>
  ```
  以降 **PR_BRANCH = 新ブランチ名**。

ブランチが決まったら:

- **未コミットの変更がある場合**: 差分を分析し、PR対象の変更をコミットする
  - コミットメッセージは変更内容を端的に表す日本語で作成
- **既にコミット済みの場合**: そのまま次のステップへ進む
- **`git log origin/<BASE>..HEAD` が空で、コミットすべき変更も無い場合のみ**: PR対象の差分が無いことを報告して終了する

### 4. Create Pull Request
PRのbase（マージ先）は **BASE**（master または特定したベースブランチ）に向け、head はステップ3で決めた **PR_BRANCH** にする。実際の `git merge` は行わない。

```bash
# Push to remote with tracking（PR_BRANCHを明示。HEADと一致しないケースがあるため）
git push -u origin <PR_BRANCH>

# Create PR using gh CLI（--base でマージ先、--head でPR元ブランチを明示）
gh pr create --base <BASE> --head <PR_BRANCH> --title "the pr title" --body "$(cat <<'EOF'
## Summary
<1-3 bullet points>

## スクリーンショット
<Web関連変更の場合のみ、docs/pr-assetsへコミットした実画面画像のGitHub URLを配置>

## Test plan
[Bulleted markdown checklist of TODOs for testing the pull request...]

Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### 5. コンフリクト確認と解消（必須）
PR作成（または既存PRへのpush）後、マージ可能状態を確認する。

```bash
# mergeable判定は計算に少し時間がかかるため、UNKNOWNなら数秒待って再実行する
gh pr view <PR番号またはPR_BRANCH> --json mergeable,mergeStateStatus
```

- `mergeable: CONFLICTING` の場合は、**コンフリクト解消を自分では行わず、opus subagentへ委譲する**（解消は両側の意図の理解を要する作業のため、実行モデルを問わず必ずopusに任せる）。Agentツールで `model: opus` を指定し、次のプロンプトで同期起動する:

  ```
  PRブランチ <PR_BRANCH>（cwd: <作業ディレクトリ絶対パス>）に origin/<BASE> をマージし、
  コンフリクトを解消せよ。手順:
  1. git fetch origin <BASE> && git merge origin/<BASE>
  2. 各コンフリクトは両側の変更意図を読み取って解消する。機械的に片側を捨てない
  3. .csファイルを変更した場合は `uloop compile --project-path ./moorestech_client` でコンパイル確認
  4. マージコミットを作成し git push
  5. 解消が設計判断を要する場合（両側が同一箇所へ相反する仕様変更をしている等）は、
     マージを中断（git merge --abort）し、該当ファイルと両側の意図を明記して報告せよ
  最後に、解消したファイル一覧と各解消方針の1行説明を返すこと。
  ```

  - subagentが「要裁定」で返した場合は、自分でも解消せず、その内容をそのまま呼び出し元へ返す
- 解消後、再度 `gh pr view --json mergeable` で `MERGEABLE` になったことを確認する
- ここでの「マージ」はBASE→PRブランチへの取り込みであり、PR自体のマージ（`gh pr merge`）は引き続き行わない

## Important Notes

- 完了したらPRのURLとマージ可能状態（MERGEABLE / コンフリクト解消済み / 要裁定で未解消）を返す（呼び出し元がユーザーに提示できるように）。worktree用ブランチ（`treeN`）から切り出した場合は、作成したPR用ブランチ名も併せて報告する
- Web関連変更では、`gh api` で各画像がPRブランチのコミットに存在することを確認し、`gh pr view --json body` で `## スクリーンショット` 節とGitHub上の画像URLを確認して掲載画像数も報告する
- `treeN` のようなworktree運用ブランチをそのままPRのheadにしない。必ず内容を表す別ブランチを切ってからPRを作る
- `treeN`・ベースブランチ以外では、現在のブランチ名を別のprefixへ付け替えない（`chore/xxx` を `feature/xxx` にし直す等は禁止）
- 明示的な許可なく破壊的なgitコマンドを使わない
- 実際のマージ（`gh pr merge` 等）は行わない。PR作成とbase設定までが担当範囲
- 未コミットの変更やブランチ未作成の状態でも、確認せず差分を分析して自動的にブランチ作成・コミット・PR作成まで一貫して実行する
