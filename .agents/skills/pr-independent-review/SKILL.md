---
name: pr-independent-review
description: |
  実装セッションと完全に独立したセッションでPRをレビューする手動発火スキル。PR URLまたは番号を受け取り、
  レビュー専用worktreeにcheckoutして moores-code-review（report-only）＋新規性ゲートL1を実行し、
  実コード抜粋入りのインフォグラフィックHTMLダイジェスト（verdict/裁定カード/suppressed）と
  シャドー台帳を出力する。実装セッションの自己申告contextは一切受け取らない。
  レビューと指摘への対応が完了したPRには「独立レビュー&対応完了」ラベルを付与する。
  Use When:
  1. 「/pr-independent-review <PR URL|番号>」で起動された時
  2. 「このPRを独立レビューして」「シャドーレビューして」と言われた時
  3. 「/pr-independent-review reconcile <番号>」で起動された時（人間レビューとの突き合わせ・見逃し検知・改善発火）
---

# pr-independent-review — 独立セッションPRレビュー（シャドー運用v1）

対応spec: `docs/superpowers/specs/2026-07-27-pr-independent-review-design.md`

**正典tree**: このSKILL.md自身が置かれているリポジトリルート（以下 `$CANON`）。
スクリプト・レンズ・統合ルールは必ず `$CANON` の絶対パスで参照する。レビューworktree側の
`.claude/` は**絶対に使わない**（PRごとに測定器が変わり見逃し率実測が壊れる・自己弱体化経路）。

**$CANONの決定手順（最初に必ず1回やる）**:

1. このSKILL.mdをReadしたときの絶対パスを取る（例: `~/moorestech/.agents/skills/pr-independent-review/SKILL.md`）
2. その末尾から `/<dir>/skills/pr-independent-review/SKILL.md`（`<dir>` は `.agents`/`.claude`/`.codex` のいずれか。
   skills実体は `.agents/skills` で他2つはsymlink）を**文字列として取り除いた**残りが `$CANON`
   （上例なら `~/moorestech`）
3. 手順2の実値を展開した `ls <実値>/.agents/skills/pr-independent-review/scripts/novelty_gate.py` で実在確認する。
   失敗したら即エラー終了（$CANON誤決定のまま走らせない）。**確認先はこのファイルでなければならない** —
   `moores-code-review/SKILL.md` はレビューworktree側にも存在しうるため、誤決定した$CANONでも通ってしまい弁別にならない

**記録repo `$LOGS`**: レビュー実行記録（`records/pr-*.md`・シャドー台帳・改善キュー・前向きログ・
下記 `$RUNDIR` の中間生成物とダイジェスト）はコードrepoではなく `../moorestech_logs`
（以下 `$LOGS`、privateログrepo）の `harness/` 配下に置く。
featureブランチが記録ファイルに触れてマージ衝突する構造を断つための分離であり、コードrepo側へ記録を書き戻さない。
`$LOGS` への書き込みはStop/SessionEnd hookが自動でcommit・pushする（Step 8末尾。セッション側でcommitしない）。

**実行ディレクトリ `$RUNDIR`**: 1回のレビューが作る中間生成物（patch・context・novelty・detchecks・codex監査プロンプト・
ダイジェストHTML）は**すべて** `$LOGS/harness/pr-independent-review/runs/pr-<番号>/` 配下に置く。以下これを `$RUNDIR` と呼ぶ
（`$CANON`・`$LOGS` と同じくプレースホルダであり、シェル変数ではない。コマンドには実値の絶対パスへ展開して書く）。

- **`/tmp` には一切置かない** — OSに掃除されて消える。これらはreconcileのフォレンジック・リプレイの入力そのもの
  （どのpatchを・どのcontextで・どのdetchecks結果のもとに測ったか）であり、失うと後から見逃しの原因が特定できない
- 再レビュー時は `runs/pr-<番号>-r2/`（以降 `-r3`…）を新規作成する。records の `pr-<番号>-r2.md` と1対1で対応させ、
  既存runを上書きしない
- Step 1の直後に `mkdir -p <$RUNDIRの実値>` を1回だけ実行する
- ファイル名は固定: `patch.diff` / `context.md` / `novelty.json` / `detchecks.json` / `codex-audit.md` /
  `digest.md` / `digest.html` / `findings.json` / `reconcile-comments.json`。PR番号はディレクトリ名が持つのでファイル名に含めない
- `$RUNDIR` 配下もhookで自動commit・pushされる（PRの実コードを含むが、logs repoはprivateなので出荷先として正しい）

- **`$CANON` は本ドキュメント上のプレースホルダであり、シェル変数ではない**。Bashコマンド・subagentのprompt・
  ファイルパスに渡すときは**必ず手順2で得た実値の絶対パスへ展開して書く**。`$CANON` をリテラルのまま渡すと
  未定義変数で空文字に展開され、`/.claude/skills/...` という不存在パスを叩いて沈黙故障する
- `$CANON` は `~/moorestech` とは限らない（worktreeから発火する運用が現にある）。`~/moorestech` を決め打ちしない

改善と言われたときは0.5を実行する。
修正と言われたときは改善ではなく、PRそのもののコード修正を行う。修正がpushまで完了したら
Step 9（対応完了ラベル）の付与条件を確認し、満たしていればラベルを付ける。

## Step 0: 独立性の自己申告ガード

**このセッションが対象PRの実装・レビュー・計画に何らかの形で関与していた場合は、ここで実行を中止する**（PRブランチで
コードを書いた・その実装のspec/planを書いた・同じPRを既にレビューした・実装セッションからの引き継ぎcontextを受け取った、
のいずれか）。独立レビューの値打ちは「実装の意図を知らない目で見る」ことにあり、関与済みセッションが走ると
見逃し率の実測値がそのぶん楽観側へ歪む。中止時はユーザーへ「このセッションは対象PRに関与済みのため独立性を満たさない。
新規セッションで起動されたい」と報告して終わる。判定は自己申告でよいが、迷ったら中止側に倒す。

## Step 0.5: reconcile負債ゲート（新規レビューの前に必ず通る）

シャドー台帳（`$LOGS/harness/pr-independent-review/records/shadow-ledger.md`）を読み、
`reconcile` 列が空欄の行それぞれについて人間レビューの有無を確認する:

    gh api repos/moorestech/moorestech/pulls/<番号>/comments --paginate --jq 'length'

- **1件以上 → そのPRのreconcile（下記「reconcileモード」）を新規レビューより先に実行する（ブロック型）**。
  未reconcileの見逃しを放置したまま同じ測定器で次のPRを測っても、同じ見逃しを再生産するだけだからである。
  reconcileは1コマンドで走るため、実質は順序の強制であって作業の追加ではない
- 0件 → 人間がまだレビューしていないだけなので保留のまま進んでよい（`reconcile` 列は空欄のまま）
- スタブ行（verdict=未測定（スタブ））は見逃し率の測定外なのでreconcile対象外。`reconcile` 列に
  `対象外（スタブ）` と記入して負債から外す
- **健全性1行を必ず表示する**（新規レビュー・reconcileどちらの起動でも冒頭に出す。滞留を無言にしない）:
  `未reconcile: N PR / 改善キューopen: M件 / 直近見逃し率: X%（missed A / human-confirmed B）`
  （キューは `$LOGS/harness/pr-independent-review/records/improvement-queue.md` の `open` 行数。見逃し率は最新の `## 突き合わせ内訳` から。未計測なら「未計測」）

## Step 1: PR取得

`gh pr view <番号> --repo moorestech/moorestech --json number,title,body,baseRefName,headRefName,headRefOid,additions,deletions,files,state,mergeCommit`
で取得。失敗（未認証・不存在）は即エラー終了し理由を報告する。黙って縮退しない。
`state` と `mergeCommit` は次節の `BASE_REF` 確定に必須なので、必ずこの1回で一緒に取る。
`headRefOid` はStep 2末尾のcheckout整合確認に使うので同時に取る（後から取り直さない）。

取得できたら `$RUNDIR` を作る（以降の全生成物の置き場。既存 `pr-<番号>/` があれば再レビューなので `-r2` を使う）:

    mkdir -p <$RUNDIRの実値>

## Step 1.5: BASE_REF の確定（base参照はここで一度だけ決める）

以降のStep 2/3/5/6で使うbase参照は**この節で決めた `BASE_REF` ただ1個**とする。各Stepで `origin/<baseRefName>` を
ベタ書きしない（同じ値が複数箇所に散ると、片方だけ直す事故で沈黙故障する）。

- Step 1の `state` で分岐する:
  - **`state=OPEN`** → `BASE_REF = origin/<baseRefName>`
  - **`state=MERGED`** → `BASE_REF = <mergeCommit>^1`（マージコミットの第1親）
  - `state=CLOSED`（未マージclose）は独立レビューの対象外。即エラー終了する
- **`<mergeCommit>` はSHA文字列ではない（本スキル全体の共通規約・ここで一度だけ定義する）**:
  `gh pr view --json mergeCommit` は `{"oid":"<40桁SHA>"}` という**オブジェクト**を返す。
  以降のStep（Step 2のフォールバックcheckout・`fetch origin <mergeCommit>`・エラー処理節を含む）で
  `<mergeCommit>` と書いてある箇所は**すべて `.mergeCommit.oid` の値（40桁SHA）に展開して使う**。
  オブジェクトのまま（`map[oid:...]` や `{"oid":...}`）をコマンドへ渡すと `unknown revision` で落ちる。
  `--jq '.mergeCommit.oid'` で取り出すのが確実
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
- base最新化（**refspecを明示する**）:

        git -C ~/moorestech-worktrees/pr-review fetch origin \
          "+refs/heads/<baseRefName>:refs/remotes/origin/<baseRefName>"

  引数なしの `fetch origin <baseRefName>` はremote-tracking ref（`refs/remotes/origin/<baseRefName>`）を
  更新せずFETCH_HEADだけを書く設定があり得るため、`BASE_REF`＝`origin/<baseRefName>` が古いまま解決されて
  base取り違えになる。refspecを明示すればtracking refの更新が保証される。
  MERGEDでも実行する（`<mergeCommit>` とその第1親をローカルへ持ってくるため。
  本節以降の `<mergeCommit>` はすべてStep 1.5の規約どおり `.mergeCommit.oid` の40桁SHAへ展開して書く）。
  **このfetchの失敗ではエラー終了しない** — マージ後にbaseブランチが削除されているとremote refが無く落ちるが、
  下の「BASE_REF の解決確認」のフォールバック（`fetch origin <mergeCommit>`）で回収できるため、そこまで進んで判定する
- checkout（`state` で分岐）:
  - **OPEN**: `cd ~/moorestech-worktrees/pr-review && gh pr checkout <番号> --detach`
    （--detach必須: PRブランチは実装worktreeが保持していることが多くブランチロックで失敗する。
    `gh pr checkout` はリポジトリコンテキストを要求し `-C` にできないので、`cd` は必ず同一コマンド内に置く）
  - **MERGED**、または OPEN でも headブランチ削除済みで上が `fatal: couldn't find remote ref` / exit 128 になる場合:

        git -C ~/moorestech-worktrees/pr-review fetch origin pull/<番号>/head && \
          git -C ~/moorestech-worktrees/pr-review checkout --detach FETCH_HEAD

    それも失敗する場合は `<mergeCommit>` 自体をcheckoutする（`git -C ~/moorestech-worktrees/pr-review checkout --detach <mergeCommit>`。
    `<mergeCommit>` はStep 1.5の規約どおり `.mergeCommit.oid` のSHA）。
    差分は `BASE_REF`＝`<mergeCommit>^1` との比較なので、PRの変更集合としては同じものが取れる
- **BASE_REF の解決確認（ここで必ず行う）**: `git -C ~/moorestech-worktrees/pr-review rev-parse --verify "<BASE_REF>^{commit}"`
  が成功することを確かめる。MERGEDで `<mergeCommit>` がローカルに無くて失敗した場合のみ
  `git -C ~/moorestech-worktrees/pr-review fetch origin <mergeCommit>`（同じく `.mergeCommit.oid` のSHA）を挟んで再確認する。
  それでも解決できなければ即エラー終了（不正・未解決のbaseのまま先へ進まない）
- **checkout整合の確認（ここで必ず行う）**: `git -C ~/moorestech-worktrees/pr-review rev-parse HEAD` の出力が
  Step 1で取った `headRefOid` と一致することを確かめる。一致すればPRのhead実体をレビューしている。
  不一致のときの扱いは経路で分かれる（混同禁止）:
  - **OPENの通常経路（`gh pr checkout` / `pull/<番号>/head` でcheckoutした場合）で不一致**: **即エラー終了する**。
    レビュー実行中にPRへ新しいpushが入った（＝メタデータが陳腐化した）ことを意味し、そのまま進むと
    「Step 1で取ったタイトル・base・差分規模」と「実際に読むコード」がずれた記録を残す。
    ユーザーへ理由を報告し、**Step 1のメタデータ再取得からやり直す**（`headRefOid` を取り直して再実行）
  - **第3フォールバック（`<mergeCommit>` 自体をcheckoutした経路）で不一致**: これは設計どおりなので先へ進んでよい。
    ただし **`$LOGS/harness/pr-independent-review/records/pr-<番号>.md` の `- checkout:` 行に「headRefOid不一致・mergeCommit検査」と明記する**
    （マージ結果ツリーを見ているのであってhead実体ではない、と後から分かるようにするため）

## Step 3: patch生成（exclude方式）

    git -C ~/moorestech-worktrees/pr-review -c core.quotepath=false diff \
      --no-color --no-ext-diff --no-textconv --text --no-renames \
      <BASE_REF>...HEAD -- . \
      ':(exclude)*.meta' ':(exclude)*.prefab' ':(exclude)*.asset' ':(exclude)*.unity' \
      ':(exclude)*.png' ':(exclude)*.jpg' ':(exclude)*.controller' ':(exclude)*.mat' ':(exclude)*.fbx' \
      ':(exclude,glob)**/unity-playmode-recorded-playtest/**/*.cs' \
      > <$RUNDIRの実値>/patch.diff

`<BASE_REF>` はStep 1.5で確定した実値。yml/jsonは残す（master-data系レンズの守備範囲のため）。
**プレイテストシナリオの `.cs` も除外する** — 使い捨ての操作台本をプロダクトコードの規約で裁かない
（ユーザー裁定 2026-08-16 / PR#1137-F12）。moores-code-review Step 1と同一のpathspecで揃える

**フラグは省略禁止（本体パーサ保護）** — このpatchは `deterministic_checks.py` とレンズ/reviewer/Codexが読む唯一の
差分実体であり、次はいずれもユーザー側git設定で有効化され得て、**patchを静かに痩せさせる**:

- `-c core.quotepath=false` — 非ASCIIパスが `"b/\346\226..."` とクォートされ、パス基準の判定が全て外れる
- `--no-color` — `color.diff=always` のANSIが混入し全パターンが不一致になる
- `--no-ext-diff` / `--no-textconv` — 外部diffドライバ・textconvが差分本文を別物に置き換える
- `--text` — バイナリ判定された差分が `Binary files differ` の1行に潰れ、中身がレビューされない
- `--no-renames` — 移動が `rename from/to` に圧縮され、移動先ファイルの中身が1行も現れない（＝丸ごと見逃す）

**成功条件＝patch非空（必須ガード・省略禁止）**: 生成直後に

    grep -c '^diff' <$RUNDIRの実値>/patch.diff

を実行し、**1以上**であることを確認する。**0なら「base指定ミスまたはpatch取得失敗」として即エラー終了する**。
空patchのまま先へ進むのは禁止 — 空patchは全レンズ・全reviewerを無所見にしverdictを「自動マージ可」へ化けさせるが、
`git diff` はこのケースでもexit 0を返すため、**このgrepが唯一の検知点**である。
0だったときの第一の疑いは `BASE_REF`（MERGEDなのに `origin/<baseRefName>` を使っていないか）＝Step 1.5へ戻る。

## Step 4: 4カテゴリcontextの独立再構成

`<$RUNDIRの実値>/context.md` に書く。**情報源はPR本文とリポジトリ内のspec/planの判断台帳（ADR）のみ**。
実装セッションの申告・PRコメントの合意主張は使わない。

- **4カテゴリは必ず `##` 見出しで書く**（太字箇条書き・箇条書きの見出し代用は不可）。カテゴリ名は本体Step 1と同一の
  `## 目指す（ゴール）` / `## 目指さない（非目標）` / `## 許容するトレードオフ` / `## 尊重すべき制約` の4本を使う。
  `checks_context.py` は `許容するトレードオフ` と `目指さない（非目標）` の `##` 見出し欠落をfail-closedで
  confirmed（`context_source_label`）にするため、書式を外すと決定論チェックがそれで埋まり本来の検査が読めなくなる。
  この検出はPRの欠陥ではないのでverdictには数えず（「verdict判定規則」参照）、contextを直して再実行する
- 出所ラベル正式文法: ユーザー裁定=`[ADR: <spec名>#<台帳項目>]`（実在するADR項目のみ）/ それ以外=`[agent前提]`
- PR本文が主張する方針・トレードオフは全部 `[agent前提]`（免責力なし）として書く
- **`[ADR:]` を引用する前に、そのspec/planファイルがPR diff自身で追加・変更されていないか必ず確認する**:

        git -C ~/moorestech-worktrees/pr-review diff <BASE_REF>...HEAD --name-only -- docs/superpowers/

  （`<BASE_REF>` はStep 1.5で確定した実値）の出力に引用元ファイルが含まれる場合、そのファイル由来のADR項目は
  **`[agent前提]` へ自動降格する**（＝免責力なし）。contextの当該行末に `（PR内新設ADR）` と注記する。
  根拠: PRが自作したADRを免責ソースとして認めると免責ロンダリング事故の再演経路になる
  （承認済み3原則①「引用不能な合意は `[agent前提]`」の適用。独立セッションからはPR内ADRのユーザー承認の実在を
  検証できないため「引用不能」に分類する）。
  **この降格はverdictに影響しない** — 降格された項目で免責されなくなった指摘は通常のCritical/Warningとして扱われ、
  通常の判定規則（「verdict判定規則」）に乗る

## Step 5: 新規性ゲートL1

    python3 "$CANON/.claude/skills/pr-independent-review/scripts/novelty_gate.py" \
      ~/moorestech-worktrees/pr-review <BASE_REF> > <$RUNDIRの実値>/novelty.json

（`$CANON` は冒頭で決めた実値に展開して書く。リテラルのまま渡さない。第2引数はStep 1.5の `BASE_REF` の実値であり、
`origin/<baseRefName>` のベタ書きではない）

**出力は必ずこのファイルへ保存し、以降のStep（新形の数え上げ・裁定カード化・Step 8の記録）は
`<$RUNDIRの実値>/novelty.json` を読み直して行う**。stdoutの見た目や記憶から新形を数えない
（件数の写し間違いが台帳の実測値を直接汚す）。

**保存直後の受け取り検査（必須・省略禁止）**: 次の1行で「JSONとしてパースできること」と
「`new_edges` / `asmdef_refs` / `grammar` の3キーが揃っていること」を確認する。失敗したら即エラー終了する
（ゲートが途中で壊れた出力を、空＝新形0件として受け取らないため）:

    python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); assert {"new_edges","asmdef_refs","grammar"} <= d.keys(), d.keys(); print({k: len(d[k]) for k in ("new_edges","asmdef_refs","grammar")})' <$RUNDIRの実値>/novelty.json

出力JSONのうち次を**新形フラグ**として数える（3系統で採用基準が違う。混同禁止）:

- `new_edges` — **`generic_origin=true` かつ `dir_is_new=false` のものだけ**（それ以外は新形に数えない。下の参考情報行を参照）
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
- **generic_origin=falseのnew_edgesは参考情報**: 主シグナルは `generic_origin=true` のみとし、
  falseのエッジは裁定カードにせずダイジェストの折りたたみ参考節へ回す
- **`dir_is_new=true` のnew_edgesも参考情報**: そのディレクトリはbaseにusing記録が1件も無い＝新設であり、
  配下の全usingが機械的に新エッジ化する（「新設だから新しい」だけで設計上の新形ではない）。
  `generic_origin=true` であっても `dir_is_new=true` なら裁定カードにせず折りたたみ参考節へ回す。
  ただし**件数はStep 8の記録に「うちdir_is_new N件」として残す**（黙って消さない）
- **スキルミラーの除外**: `.claude/` `.agents/` `.codex/` 配下の `.cs` はプロダクトコードでないため、
  novelty_gate出力からファイルパスで除外して解釈する（新形にもverdictにも数えない）
- **外部リビジョンピンの除外**: `.moorestech-external-revisions.json` の差分は指摘対象にしない
  （兄弟クローンのHEADへ追随するだけの機械的な更新であり、内容がずれても後から幾らでも直せる。
  ユーザー裁定 2026-08-16 / PR#1127-F06）。findings.jsonへ起こさず、ダイジェストにも裁定カードを作らない
- **`line` が `null` の所見はファイル単位の所見**（`schema_change` / `new_protocol_file` / `new_datastore_file`）。
  ダイジェスト・records の表記は `ファイル:行` ではなく**ファイルパスのみ**にする。`:1` や `:null` を書き足さない
  （存在しない行番号を書くと、後から「その行を見た」という誤った痕跡になる）

## Step 6: moores-code-review本体をreport-onlyで発火

`$CANON/.claude/skills/moores-code-review/SKILL.md` の手順に従うが、以下を上書きする:

- PATCH_PATH = Step 3の生成物 / USER_PROMPT_PATH = Step 4の生成物 / cwd＝レビューworktree（コード読み取り専用）
- スクリプト実行・レンズ/reviewer/統合ルールのReadパスは全部 `$CANON` 配下の絶対パス

### 本体のコマンド例を使わず、次の3行をそのまま使う

本体SKILL.mdのコマンド例は `.claude/skills/...` の**相対パス**で書かれている。cwdがレビューworktreeなので
コピペするとPR側の `.claude/` を実行してしまう（＝正典tree原則の破れ・自己弱体化経路そのもの）。必ず下記で置き換える
（`$CANON` は冒頭で決めた実値に展開して書くこと）:

```bash
python3 "$CANON/.claude/skills/moores-code-review/scripts/deterministic_checks.py" "<PATCH_PATH>" --repo-root ~/moorestech-worktrees/pr-review --context "<USER_PROMPT_PATH>" > <$RUNDIRの実値>/detchecks.json
python3 "$CANON/.claude/skills/moores-code-review/scripts/select_lenses.py" "<PATCH_PATH>"
python3 "$CANON/.claude/skills/moores-code-review/scripts/select_reviewers.py" "<PATCH_PATH>"
```

- **`--repo-root` はレビューworktree側**（`~/moorestech-worktrees/pr-review`）。ADR参照の解決と200行判定は
  PR側の木のファイル実体を見る必要があるため。スクリプト本体だけが `$CANON` 側という非対称は意図的
- `--context` は本体Step 2どおり必須（Step 4の出所ラベル・`##` 見出し検査はこの指定が無いと一切走らない）
- **report-only**: 確定修正の自動適用（本体Step 6）・uloop compile・本体Step 6.5の適用後diff再生成・
  本体Step 7の項目3（`$LOGS/harness/moores-code-review/records/YYYY-MM-DD-*.md` と `$LOGS/harness/moores-code-review/eval-log.md` への記録）は行わない。指摘は全部ダイジェストへ
- 本体Step 6.5のガード2本（comment-rationale-guard / comment-convention-guard）は**実行する**。
  適用がない以上最終diff＝Step 3のpatchなので、それをそのまま渡す。convention-guardの「機械的は自動適用」も
  report-onlyでは適用せず指摘として出す
- **comment-convention-guardの `Candidates :` は本体Step 2相当（本スキルStep 6冒頭の決定論チェック）で生成したdetchecks JSON**
  （`<$RUNDIRの実値>/detchecks.json`）を渡す。本体は「最終diffで再計測したdetchecks-final」を渡す規定だが、
  report-onlyでは**修正適用が無いため最終diff＝Step 3のpatchであり、Step 6冒頭のdetchecks出力がそのまま最終値**になる。
  よって `deterministic_checks.py` の再実行はしない。4行契約の残り3行は `Read this : $CANON/.claude/skills/moores-code-review/post-checks/comment-convention-guard.md` /
  `Patch path : <PATCH_PATH>` / `User prompt : <USER_PROMPT_PATH>`（いずれも実値の絶対パスへ展開。下記「subagent起動契約への必須追記」参照）
- **中間生成物の削除（本体Step 7の項目4）は行わない** — `$RUNDIR` 配下は保存物であって一時ファイルではない。
  Step 3のpatchは後段のコード抜粋転記で読むうえ、reconcileのフォレンジック・リプレイの入力でもある
- AskUserQuestionは使わない。設計判断もダイジェストの裁定カードへ

### Codex外部監査（本体Step 3）の起動手当て

codexはプロンプトのテキストしか受け取らず、差分は**自分のcwdで**解決する。素直に起動するとこのセッションのcwd
（＝`$CANON`）を監査してしまい、PRと無関係なコードに所見を出す。かといってレビューworktreeへ `cd` して起動すると、
今度は**PR側の `AGENTS.md` / `CLAUDE.md` / `.codex/` をcodexが上位指示として読み込む**（＝レビュー対象が
レビュアーの指示を書ける自己弱体化経路）。次を必ず守る:

- **中立ディレクトリ（`/tmp` 等・リポジトリ外）から起動し、対象は全部プロンプト内の絶対パスで渡す**
  （バックグラウンド起動は本体どおり）。ここの `/tmp` は**codexのcwdとして使うだけ**でありファイル置き場ではない
  （`$RUNDIR` は `$LOGS` 配下＝git repo内なので、cwdにするとcodexがlogs repoを覗く。cwdは中立のまま保つ）:

      cd /tmp && codex exec --sandbox read-only --skip-git-repo-check - < <$RUNDIRの実値>/codex-audit.md

  **レビューworktreeへ `cd` しない**。プロンプト内でリポジトリを参照する箇所は必ず
  `git -C /Users/<ユーザー名>/moorestech-worktrees/pr-review ...` の形（`-C` に実値の絶対パス）で書き、
  読ませたいファイルも絶対パスで指定する。`~` は展開して書く（プロンプトはシェルを通らない）

- **audit-templateの差分指定欄を書き換える** — テンプレートは
  `$CANON/.claude/skills/moores-code-review/scripts/codex-audit-template.md`（`$CANON` は冒頭で決めた実値の絶対パスに
  展開してRead）。これは「レビュー対象は、このセッションで私が作業した成果物だけです」＋コミット済み/staged/unstaged の
  3行構成だが、独立レビューでは作業成果物が存在しない（worktreeはcleanなcheckout）。
  **2行目（「レビュー対象は、このセッションで私が作業した成果物だけです。」の行）を「レビュー対象は PR #<番号> の
  差分だけです。」に差し替え**、続く3行（コミット済み／staged／unstaged）を
  `- 差分: git -C <レビューworktreeの実値> diff <BASE_REF>...HEAD`
  （`BASE_REF` とworktreeパスはいずれも実値の絶対パスへ展開）の1行に置き換える。
  1行目の役割宣言行はそのまま使う。staged/unstaged 行を残してはいけない（常に空で「変更なし＝問題なし」という誤結論を誘発する）。
  **`-C` の省略も禁止** — 起動cwdが中立ディレクトリなので、省くと差分が1行も取れないまま監査が走る
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
  `Candidates :` / `Patch path :` / `User prompt :` の各パス（`$RUNDIR` 配下）も同様に絶対パスで書く

## Step 7: ダイジェスト生成（digest.md → コンバータ）

sonnet subagentに `<$RUNDIRの実値>/digest.md` を**Markdownで**生成させる。フォーマットの正本は
`$CANON/.agents/skills/pr-independent-review/README-digest-format.md` を読ませる（生成subagentの参照先はこの1本のみ）。

- 生成後に次を実行する:

      python3 $CANON/.agents/skills/pr-independent-review/scripts/digest_build.py <$RUNDIRの実値>

  非0終了なら **digest.mdを直して再実行する**（HTMLを手で直すのは禁止。コンバータのエラーメッセージが
  何のキー・見出しが欠けているかを指すので、それに従ってdigest.mdを修正する）
- 成功したら `open <$RUNDIRの実値>/digest.html`
- **残す規約**（生成subagentへの指示として引き継ぐ）:
  - カードのトリアージ基準（`must_read: true` を付ける条件）: (a)指摘系統の一致数が多い
    (b)裁定がCriticalの直し方を左右する (c)ゲームプレイ・アーキテクチャの方向を変える
  - 一言サマリの書式: 欠陥・裁定対象そのものを主語にした短文1つ（目安20字前後）。免責の仕組み・
    出所ラベルの話・系統数・規約条番号などのメタ情報はサマリに書かない
  - コード抜粋は全カード必須（`code-card` フェンス）。patchから機械的に転記する（創作・要約禁止）
  - `# 折りたたみ参考` に必ず入れる5項目: Criticalの修正方針詳細／Warning全件（1件1行・出所系統つき・
    要約による間引き禁止）／Info一覧（圧縮列挙可）／参考扱いのnew_edges／各系統の生所見要約
  - 推奨案は `options` の先頭に書く（`recommended` というキーは存在しない。README-digest-format.md参照）
- 旧フローにあったHTML手組みの細則（タグ・属性・置換・見た目の整形・生成後の確認手順など）は
  すべてコンバータの責務へ移っており、生成subagentへ指示する必要はない
- **保存**: `digest.md` / `digest.html` / `findings.json` はいずれも `$RUNDIR` 直下に保存する。
  `/tmp` へは一切書かない。`$RUNDIR` 配下はStop/SessionEnd hookが自動でcommit・pushする

## Step 7.5: findings.json（コンバータ出力の確認）

`findings.json` はStep 7のコンバータ（`digest_build.py`）が生成する。**手で書かない・手で直さない**。

- スキーマ（裁定サイトと `pr-adjudicated-apply` の入力契約であるため、読み方として残す）:

```json
{
  "pr": <PR番号>,
  "head": "<レビューしたheadの40桁SHA（Step 8の `- head:` と同値）>",
  "verdict": "<verdict判定規則で確定した最終verdict>",
  "generated_at": "<ISO8601（Step 7実行時刻）>",
  "findings": [
    {
      "id": "F01",
      "title": "<指摘の一行タイトル>",
      "severity": "critical|high|medium|low",
      "category": "critical|design-decision|novelty",
      "files": ["path/to/file.cs:123"],
      "excerpt": "<問題箇所のコード抜粋>",
      "recommendation": "<推奨対応の要約>",
      "options": [
        {"key": "A", "summary": "<案Aの要約>", "recommended": true},
        {"key": "B", "summary": "<案Bの要約>"}
      ],
      "suppressed": false,
      "suppress_reason": ""
    }
  ]
}
```

- **`recommended` は `options` の先頭に必ず付く。推奨したい案を digest.md の `options` 先頭に書くのが唯一の指定方法**。
  `recommended` というキーを digest.md に書くとコンバータがエラーで落ちる（`recommended` を書く欄は存在しない）
- **id採番規則**: コンバータがseverity降順（critical→high→medium→low）→ファイルパス昇順→行番号昇順で
  `F01` から連番を振る。digest.mdには `F01` のようなidを書かず、相互参照は `[F:slug]`（finding YAMLの `slug` を指す）で書く

## Step 8: 記録

- md版サマリを `$LOGS/harness/pr-independent-review/records/pr-<番号>.md` に保存
  （verdict・裁定/suppressed/新形の各明細のテキスト縮約。grep用）。
  **書式は下記で固定する**（grepで横断集計するため、見出し文言を生成ごとに変えない。0件のセクションも省略せず
  「該当なし（0件）」の1行を置く）:

      # PR <番号> 独立レビュー

      - verdict: <Critical差し戻し|新形につき裁定行き|自動マージ可|未測定（スタブ）>
      - PRタイトル: <PRタイトル>
      - BASE_REF: <実値（式のまま。例: 8ce6f4dd...^1）>
      - 実施日: YYYY-MM-DD
      - checkout: <headRefOid一致|headRefOid不一致・mergeCommit検査>
      - 縮退: <なし（5系統フル実行）|<縮退内容。例: codex不在>|スタブ（Step 6未実行）>
      - head: <レビューしたHEADの40桁SHA>
      - base: <BASE_REFを解決した40桁SHA>
      - canonical: <$CANONのHEAD SHA>（<clean|dirty>）
      - 系統: <発火した系統名と各々の完了/縮退。例: 決定論=完了/レンズ3本=完了/reviewer5本=完了/Codex=縮退（不在）/Fable=完了>
      - session: <このレビューセッションの識別子>
      - rundir: <$LOGS/harness/pr-independent-review/ からの相対パス。例: runs/pr-1116/>

      ## 新形
      <新形フラグ1件1行（系統名・ファイル:行（lineがnullならファイルのみ）・要点）>
      - 系統別件数: new_edges（採用基準を満たすもの）N / asmdef_refs N / grammar N
      - 参考（新形に数えない）: generic_origin=false N件 / dir_is_new=true N件

      ## 裁定
      <裁定カード1件1行（ファイル:行・指摘要点・代替案）>

      ## suppressed
      <1件1行（ファイル:行・指摘要点・suppressed-by出所）>

- **測定器メタデータ行（`head` / `base` / `canonical` / `系統` / `session` / `rundir`）は省略禁止**。
  `rundir` は「このverdictを出したときの実入力（patch・context・detchecks）がどこにあるか」の唯一の口であり、
  reconcileのフォレンジック・リプレイはここから辿る。
  これらは「何を・どの測定器で測ったか」の記録であり、欠けると後からverdictの再現も、
  測定器の版差による見逃し率の比較もできなくなる。`head` と `base` は
  `git -C ~/moorestech-worktrees/pr-review rev-parse HEAD` / `rev-parse "<BASE_REF>^{commit}"` の実出力、
  `canonical` は `git -C <$CANONの実値> rev-parse HEAD` と `git -C <$CANONの実値> status --porcelain`
  （出力が空なら `clean`・非空なら `dirty`）で取る
- **同一PRを再レビューしたときは `pr-<番号>.md` を上書きせず `pr-<番号>-r2.md`（以降 `-r3`…）を新規作成する**。
  上書きは「前回何を見て何を見落としたか」を消す＝見逃し率の実測そのものを壊す。
  台帳にも再レビュー分を別行として追記する

- シャドー台帳 `$LOGS/harness/pr-independent-review/records/shadow-ledger.md` に1行追記:
  `| 日付 | PR番号 | head | verdict | 新形数 | suppressed数 | 縮退 | あなたの実判断（空欄） | 一致（空欄） | reconcile（空欄） |`
  - `head` 列は records の `- head:` を**short SHA（先頭7桁）**にしたもの。同じPRを別headで再レビューした行を
    区別するため空欄にしない
  - `縮退` 列は records の `- 縮退:` と同じ値（`なし（5系統フル実行）` / 縮退内容 / `スタブ`）。
    verdictを額面どおり見逃し率へ数えてよいかがこの列だけで判別できるようにするため、空欄にしない
  - `reconcile` 列はreconcileモードだけが記入する（実施日 / `対象外（スタブ）`）。空欄＝突き合わせ未実施であり、
    Step 0.5の負債ゲートはこの列だけを見る

- **見逃しの記録粒度（ユーザー裁定 2026-07-27 → 2026-08-02改訂）**: 台帳は**verdict比較（`一致` 列）のまま**とし、
  欠陥単位の内訳は台帳の列にしない。内訳は**reconcileモードが、人間コメントが1件以上存在するPR全件について**、対象の
  `$LOGS/harness/pr-independent-review/records/pr-<番号>.md`（再レビュー分は該当する `-rN` ファイル）の末尾へ
  次のセクションとして**追記する**。記入はセッション側の作業であり、人間は確認のみ行う（人間に内訳を書かせない）。
  （旧トリガー「`一致` 列が不一致のPRのみ」は廃止 — PR #1095でverdict一致のままmissed 17件が出た実測により、
  verdict一致は見逃しゼロを意味しないことが確定したため。ユーザー裁定 2026-08-01「これの再発防止が一番大事」→
  機構承認 2026-08-02）:

      ## 突き合わせ内訳（reconcile YYYY-MM-DD）

      ### caught
      <独立レビューが挙げ、人間も欠陥と認めたもの。1件1行（ファイル:行・要点）>

      ### missed
      <人間が欠陥と認めたが、独立レビューが挙げなかったもの。1件1行＋分類タグ＋コメントURL>

      ### false-positive
      <独立レビューが挙げたが、人間は欠陥と認めなかったもの。1件1行>

  - 3小節とも見出しは省略せず、空なら「該当なし（0件）」の1行を置く（0件と収集し忘れを区別するため）
  - **見逃し率の集計は `missed / human-confirmed`**（`human-confirmed` ＝ `caught` ＋ `missed` ＝ 人間が欠陥と
    認めた総数）で行う。`false-positive` はこの分母に入れない（別途の誤検知率として数える）
  - 本セクションは上の固定書式への**追補**であり、reconcile実施まで当該ファイルに存在しないのが正
    （固定書式の「0件のセクションも省略せず」は本セクションには適用しない）
- **記録類のcommitはセッションが行わない。書き込むだけでよい**（`$LOGS` 配下のrecords・shadow-ledger・
  improvement-queue・`$RUNDIR` 配下の中間生成物とダイジェスト のすべて）。Stop/SessionEnd に登録された `.dev-hooks/logs-sync.mjs` が
  logs repoで `git add -A` → `auto: logs-sync` → `pull --rebase` → `push` まで自動で行うため、
  セッション側で `git commit` すると同一内容を二重に扱うことになる。**書いたら放置が正**
  （旧版の「正典treeへ書き込むが勝手にcommitしない」は記録先を `$LOGS` へ分離する前の記述。
  現在は正典tree＝コードrepoへは記録を一切書かない）

## Step 9: 対応完了ラベル（レビューと対応が両方終わった時のみ）

レビュー（Step 1〜8）と、検出した指摘への対応が**両方**完了した時点で、PRへ `独立レビュー&対応完了` ラベルを付ける
（このラベルはリポジトリに既存。`独立レビュー待ち` が付いていれば同時に外す）:

    gh pr edit <番号> --repo moorestech/moorestech \
      --add-label "独立レビュー&対応完了" --remove-label "独立レビュー待ち"

（`--remove-label` はPRに付いていないラベルの指定でもエラーにならないので常に両方指定でよい）

付与条件はverdictで分岐する。**「レビューだけ終わった」状態では絶対に付けない** — このラベルは
「人間はマージ判断だけすればよい」の合図であり、対応未実施のPRに付くと未修正のCriticalがマージされる:

- **Critical差し戻し** → 全Criticalへの修正コミットがPRブランチへ**push済みであることを確認してから**付ける
  （`gh pr view <番号> --json headRefOid` が修正コミットを指しているか、または `git log` で修正コミットが
  headの祖先にあることを確認する）。修正が未実施・ローカルのみなら付けない
- **新形につき裁定行き** → ユーザー裁定が出て、裁定に伴う対応（あれば）がpushされてから付ける。裁定待ちの間は付けない
- **自動マージ可** → 対応すべきものが無いので、レビュー完了（Step 8の記録まで）の時点で付けてよい
- **未測定（スタブ）** → 付けない（測定していないものに完了の合図を出さない）

## reconcileモード（人間レビューとの突き合わせ・改善発火）

`/pr-independent-review reconcile <番号>` で単独起動、またはStep 0.5の負債ゲートから強制実行される。
**ここは改善機構の発火装置であり、改善の手法・検証・回帰コーパスは moores-code-review 側
（`references/skill-improvement.md`・`eval/`）が単一の正である。手順・fixture・検証規則をこちらへ複製しない。**

**reconcileでの `$RUNDIR`**: reconcileはレビュー本体とは別セッションで走るので、`$RUNDIR` は自分で決めずに
`$LOGS/harness/pr-independent-review/records/pr-<番号>.md`（最新の `-rN`）の `- rundir:` 行が指すディレクトリを使う。
その行が無い古い記録（2026-08-08以前のレビュー）はrun保存前のものなので、中間生成物は存在しない前提で進める
（人間コメントとrecordsのテキストだけで突き合わせる。無いものを探して止まらない）。

1. **入力は人間のGitHubコメントのみ**（人間に台帳記入・ラベル付け・分類を求めない。人間の自然なレビュー行為の
   排気だけを信号源にする）:

       gh api repos/moorestech/moorestech/pulls/<番号>/comments --paginate \
         --jq '.[] | {path, line, body, html_url, commit_id}' > <$RUNDIRの実値>/reconcile-comments.json

   **`commit_id` は必ず一緒に取る** — 改善時のフォレンジック・リプレイのピン先はこの `commit_id` であり、
   `$LOGS/harness/pr-independent-review/records/pr-<番号>.md` に記録された自動レビュー当時のheadではない（人間指摘の行番号・コード実体は
   `commit_id` 側に紐づく。PR1095で両者が食い違い実装形まで別物だった実測あり。詳細は
   moores-code-review `eval/README.md` のフォレンジック・リプレイ手順1）

   レビューbody（`gh api repos/moorestech/moorestech/pulls/<番号>/reviews --paginate`）と通常コメント
   （`gh pr view <番号> --comments`）も読む。全部0件なら「人間レビュー未実施」として `reconcile` 列は
   空欄のまま終了する
2. **突き合わせ**: `$LOGS/harness/pr-independent-review/records/pr-<番号>.md`（最新の `-rN`）の裁定・suppressed・Warning（折りたたみ参考含む）と
   各コメントを照合し、caught / missed / 対象外（質問・運用連絡・レビュー対象外の雑談）に分類する。
   **迷ったらmissedに倒す**（見逃し率を楽観側へ歪めない。Step 0の独立性ガードと同じ倒し方）
3. **内訳をrecordsへ追記**（Step 8の `## 突き合わせ内訳` 書式）。missedの各行に**分類タグとコメントURL**を付ける:
   - `[レンズ盲点]` `[reviewer盲点]` `[決定論較正]` — ハーネス既存観点の欠落・較正ミス
   - `[L1語彙]` `[配管]` — 本スキル固有部品（novelty gate・patch生成・context再構成・digest）の欠陥
   - `[規範初出]` — 既存のAGENTS.md・レンズ・reviewerのどこにも成文化されていない規範を人間が初めて示したもの。
     ハーネスの欠陥ではなく**成文化の入力**であり、人間にしか出せない類として分計する
     （この割合の推移が自動マージ移行可否の実測境界になる）
4. **ルーティング（改善の実施はここから先、全部あちらの規則で行う）**:
   - `[レンズ盲点]` `[reviewer盲点]` `[決定論較正]` → `$CANON/.claude/skills/moores-code-review/references/skill-improvement.md`
     の手順にそのまま流す（フォレンジック・リプレイ診断 → 対策先決定 → 実例追記 → **4段階検証**
     （発火・由来サニティ・ブラインド陽陰・**実diffバックテスト**）→
     `eval/fixtures.tsv`・`eval/expected-findings.md` へ追記）。この手順を完了しない改修は改善と認めない。
     診断をrecordsのテキスト照合で代用するのも禁止（あちらの手順1に明記）
   - `[規範初出]` → まずAGENTS.mdまたは決定論チェックへ成文化し、その改修を同じ4段階検証に通す
   - `[L1語彙]` `[配管]` → 本スキルの `scripts/` を修正し、`tests/test_novelty_gate.py` に**赤→緑**のケースを追加する
5. **改善キューへ起票**: `$LOGS/harness/pr-independent-review/records/improvement-queue.md` に1行/件で追記する。
   状態を `closed` にできるのは**手順4の検証完了根拠を `closed根拠` 列に書けた時だけ**。根拠の要件は分類で異なる:
   - レンズ/reviewer/決定論較正/規範成文化 → **4段階検証の完了記録**。特に段階4（実diffバックテスト）の
     「見逃しsurface×検出元マトリクス＋過検知数」が必須。**合成fixture緑（段階3まで）だけではclosedにしない** —
     合成陽性は観点本文と同じ見逃しリストから書かれるため実ノイズ下の再現率を証明しない
     （2026-08-02 PR1095改善で合成緑のみをclosed根拠にした前科があり、この行はその再発防止）
   - `[L1語彙]` `[配管]`（スクリプト改修）→ 赤→緑を実証したテストの緑
   観点ファイルへの追記だけでは絶対にclosedにしない（作文はclosedの根拠にならない）
6. **前向きログの記入**: `$LOGS/harness/moores-code-review/eval-log.md` に1行追記する（PR番号・人間指摘数・分類内訳・
   ハーネス事前検出数・却下数・recordsへの相対リンク）。同ファイル「前向きログ」枠の書き手はこのreconcileである
7. **台帳更新**: `reconcile` 列に実施日を記入する。`あなたの実判断`・`一致` 列が空欄なら、観測可能な事実
   （差し戻しコメント・approve・マージ状態）から記入する（人間は確認のみ・Q2裁定の記入分担どおり）

## verdict判定規則

- **Critical差し戻し**: 統合後Criticalが1件以上（**決定論チェックの `confirmed` を含む**・
  **200行超過（file-too-long）は除外**＝努力目標・**`context_source_label` も除外**）
  - `context_source_label` はStep 4で**自分が書いた**contextファイルの `##` 見出し／出所ラベル欠落の検出であり、
    PR側の欠陥ではない。検出時はcontextファイル（`<$RUNDIRの実値>/context.md`）を書式どおりに修正して
    `deterministic_checks.py` を再実行し、消えたことを確認してから先へ進む。verdictには一切数えない
    （PRを自分の書式ミスで差し戻すのは誤判定であり、見逃し率実測を壊す）
- **新形につき裁定行き**: Criticalなし、かつ新形フラグ or `設計判断: あり` が1件以上
- **自動マージ可**: 上記いずれも無し
- **未測定（スタブ）**: Step 6（moores-code-review本体5系統）を実行していない場合は、上の3値を名乗ってはいけない。
  Critical/Warning/Info/suppressedが未収集である以上「Criticalなし」は測定結果ではなく未測定であり、
  `自動マージ可` や `新形につき裁定行き` を書くと台帳上は測定済みの1件として数えられてしまう。
  配管スモークテスト等でStep 6を意図的に飛ばした場合は **verdictを `未測定（スタブ）` とし**、
  新形フラグの件数だけを記録する（`- 縮退:` にも `スタブ（Step 6未実行）` を書く）
- suppressedはverdictに影響しない（ダイジェストに全件列挙）
- `data-verdict` 属性の値はこの語彙に対応させる: `reject` / `ruling` / `auto` / `stub`

## エラー処理

- このセッションが対象PRに関与済み: レビューせず中止・理由報告（Step 0参照）
- gh未認証・PR不存在・checkout失敗（MERGED分岐のフォールバックも尽きた場合）: 即エラー終了・理由報告
- OPENの通常経路で `headRefOid` とcheckout結果が不一致: 即エラー終了・理由報告し、Step 1のメタデータ再取得から
  やり直す（Step 2参照）。第3フォールバック経路の不一致は継続可
- Step 2のbase最新化fetch（`+refs/heads/<baseRefName>:refs/remotes/origin/<baseRefName>`）の失敗
  （MERGED後にbaseブランチが削除されている等でremote refが無い場合）:
  これ単独ではエラー終了しない。Step 2末尾の **BASE_REF解決確認**まで進み、そこで失敗したら
  同節のフォールバック `git -C ~/moorestech-worktrees/pr-review fetch origin <mergeCommit>`
  （`.mergeCommit.oid` のSHA）で `BASE_REF`＝`<mergeCommit>^1` を取り寄せて継続する。
  そのフォールバックでも解決できなければ即エラー終了
- `state=CLOSED`（未マージclose）・`BASE_REF` が解決できない: 即エラー終了・理由報告（Step 1.5 / Step 2参照）
- patchが空（`grep -c '^diff'` が0）: 即エラー終了・理由報告（Step 3参照）。空patchのまま後続Stepへ進まない
- 新規性ゲートの非ゼロexit: 即エラー終了・理由報告（Step 5参照）
- 新規性ゲート出力の受け取り検査失敗（JSONパース不能・3キーのいずれか欠落）: 即エラー終了・理由報告（Step 5参照）
- 新規性ゲートが3系統全空（patchは非空）: `BASE_REF` の妥当性を確認してから継続（Step 5参照）
- codex不在などmoores-code-review内の縮退: 本体規約に従いダイジェストの参考節に明記
