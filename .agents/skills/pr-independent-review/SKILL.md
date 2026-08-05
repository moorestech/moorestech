---
name: pr-independent-review
description: |
  実装セッションと完全に独立したセッションでPRをレビューする手動発火スキル。PR URLまたは番号を受け取り、
  レビュー専用worktreeにcheckoutして moores-code-review（report-only）＋新規性ゲートL1を実行し、
  実コード抜粋入りのインフォグラフィックHTMLダイジェスト（verdict/裁定カード/suppressed）と
  シャドー台帳を出力し、ダイジェストはcloudflaredクイックトンネルで外部からも閲覧できるURLを毎回発行する。
  実装セッションの自己申告contextは一切受け取らない。
  Use When:
  1. 「/pr-independent-review <PR URL|番号>」で起動された時
  2. 「このPRを独立レビューして」「シャドーレビューして」と言われた時
  3. 「/pr-independent-review reconcile <番号>」で起動された時（人間レビューとの突き合わせ・見逃し検知・改善発火）
---

# pr-independent-review — 独立セッションPRレビュー（シャドー運用v1）

対応spec: `docs/superpowers/specs/2026-07-27-pr-independent-review-design.md`

**正典tree `$CANON`**: 測定器（スクリプト・レンズ・reviewer・統合ルール・テンプレート）の唯一の読み取り元。
**`origin/master` に固定した専用worktree**とする（ユーザー裁定 2026-08-05）。
スクリプト・レンズ・統合ルールは必ず `$CANON` の絶対パスで参照する。レビューworktree側の
`.claude/` は**絶対に使わない**（PRごとに測定器が変わり見逃し率実測が壊れる・自己弱体化経路）。

`$CANON` を「このSKILL.mdが置かれているtree」にしてはいけない — それはたいてい他セッションが実装作業中の
メインworktreeであり、**レビュー実行中にブランチが切り替わって物差しが変わる**（2026-08-05に実測）。
台帳の `canonical:` は測定器の版を記録する欄なので、版が実行中に動く前提では記録が意味を失う。

**$CANONの用意（最初に必ず1回やる）**:

1. **起動元repo `$ORIGIN` を特定する** — このSKILL.mdをReadしたときの絶対パスから
   `/<dir>/skills/pr-independent-review/SKILL.md`（`<dir>` は `.agents`/`.claude`/`.codex` のいずれか。
   skills実体は `.agents/skills` で他2つはsymlink）を**文字列として取り除いた**残り。
   **`$ORIGIN` はworktreeを生やす起点としてのみ使い、読み取り元にも書き込み先にもしない**
2. **固定worktreeの場所**: レビューworktree（`pr-review`）と**同じ親ディレクトリ**の `skills-canon`。
   これが `$CANON` の実値。無ければ作る:

       git -C <$ORIGINの実値> worktree add <$CANONの実値> --detach origin/master

3. **毎回 `origin/master` へ固定し直す**（前回実行の残骸・他セッションの巻き込みを断つ）:

       git -C <$CANONの実値> fetch origin "+refs/heads/master:refs/remotes/origin/master"
       git -C <$CANONの実値> reset --hard origin/master
       git -C <$CANONの実値> clean -fd

4. 実在確認: `ls <$CANONの実値>/.agents/skills/pr-independent-review/scripts/novelty_gate.py`。
   失敗したら即エラー終了（$CANON誤決定のまま走らせない）。**確認先はこのファイルでなければならない** —
   `moores-code-review/SKILL.md` はレビューworktree側にも存在しうるため、誤決定した$CANONでも通ってしまい弁別にならない
5. **SKILL.md同一性ガード（必須・省略禁止）**:

       diff <$ORIGINの実値>/.agents/skills/pr-independent-review/SKILL.md \
            <$CANONの実値>/.agents/skills/pr-independent-review/SKILL.md

   **差分が出たら先へ進まず、ユーザーへ報告して指示を仰ぐ。** 理由: SKILL.md本体はharnessが `$ORIGIN` から
   読み込むものでskillには選べない。つまり固定できるのは参照ファイル（レンズ・スクリプト・テンプレート）だけで、
   `$ORIGIN` に未マージのskill改修があると「新しい指示 × 古いレンズ」の版ズレで走る。
   これは黙って進むと所見の由来が説明不能になる種類の故障なのでfail-closedにする。
   ユーザーが続行を選んだ場合のみ進み、**recordsの `canonical:` に `skew` と両SHAを明記する**

**記録repo `$LOGS`**: レビュー実行記録（`records/pr-*.md`・シャドー台帳・改善キュー・前向きログ）は
コードrepoではなく `../moorestech_logs`（以下 `$LOGS`、privateログrepo）の `harness/` 配下に置く。
featureブランチが記録ファイルに触れてマージ衝突する構造を断つための分離であり、コードrepo側へ記録を書き戻さない。

- **`$CANON` / `$ORIGIN` は本ドキュメント上のプレースホルダであり、シェル変数ではない**。Bashコマンド・
  subagentのprompt・ファイルパスに渡すときは**必ず実値の絶対パスへ展開して書く**。リテラルのまま渡すと
  未定義変数で空文字に展開され、`/.claude/skills/...` という不存在パスを叩いて沈黙故障する
- `$ORIGIN` は `~/moorestech` とは限らない（worktreeから発火する運用が現にある）。`~/moorestech` を決め打ちしない

**書き込み先の規律（3treeとも書き込み禁止）**:
このスキルが触るtreeは3つあり、**どれも書き込み先ではない**。

| tree | 中身 | 書けない理由 |
| --- | --- | --- |
| `$CANON`（skills-canon） | `origin/master` 固定の測定器 | 毎回 `reset --hard` されるので書いても消える |
| `$ORIGIN`（起動元・多くはメインworktree） | 他セッションの作業中ブランチ | 他人の作業ツリーを汚し、コミットすれば無関係なブランチへ混入する（**実際に実行中ブランチが切り替わった**） |
| `pr-review` | PRのhead（detached） | 毎回 `reset --hard && clean -fd` で消え、PRのコードの上にツーリング変更を積む筋も通らない |

- レビュー成果物の置き先は既に分離済み — ダイジェストは `/tmp/pr-review-<番号>/`、実行記録は `$LOGS`。
  **通常のレビュー1周では、コードrepoへの書き込みは1バイトも発生しない**
- 例外はコードrepoの中身そのものを変える依頼だけ（skill改修・`.decisions/` への裁定記録）。
  その場合は上の3treeのどれでもない**専用worktreeを新たに切ってそこで完結させる**:

      git -C <$ORIGINの実値> worktree add \
        <worktree親ディレクトリ>/skill-<用件> -b chore/<用件> origin/master

  worktree親ディレクトリは `pr-review` / `skills-canon` と同じ場所。`origin/master` 起点にするのは、
  `$ORIGIN` の現在ブランチ（他人の作業中ブランチ）を巻き込まないため
- **撤収確認（必須）**: 作業後に `git -C <$ORIGINの実値> status --porcelain -- <触れたパス>` が**空**であることを
  確かめる。空でなければ `$ORIGIN` に自分の変更が残っている＝ミスの再演
- **報告義務**: skill改修を専用worktreeのブランチに載せた場合、**その改修はmasterへマージされるまで有効にならない**
  （`$CANON` は毎回 `origin/master` に固定されるため）。
  「どのブランチに載せたか」「まだ有効でないこと」を必ず報告に書く

改善と言われたときは0.5を実行する。
修正と言われたときは改善ではなく、PRそのもののコード修正を行う。

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

- 場所固定: `skills-canon` と同じ親ディレクトリの `pr-review`。無ければ
  `git -C <$ORIGINの実値> worktree add <pr-reviewの実値> origin/master --detach` で作成
  （`$ORIGIN` は冒頭で決めた実値に展開して渡す。`~/moorestech-worktrees` の決め打ちは禁止 —
  worktree親ディレクトリが `$ORIGIN` の兄弟にあるケースが現に存在する）
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
      > /tmp/pr-review-<番号>-patch.diff

`<BASE_REF>` はStep 1.5で確定した実値。yml/jsonは残す（master-data系レンズの守備範囲のため）。

**フラグは省略禁止（本体パーサ保護）** — このpatchは `deterministic_checks.py` とレンズ/reviewer/Codexが読む唯一の
差分実体であり、次はいずれもユーザー側git設定で有効化され得て、**patchを静かに痩せさせる**:

- `-c core.quotepath=false` — 非ASCIIパスが `"b/\346\226..."` とクォートされ、パス基準の判定が全て外れる
- `--no-color` — `color.diff=always` のANSIが混入し全パターンが不一致になる
- `--no-ext-diff` / `--no-textconv` — 外部diffドライバ・textconvが差分本文を別物に置き換える
- `--text` — バイナリ判定された差分が `Binary files differ` の1行に潰れ、中身がレビューされない
- `--no-renames` — 移動が `rename from/to` に圧縮され、移動先ファイルの中身が1行も現れない（＝丸ごと見逃す）

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
      ~/moorestech-worktrees/pr-review <BASE_REF> > /tmp/pr-review-<番号>-novelty.json

（`$CANON` は冒頭で決めた実値に展開して書く。リテラルのまま渡さない。第2引数はStep 1.5の `BASE_REF` の実値であり、
`origin/<baseRefName>` のベタ書きではない）

**出力は必ずこのファイルへ保存し、以降のStep（新形の数え上げ・裁定カード化・Step 8の記録）は
`/tmp/pr-review-<番号>-novelty.json` を読み直して行う**。stdoutの見た目や記憶から新形を数えない
（件数の写し間違いが台帳の実測値を直接汚す）。

**保存直後の受け取り検査（必須・省略禁止）**: 次の1行で「JSONとしてパースできること」と
「`new_edges` / `asmdef_refs` / `grammar` の3キーが揃っていること」を確認する。失敗したら即エラー終了する
（ゲートが途中で壊れた出力を、空＝新形0件として受け取らないため）:

    python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); assert {"new_edges","asmdef_refs","grammar"} <= d.keys(), d.keys(); print({k: len(d[k]) for k in ("new_edges","asmdef_refs","grammar")})' /tmp/pr-review-<番号>-novelty.json

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
python3 "$CANON/.claude/skills/moores-code-review/scripts/deterministic_checks.py" "<PATCH_PATH>" --repo-root ~/moorestech-worktrees/pr-review --context "<USER_PROMPT_PATH>" > /tmp/pr-review-<番号>-detchecks.json
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
  （`/tmp/pr-review-<番号>-detchecks.json`）を渡す。本体は「最終diffで再計測したdetchecks-final」を渡す規定だが、
  report-onlyでは**修正適用が無いため最終diff＝Step 3のpatchであり、Step 6冒頭のdetchecks出力がそのまま最終値**になる。
  よって `deterministic_checks.py` の再実行はしない。4行契約の残り3行は `Read this : $CANON/.claude/skills/moores-code-review/post-checks/comment-convention-guard.md` /
  `Patch path : <PATCH_PATH>` / `User prompt : <USER_PROMPT_PATH>`（いずれも実値の絶対パスへ展開。下記「subagent起動契約への必須追記」参照）
- **`/tmp` の一時ファイル削除（本体Step 7の項目4）も行わない** — Step 3のpatchは後段のコード抜粋転記で読むため、
  ここで消すとダイジェストの実コードが作れなくなる
- AskUserQuestionは使わない。設計判断もダイジェストの裁定カードへ

### Codex外部監査（本体Step 3）の起動手当て

codexはプロンプトのテキストしか受け取らず、差分は**自分のcwdで**解決する。素直に起動するとこのセッションのcwd
（＝`$CANON`）を監査してしまい、PRと無関係なコードに所見を出す。かといってレビューworktreeへ `cd` して起動すると、
今度は**PR側の `AGENTS.md` / `CLAUDE.md` / `.codex/` をcodexが上位指示として読み込む**（＝レビュー対象が
レビュアーの指示を書ける自己弱体化経路）。次を必ず守る:

- **中立ディレクトリ（`/tmp` 等・リポジトリ外）から起動し、対象は全部プロンプト内の絶対パスで渡す**
  （バックグラウンド起動は本体どおり）:

      cd /tmp && codex exec --sandbox read-only --skip-git-repo-check - < /tmp/pr-review-<番号>-audit.md

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
  `Candidates :` / `Patch path :` / `User prompt :` の各パス（`/tmp` 配下）も同様に絶対パスで書く

## Step 7: ダイジェストHTML生成

`$CANON/.claude/skills/pr-independent-review/assets/digest-template.html` をReadし、sonnet subagentに
`/tmp/pr-review-<番号>/index.html` を生成させて `open` する。CSS・コメント機能JSはverbatim維持。

- **並び順の原則: ユーザーの裁定が必要なものほど上（ユーザー裁定 2026-07-30）**。Criticalは裁定不要の修正リストなので
  上位に置かない。構成は次の順で固定する:
  1. **verdictヘッダ**（verdict＋件数）
  2. **「あなたが判断すること」インデックス（新設・必須）** — このページでユーザーの裁定が要る項目だけを箇条書きで列挙
     （①必読の設計判断（下記トリアージ上位・通常2〜4枚） ②suppressedのPR内新設ADR検証 ③新形の入国審査は実質何問に畳めるか）。
     各行は該当カードへのページ内アンカーリンクにする
  3. **必読の設計判断カード** — 設計判断を次の基準でトリアージし、上位だけをここへ置く:
     (a)指摘系統の一致数が多い (b)裁定がCriticalの直し方を左右する（C連動） (c)ゲームプレイ・アーキテクチャの方向を変える。
     カード冒頭に「なぜ必読か」を1行で書く
  4. **残りの設計判断カード** — 「推奨案どおりで良ければ一言でよい」ものはその旨をカード内に明記
  5. **suppressedカード** — PR内新設ADR由来（免責の真偽確認が要るもの）を先頭に、他はその後ろ（全件・同形式＋suppressed-by出所）
  6. **新形カード** — 入国審査。論理グループ（新アセンブリ単位等）に畳み、各グループを「この新語彙を認めるか」の1問として提示
  7. **Critical要点カード** — 裁定不要の修正リストであることをセクション冒頭に明記（詳細は折りたたみ）
  8. **判断台帳**（ユーザー裁定/agent前提/PR内新設ADR（降格済み・要検証））
  9. **折りたたみ参考**
  各カードの中身: ファイル名太字・リポジトリ相対フルパス・行番号 → **その直下に一言サマリ1行（必須・ユーザー裁定 2026-07-30）**
  → 当該diffハンクの実コード抜粋（前後数行・追加行`<ins>`・問題行`.hl`）→ PR側の主張（出所ラベル付き）→ 代替案
- **一言サマリの書式（ユーザー裁定 2026-07-30「デッドコードがあるが免責されてる、でいい」）**: **欠陥・裁定対象そのものを
  主語にした短文1つ**（目安20字前後）。免責の仕組み・出所ラベルの話・系統数・規約条番号などのメタ情報をサマリに書くのは禁止
  （それらはsuppressed-by行・出所行が既に持っている）。例: Critical「絶対に発火しないifガードが2箇所ある」/
  suppressed「デッドなDI登録があるが免責されている」/ 設計判断「クールダウンキーの粒度を決める」。
  長い説明が要るならサマリの下に別段落で書き、サマリ行自体は絶対に伸ばさない
- **コード抜粋は全カード必須**（コード位置を持たない所見＝PR本文由来の申告等のみ例外で、その場合は出典テキストを引用）。
  suppressedカードも免責されているだけで所見のコード位置は持つので省略しない。生成後検査に「code-card（または出典引用）を
  持たないカードが0枚」を加える
- CONFIG固有化: `STORAGE_KEY='pr-review-<番号>-comments-v1'`、`COPY_TITLE='PR #<番号> 独立レビュー裁定'`。
  テンプレートは `REPLACE_WITH_UNIQUE_STORAGE_KEY` / `REPLACE_WITH_COPY_HEADING` のプレースホルダのまま出荷されているので置換必須（未置換だと別PRのコメントがlocalStorageで混ざる）
- **カード間の視覚分離**: 裁定カード・suppressedカードはテンプレート既定では背景・枠線を持たず、連続すると境界が曖昧になる。
  生成時に各カードのdivへ背景色または枠線を付けるようsubagentへ指示する
- **suppressedが0件でもセクションは省略しない**: suppressedセクションの見出しは常に出し、中身は
  「該当なし（0件）」の**1行**にする（カードは作らない）。セクションごと消すと「収集し忘れ」と区別がつかなくなる
- **判断台帳の「PR内新設ADR（降格済み・要検証）」グループ**: Step 4で `[agent前提]` へ降格したPR内新設ADR由来の
  項目は、判断台帳セクション内に**この名前のグループを設けてまとめて表示する**（ユーザー裁定・一般の `[agent前提]` の
  各項目と混ぜない）。0件でもグループの見出しは出し、中身は「該当なし（0件）」の1行にする。
  降格の事実が画面から消えると、PR自作のADRが免責ソースとして通ったように読めてしまうため
- **設計判断カードのバッジ**: テンプレートのバッジは `badge-new` / `badge-sup` の2種のみで「設計判断」用が無い。
  **`badge-new` のclassをそのまま流用し、表示文言だけ「設計判断」とする**（新形カードの文言は「新形」）。
  テンプレート側にclassを追加する改変はしない
- 実コード抜粋はStep 3のpatchから機械的に転記する（創作・要約禁止）
- **HTMLエスケープ契約（省略禁止）**: レビュー対象の文字列はそのままHTMLへ流し込むと壊れる。C#のジェネリクス
  （`Subject<int>`・`List<Action>`）・XMLコメント・yml・PRタイトルはいずれも `<` `>` `&` `"` `'` を含み得る。
  **PRタイトル・ファイルパス・コード抜粋・`data-label` などの動的文字列は、まず `&` `<` `>` `"` `'` を
  `&amp;` `&lt;` `&gt;` `&quot;` `&#39;` へ置換し（`&` を最初に置換する）、その後に**行番号 `<span class="ln">`・
  追加行 `<ins>`・問題行 `<span class="hl">` のマークアップを付与する。順序を逆にすると自分で付けたタグまで
  エスケープされて `&lt;ins&gt;` が画面に出る。エスケープを怠ると `Subject<int>` の `<int>` がタグとして食われ、
  **コード抜粋が黙って一部消える**（レビュー成果物としては致命傷。消えたことが画面から分からない）
- **生成後検査3点（必須・全部通るまで出荷しない）**:
  1. 未置換プレースホルダ0件 — `grep -c '{{' /tmp/pr-review-<番号>/index.html` が0
  2. `<script>` 要素がちょうど1個 — `grep -c '<script' /tmp/pr-review-<番号>/index.html` が1
     （コメント機能JSはverbatim維持＝増減しないのが正）
  3. `code-card` 内に生の `<` タグが無い — `<pre class="code-card">` ブロックを目視し、
     `<ins>` `</ins>` `<span class="ln">` `<span class="hl">` `</span>` 以外の `<` が残っていないことを確認する
     （残っていればエスケープ漏れ＝抜粋欠落）
- **プレースホルダ置換**: `{{TITLE}}`（hero・`<footer>`・`<title>` の計3箇所）/ `{{DATE}}` / `{{SUBTITLE}}` を実値へ置換する。
  `{{TITLE}}` = `独立レビュー: PR #<番号> <PRタイトル>`、`{{DATE}}` = レビュー実施日、`{{SUBTITLE}}` = verdict文字列。
  `<title>` の置換漏れはタブ名が `{{TITLE}}` のまま出荷される
- **`.verdict-header` の `data-verdict` 属性を必ずverdictに合わせて設定する**（テンプレート既定値は `ruling` 固定）。
  値は `auto`（自動マージ可）/ `ruling`（新形につき裁定行き）/ `reject`（Critical差し戻し）/
  `stub`（未測定（スタブ））の4語のみ（「verdict判定規則」の語彙と1対1）。
  表示には使われないが、成果物HTMLからverdictを機械抽出する唯一の口なので、見出し文言と食い違わせない
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
  4. 参考扱いのnew_edges（`generic_origin=false` のもの・`dir_is_new=true` のもの。裁定カードにはしない）
  5. 各系統（決定論／レンズ／reviewer／Codex／Fable／post-checksガード）の生所見要約を系統ごとに1ブロック。
     Codex不在等の縮退があればここに明記する

## Step 7.5: ダイジェストの外部公開（cloudflaredクイックトンネル）

`open` によるローカル表示に加え、**毎回必ず**ダイジェストを外部からアクセス可能にしてURLを報告する
（ユーザー裁定 2026-08-05・[[2026-08-05-レビューダイジェストの外部公開はcloudflaredクイックトンネルで行う]]）。
方式はローカルHTTPサーバ＋クイックトンネル固定。named tunnel（`tar-atari.com` 配下の固定サブドメイン）を
使ってはいけない — 恒久設定と常時公開になり「プロセスを止めれば失効する」という選択理由が失われる。

1. **ポートを選ぶ** — `8791` を既定とし、`lsof -nP -iTCP:<port> -sTCP:LISTEN` が非空なら +1 して空くまでずらす
   （同一マシンで複数PRのレビューが並走しうるため、決め打ちで潰さない）
2. **静的配信を起動**（`run_in_background: true`）:

       cd /tmp/pr-review-<番号> && exec python3 -m http.server <port> --bind 127.0.0.1

   `--bind 127.0.0.1` は省略禁止（LAN全体への無用な露出を避ける。外部到達はトンネルだけが担う）。
   `exec` はシェルを置き換えてPIDを一致させ、後で確実に止められるようにするため
3. **クイックトンネルを起動**（`run_in_background: true`）:

       cloudflared tunnel --url http://127.0.0.1:<port> --no-autoupdate

4. **URLを取り出す** — 起動ログから `https://<ランダム>.trycloudflare.com` を拾う。
   **ログを読む前に「URLはこれだろう」と推測して報告してはいけない**（毎回変わる）
5. **到達確認（必須・省略禁止）** — 次が **HTTP 200 かつ `<title>` が当該PRのものである**ことを確かめる:

       curl -s -o /tmp/tunnel-check-<番号>.html -w "%{http_code}\n" --max-time 25 https://<ランダム>.trycloudflare.com/
       grep -o '<title>[^<]*</title>' /tmp/tunnel-check-<番号>.html

   確認せずURLを報告するのは禁止 — トンネル確立とオリジン到達は別物で、`cloudflared` はオリジンが死んでいても
   URLを印字する。この確認だけが「本当に見える」ことの検知点である。
   なお**サンドボックス環境では `curl http://127.0.0.1:<port>` が `000` を返すことがある**が、
   トンネル側が200ならオリジンは生きている（ループバック直叩きの遮断であってサーバの故障ではない）
6. **報告に必ず添える2点**:
   - **URLは認証なし**で、知っている者全員がprivateリポジトリのソース抜粋を閲覧できる
   - **寿命はプロセス生存中のみ**。セッション終了・`TaskStop`・マシン再起動で失効する
7. **`/tmp/pr-review-<番号>/` は消さない**（Step 7の成果物が配信実体。Step 6のreport-only規約どおり `/tmp` は残す）

- **公開URLを `$LOGS` の records・シャドー台帳へ書かない**。URLは毎回変わる使い捨てで台帳の再現性に寄与せず、
  失効済みURLが記録に残ると後から「まだ見える」と誤読される。固定書式にフィールドを足すのも禁止（grep横断集計が壊れる）
- 明示的に停止を求められたら、2つのバックグラウンドシェル（http.server と cloudflared）を両方止める。
  **片方だけ止めない** — 配信だけ残るとLAN内に開いたまま、トンネルだけ残ると502を返し続ける

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
      - canonical: <$CANONのHEAD SHA>（origin/master固定）<SKILL.md同一性ガードで差分が出たまま続行した場合のみ ・skew: $ORIGIN=<SHA> を追記>
      - 系統: <発火した系統名と各々の完了/縮退。例: 決定論=完了/レンズ3本=完了/reviewer5本=完了/Codex=縮退（不在）/Fable=完了>
      - session: <このレビューセッションの識別子>

      ## 新形
      <新形フラグ1件1行（系統名・ファイル:行（lineがnullならファイルのみ）・要点）>
      - 系統別件数: new_edges（採用基準を満たすもの）N / asmdef_refs N / grammar N
      - 参考（新形に数えない）: generic_origin=false N件 / dir_is_new=true N件

      ## 裁定
      <裁定カード1件1行（ファイル:行・指摘要点・代替案）>

      ## suppressed
      <1件1行（ファイル:行・指摘要点・suppressed-by出所）>

- **測定器メタデータ行（`head` / `base` / `canonical` / `系統` / `session`）は省略禁止**。
  これらは「何を・どの測定器で測ったか」の記録であり、欠けると後からverdictの再現も、
  測定器の版差による見逃し率の比較もできなくなる。`head` と `base` は
  `git -C ~/moorestech-worktrees/pr-review rev-parse HEAD` / `rev-parse "<BASE_REF>^{commit}"` の実出力、
  `canonical` は `git -C <$CANONの実値> rev-parse HEAD` の実出力（`origin/master` 固定＋毎回 `reset --hard`
  なので `clean`/`dirty` の別は生じない）。SKILL.md同一性ガードで差分が出たまま続行した場合のみ、
  `git -C <$ORIGINの実値> rev-parse HEAD` も併記して版ズレを残す
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

- **見逃しの記録粒度（ユーザー裁定 2026-08-02）**: 台帳は**verdict比較（`一致` 列）のまま**とし、
  欠陥単位の内訳は台帳の列にしない。内訳は**reconcileモードが、人間コメントが1件以上存在するPR全件について**、対象の
  `$LOGS/harness/pr-independent-review/records/pr-<番号>.md`（再レビュー分は該当する `-rN` ファイル）の末尾へ
  次のセクションとして**追記する**。記入はセッション側の作業であり、人間は確認のみ行う（人間に内訳を書かせない）。
  **verdictが一致していてもreconcileを省いてはいけない** — PR #1095でverdict一致のままmissed 17件が出ており、
  verdict一致は見逃しゼロを意味しない:

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
- 記録類は `$LOGS` にのみ書き、コミットはユーザーに委ねる。**コードrepoのどのtreeへも書かない**
  （冒頭「書き込み先の規律」参照）

## reconcileモード（人間レビューとの突き合わせ・改善発火）

`/pr-independent-review reconcile <番号>` で単独起動、またはStep 0.5の負債ゲートから強制実行される。
**ここは改善機構の発火装置であり、改善の手法・検証・回帰コーパスは moores-code-review 側
（`references/skill-improvement.md`・`eval/`）が単一の正である。手順・fixture・検証規則をこちらへ複製しない。**

1. **入力は人間のGitHubコメントのみ**（人間に台帳記入・ラベル付け・分類を求めない。人間の自然なレビュー行為の
   排気だけを信号源にする）:

       gh api repos/moorestech/moorestech/pulls/<番号>/comments --paginate \
         --jq '.[] | {path, line, body, html_url, commit_id}' > /tmp/pr-reconcile-<番号>-comments.json

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
    PR側の欠陥ではない。検出時はcontextファイル（`/tmp/pr-review-<番号>-context.md`）を書式どおりに修正して
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
- `$CANON`（skills-canon）の用意に失敗（`worktree add` 失敗・`fetch`/`reset --hard origin/master` 失敗・
  `novelty_gate.py` 不在）: 即エラー終了・理由報告。起動元treeの `.claude/` で代替するのは**禁止**
  （物差しが実行中に動く状態へ戻るだけで、それは今の固定方式が解こうとしている問題そのもの）
- SKILL.md同一性ガードで差分が出た: **先へ進まずユーザーへ報告して指示を仰ぐ**（冒頭「$CANONの用意」手順5）。
  続行を指示された場合のみ進み、recordsの `canonical:` に `skew` と両SHAを明記する
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
- Step 7.5の外部公開に失敗（`cloudflared` 不在・トンネル未確立・到達確認がHTTP 200以外またはtitle不一致）:
  **レビュー自体はエラー終了させない**（ダイジェストとrecordsは既に手元にあり、成果物としては完成しているため）。
  ローカルの `open` 済みパスを案内し、公開できなかった事実と理由を報告に1行明記する。
  URLに触れないまま黙って完了報告するのは禁止
