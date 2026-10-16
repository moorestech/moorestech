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

### 2.5 報告ページの素材を集める

すべてのPRで報告ページ（`https://review.moores.tech/pr/<PR番号>/report/`）を公開し、PR本文の先頭行にそのURLを1本だけh1見出し（`# https://review.moores.tech/pr/<PR番号>/report/`）で大きく張る。リンク文字列で包まずURLそのものを見出しにする。書式・manifest・公開手順は [references/pr-report.md](references/pr-report.md) が正本。

1. 受入条件を現行headで示す最終成功素材（スクリーンショット、unityプレイ録画テストの `recording.mp4`、Playwright の撮影など）と、説明に必要な診断素材を選ぶ。古い素材や失敗素材を成功の証拠に使わず、原本は既存の記録先へ保全する。
2. 見た目が変わる変更（Web UI・ゲーム内UI・演出など）で選んだ素材が無い・古い・主要状態が欠ける場合は、必要な状態だけ既存のcaptureスクリプト・Playwright・unityプレイ録画テストで撮る。
3. PR作業のscratch（コミットしない場所）に `manifest.json` を書く。非visual変更では素材生成を目的としたUI録画を行わず、`no_media_reason`に理由を書く。

禁止: PR本文に画像・動画のURLを個別に並べる、`docs/pr-assets/` へ素材をコミットする、ローカルパス・`file://`・tailnet（share）のURLを書く。

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
- **例外2: 現在のブランチがベースブランチ（master等）の場合**: ベースブランチ自身をheadにはできないため、例外1と同じ手順で新ブランチを切る。未コミットの変更があれば先にコミットし、そのHEADから新ブランチだけを作る（切り替えない）。
  ```bash
  git branch <新ブランチ名> HEAD
  ```
  - `git checkout -b` と `git branch -f <BASE> origin/<BASE>` は使わない。メインworktreeでは main-worktree-guard hook が `git checkout -b` を拒否し、`branch -f` はローカル参照の巻き戻しで破壊的なため
  - ベースブランチのローカル参照は巻き戻さない（次回の pull が ff できなければ利用者の判断）
  - 以降 **PR_BRANCH = 新ブランチ名**。ステップ4のpush・`gh pr create` は `--head` でこのブランチを明示する

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
# （報告URLは公開後に差し替え）

## Summary
<1-3 bullet points>

## Test plan
[Bulleted markdown checklist of TODOs for testing the pull request...]

Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### 4.5 報告ページを公開する（必須）

PR番号が決まったら、mac-mini で報告ページを公開し、PR本文の先頭行を `# <公開URL>` に差し替える。既存PRへのpushで成果物が変わった場合も再公開する。

```bash
python3 scripts/pr-report/publish_pr_report.py <PR番号> <manifest.json>   # 標準出力が公開URL
gh pr edit <PR番号> --body-file <URLを入れた本文>
```

公開スクリプトが失敗した場合は、報告URL無しのまま完了報告をしてはいけない。失敗した操作と必要な復旧操作を明記して呼び出し元へ返す。

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
- `gh pr view --json body` で本文の先頭行が `# https://review.moores.tech/pr/<PR番号>/report/` であり、他に報告URLが無いことを確認し、公開スクリプトが出した「画像 N 枚 / 動画 M 本」を報告する
- `treeN` のようなworktree運用ブランチをそのままPRのheadにしない。必ず内容を表す別ブランチを切ってからPRを作る
- `treeN`・ベースブランチ以外では、現在のブランチ名を別のprefixへ付け替えない（`chore/xxx` を `feature/xxx` にし直す等は禁止）
- 明示的な許可なく破壊的なgitコマンドを使わない
- 実際のマージ（`gh pr merge` 等）は行わない。PR作成とbase設定までが担当範囲
- 未コミットの変更やブランチ未作成の状態でも、確認せず差分を分析して自動的にブランチ作成・コミット・PR作成まで一貫して実行する
