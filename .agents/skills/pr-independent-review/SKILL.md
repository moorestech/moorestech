---
name: pr-independent-review
description: |
  実装セッションと完全に独立したセッションでPRをレビューする手動発火スキル。PR URLまたは番号を受け取り、
  レビュー専用worktreeにcheckoutして moores-code-review（report-only）＋新規性ゲートL1を実行し、
  実コード抜粋入りのインフォグラフィックHTMLダイジェスト（verdict/裁定カード/suppressed）と
  シャドー台帳を出力し、ダイジェストはcloudflaredクイックトンネルで外部からも閲覧できるURLを毎回発行する。
  実装セッションの自己申告contextは一切受け取らない。
  レビューと指摘への対応が完了したPRには「独立レビュー&対応完了」ラベルを付与する。
  Use When:
  1. 「/pr-independent-review <PR URL|番号>」で起動された時
  2. 「このPRを独立レビューして」「シャドーレビューして」と言われた時
  3. 「/pr-independent-review reconcile <番号>」で起動された時（人間レビューとの突き合わせ・見逃し検知・改善発火）
---

# pr-independent-review — 独立セッションPRレビュー（シャドー運用v1）

## 最重要: 無人起動でも「findings.json か abort.json で終える」

環境変数 `PR_REVIEW_UNATTENDED=1` が立っているとき、このスキルは poller から cmux ワークスペース上の
**対話モード** claude でフォアグラウンド起動されている（ADR 0023。2026-08-20 までは `claude -p` だった）。
対話モードではターンを終えてもプロセスは消えないが、**poller はあなたが動いているかを transcript の更新で見ている**。
session と subagents の transcript が 1200 秒更新されないと「自壊相当」と判定され、同じペインへ
RESUME 指示が1回送られ、それでも進まなければ失敗ラベルになる。

したがって無人起動時は:

- **待機は同一ターン内でブロッキングして行う**（subagent の完了待ちは Monitor 等で待ち切る）。
  「後で結果を確認します」とターンを閉じて待つことは、transcript が止まるため自壊と判定される
- **質問して停止することを禁止する**。判断が要る指摘はダイジェストの裁定カード（設計判断）へ落とす
- **終了地点は2つだけ** — Step 7.5 の `findings.json` が生成された直後か、下記「中止の申告」で
  `abort.json` を書いた直後
- **session limit に当たったら何もしなくてよい**。poller が reset 時刻まで待ち、同じペインへ
  「$RUNDIR/agents/*.md を点検し、オーケストレータのエージェントIDへ SendMessage で未完了分だけ続行」
  という継続指示を送る。その指示が来たら、完了済みの体は再派遣せず、保持しているIDへ SendMessage で続きを頼むこと
- 人がペインに割り込んで指示した場合は「止める」「続きを指示する」に限って従う

`PR_REVIEW_UNATTENDED` が無い（人が対話で起動した）場合は、質問して止まってよい。ただし
`findings.json` / `abort.json` のどちらかで終える規律は同じく守る。

### 中止の申告（abort.json）

「エラー処理」節のどの規定で中止するときも、**終わる前に `$RUNDIR/abort.json` を書く**。
これが無いままの終了は poller から自壊と見なされ、同一セッションが1回 resume される（＝人間を呼ぶべき
fail-closedが、押し切られて続行される）。`$RUNDIR` がまだ無い段階での中止なら `mkdir -p` してから書く。

```json
{"reason": "<中止理由の一行>", "step": "<中止したStep名>", "at": "<ISO8601>"}
```

`reason` は失敗コメントへそのまま転記されるので、人間が次の一手を決められる粒度で書く（1行・バッククォート不可）。

**書き先は「このrunの `$RUNDIR`」ちょうど**。まだ `$RUNDIR` を決めていない段階の中止なら、
`runs/pr-<番号>`（2回目以降は `runs/pr-<番号>-r<N>`）のうち**このrunに割り当てた1つ**を `mkdir -p` して書く。
pollerは `runs/pr-<番号>*` を全部走査して最も新しい申告を拾うので、どのrunディレクトリでも検出はされる。

対応spec: `docs/superpowers/specs/2026-07-27-pr-independent-review-design.md`

**正典tree `$CANON`**: 測定器（スクリプト・レンズ・reviewer・統合ルール・テンプレート）の唯一の読み取り元。
**起動時に解決した `origin/master` のSHAへピンした使い捨てworktree `skills-canon-<sha8>`** とする
（ユーザー裁定 2026-08-05。共有worktreeへの毎回resetをSHAピンへ変えたのは2026-08-19裁定 —
並列レビューが共有canonをresetし合うと、実行中に物差しが差し替わる競合が生じるため）。
スクリプト・レンズ・統合ルールは必ず `$CANON` の絶対パスで参照する。`$PRWT` 側の
`.claude/` は**絶対に使わない**（PRごとに測定器が変わり見逃し率実測が壊れる・自己弱体化経路）。

`$CANON` を「このSKILL.mdが置かれているtree」にしてはいけない — それはたいてい他セッションが実装作業中の
メインworktreeであり、**レビュー実行中にブランチが切り替わって物差しが変わる**（2026-08-05に実測）。
台帳の `canonical:` は測定器の版を記録する欄なので、版が実行中に動く前提では記録が意味を失う。

**$CANONの用意（最初に必ず1回やる）**:

1. **起動元repo `$ORIGIN` を特定する** — このSKILL.mdをReadしたときの絶対パスから
   `/<dir>/skills/pr-independent-review/SKILL.md`（`<dir>` は `.agents`/`.claude`/`.codex` のいずれか。
   skills実体は `.agents/skills` で他2つはsymlink）を**文字列として取り除いた**残り。
   **`$ORIGIN` はworktreeを生やす起点としてのみ使い、読み取り元にも書き込み先にもしない**
2. **ピンSHAの解決**: `$ORIGIN` の `origin/master` を更新し、今回の測定器の版を確定する:

       git -C <$ORIGINの実値> fetch origin "+refs/heads/master:refs/remotes/origin/master"
       git -C <$ORIGINの実値> rev-parse --short=8 refs/remotes/origin/master

   出力（曖昧回避で8桁より伸びることがある。そのまま使う）を `<sha8>` とする
3. **SHAピンworktreeの場所**: PR専用worktree `$PRWT`（`pr-<番号>`）と**同じ親ディレクトリ**の
   `skills-canon-<sha8>`。これが `$CANON` の実値。無ければ作る:

       git -C <$ORIGINの実値> worktree add <$CANONの実値> --detach <sha8>

   既にあればそのまま再利用する。**SHA固定なので内容は不変であり、`fetch`・`reset`・`clean` は一切行わない**
   （並列レビューが同じピンを同時に読んでも安全。これがSHAピン化の目的）
4. **使用記録と古ピンの掃除**: `touch <$CANONの実値>/.last-used` で使用時刻を記録する。そのうえで
   同じ親ディレクトリの他の `skills-canon-*`（旧方式のsha8無し `skills-canon` を含む）のうち、
   `.last-used` の更新時刻が**24時間より古い**もの・`.last-used` が無いものを
   `git -C <$ORIGINの実値> worktree remove --force <ピンの実値>` で消す。
   24時間はレビュー1本の所要より十分長く、実行中の他レビューのピンを消さないための猶予
5. 実在確認: `ls <$CANONの実値>/.agents/skills/pr-independent-review/scripts/novelty_gate.py`。
   失敗したら即エラー終了（$CANON誤決定のまま走らせない）。**確認先はこのファイルでなければならない** —
   `moores-code-review/SKILL.md` は `$PRWT` 側にも存在しうるため、誤決定した$CANONでも通ってしまい弁別にならない
6. **SKILL.md同一性ガード（必須・省略禁止）**:

       diff <$ORIGINの実値>/.agents/skills/pr-independent-review/SKILL.md \
            <$CANONの実値>/.agents/skills/pr-independent-review/SKILL.md

   **差分が出たら先へ進まず、ユーザーへ報告して指示を仰ぐ。** 理由: SKILL.md本体はharnessが `$ORIGIN` から
   読み込むものでskillには選べない。つまり固定できるのは参照ファイル（レンズ・スクリプト・テンプレート）だけで、
   `$ORIGIN` に未マージのskill改修があると「新しい指示 × 古いレンズ」の版ズレで走る。
   これは黙って進むと所見の由来が説明不能になる種類の故障なのでfail-closedにする。
   ユーザーが続行を選んだ場合のみ進み、**recordsの `canonical:` に `skew` と両SHAを明記する**
   （無人起動では「指示を仰ぐ」ことができないので、**中止する前に `$RUNDIR/abort.json` を書く**。
   冒頭「中止の申告」節。書かずに終わるとpollerが自壊と誤認し、fail-closedを押し切ってresumeする）

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

- **`$CANON` / `$ORIGIN` は本ドキュメント上のプレースホルダであり、シェル変数ではない**。Bashコマンド・
  subagentのprompt・ファイルパスに渡すときは**必ず実値の絶対パスへ展開して書く**。リテラルのまま渡すと
  未定義変数で空文字に展開され、`/.claude/skills/...` という不存在パスを叩いて沈黙故障する
- `$ORIGIN` は `~/moorestech` とは限らない（worktreeから発火する運用が現にある）。`~/moorestech` を決め打ちしない

**書き込み先の規律**:
このスキルが触るtreeは3つある。**書いてよいのは `$PRWT` にPRのコード修正を入れるときだけ**で、他は読み取り専用。

| tree | 中身 | 書けない理由 |
| --- | --- | --- |
| `$CANON`（skills-canon-<sha8>） | 起動時のorigin/master SHAへピンした測定器 | SHAピンは「不変」が契約。書けば同じピンを読む並列レビュー全員の物差しが汚れる（`.last-used` の `touch` だけが例外） |
| `$ORIGIN`（起動元・多くはメインworktree） | 他セッションの作業中ブランチ | 他人の作業ツリーを汚し、コミットすれば無関係なブランチへ混入する（**実際に実行中ブランチが切り替わった**） |
| `$PRWT`（`pr-<番号>`） | PRのheadブランチ | **PRのコード修正だけは書いてよい**（Step 9）。skill改修・`.decisions/` の裁定記録をここに積むのは筋が通らない |

- レビュー成果物の置き先は既に分離済み — ダイジェスト・中間生成物は `$RUNDIR`、実行記録は `$LOGS`。
  **レビューだけで終わる1周では、コードrepoへの書き込みは1バイトも発生しない**
- PRのコード修正を頼まれたときだけ `$PRWT` へ書く（Step 9）
- skill改修・`.decisions/` への裁定記録は、上の3treeのどれでもない**専用worktreeを新たに切ってそこで完結させる**:

      git -C <$ORIGINの実値> worktree add \
        <worktree親ディレクトリ>/skill-<用件> -b chore/<用件> origin/master

  worktree親ディレクトリは `$PRWT` / `skills-canon-<sha8>` と同じ場所。`origin/master` 起点にするのは、
  `$ORIGIN` の現在ブランチ（他人の作業中ブランチ）を巻き込まないため。
  **裁定記録を `$PRWT` に積まない** — PRブランチが `.decisions/` を抱えるとレビュー対象と記録が混ざる
- **撤収確認（必須）**: 作業後に `git -C <$ORIGINの実値> status --porcelain -- <触れたパス>` が**空**であることを
  確かめる。空でなければ `$ORIGIN` に自分の変更が残っている＝ミスの再演
- **報告義務**: skill改修を専用worktreeのブランチに載せた場合、**その改修はmasterへマージされるまで有効にならない**
  （`$CANON` はmasterのSHAからピンされるため、マージ前の改修は測定器に入らない）。
  「どのブランチに載せたか」「まだ有効でないこと」を必ず報告に書く

改善と言われたときは0.5を実行する。
修正と言われたときは改善ではなく、PRそのもののコード修正を行う。修正がpushまで完了したら
Step 10（対応完了ラベル）の付与条件を確認し、満たしていればラベルを付ける。

## Step 0: 独立性の自己申告ガード

**このセッションが対象PRの実装・レビュー・計画に何らかの形で関与していた場合は、ここで実行を中止する**（PRブランチで
コードを書いた・その実装のspec/planを書いた・同じPRを既にレビューした・実装セッションからの引き継ぎcontextを受け取った、
のいずれか）。独立レビューの値打ちは「実装の意図を知らない目で見る」ことにあり、関与済みセッションが走ると
見逃し率の実測値がそのぶん楽観側へ歪む。中止時はユーザーへ「このセッションは対象PRに関与済みのため独立性を満たさない。
新規セッションで起動されたい」と報告し、**`$RUNDIR/abort.json` を書いてから**終わる（冒頭「中止の申告」節）。
判定は自己申告でよいが、迷ったら中止側に倒す。

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

## Step 2: PR専用worktree `$PRWT` へcheckout

コマンドは `git -C <絶対パス>` 形式か、**`cd` を同一コマンド内に含めた形**で書く。agent実行系ではbash呼び出し間で
cwdがリセットされるため、単独の `cd` は次のコマンドに効かない。`~` はsubagentのpromptやファイルパスへ渡す時点で
絶対パスに展開する。

**worktreeはPRごとに1つ作り、レビューからpushまでそこで完結させる**（ユーザー裁定 2026-08-05）。
共用の使い回しworktreeにしてはいけない — 並行レビューで奪い合いになり、修正作業中のツリーを次のレビューが
`reset --hard` で消すためである。

- **場所**: `skills-canon-<sha8>` ピン群と同じ親ディレクトリの `pr-<番号>`。以下これを `$PRWT` と呼ぶ
- **無ければ作る**（`$ORIGIN` は冒頭で決めた実値に展開して渡す。`~/moorestech-worktrees` の決め打ちは禁止 —
  worktree親ディレクトリが `$ORIGIN` の兄弟にあるケースが現に存在する）:

      git -C <$ORIGINの実値> fetch origin "+refs/heads/<headRefName>:refs/remotes/origin/<headRefName>"
      git -C <$ORIGINの実値> worktree add <$PRWTの実値> origin/<headRefName>

  **`$ORIGIN` で `gh pr checkout` を実行してはいけない**（2026-08-05に実際にやった事故）。`gh pr checkout` は
  cwdのworktreeのブランチを切り替えるため、**メインworktreeが他セッションの作業ブランチから引き剥がされる**。
  PRブランチの取得は上記の `fetch` + `worktree add` で行い、`gh pr checkout` を使う場合は必ず `$PRWT` へ
  `cd` した状態で叩く
- **既にあれば作り直さない**。`git -C <$PRWTの実値> status --porcelain` が**非空なら即エラー終了**して報告する
  （前回の修正作業が残っている可能性がある。`reset --hard` で他人の作業を消さない）。空なら次へ進む
- **PR headへ追随**: `git -C <$PRWTの実値> fetch origin "+refs/heads/<headRefName>:refs/remotes/origin/<headRefName>"`
  のうえ `git -C <$PRWTの実値> merge --ff-only origin/<headRefName>`。fast-forwardできない場合は
  ローカルに独自コミットがある＝前回の修正が未pushなので、即エラー終了して報告する
- **後片付けはユーザーに委ねる**。PRがマージ・closeされたら `git -C <$ORIGINの実値> worktree remove <$PRWTの実値>`
  で消せるが、独立セッションが勝手に消さない（未pushの修正が入っていることがある）
- base最新化（**refspecを明示する**）:

        git -C <$PRWTの実値> fetch origin \
          "+refs/heads/<baseRefName>:refs/remotes/origin/<baseRefName>"

  引数なしの `fetch origin <baseRefName>` はremote-tracking ref（`refs/remotes/origin/<baseRefName>`）を
  更新せずFETCH_HEADだけを書く設定があり得るため、`BASE_REF`＝`origin/<baseRefName>` が古いまま解決されて
  base取り違えになる。refspecを明示すればtracking refの更新が保証される。
  MERGEDでも実行する（`<mergeCommit>` とその第1親をローカルへ持ってくるため。
  本節以降の `<mergeCommit>` はすべてStep 1.5の規約どおり `.mergeCommit.oid` の40桁SHAへ展開して書く）。
  **このfetchの失敗ではエラー終了しない** — マージ後にbaseブランチが削除されているとremote refが無く落ちるが、
  下の「BASE_REF の解決確認」のフォールバック（`fetch origin <mergeCommit>`）で回収できるため、そこまで進んで判定する
- checkout（`state` で分岐）:
  - **OPEN**: `cd <$PRWTの実値> && gh pr checkout <番号>`（`--detach` を付けない）
    **後で修正をpushするため、PRブランチをブランチとしてcheckoutする**。detachedだとcommitはできてもpush先が無い。
    `gh pr checkout` はリポジトリコンテキストを要求し `-C` にできないので、`cd` は必ず同一コマンド内に置く。
    **`cd` 先が `$PRWT` であることを目視してから実行する**（`$ORIGIN` で叩くとメインworktreeのブランチが変わる）。
    ブランチロック（`fatal: '<branch>' is already checked out at ...`）で失敗したら、**奪わずに**
    どのworktreeが保持しているかを報告して指示を仰ぐ。実装セッションが作業中の可能性がある
  - **MERGED**、または OPEN でも headブランチ削除済みで上が `fatal: couldn't find remote ref` / exit 128 になる場合:

        git -C <$PRWTの実値> fetch origin pull/<番号>/head && \
          git -C <$PRWTの実値> checkout --detach FETCH_HEAD

    それも失敗する場合は `<mergeCommit>` 自体をcheckoutする（`git -C <$PRWTの実値> checkout --detach <mergeCommit>`。
    `<mergeCommit>` はStep 1.5の規約どおり `.mergeCommit.oid` のSHA）。
    差分は `BASE_REF`＝`<mergeCommit>^1` との比較なので、PRの変更集合としては同じものが取れる
- **BASE_REF の解決確認（ここで必ず行う）**: `git -C <$PRWTの実値> rev-parse --verify "<BASE_REF>^{commit}"`
  が成功することを確かめる。MERGEDで `<mergeCommit>` がローカルに無くて失敗した場合のみ
  `git -C <$PRWTの実値> fetch origin <mergeCommit>`（同じく `.mergeCommit.oid` のSHA）を挟んで再確認する。
  それでも解決できなければ即エラー終了（不正・未解決のbaseのまま先へ進まない）
- **checkout整合の確認（ここで必ず行う）**: `git -C <$PRWTの実値> rev-parse HEAD` の出力が
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

    git -C <$PRWTの実値> -c core.quotepath=false diff \
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
- `[ユーザー裁定: "発言引用" …]` の引用欄に書けるのは**ユーザー発言または AskUserQuestion の質問文＋採択ラベルの逐語**だけ。`.decisions/` のファイル名・ADRの決定文・「質問で採択」等の言い換えは引用ではない。引用元（ADR・`.decisions/`）に逐語が無い場合は `[ADR: <spec名>#<台帳項目>（原文引用なし）]` と注記して書く（免責力は保つが、reviewer の含意チェックがこの注記で「検査不能」を判定できるようにする）
- **`[ADR:]` を引用する前に、そのspec/planファイルがPR diff自身で追加・変更されていないか必ず確認する**:

        git -C <$PRWTの実値> diff <BASE_REF>...HEAD --name-only -- docs/superpowers/

  （`<BASE_REF>` はStep 1.5で確定した実値）の出力に引用元ファイルが含まれる場合、そのファイル由来のADR項目は
  **`[agent前提]` へ自動降格する**（＝免責力なし）。contextの当該行末に `（PR内新設ADR）` と注記する。
  根拠: PRが自作したADRを免責ソースとして認めると免責ロンダリング事故の再演経路になる
  （承認済み3原則①「引用不能な合意は `[agent前提]`」の適用。独立セッションからはPR内ADRのユーザー承認の実在を
  検証できないため「引用不能」に分類する）。
  **この降格はverdictに影響しない** — 降格された項目で免責されなくなった指摘は通常のCritical/Warningとして扱われ、
  通常の判定規則（「verdict判定規則」）に乗る

## Step 5: 新規性ゲートL1

    python3 "$CANON/.claude/skills/pr-independent-review/scripts/novelty_gate.py" \
      <$PRWTの実値> <BASE_REF> > <$RUNDIRの実値>/novelty.json

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
  2. `git -C <$PRWTの実値> merge-base <BASE_REF> HEAD` が **HEADと一致しないこと**
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

- PATCH_PATH = Step 3の生成物 / USER_PROMPT_PATH = Step 4の生成物 / cwd＝`$PRWT`（この系統ではコード読み取り専用）
- スクリプト実行・レンズ/reviewer/統合ルールのReadパスは全部 `$CANON` 配下の絶対パス

### 本体のコマンド例を使わず、次の3行をそのまま使う

本体SKILL.mdのコマンド例は `.claude/skills/...` の**相対パス**で書かれている。cwdが `$PRWT` なので
コピペするとPR側の `.claude/` を実行してしまう（＝正典tree原則の破れ・自己弱体化経路そのもの）。必ず下記で置き換える
（`$CANON` は冒頭で決めた実値に展開して書くこと）:

```bash
python3 "$CANON/.claude/skills/moores-code-review/scripts/deterministic_checks.py" "<PATCH_PATH>" --repo-root <$PRWTの実値> --context "<USER_PROMPT_PATH>" > <$RUNDIRの実値>/detchecks.json
python3 "$CANON/.claude/skills/moores-code-review/scripts/select_lenses.py" "<PATCH_PATH>"
python3 "$CANON/.claude/skills/moores-code-review/scripts/select_reviewers.py" "<PATCH_PATH>"
```

- **`--repo-root` は `$PRWT` 側**（`<$PRWTの実値>`）。ADR参照の解決と200行判定は
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
（＝`$CANON`）を監査してしまい、PRと無関係なコードに所見を出す。かといって `$PRWT` へ `cd` して起動すると、
今度は**PR側の `AGENTS.md` / `CLAUDE.md` / `.codex/` をcodexが上位指示として読み込む**（＝レビュー対象が
レビュアーの指示を書ける自己弱体化経路）。次を必ず守る:

- **中立ディレクトリ（`/tmp` 等・リポジトリ外）から起動し、対象は全部プロンプト内の絶対パスで渡す**
  （バックグラウンド起動は本体どおり）。ここの `/tmp` は**codexのcwdとして使うだけ**でありファイル置き場ではない
  （`$RUNDIR` は `$LOGS` 配下＝git repo内なので、cwdにするとcodexがlogs repoを覗く。cwdは中立のまま保つ）:

      cd /tmp && codex exec --sandbox read-only --skip-git-repo-check -o <$RUNDIRの実値>/codex-audit.final.md - < <$RUNDIRの実値>/codex-audit.md > <$RUNDIRの実値>/codex-audit.out.md 2>&1

  **`-o` は必須で、結論の正本は `.final.md`**（stdoutは完走しても最終回答が届かないことがある）。`.final.md` が
  空・不在なら欠員と断定する前に
  `python3 $CANON/.claude/skills/moores-code-review/scripts/codex_recover.py --prompt <$RUNDIRの実値>/codex-audit.md --out <$RUNDIRの実値>/codex-audit.out.md`
  を走らせる（exit 0=回収成功で通常の1系統として扱う / 3=未完走 / 4=起動失敗＝真の欠員）

  **`$PRWT` へ `cd` しない**。プロンプト内でリポジトリを参照する箇所は必ず
  `git -C <$PRWTの実値> ...` の形（`-C` に実値の絶対パス）で書き、
  読ませたいファイルも絶対パスで指定する。`~` は展開して書く（プロンプトはシェルを通らない）

- **audit-templateの差分指定欄を書き換える** — テンプレートは
  `$CANON/.claude/skills/moores-code-review/scripts/codex-audit-template.md`（`$CANON` は冒頭で決めた実値の絶対パスに
  展開してRead）。これは「レビュー対象は、このセッションで私が作業した成果物だけです」＋コミット済み/staged/unstaged の
  3行構成だが、独立レビューでは作業成果物が存在しない（worktreeはcleanなcheckout）。
  **2行目（「レビュー対象は、このセッションで私が作業した成果物だけです。」の行）を「レビュー対象は PR #<番号> の
  差分だけです。」に差し替え**、続く3行（コミット済み／staged／unstaged）を
  `- 差分: git -C <$PRWTの実値> diff <BASE_REF>...HEAD`
  （`BASE_REF` とworktreeパスはいずれも実値の絶対パスへ展開）の1行に置き換える。
  1行目の役割宣言行はそのまま使う。staged/unstaged 行を残してはいけない（常に空で「変更なし＝問題なし」という誤結論を誘発する）。
  **`-C` の省略も禁止** — 起動cwdが中立ディレクトリなので、省くと差分が1行も取れないまま監査が走る
- `## 目指す / 目指さない / 許容するトレードオフ / 尊重すべき制約` 欄にはStep 4のcontextをそのまま貼る
- `which codex` が失敗したらスキップし、ダイジェストの折りたたみ参考節に縮退として明記する（本体規約どおり）

### subagent起動契約への必須追記

レンズ・reviewer・Fable全般・verifier・post-checksガードの**全promptに、3行/4行契約に加えて次の2行を必ず含める**
（含め忘れると、subagentは自分のcwdや `$CANON` 配下のコードを読んでPRと無関係な箇所をレビューする）:

```
対象コードのルート: <$PRWTの実値>（絶対パス）。コードのReadは必ずこの配下で行う。
`.claude/` 配下のスキル・レンズ・post-checks・統合ルールの定義のReadは <$CANONの実値> 配下で行う。
```

- `<$CANONの実値>` は冒頭で決めた絶対パスへ展開して書く（リテラルの `$CANON` を渡さない）
- `<$PRWTの実値>` も **`~` を展開した絶対パスで書く**（例: `echo ~/moorestech-worktrees/pr-1129` の出力＝
  `<$PRWTの実値>`）。subagentのpromptは文字列であってシェルを通らないため、
  `~` のまま渡すとリテラルの `~` ディレクトリを探して読めない
- **全サブエージェント契約（レンズ・reviewer・Fable全般・verifier・comment-rationale-guard・comment-convention-guard）の
  `Read this :` 行は `$CANON` 実値の絶対パスで書く** — 本体SKILL.mdの契約例は `.claude/skills/moores-code-review/...` の
  相対パスなので、そのままコピペするとsubagentのcwd（＝`$PRWT`）側のPR同梱スキルを読む。
  `Candidates :` / `Patch path :` / `User prompt :` の各パス（`$RUNDIR` 配下）も同様に絶対パスで書く

## Step 7: ダイジェスト生成（digest.md → コンバータ）

sonnet subagentに `<$RUNDIRの実値>/digest.md` を**Markdownで**生成させる。フォーマットの正本は
`$CANON/.agents/skills/pr-independent-review/README-digest-format.md` を読ませる（生成subagentの参照先はこの1本のみ）。

- 生成後に次を実行する:

      python3 $CANON/.agents/skills/pr-independent-review/scripts/digest_build.py <$RUNDIRの実値>

  非0終了なら **digest.mdを直して再実行する**（HTMLを手で直すのは禁止。コンバータのエラーメッセージが
  何のキー・見出しが欠けているかを指すので、それに従ってdigest.mdを修正する）
- コンバータは `$RUNDIR/patch.diff` を読む。Step 3 の生成物なので通常は存在するが、
  無い場合はエラーで落ちる（`patch.diff がありません`）
- 成功したら `open <$RUNDIRの実値>/digest.html`
- **残す規約**（生成subagentへの指示として引き継ぐ）:
  - カードのトリアージ基準（`must_read: true` を付ける条件）: (a)指摘系統の一致数が多い
    (b)裁定がCriticalの直し方を左右する (c)ゲームプレイ・アーキテクチャの方向を変える
  - 一言サマリの書式: 欠陥・裁定対象そのものを主語にした短文1つ（目安20字前後）。免責の仕組み・
    出所ラベルの話・系統数・規約条番号などのメタ情報はサマリに書かない
  - コード抜粋は全カード必須（`code-card` フェンス）。patchから機械的に転記する（創作・要約禁止）。
    **置換なら削除行 `-<旧行番号>|<コード>` も必ず転記する**（コンバータが `patch.diff` と照合し、
    欠けていればエラーで落ちる）。1カードには単一ファイルの抜粋だけを入れる（言語は `files` 先頭の
    拡張子から自動判定されるため、複数言語を混ぜると後半が誤着色される）
  - `# 折りたたみ参考` に必ず入れる5項目: Criticalの修正方針詳細／Warning全件（1件1行・出所系統つき・
    要約による間引き禁止）／Info一覧（圧縮列挙可）／参考扱いのnew_edges／各系統の生所見要約
  - 案はカード本文へ手で書かない。`options:` へ書けばコンバータが案A/案B…として描き、
    先頭へ推奨マークを付ける。本文に「代替案」を書くとエラーで落ちる（`recommendation` も書けない）
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

## Step 7.6: ダイジェストの外部公開（cloudflaredクイックトンネル）

`open` によるローカル表示に加え、**毎回必ず**ダイジェストを外部からアクセス可能にしてURLを報告する
（ユーザー裁定 2026-08-05・[[2026-08-05-レビューダイジェストの外部公開はcloudflaredクイックトンネルで行う]]）。
方式はローカルHTTPサーバ＋クイックトンネル固定。named tunnel（`tar-atari.com` 配下の固定サブドメイン）を
使ってはいけない — 恒久設定と常時公開になり「プロセスを止めれば失効する」という選択理由が失われる。

1. **ポートを選ぶ** — `8791` を既定とし、`lsof -nP -iTCP:<port> -sTCP:LISTEN` が非空なら +1 して空くまでずらす
   （同一マシンで複数PRのレビューが並走しうるため、決め打ちで潰さない）
2. **静的配信を起動**（`run_in_background: true`）:

       exec python3 -m http.server <port> --bind 127.0.0.1 --directory <$RUNDIRの実値>

   `--bind 127.0.0.1` は省略禁止（LAN全体への無用な露出を避ける。外部到達はトンネルだけが担う）。
   `exec` はシェルを置き換えてPIDを一致させ、後で確実に止められるようにするため
3. **クイックトンネルを起動**（`run_in_background: true`）:

       cloudflared tunnel --url http://127.0.0.1:<port> --no-autoupdate

4. **URLを取り出す** — 起動ログから `https://<ランダム>.trycloudflare.com` を拾う。
   **ログを読む前に「URLはこれだろう」と推測して報告してはいけない**（毎回変わる）。
   報告するURLは `https://<ランダム>.trycloudflare.com/digest.html`（配信ルートは `$RUNDIR` なのでファイル名まで付ける）
5. **到達確認（必須・省略禁止）** — 次が **HTTP 200 かつ `<title>` が当該PRのものである**ことを確かめる:

       curl -s -o /tmp/tunnel-check-<番号>.html -w "%{http_code}\n" --max-time 25 https://<ランダム>.trycloudflare.com/digest.html
       grep -o '<title>[^<]*</title>' /tmp/tunnel-check-<番号>.html

   この一時ファイルは成果物ではないので `/tmp` でよい（`$RUNDIR` へ置くとhookが記録として拾ってしまう）。

   確認せずURLを報告するのは禁止 — トンネル確立とオリジン到達は別物で、`cloudflared` はオリジンが死んでいても
   URLを印字する。この確認だけが「本当に見える」ことの検知点である。
   なお**サンドボックス環境では `curl http://127.0.0.1:<port>` が `000` を返すことがある**が、
   トンネル側が200ならオリジンは生きている（ループバック直叩きの遮断であってサーバの故障ではない）
6. **報告に必ず添える3点**:
   - **URLは認証なし**で、知っている者全員がprivateリポジトリのソース抜粋を閲覧できる
   - **配信ルートは `$RUNDIR` 全体**なので、`digest.html` 以外の中間生成物（`patch.diff`・`context.md`・
     `findings.json`）も同じURL配下で読める。PRの差分が丸ごと出ることを承知のうえで渡す
   - **寿命はプロセス生存中のみ**。セッション終了・`TaskStop`・マシン再起動で失効する
7. **`$RUNDIR` を消さない・移動しない**（Step 7の成果物が配信実体。hookが自動commitする記録の正本でもある）

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
      - canonical: <$CANONのHEAD SHA>（起動時origin/masterのSHAピン）<SKILL.md同一性ガードで差分が出たまま続行した場合のみ ・skew: $ORIGIN=<SHA> を追記>
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
  `git -C <$PRWTの実値> rev-parse HEAD` / `rev-parse "<BASE_REF>^{commit}"` の実出力、
  `canonical` は `git -C <$CANONの実値> rev-parse HEAD` の実出力（SHAピンで不変なので
  `clean`/`dirty` の別は生じない。`.last-used` は未追跡ファイルでdirtyに数えない）。SKILL.md同一性ガードで差分が出たまま続行した場合のみ、
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
- **記録類のcommitはセッションが行わない。書き込むだけでよい**（`$LOGS` 配下のrecords・shadow-ledger・
  improvement-queue・`$RUNDIR` 配下の中間生成物とダイジェスト のすべて）。Stop/SessionEnd に登録された `.dev-hooks/logs-sync.mjs` が
  logs repoで `git add -A` → `auto: logs-sync` → `pull --rebase` → `push` まで自動で行うため、
  セッション側で `git commit` すると同一内容を二重に扱うことになる。**書いたら放置が正**
  （旧版の「正典treeへ書き込むが勝手にcommitしない」は記録先を `$LOGS` へ分離する前の記述。
  現在は正典tree＝コードrepoへは記録を一切書かない）
- **本Step完了の最後に `$RUNDIR/session-done.marker` を書く**（中身は空でよい）。
  pollerはcmuxワークスペースをフォアグラウンド起動しており「ターン終了＝プロセス終了」の合図を持たないため、
  `findings.json` の生成だけでは Step 7.6/8 の完了を意味しない。このマーカーが「終了してよい」の唯一の合図であり、
  pollerは `findings.json ∧ session-done.marker` の両方が揃うまでワークスペースを閉じない
  (This marker is the sole "safe to close" signal for the poller's foreground cmux launch — write it only
  after Step 7.6/8 are truly done, since findings.json alone no longer implies the session has finished)

## Step 9: 修正モード（「修正して」と言われたときだけ）

レビューは report-only なので、**指示があるまでPRのコードには触らない**。「修正して」と言われたら本節に入る。
作業場所は `$PRWT`（Step 2で作ったPR専用worktree）。ここがレビューからpushまで一貫した唯一の作業場所である。

1. **裁定済みであることを確認する** — 直し方に選択肢がある指摘は、ダイジェストの設計判断カードでユーザーが
   選んだ案が確定していること。未裁定のまま実装しない（AGENTS.mdのgrill-first HARD GATEの趣旨。
   ダイジェストの設計判断がその裁定の場を兼ねているので、裁定済みなら改めてgrillは起動しない）
2. **PR headが動いていないか確認する** — レビュー時の `head` SHA（recordsの `- head:`）と
   `git -C <$PRWTの実値> rev-parse HEAD` を比べる。動いていたら**差分を読み、指摘がまだ成立するか確認してから**直す。
   成立しなくなった指摘は直さず、その旨を報告する
3. **修正を適用する**（subagentへ委譲してよい。その場合も作業ディレクトリを `$PRWT` に限定し、
   `$ORIGIN` / `$CANON` / 他のworktreeを編集しないことをpromptに明記する）
4. **コンパイル**（`.cs` を触ったら必須・AGENTS.md）:
   `uloop compile --project-path <$PRWTの実値>/moorestech_client`
   **`$PRWT` には `Library/` が無いため初回は膨大な再インポートが走る**。AGENTS.mdの指示どおり
   `$ORIGIN` の `moorestech_client/Library` をコピーして時間を短縮する。コピー元は数十GB規模になりうるので、
   **所要時間とディスク消費を先にユーザーへ伝えてから実行する**
5. **テスト**: `uloop run-tests --project-path <$PRWTの実値>/moorestech_client --filter-type regex --filter-value "<対象>"`
   で、修正した箇所と回帰テストに絞って実行する
6. **コミット**。レビュー由来の修正であることが後から分かるメッセージにする
7. **push**: `git -C <$PRWTの実値> push`。PRブランチをブランチとしてcheckoutしてあるので追加設定は要らない。
   **pushは外向きの操作なので、明示の指示がない限り行わない**（「修正して」だけならcommitで止めて可否を確認する）
- コンパイル・テストを実行できなかった場合は、**やっていないことを報告に明記する**。
  「直した」とだけ言って検証状況を伏せるのは禁止

## Step 10: 対応完了ラベル（レビューと対応が両方終わった時のみ）

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

**下記のどの中止でも、終わる前に `$RUNDIR/abort.json` を書く**（冒頭「中止の申告」節。書かずに終わると自壊と誤認されresumeされる）。

- このセッションが対象PRに関与済み: レビューせず中止・理由報告（Step 0参照）
- `$PRWT` が既にあり `status --porcelain` が非空 / `merge --ff-only` が失敗: 即エラー終了・理由報告（Step 2参照）。
  前回の修正作業が残っている可能性があるので `reset --hard` で潰さない
- `gh pr checkout` がブランチロックで失敗: **奪わずに**保持しているworktreeを報告して指示を仰ぐ（Step 2参照）
- `$CANON`（skills-canon-<sha8>）の用意に失敗（`fetch`/`rev-parse` によるピンSHA解決の失敗・
  `worktree add` 失敗・`novelty_gate.py` 不在）: 即エラー終了・理由報告。起動元treeの `.claude/` で代替するのは**禁止**
  （物差しが実行中に動く状態へ戻るだけで、それは今の固定方式が解こうとしている問題そのもの）。
  ただし**古ピンの掃除の失敗だけは例外** — エラー終了せず報告のみで続行してよい（掃除は衛生であって測定の前提ではない）
- SKILL.md同一性ガードで差分が出た: **先へ進まずユーザーへ報告して指示を仰ぐ**（冒頭「$CANONの用意」手順6）。
  続行を指示された場合のみ進み、recordsの `canonical:` に `skew` と両SHAを明記する
- gh未認証・PR不存在・checkout失敗（MERGED分岐のフォールバックも尽きた場合）: 即エラー終了・理由報告
- OPENの通常経路で `headRefOid` とcheckout結果が不一致: 即エラー終了・理由報告し、Step 1のメタデータ再取得から
  やり直す（Step 2参照）。第3フォールバック経路の不一致は継続可
- Step 2のbase最新化fetch（`+refs/heads/<baseRefName>:refs/remotes/origin/<baseRefName>`）の失敗
  （MERGED後にbaseブランチが削除されている等でremote refが無い場合）:
  これ単独ではエラー終了しない。Step 2末尾の **BASE_REF解決確認**まで進み、そこで失敗したら
  同節のフォールバック `git -C <$PRWTの実値> fetch origin <mergeCommit>`
  （`.mergeCommit.oid` のSHA）で `BASE_REF`＝`<mergeCommit>^1` を取り寄せて継続する。
  そのフォールバックでも解決できなければ即エラー終了
- `state=CLOSED`（未マージclose）・`BASE_REF` が解決できない: 即エラー終了・理由報告（Step 1.5 / Step 2参照）
- patchが空（`grep -c '^diff'` が0）: 即エラー終了・理由報告（Step 3参照）。空patchのまま後続Stepへ進まない
- 新規性ゲートの非ゼロexit: 即エラー終了・理由報告（Step 5参照）
- 新規性ゲート出力の受け取り検査失敗（JSONパース不能・3キーのいずれか欠落）: 即エラー終了・理由報告（Step 5参照）
- 新規性ゲートが3系統全空（patchは非空）: `BASE_REF` の妥当性を確認してから継続（Step 5参照）
- codex不在などmoores-code-review内の縮退: 本体規約に従いダイジェストの参考節に明記
- Step 7.6の外部公開に失敗（`cloudflared` 不在・トンネル未確立・到達確認がHTTP 200以外またはtitle不一致）:
  **レビュー自体はエラー終了させない**（ダイジェストとrecordsは既に手元にあり、成果物としては完成しているため）。
  ローカルの `open` 済みパスを案内し、公開できなかった事実と理由を報告に1行明記する。
  URLに触れないまま黙って完了報告するのは禁止
