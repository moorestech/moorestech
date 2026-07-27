---
name: pr-independent-review
description: |
  実装セッションと完全に独立したセッションでPRをレビューする手動発火スキル。PR URLまたは番号を受け取り、
  レビュー専用worktreeにcheckoutして moores-code-review（report-only）＋新規性ゲートL1を実行し、
  実コード抜粋入りのインフォグラフィックHTMLダイジェスト（verdict/裁定カード/suppressed）と
  シャドー台帳を出力する。実装セッションの自己申告contextは一切受け取らない。
  Use When:
  1. 「/pr-independent-review <PR URL|番号>」で起動された時
  2. 「このPRを独立レビューして」「シャドーレビューして」と言われた時
---

# pr-independent-review — 独立セッションPRレビュー（シャドー運用v1）

対応spec: `docs/superpowers/specs/2026-07-27-pr-independent-review-design.md`

**正典tree**: このSKILL.md自身が置かれているリポジトリルート（以下 `$CANON`）。
スクリプト・レンズ・統合ルールは必ず `$CANON` の絶対パスで参照する。レビューworktree側の
`.claude/` は**絶対に使わない**（PRごとに測定器が変わり見逃し率実測が壊れる・自己弱体化経路）。

## Step 1: PR取得

`gh pr view <番号> --repo moorestech/moorestech --json number,title,body,baseRefName,headRefName,additions,deletions,files`
で取得。失敗（未認証・不存在）は即エラー終了し理由を報告する。黙って縮退しない。

## Step 2: レビューworktreeへcheckout

- 場所固定: `~/moorestech-worktrees/pr-review`。無ければ `git -C ~/moorestech worktree add ~/moorestech-worktrees/pr-review origin/master --detach` で作成
- 毎回リセット: `git -C ~/moorestech-worktrees/pr-review reset --hard && git -C ~/moorestech-worktrees/pr-review clean -fd`
- checkout: `cd ~/moorestech-worktrees/pr-review && gh pr checkout <番号> --detach`
  （--detach必須: PRブランチは実装worktreeが保持していることが多くブランチロックで失敗する）
- `git fetch origin <baseRefName>` してbaseを最新化する

## Step 3: patch生成（exclude方式）

    git -C ~/moorestech-worktrees/pr-review diff origin/<baseRefName>...HEAD -- . \
      ':(exclude)*.meta' ':(exclude)*.prefab' ':(exclude)*.asset' ':(exclude)*.unity' \
      ':(exclude)*.png' ':(exclude)*.jpg' ':(exclude)*.controller' ':(exclude)*.mat' ':(exclude)*.fbx' \
      > /tmp/pr-review-<番号>-patch.diff

yml/jsonは残す（master-data系レンズの守備範囲のため）。

## Step 4: 4カテゴリcontextの独立再構成

`/tmp/pr-review-<番号>-context.md` に書く。**情報源はPR本文とリポジトリ内のspec/planの判断台帳（ADR）のみ**。
実装セッションの申告・PRコメントの合意主張は使わない。

- 出所ラベル正式文法: ユーザー裁定=`[ADR: <spec名>#<台帳項目>]`（実在するADR項目のみ）/ それ以外=`[agent前提]`
- PR本文が主張する方針・トレードオフは全部 `[agent前提]`（免責力なし）として書く

## Step 5: 新規性ゲートL1

    python3 $CANON/.claude/skills/pr-independent-review/scripts/novelty_gate.py ~/moorestech-worktrees/pr-review origin/<baseRefName>

出力JSONのうち **generic_origin=true のnew_edges・asmdef_refs・grammar全件**が新形フラグ。

- **非ゼロexitは即エラー終了**: `novelty_gate.py` がexit≠0で落ちたら「ゲート実行失敗」として理由付きで終了する。
  空JSON扱い・新形0件扱いで先へ進めるのは禁止（沈黙故障でverdictが自動マージ可に化け、見逃し率実測が壊れる）
- **generic_origin=falseのnew_edgesは参考情報**: 新規ディレクトリを追加するPRでは配下の全usingがnew_edge化する。
  主シグナルは `generic_origin=true` のみとし、falseのエッジは裁定カードにせずダイジェストの折りたたみ参考節へ回す
- **スキルミラーの除外**: `.claude/` `.agents/` `.codex/` 配下の `.cs` はプロダクトコードでないため、
  novelty_gate出力からファイルパスで除外して解釈する（新形にもverdictにも数えない）

## Step 6: moores-code-review本体をreport-onlyで発火

`$CANON/.claude/skills/moores-code-review/SKILL.md` の手順に従うが、以下を上書きする:

- PATCH_PATH = Step 3の生成物 / USER_PROMPT_PATH = Step 4の生成物 / cwd＝レビューworktree（コード読み取り専用）
- スクリプト実行・レンズ/reviewer/統合ルールのReadパスは全部 `$CANON` 配下の絶対パス
- **report-only**: 確定修正の自動適用（本体Step 6）・uloop compile・本体Step 6.5の適用後diff再生成・
  本体Step 7の項目3（`records/YYYY-MM-DD-*.md` と `eval/log.md` への記録）は行わない。指摘は全部ダイジェストへ
- 本体Step 6.5のガード2本（comment-rationale-guard / comment-convention-guard）は**実行する**。
  適用がない以上最終diff＝Step 3のpatchなので、それをそのまま渡す。convention-guardの「機械的は自動適用」も
  report-onlyでは適用せず指摘として出す
- **`/tmp` の一時ファイル削除（本体Step 7の項目4）も行わない** — Step 3のpatchは後段のコード抜粋転記で読むため、
  ここで消すとダイジェストの実コードが作れなくなる
- AskUserQuestionは使わない。設計判断もダイジェストの裁定カードへ

## Step 7: ダイジェストHTML生成

`$CANON/.claude/skills/pr-independent-review/assets/digest-template.html` をReadし、sonnet subagentに
`/tmp/pr-review-<番号>/index.html` を生成させて `open` する。CSS・コメント機能JSはverbatim維持。

- verdictヘッダ（verdict＋件数） → 裁定カード（新形・設計判断。各カードに: ファイル名太字・リポジトリ相対フルパス・
  行番号・当該diffハンクの実コード抜粋（前後数行・追加行`<ins>`・問題行`.hl`）・PR側の主張（出所ラベル付き）・代替案） →
  suppressedカード（全件・同形式＋suppressed-by出所） → 判断台帳（ユーザー裁定/agent前提） → 折りたたみ参考
- CONFIG固有化: `STORAGE_KEY='pr-review-<番号>-comments-v1'`、`COPY_TITLE='PR #<番号> 独立レビュー裁定'`。
  テンプレートは `REPLACE_WITH_UNIQUE_STORAGE_KEY` / `REPLACE_WITH_COPY_HEADING` のプレースホルダのまま出荷されているので置換必須（未置換だと別PRのコメントがlocalStorageで混ざる）
- **カード間の視覚分離**: 裁定カード・suppressedカードはテンプレート既定では背景・枠線を持たず、連続すると境界が曖昧になる。
  生成時に各カードのdivへ背景色または枠線を付けるようsubagentへ指示する
- 実コード抜粋はStep 3のpatchから機械的に転記する（創作・要約禁止）

## Step 8: 記録

- md版サマリを `$CANON/.claude/skills/pr-independent-review/records/pr-<番号>.md` に保存
  （verdict・裁定/suppressed/新形の各明細のテキスト縮約。grep用）
- シャドー台帳 `$CANON/.claude/skills/pr-independent-review/records/shadow-ledger.md` に1行追記:
  `| 日付 | PR番号 | verdict | 新形数 | suppressed数 | あなたの実判断（空欄） | 一致（空欄） |`
- 正典treeでの記録類のコミットはユーザーに委ねる（独立セッションは正典treeへ書き込むが勝手にcommitしない）

## verdict判定規則

- **Critical差し戻し**: 統合後Criticalが1件以上（200行超過は除外＝努力目標）
- **新形につき裁定行き**: Criticalなし、かつ新形フラグ or `設計判断: あり` が1件以上
- **自動マージ可**: 上記いずれも無し
- suppressedはverdictに影響しない（ダイジェストに全件列挙）

## エラー処理

- gh未認証・PR不存在・checkout失敗: 即エラー終了・理由報告
- 新規性ゲートの非ゼロexit: 即エラー終了・理由報告（Step 5参照）
- codex不在などmoores-code-review内の縮退: 本体規約に従いダイジェストの参考節に明記
