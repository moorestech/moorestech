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

**$CANONの決定手順（最初に必ず1回やる）**:

1. このSKILL.mdをReadしたときの絶対パスを取る（例: `/Users/katsumi/moorestech/.claude/worktrees/pr-independent-review/.claude/skills/pr-independent-review/SKILL.md`）
2. その末尾から `/.claude/skills/pr-independent-review/SKILL.md` を**文字列として取り除いた**残りが `$CANON`
   （上例なら `/Users/katsumi/moorestech/.claude/worktrees/pr-independent-review`）
3. 手順2の実値を展開した `ls <実値>/.claude/skills/pr-independent-review/scripts/novelty_gate.py` で実在確認する。
   失敗したら即エラー終了（$CANON誤決定のまま走らせない）。**確認先はこのファイルでなければならない** —
   `moores-code-review/SKILL.md` はレビューworktree側にも存在しうるため、誤決定した$CANONでも通ってしまい弁別にならない

- **`$CANON` は本ドキュメント上のプレースホルダであり、シェル変数ではない**。Bashコマンド・subagentのprompt・
  ファイルパスに渡すときは**必ず手順2で得た実値の絶対パスへ展開して書く**。`$CANON` をリテラルのまま渡すと
  未定義変数で空文字に展開され、`/.claude/skills/...` という不存在パスを叩いて沈黙故障する
- `$CANON` は `~/moorestech` とは限らない（worktreeから発火する運用が現にある）。`~/moorestech` を決め打ちしない

## Step 1: PR取得

`gh pr view <番号> --repo moorestech/moorestech --json number,title,body,baseRefName,headRefName,additions,deletions,files,state,mergeCommit`
で取得。失敗（未認証・不存在）は即エラー終了し理由を報告する。黙って縮退しない。
`state` と `mergeCommit` は次節の `BASE_REF` 確定に必須なので、必ずこの1回で一緒に取る。

## Step 1.5: BASE_REF の確定（base参照はここで一度だけ決める）

以降のStep 2/3/5/6で使うbase参照は**この節で決めた `BASE_REF` ただ1個**とする。各Stepで `origin/<baseRefName>` を
ベタ書きしない（同じ値が複数箇所に散ると、片方だけ直す事故で沈黙故障する）。

- Step 1の `state` で分岐する:
  - **`state=OPEN`** → `BASE_REF = origin/<baseRefName>`
  - **`state=MERGED`** → `BASE_REF = <mergeCommit>^1`（マージコミットの第1親）
  - `state=CLOSED`（未マージclose）は独立レビューの対象外。即エラー終了する
- **マージ済みPRで `origin/<baseRefName>` を使ってはいけない**: マージ済みHEADは `origin/<baseRefName>` の祖先に
  なるため三点diffのmerge-baseがHEAD自身と一致し、**patchが空・新規性ゲートが全空JSON・どちらもexit 0**という
  沈黙故障になる。verdictが「Critical 0・新形0 → 自動マージ可」に化けて見逃し率実測が壊れる（PR #1041で実測）
- **`BASE_REF` は本ドキュメント上のプレースホルダであり、シェル変数ではない**（`$CANON` と同じ扱い）。
  コマンドに書くときは必ず実値へ展開する（例: `8ce6f4ddae1a0d1c03059d3e3ac6d8acb994de80^1`）
- 解決可能性の確認はStep 2の末尾で行う（fetch後でないと参照できないため）

## Step 2: レビューworktreeへcheckout

コマンドは `git -C <絶対パス>` 形式か、**`cd` を同一コマンド内に含めた形**で書く。agent実行系ではbash呼び出し間で
cwdがリセットされるため、単独の `cd` は次のコマンドに効かない。`~` はsubagentのpromptやファイルパスへ渡す時点で
絶対パスに展開する。

- 場所固定: `~/moorestech-worktrees/pr-review`。無ければ `git -C "$CANON" worktree add ~/moorestech-worktrees/pr-review origin/master --detach` で作成
  （`$CANON` は冒頭で決めた実値に展開して渡す。`~/moorestech` 決め打ちは禁止 — `$CANON` が別worktreeのケースが現に存在する）
- 毎回リセット: `git -C ~/moorestech-worktrees/pr-review reset --hard && git -C ~/moorestech-worktrees/pr-review clean -fd`
- base最新化: `git -C ~/moorestech-worktrees/pr-review fetch origin <baseRefName>`
  （MERGEDでも実行する。`<mergeCommit>` とその第1親をローカルへ持ってくるため）
- checkout（`state` で分岐）:
  - **OPEN**: `cd ~/moorestech-worktrees/pr-review && gh pr checkout <番号> --detach`
    （--detach必須: PRブランチは実装worktreeが保持していることが多くブランチロックで失敗する。
    `gh pr checkout` はリポジトリコンテキストを要求し `-C` にできないので、`cd` は必ず同一コマンド内に置く）
  - **MERGED**、または OPEN でも headブランチ削除済みで上が `fatal: couldn't find remote ref` / exit 128 になる場合:

        git -C ~/moorestech-worktrees/pr-review fetch origin pull/<番号>/head && \
          git -C ~/moorestech-worktrees/pr-review checkout --detach FETCH_HEAD

    それも失敗する場合は `<mergeCommit>` 自体をcheckoutする（`git -C ~/moorestech-worktrees/pr-review checkout --detach <mergeCommit>`）。
    差分は `BASE_REF`＝`<mergeCommit>^1` との比較なので、PRの変更集合としては同じものが取れる
- **BASE_REF の解決確認（ここで必ず行う）**: `git -C ~/moorestech-worktrees/pr-review rev-parse --verify "<BASE_REF>^{commit}"`
  が成功することを確かめる。MERGEDで `<mergeCommit>` がローカルに無くて失敗した場合のみ
  `git -C ~/moorestech-worktrees/pr-review fetch origin <mergeCommit>` を挟んで再確認する。
  それでも解決できなければ即エラー終了（不正・未解決のbaseのまま先へ進まない）

## Step 3: patch生成（exclude方式）

    git -C ~/moorestech-worktrees/pr-review diff <BASE_REF>...HEAD -- . \
      ':(exclude)*.meta' ':(exclude)*.prefab' ':(exclude)*.asset' ':(exclude)*.unity' \
      ':(exclude)*.png' ':(exclude)*.jpg' ':(exclude)*.controller' ':(exclude)*.mat' ':(exclude)*.fbx' \
      > /tmp/pr-review-<番号>-patch.diff

`<BASE_REF>` はStep 1.5で確定した実値。yml/jsonは残す（master-data系レンズの守備範囲のため）。

**成功条件＝patch非空（必須ガード・省略禁止）**: 生成直後に

    grep -c '^diff' /tmp/pr-review-<番号>-patch.diff

を実行し、**1以上**であることを確認する。**0なら「base指定ミスまたはpatch取得失敗」として即エラー終了する**。
空patchのまま先へ進むのは禁止 — 空patchは全レンズ・全reviewerを無所見にしverdictを「自動マージ可」へ化けさせるが、
`git diff` はこのケースでもexit 0を返すため、**このgrepが唯一の検知点**である。
0だったときの第一の疑いは `BASE_REF`（MERGEDなのに `origin/<baseRefName>` を使っていないか）＝Step 1.5へ戻る。

## Step 4: 4カテゴリcontextの独立再構成

`/tmp/pr-review-<番号>-context.md` に書く。**情報源はPR本文とリポジトリ内のspec/planの判断台帳（ADR）のみ**。
実装セッションの申告・PRコメントの合意主張は使わない。

- **4カテゴリは必ず `##` 見出しで書く**（太字箇条書き・箇条書きの見出し代用は不可）。カテゴリ名は本体Step 1と同一の
  `## 目指す（ゴール）` / `## 目指さない（非目標）` / `## 許容するトレードオフ` / `## 尊重すべき制約` の4本を使う。
  `checks_context.py` は `許容するトレードオフ` と `目指さない（非目標）` の `##` 見出し欠落をfail-closedで
  confirmed（`context_source_label`）にするため、書式を外すと決定論チェックがそれで埋まり本来の検査が読めなくなる。
  この検出はPRの欠陥ではないのでverdictには数えず（「verdict判定規則」参照）、contextを直して再実行する
- 出所ラベル正式文法: ユーザー裁定=`[ADR: <spec名>#<台帳項目>]`（実在するADR項目のみ）/ それ以外=`[agent前提]`
- PR本文が主張する方針・トレードオフは全部 `[agent前提]`（免責力なし）として書く

## Step 5: 新規性ゲートL1

    python3 "$CANON/.claude/skills/pr-independent-review/scripts/novelty_gate.py" ~/moorestech-worktrees/pr-review <BASE_REF>

（`$CANON` は冒頭で決めた実値に展開して書く。リテラルのまま渡さない。第2引数はStep 1.5の `BASE_REF` の実値であり、
`origin/<baseRefName>` のベタ書きではない）

出力JSONのうち次を**新形フラグ**として数える（3系統で採用基準が違う。混同禁止）:

- `new_edges` — **`generic_origin=true` のものだけ**（`false` は新形に数えない。下の参考情報行を参照）
- `asmdef_refs` — **全件**（generic_originによる絞り込みをしない）
- `grammar` — **全件**（同上）

- **非ゼロexitは即エラー終了**: `novelty_gate.py` がexit≠0で落ちたら「ゲート実行失敗」として理由付きで終了する。
  空JSON扱い・新形0件扱いで先へ進めるのは禁止（沈黙故障でverdictが自動マージ可に化け、見逃し率実測が壊れる）
- **patchが非空なのに3系統全空なら baseずれを疑う（必須確認）**: 3系統が全部空でもexit 0で返るため、上の
  非ゼロexitガードでは捕まらない。Step 3のpatchが非空（`grep -c '^diff'` が1以上）なのに `new_edges` /
  `asmdef_refs` / `grammar` が全部空だった場合は、先へ進む前に次の2点で `BASE_REF` の妥当性を確認する:
  1. `novelty_gate.py` の第2引数がStep 1.5の `BASE_REF` 実値と一致しているか（`origin/<baseRefName>` を
     ベタ書きしていないか。MERGED PRでの典型的な取り違え）
  2. `git -C ~/moorestech-worktrees/pr-review merge-base <BASE_REF> HEAD` が **HEADと一致しないこと**
     （一致＝HEADがbaseの祖先＝base取り違え。この場合は `BASE_REF` を直してStep 3からやり直す）

  両方通って初めて「本当に新形0件」と判断してよい。確認せずに0件として先へ進むのは禁止
- **generic_origin=falseのnew_edgesは参考情報**: 新規ディレクトリを追加するPRでは配下の全usingがnew_edge化する。
  主シグナルは `generic_origin=true` のみとし、falseのエッジは裁定カードにせずダイジェストの折りたたみ参考節へ回す
- **スキルミラーの除外**: `.claude/` `.agents/` `.codex/` 配下の `.cs` はプロダクトコードでないため、
  novelty_gate出力からファイルパスで除外して解釈する（新形にもverdictにも数えない）

## Step 6: moores-code-review本体をreport-onlyで発火

`$CANON/.claude/skills/moores-code-review/SKILL.md` の手順に従うが、以下を上書きする:

- PATCH_PATH = Step 3の生成物 / USER_PROMPT_PATH = Step 4の生成物 / cwd＝レビューworktree（コード読み取り専用）
- スクリプト実行・レンズ/reviewer/統合ルールのReadパスは全部 `$CANON` 配下の絶対パス

### 本体のコマンド例を使わず、次の3行をそのまま使う

本体SKILL.mdのコマンド例は `.claude/skills/...` の**相対パス**で書かれている。cwdがレビューworktreeなので
コピペするとPR側の `.claude/` を実行してしまう（＝正典tree原則の破れ・自己弱体化経路そのもの）。必ず下記で置き換える
（`$CANON` は冒頭で決めた実値に展開して書くこと）:

```bash
python3 "$CANON/.claude/skills/moores-code-review/scripts/deterministic_checks.py" "<PATCH_PATH>" --repo-root ~/moorestech-worktrees/pr-review --context "<USER_PROMPT_PATH>" > /tmp/pr-review-<番号>-detchecks.json
python3 "$CANON/.claude/skills/moores-code-review/scripts/select_lenses.py" "<PATCH_PATH>"
python3 "$CANON/.claude/skills/moores-code-review/scripts/select_reviewers.py" "<PATCH_PATH>"
```

- **`--repo-root` はレビューworktree側**（`~/moorestech-worktrees/pr-review`）。ADR参照の解決と200行判定は
  PR側の木のファイル実体を見る必要があるため。スクリプト本体だけが `$CANON` 側という非対称は意図的
- `--context` は本体Step 2どおり必須（Step 4の出所ラベル・`##` 見出し検査はこの指定が無いと一切走らない）
- **report-only**: 確定修正の自動適用（本体Step 6）・uloop compile・本体Step 6.5の適用後diff再生成・
  本体Step 7の項目3（`records/YYYY-MM-DD-*.md` と `eval/log.md` への記録）は行わない。指摘は全部ダイジェストへ
- 本体Step 6.5のガード2本（comment-rationale-guard / comment-convention-guard）は**実行する**。
  適用がない以上最終diff＝Step 3のpatchなので、それをそのまま渡す。convention-guardの「機械的は自動適用」も
  report-onlyでは適用せず指摘として出す
- **comment-convention-guardの `Candidates :` は本体Step 2相当（本スキルStep 6冒頭の決定論チェック）で生成したdetchecks JSON**
  （`/tmp/pr-review-<番号>-detchecks.json`）を渡す。本体は「最終diffで再計測したdetchecks-final」を渡す規定だが、
  report-onlyでは**修正適用が無いため最終diff＝Step 3のpatchであり、Step 6冒頭のdetchecks出力がそのまま最終値**になる。
  よって `deterministic_checks.py` の再実行はしない。4行契約の残り3行は `Read this : $CANON/.claude/skills/moores-code-review/post-checks/comment-convention-guard.md` /
  `Patch path : <PATCH_PATH>` / `User prompt : <USER_PROMPT_PATH>`（いずれも実値の絶対パスへ展開。下記「subagent起動契約への必須追記」参照）
- **`/tmp` の一時ファイル削除（本体Step 7の項目4）も行わない** — Step 3のpatchは後段のコード抜粋転記で読むため、
  ここで消すとダイジェストの実コードが作れなくなる
- AskUserQuestionは使わない。設計判断もダイジェストの裁定カードへ

### Codex外部監査（本体Step 3）の起動手当て

codexはプロンプトのテキストしか受け取らず、差分は**自分のcwdで**解決する。素直に起動するとこのセッションのcwd
（＝`$CANON`）を監査してしまい、PRと無関係なコードに所見を出す。次を必ず守る:

- **cwdをレビューworktreeへ移して起動する**（バックグラウンド起動は本体どおり）:

      cd ~/moorestech-worktrees/pr-review && codex exec --sandbox read-only --skip-git-repo-check - < /tmp/pr-review-<番号>-audit.md

- **audit-templateの差分指定欄を書き換える** — テンプレートは
  `$CANON/.claude/skills/moores-code-review/scripts/codex-audit-template.md`（`$CANON` は冒頭で決めた実値の絶対パスに
  展開してRead）。これは「レビュー対象は、このセッションで私が作業した成果物だけです」＋コミット済み/staged/unstaged の
  3行構成だが、独立レビューでは作業成果物が存在しない（worktreeはcleanなcheckout）。
  **2行目（「レビュー対象は、このセッションで私が作業した成果物だけです。」の行）を「レビュー対象は PR #<番号> の
  差分だけです。」に差し替え**、続く3行（コミット済み／staged／unstaged）を
  `- 差分: git diff <BASE_REF>...HEAD`（`BASE_REF` は実値へ展開）の1行に置き換える。
  1行目の役割宣言行はそのまま使う。staged/unstaged 行を残してはいけない（常に空で「変更なし＝問題なし」という誤結論を誘発する）
- `## 目指す / 目指さない / 許容するトレードオフ / 尊重すべき制約` 欄にはStep 4のcontextをそのまま貼る
- `which codex` が失敗したらスキップし、ダイジェストの折りたたみ参考節に縮退として明記する（本体規約どおり）

### subagent起動契約への必須追記

レンズ・reviewer・Fable全般・verifier・post-checksガードの**全promptに、3行/4行契約に加えて次の2行を必ず含める**
（含め忘れると、subagentは自分のcwdや `$CANON` 配下のコードを読んでPRと無関係な箇所をレビューする）:

```
対象コードのルート: <レビューworktreeの実値>（絶対パス）。コードのReadは必ずこの配下で行う。
`.claude/` 配下のスキル・レンズ・post-checks・統合ルールの定義のReadは <$CANONの実値> 配下で行う。
```

- `<$CANONの実値>` は冒頭で決めた絶対パスへ展開して書く（リテラルの `$CANON` を渡さない）
- `<レビューworktreeの実値>` も **`~` を展開した絶対パスで書く**（例: `echo ~/moorestech-worktrees/pr-review` の出力＝
  `/Users/<ユーザー名>/moorestech-worktrees/pr-review`）。subagentのpromptは文字列であってシェルを通らないため、
  `~` のまま渡すとリテラルの `~` ディレクトリを探して読めない
- **全サブエージェント契約（レンズ・reviewer・Fable全般・verifier・comment-rationale-guard・comment-convention-guard）の
  `Read this :` 行は `$CANON` 実値の絶対パスで書く** — 本体SKILL.mdの契約例は `.claude/skills/moores-code-review/...` の
  相対パスなので、そのままコピペするとsubagentのcwd（＝レビューworktree）側のPR同梱スキルを読む。
  `Candidates :` / `Patch path :` / `User prompt :` の各パス（`/tmp` 配下）も同様に絶対パスで書く

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
- **suppressedが0件でもセクションは省略しない**: suppressedセクションの見出しは常に出し、中身は
  「該当なし（0件）」の**1行**にする（カードは作らない）。セクションごと消すと「収集し忘れ」と区別がつかなくなる
- **設計判断カードのバッジ**: テンプレートのバッジは `badge-new` / `badge-sup` の2種のみで「設計判断」用が無い。
  **`badge-new` のclassをそのまま流用し、表示文言だけ「設計判断」とする**（新形カードの文言は「新形」）。
  テンプレート側にclassを追加する改変はしない
- 実コード抜粋はStep 3のpatchから機械的に転記する（創作・要約禁止）
- **プレースホルダ置換**: `{{TITLE}}`（hero・`<footer>`・`<title>` の計3箇所）/ `{{DATE}}` / `{{SUBTITLE}}` を実値へ置換する。
  `{{TITLE}}` = `独立レビュー: PR #<番号> <PRタイトル>`、`{{DATE}}` = レビュー実施日、`{{SUBTITLE}}` = verdict文字列。
  `<title>` の置換漏れはタブ名が `{{TITLE}}` のまま出荷される
- **テンプレート冒頭の使い方コメントブロック（`<!DOCTYPE html>` 直後の `<!-- 使い方: ... -->`）は生成時に削除する**
  （`{{TITLE}}` 等の文字列を含むため、残すと置換漏れの誤検知源になり成果物にも不要）
- **`<h1>` はページに1個だけ**: テンプレートはhero（`{{TITLE}}`）と `.verdict-header` の両方が `h1` になっている。
  **heroのh1を唯一のh1とし、`.verdict-header` 側は `h2` へ落とす**。heroの見出しとverdictヘッダの見出しで
  同じ文言を二度出さない（verdictヘッダは `verdict: <判定>` ＋件数の1行サマリに徹する）
- **絵文字はHTML全体で不使用**（hero・バッジ・カード・折りたたみ・footer・コメント機能の文言すべて）。
  状態表現はテンプレート既定のバッジ（`badge-new` / `badge-sup` 等）と文字で行う
- **折りたたみ参考節に必ず入れるもの**（本体規約「Warningを黙って落とさない」の担保。0件の項目は「0件」と明記する）:
  1. Criticalの修正方針詳細（裁定カードは要点のみ・詳細はここ）
  2. **Warning全件**（1件1行・出所系統つき。要約による間引き禁止）
  3. Info一覧（圧縮列挙可）
  4. `generic_origin=false` のnew_edges（参考情報。裁定カードにはしない）
  5. 各系統（決定論／レンズ／reviewer／Codex／Fable／post-checksガード）の生所見要約を系統ごとに1ブロック。
     Codex不在等の縮退があればここに明記する

## Step 8: 記録

- md版サマリを `$CANON/.claude/skills/pr-independent-review/records/pr-<番号>.md` に保存
  （verdict・裁定/suppressed/新形の各明細のテキスト縮約。grep用）。
  **書式は下記で固定する**（grepで横断集計するため、見出し文言を生成ごとに変えない。0件のセクションも省略せず
  「該当なし（0件）」の1行を置く）:

      # PR <番号> 独立レビュー

      - verdict: <Critical差し戻し|新形につき裁定行き|自動マージ可>
      - PRタイトル: <PRタイトル>
      - BASE_REF: <実値>
      - 実施日: YYYY-MM-DD

      ## 新形
      <新形フラグ1件1行（系統名・ファイル:行・要点）>

      ## 裁定
      <裁定カード1件1行（ファイル:行・指摘要点・代替案）>

      ## suppressed
      <1件1行（ファイル:行・指摘要点・suppressed-by出所）>

- シャドー台帳 `$CANON/.claude/skills/pr-independent-review/records/shadow-ledger.md` に1行追記:
  `| 日付 | PR番号 | verdict | 新形数 | suppressed数 | あなたの実判断（空欄） | 一致（空欄） |`
- 正典treeでの記録類のコミットはユーザーに委ねる（独立セッションは正典treeへ書き込むが勝手にcommitしない）

## verdict判定規則

- **Critical差し戻し**: 統合後Criticalが1件以上（**決定論チェックの `confirmed` を含む**・
  **200行超過（file-too-long）は除外**＝努力目標・**`context_source_label` も除外**）
  - `context_source_label` はStep 4で**自分が書いた**contextファイルの `##` 見出し／出所ラベル欠落の検出であり、
    PR側の欠陥ではない。検出時はcontextファイル（`/tmp/pr-review-<番号>-context.md`）を書式どおりに修正して
    `deterministic_checks.py` を再実行し、消えたことを確認してから先へ進む。verdictには一切数えない
    （PRを自分の書式ミスで差し戻すのは誤判定であり、見逃し率実測を壊す）
- **新形につき裁定行き**: Criticalなし、かつ新形フラグ or `設計判断: あり` が1件以上
- **自動マージ可**: 上記いずれも無し
- suppressedはverdictに影響しない（ダイジェストに全件列挙）

## エラー処理

- gh未認証・PR不存在・checkout失敗（MERGED分岐のフォールバックも尽きた場合）: 即エラー終了・理由報告
- `state=CLOSED`（未マージclose）・`BASE_REF` が解決できない: 即エラー終了・理由報告（Step 1.5 / Step 2参照）
- patchが空（`grep -c '^diff'` が0）: 即エラー終了・理由報告（Step 3参照）。空patchのまま後続Stepへ進まない
- 新規性ゲートの非ゼロexit: 即エラー終了・理由報告（Step 5参照）
- 新規性ゲートが3系統全空（patchは非空）: `BASE_REF` の妥当性を確認してから継続（Step 5参照）
- codex不在などmoores-code-review内の縮退: 本体規約に従いダイジェストの参考節に明記
