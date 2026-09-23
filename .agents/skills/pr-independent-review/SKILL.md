---
name: pr-independent-review
description: |
  実装セッションと完全に独立したセッションでPRをレビューする手動発火スキル。PR URLまたは番号を受け取り、
  レビュー専用worktreeにcheckoutして moores-code-review（report-only）＋新規性ゲートL1を実行し、
  実コード抜粋入りのインフォグラフィックHTMLダイジェスト（verdict/裁定カード/suppressed）と
  シャドー台帳を出力する。ダイジェストの閲覧は裁定サイト（https://review.moores.tech/pr/<番号>）が担う。
  実装セッションの自己申告contextは一切受け取らない。
  レビューと指摘への対応が完了したPRには「独立レビュー&対応完了」ラベルを付与する。
  Use When:
  1. 「/pr-independent-review <PR URL|番号>」で起動された時
  2. 「このPRを独立レビューして」「シャドーレビューして」と言われた時
  3. 「/pr-independent-review reconcile <番号>」で起動された時（人間レビューとの突き合わせ・見逃し検知・改善発火）
hooks:
  # 無人実行の関所。スキル発動中だけ有効（repo横断のsettings.jsonに置くと開発者の通常セッションまで巻き込む）
  # Gate for unattended runs; active only while this skill runs, unlike a repo-wide settings.json hook
  PreToolUse:
    - matcher: "AskUserQuestion"
      hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py ask"
    # `gh pr checkout` を PR専用worktree の外で叩かせない（無人・対話を問わず）
    # Refuse `gh pr checkout` outside the PR-dedicated worktree, attended or not
    - matcher: "Bash"
      hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/checkout_guard.py"
  Stop:
    - hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py stop review"
---

# pr-independent-review — 独立セッションPRレビュー（シャドー運用v1）

規則の由来（どの事故・裁定から生まれたか）は `references/changelog.md`。記録の固定書式は `references/record-format.md`。
対応spec: `docs/superpowers/specs/2026-07-27-pr-independent-review-design.md`

## 記法（本文全体の共通規約）

- `$ORIGIN` / `$CANON` / `$PRWT` / `$RUNDIR` / `$LOGS` / `BASE_REF` / `<mergeCommit>` は**本ドキュメント上のプレースホルダであり、シェル変数ではない**。
  Bashコマンド・subagentのprompt・ファイルパスに渡すときは**実値の絶対パスへ展開して書く**。リテラルのまま渡すと
  空文字に展開され、`/.claude/skills/...` という不存在パスを叩いて沈黙故障する。本文の `<$PRWTの実値>` 等はこの展開を指す
- `~` も展開する。subagentのpromptはシェルを通らないため、`~` のままではリテラルのディレクトリを探す
- `<mergeCommit>` は `gh pr view --json mergeCommit` が返すオブジェクト `{"oid":"<40桁SHA>"}` の **`.oid` の値**を指す。
  オブジェクトのまま渡すと `unknown revision` で落ちる。`--jq '.mergeCommit.oid'` で取り出す
- コマンドは `git -C <絶対パス>` 形式か、`cd` を同一コマンド内に含めた形で書く。bash呼び出し間でcwdはリセットされる
- 「**即エラー終了**」と書かれた箇所はすべて、下記「中止の申告」どおり `abort.json` を書いてから終わる。黙って縮退しない

## 無人起動の規律: 「findings.json か abort.json で終える」

環境変数 `PR_REVIEW_UNATTENDED=1` が立っているとき、このスキルは poller から cmux ワークスペース上の
**対話モード** claude でフォアグラウンド起動されている（ADR 0023）。poller はあなたが動いているかを
transcript の更新で見ており、session と subagents の transcript が 1200 秒更新されないと「自壊相当」と判定して
同じペインへ RESUME 指示を1回送り、それでも進まなければ失敗ラベルにする。したがって無人起動時は:

- **待機は同一ターン内でブロッキングして行う**（subagent の完了待ちは Monitor 等で待ち切る）。
  「後で結果を確認します」とターンを閉じると transcript が止まり自壊と判定される
- **質問して停止することを禁止する**。判断が要る指摘はダイジェストの裁定カード（設計判断）へ落とす
- **終了地点は2つだけ** — Step 8 末尾で `session-done.marker` を書いた直後か、`abort.json` を書いた直後
- **session limit に当たったら何もしなくてよい**。poller が reset 時刻まで待ち、同じペインへ
  「`$RUNDIR/agents/*.md` を点検し、オーケストレータのエージェントIDへ SendMessage で未完了分だけ続行」という
  継続指示を送る。その指示が来たら、完了済みの体は再派遣せず、保持しているIDへ SendMessage で続きを頼む
- 人がペインに割り込んで指示した場合は「止める」「続きを指示する」に限って従う

`PR_REVIEW_UNATTENDED` が無い（人が対話で起動した）場合は質問して止まってよい。`findings.json` / `abort.json` の
どちらかで終える規律は同じ。

この規律は frontmatter hooks（`scripts/unattended-gate.py`）が機械的に守らせる。起動プロンプトに `【無人起動】` がある
場合に限り、`$RUNDIR/session-done.marker` も `abort.json` も無いままターンを終えようとすると Stop がブロックされ、
AskUserQuestion は deny される。ブロックは同一セッション2回でフェイルオープンする。

### 中止の申告（abort.json）

どの理由で中止するときも、**終わる前に `$RUNDIR/abort.json` を書く**。無いままの終了は poller から自壊と見なされ
同一セッションが1回 resume される（人間を呼ぶべき fail-closed が押し切られる）。

```json
{"reason": "<中止理由の一行>", "step": "<中止したStep名>", "at": "<ISO8601>"}
```

- `reason` は失敗コメントへそのまま転記されるので、人間が次の一手を決められる粒度で書く（1行・バッククォート不可）
- 書き先は「このrunの `$RUNDIR`」ちょうど。まだ決めていない段階なら `runs/pr-<番号>`（再レビューは `-r<N>`）のうち
  このrunに割り当てた1つを `mkdir -p` して書く。poller は `runs/pr-<番号>*` を全部走査して最も新しい申告を拾う

## tree と置き場

このスキルが触るtreeは3つ。**書いてよいのは `$PRWT` にPRのコード修正を入れるとき（Step 9）だけ**で、他は読み取り専用。

| tree | 中身 | 規律 |
| --- | --- | --- |
| `$CANON`（`skills-canon-<sha8>`） | 起動時の `origin/master` SHAへピンした測定器（スクリプト・reviewer・統合ルール・テンプレート）の唯一の読み取り元 | 不変が契約。`fetch`・`reset`・`clean` も書き込みも一切しない（`.last-used` の `touch` だけ例外）。並列レビューが同じピンを読むため |
| `$ORIGIN`（起動元・多くはメインworktree） | 他セッションの作業中ブランチ | worktreeを生やす起点としてのみ使う。読み取り元にも書き込み先にもしない |
| `$PRWT`（`pr-<番号>`） | PRのheadブランチ | PRのコード修正だけ書いてよい。skill改修・`.decisions/` の裁定記録は積まない |

- **`$PRWT` 側の `.claude/` は絶対に使わない**（PRごとに測定器が変わり見逃し率実測が壊れる・レビュー対象がレビュアーの指示を書ける自己弱体化経路）
- **`$CANON` を「このSKILL.mdが置かれているtree」にしない** — それは他セッションが実装作業中のメインworktreeで、実行中に物差しが動く
- **記録repo `$LOGS`**: 実行記録（`records/pr-*.md`・シャドー台帳・改善キュー・前向きログ・`$RUNDIR`）は `../moorestech_logs`
  （privateログrepo）の `harness/` 配下に置き、コードrepoへは書き戻さない。`$LOGS` への書き込みは Stop/SessionEnd hook
  （`.dev-hooks/logs-sync.mjs`）が自動で commit・push するので**セッション側で commit しない。書いたら放置が正**
- **実行ディレクトリ `$RUNDIR`**: 1回のレビューの中間生成物（patch・context・novelty・detchecks・codex監査・ダイジェスト）は
  **すべて** `$LOGS/harness/pr-independent-review/runs/pr-<番号>/` に置く。再レビューは `runs/pr-<番号>-r2/`（以降 `-r3`…）を
  新規作成し、records の `pr-<番号>-r2.md` と1対1で対応させる。ファイル名は固定: `patch.diff` / `context.md` / `novelty.json` /
  `detchecks.json` / `codex-audit.md` / `digest.md` / `digest.html` / `findings.json` / `reconcile-comments.json`
- **`/tmp` には一切置かない・`$RUNDIR` を消さない・移動しない** — 中間生成物は reconcile のフォレンジック・リプレイの入力で、
  裁定サイトも `$RUNDIR` の `digest.html` を配信実体として直接読む（`PR_REVIEW_DATA_ROOT` が `runs/` を指し、最大 `-rN` を選ぶ）
- レビューだけで終わる1周では、コードrepoへの書き込みは1バイトも発生しない

### $CANON の用意（最初に必ず1回）

1. **`$ORIGIN` を特定する** — このSKILL.mdをReadしたときの絶対パスから `/<dir>/skills/pr-independent-review/SKILL.md`
   （`<dir>` は `.agents`/`.claude`/`.codex`。実体は `.agents/skills` で他2つはsymlink）を文字列として取り除いた残り。
   `~/moorestech` を決め打ちしない（worktreeから発火する運用が現にある）
2. **スクリプトで用意する**（origin/master の fetch・ピンSHA解決・`skills-canon-<sha8>` の作成または再利用・`.last-used` の記録・
   24時間使われていない古ピンの掃除・`novelty_gate.py` の実在確認・SKILL.md同一性ガードを1コマンドで行い、終了コードで裁く）:

       python3 <$ORIGINの実値>/.agents/skills/pr-independent-review/scripts/canon_setup.py \
         --origin <$ORIGINの実値> --parent <worktree親ディレクトリ>

   stdout の JSON の `canon` が `$CANON` の実値。`origin_master_sha` は records の `canonical:` に書く。`warnings`（古ピン掃除の失敗）は報告に載せて続行する

   | exit | 意味 | 対応 |
   | --- | --- | --- |
   | 0 | 用意完了 | 進む |
   | 10 / 11 / 12 | fetch・rev-parse 失敗 / worktree add 失敗 / `novelty_gate.py` 不在 | 即エラー終了。起動元treeの `.claude/` で代替しない |
   | 13 | SKILL.md 同一性ガードで差分（`$ORIGIN` に未マージのskill改修があり「新しい指示 × 古いreviewer」の版ズレ） | 人が起動したならユーザーへ報告して指示を仰ぎ、続行を選んだ場合のみ `--allow-skew` で再実行して records の `canonical:` に `skew` と両SHAを明記する。無人起動では即エラー終了 |
   | 14 | `$ORIGIN` が作業ツリーのルートでない / `$CANON` が `$ORIGIN` と同一 | 即エラー終了（`$ORIGIN` の特定をやり直す） |

### skill改修・裁定記録を書きたいとき

上の3treeのどれでもない専用worktreeを新たに切ってそこで完結させる:

    git -C <$ORIGINの実値> worktree add <worktree親ディレクトリ>/skill-<用件> -b chore/<用件> origin/master

- 作業後に `git -C <$ORIGINの実値> status --porcelain -- <触れたパス>` が**空**であることを確かめる（撤収確認）
- **その改修はmasterへマージされるまで測定器に入らない**（`$CANON` はmasterのSHAからピンされる）。
  「どのブランチに載せたか」「まだ有効でないこと」を報告に書く

改善と言われたときは Step 0.5 を実行する。修正と言われたときはPRそのもののコード修正（Step 9）を行い、pushまで完了したら
Step 10 の付与条件を確認する。

## Step 0: 独立性の自己申告ガード

このセッションが対象PRの実装・レビュー・計画に関与していた場合（PRブランチでコードを書いた・spec/planを書いた・同じPRを既に
レビューした・実装セッションからの引き継ぎcontextを受け取った）は即エラー終了し、「このセッションは対象PRに関与済みのため
独立性を満たさない。新規セッションで起動されたい」と報告する。関与済みセッションが走ると見逃し率の実測が楽観側へ歪む。
迷ったら中止側に倒す。

## Step 0.5: reconcile負債ゲート（新規レビューの前に必ず通る）

シャドー台帳（`$LOGS/harness/pr-independent-review/records/shadow-ledger.md`）の `reconcile` 列が空欄の行それぞれについて:

    gh api repos/moorestech/moorestech/pulls/<番号>/comments --paginate --jq 'length'

- **1件以上 → そのPRのreconcile（「reconcileモード」＝`references/reconcile-mode.md`）を新規レビューより先に実行する（ブロック型）**。
  未reconcileの見逃しを放置したまま同じ測定器で次のPRを測っても同じ見逃しを再生産するだけ
- 0件 → 人間がまだレビューしていないだけなので保留のまま進む
- スタブ行（verdict=未測定（スタブ））は測定外なので `reconcile` 列に `対象外（スタブ）` と記入して負債から外す
- **健全性1行を必ず冒頭に表示する**（新規レビュー・reconcileどちらでも）:
  `未reconcile: N PR / 改善キューopen: M件 / 直近見逃し率: X%（missed A / human-confirmed B）`
  （キューは `records/improvement-queue.md` の `open` 行数。見逃し率は最新の `## 突き合わせ内訳` から。未計測なら「未計測」）

## Step 1: PR取得

`gh pr view <番号> --repo moorestech/moorestech --json number,title,body,baseRefName,headRefName,headRefOid,additions,deletions,files,state,mergeCommit`
で取得。失敗（未認証・不存在）は即エラー終了。`state` と `mergeCommit`（Step 1.5）・`headRefOid`（Step 2末尾の整合確認）は
この1回で一緒に取り、後から取り直さない。取得できたら `mkdir -p <$RUNDIRの実値>`（既存 `pr-<番号>/` があれば再レビューなので `-r2`）。

## Step 1.5: BASE_REF の確定

以降のStepで使うbase参照は**ここで決めた `BASE_REF` ただ1個**。各Stepで `origin/<baseRefName>` をベタ書きしない。

- `state=OPEN` → `BASE_REF = origin/<baseRefName>`
- `state=MERGED` → `BASE_REF = <mergeCommit>^1`（マージコミットの第1親）。**`origin/<baseRefName>` を使ってはいけない** —
  マージ済みHEADはその祖先なのでmerge-baseがHEAD自身になり、patch空・novelty全空・exit 0 の沈黙故障でverdictが「自動マージ可」に化ける
- `state=CLOSED`（未マージclose）は対象外。即エラー終了
- 解決可能性の確認はStep 2の末尾で行う（fetch後でないと参照できない）

## Step 2: PR専用worktree `$PRWT` へcheckout

**worktreeはPRごとに1つ作り、レビューからpushまでそこで完結させる**（ユーザー裁定 2026-08-05）。共用の使い回しworktreeにしない。

- **場所**: `skills-canon-<sha8>` と同じ親ディレクトリの `pr-<番号>`
- **無ければ作る**:

      git -C <$ORIGINの実値> fetch origin "+refs/heads/<headRefName>:refs/remotes/origin/<headRefName>"
      git -C <$ORIGINの実値> worktree add <$PRWTの実値> origin/<headRefName>

  `gh pr checkout` は必ず `$PRWT` へ `cd` した同一コマンド内で叩く（frontmatter の `checkout_guard.py` が、`cd <…>/pr-<番号> &&`
  を伴わない `gh pr checkout` を deny する。cwdのworktreeのブランチを切り替え、メインworktreeを他セッションの作業ブランチから
  引き剥がすため）。Step 3 の `make_patch.py` も `$PRWT`＝`$ORIGIN`（exit 20）と `pr-<番号>` 以外の名前（exit 21）を拒否する
- **既にあれば作り直さない**。`git -C <$PRWTの実値> status --porcelain` が非空なら即エラー終了（前回の修正作業が残っている可能性。
  `reset --hard` で潰さない）。次に `fetch origin "+refs/heads/<headRefName>:refs/remotes/origin/<headRefName>"` のうえ
  `merge --ff-only origin/<headRefName>`。fast-forwardできなければ前回の修正が未pushなので即エラー終了
- **後片付けはユーザーに委ねる**。独立セッションが勝手に `worktree remove` しない（未pushの修正が入っていることがある）
- **base最新化（refspecを明示する）**:

      git -C <$PRWTの実値> fetch origin "+refs/heads/<baseRefName>:refs/remotes/origin/<baseRefName>"

  引数なしの `fetch origin <baseRefName>` はtracking refを更新せずFETCH_HEADだけ書く設定があり得て、base取り違えになる。
  MERGEDでも実行する。**このfetchの失敗ではエラー終了しない**（マージ後にbaseブランチが削除されていると落ちるが、
  下の解決確認のフォールバックで回収できる）
- **checkout**（`state` で分岐）:
  - **OPEN**: `cd <$PRWTの実値> && gh pr checkout <番号>`（`--detach` を付けない。後で修正をpushするためブランチとしてcheckoutする。
    `gh pr checkout` は `-C` にできないので `cd` は同一コマンド内。`cd` 先が `$PRWT` であることを目視してから実行する）。
    ブランチロック（`fatal: '<branch>' is already checked out at ...`）なら**奪わずに**保持しているworktreeを報告して指示を仰ぐ
  - **MERGED**、またはOPENでもheadブランチ削除済みで上が `fatal: couldn't find remote ref` / exit 128 になる場合:

        git -C <$PRWTの実値> fetch origin pull/<番号>/head && \
          git -C <$PRWTの実値> checkout --detach FETCH_HEAD

    それも失敗すれば `git -C <$PRWTの実値> checkout --detach <mergeCommit>`。差分は `BASE_REF`＝`<mergeCommit>^1` との比較なので
    PRの変更集合としては同じものが取れる。全部尽きたら即エラー終了
- **BASE_REF の解決確認（必須）**: `git -C <$PRWTの実値> rev-parse --verify "<BASE_REF>^{commit}"`。MERGEDで失敗した場合のみ
  `git -C <$PRWTの実値> fetch origin <mergeCommit>` を挟んで再確認。それでも解決できなければ即エラー終了
- **checkout整合の確認（必須）**: `git -C <$PRWTの実値> rev-parse HEAD` がStep 1の `headRefOid` と一致すること。不一致の扱い:
  - OPENの通常経路（`gh pr checkout` / `pull/<番号>/head`）で不一致 → 即エラー終了。レビュー中にPRへ新しいpushが入ったので、
    Step 1のメタデータ再取得からやり直す
  - `<mergeCommit>` 自体をcheckoutした経路で不一致 → 設計どおり。records の `- checkout:` に「headRefOid不一致・mergeCommit検査」と明記して進む

## Step 3: patch生成（exclude方式）

    python3 <$CANONの実値>/.agents/skills/pr-independent-review/scripts/make_patch.py \
      --prwt <$PRWTの実値> --origin <$ORIGINの実値> --pr <番号> --base-ref <BASE_REF> --out <$RUNDIRの実値>/patch.diff

スクリプトが固定フラグ（`-c core.quotepath=false --no-color --no-ext-diff --no-textconv --text --no-renames`。ユーザー側git設定が
patchを静かに痩せさせないため）と固定の除外（Unityアセット・画像・プレイテストシナリオの `.cs`。yml/jsonは残す。
moores-code-review Step 1と同一のpathspec）で `<BASE_REF>...HEAD` の差分を書き、次を終了コードで裁く:

| exit | 意味 | 対応 |
| --- | --- | --- |
| 0 | 生成完了。stdout JSON の `diff_count` / `head` / `base_sha` / `adr_files_in_diff` を後段で使う | 進む |
| 20 / 21 | `$PRWT`＝`$ORIGIN` / ディレクトリ名が `pr-<番号>` でない | 即エラー終了。Step 2 のworktreeをやり直す |
| 22 | `BASE_REF` が解決できない | 即エラー終了（Step 1.5 / Step 2 の解決確認へ戻る） |
| 23 | merge-base が HEAD 自身（base取り違え。MERGED PR で `origin/<base>` を使った典型） | `BASE_REF` を直して再実行 |
| 24 | patch が空（`git diff` は空でも exit 0 なのでここが唯一の検知点） | 即エラー終了。第一の疑いは `BASE_REF` |

## Step 4: 4カテゴリcontextの独立再構成

`<$RUNDIRの実値>/context.md` に書く。**情報源はPR本文とリポジトリ内のspec/planの判断台帳（ADR）のみ**。実装セッションの申告・
PRコメントの合意主張は使わない。

- **4カテゴリは必ず `##` 見出し**で、本体Step 1と同一の `## 目指す（ゴール）` / `## 目指さない（非目標）` / `## 許容するトレードオフ` /
  `## 尊重すべき制約` を使う。`checks_context.py` は見出し欠落をfail-closedでconfirmed（`context_source_label`）にする。
  この検出はPRの欠陥ではないのでverdictに数えず、contextを直して再実行する（「verdict判定規則」）
- 出所ラベル: ユーザー裁定=`[ADR: <spec名>#<台帳項目>]`（実在するADR項目のみ）/ それ以外=`[agent前提]`。PR本文が主張する方針・
  トレードオフは全部 `[agent前提]`（免責力なし）
- `[ユーザー裁定: "発言引用" …]` の引用欄に書けるのはユーザー発言または AskUserQuestion の質問文＋採択ラベルの逐語だけ。
  `.decisions/` のファイル名・ADRの決定文・言い換えは引用ではない。逐語が無い場合は `[ADR: <spec名>#<台帳項目>（原文引用なし）]` と注記する
- **PR diff自身が追加・変更したspec/plan由来のADR項目は `[agent前提]` へ降格**し、当該行末に `（PR内新設ADR）` と注記する。
  対象ファイルはStep 3の `make_patch.py` が出す `adr_files_in_diff`（`docs/superpowers/` 配下）。
  降格はverdictに影響しない（免責されなくなった指摘は通常のCritical/Warningとして判定規則に乗る）

## Step 5: 新規性ゲートL1

    python3 <$CANONの実値>/.claude/skills/pr-independent-review/scripts/novelty_gate.py \
      <$PRWTの実値> <BASE_REF> > <$RUNDIRの実値>/novelty.json

- 以降のStep（新形の数え上げ・裁定カード化・Step 8の記録）は `novelty.json` を読み直して行う。stdoutや記憶から数えない
- **非ゼロexitは即エラー終了**。空JSON・新形0件扱いで先へ進まない
- **保存直後の受け取り検査（必須）**: JSONとしてパースでき `new_edges` / `asmdef_refs` / `grammar` の3キーが揃うこと。失敗は即エラー終了:

      python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); assert {"new_edges","asmdef_refs","grammar"} <= d.keys(), d.keys(); print({k: len(d[k]) for k in ("new_edges","asmdef_refs","grammar")})' <$RUNDIRの実値>/novelty.json

- **新形フラグ**の採用基準（3系統で違う）: `new_edges` は **`generic_origin=true` かつ `dir_is_new=false` のものだけ** /
  `asmdef_refs` **全件** / `grammar` **全件**
- **patchが非空なのに3系統全空なら baseずれを疑う（必須確認）**: 第2引数がStep 3で `make_patch.py` に渡した `BASE_REF` と同じ実値か
  確認する（merge-base が HEAD 自身になる取り違えは `make_patch.py` が exit 23 で弾いているので、残る疑いは引数の写し間違いだけ）。
  確認して初めて「本当に新形0件」と判断してよい
- `generic_origin=false` の new_edges と `dir_is_new=true` の new_edges は参考情報（裁定カードにせず折りたたみ参考節へ）。
  `dir_is_new` の件数はStep 8に「うちdir_is_new N件」として残す
- `.claude/` `.agents/` `.codex/` 配下の `.cs` はプロダクトコードでないため除外して解釈する
- `.moorestech-external-revisions.json` の差分は指摘対象にしない（ユーザー裁定 2026-08-16）。findings.jsonにも裁定カードにも起こさない
- `line` が `null` の所見（`schema_change` / `new_protocol_file` / `new_datastore_file`）はファイル単位。表記はファイルパスのみで `:1` や `:null` を書き足さない

## Step 6: moores-code-review本体をreport-onlyで発火

`<$CANONの実値>/.claude/skills/moores-code-review/SKILL.md` の Step 2〜6.5 に従う。**本体のコマンド例は `.claude/skills/...` の
相対パスなので、コピペするとcwd（`$PRWT`）側のPR同梱スキルを実行してしまう。スクリプトは必ず `$CANON` の絶対パスで叩く**:

```bash
python3 <$CANONの実値>/.claude/skills/moores-code-review/scripts/check_all.py <$RUNDIRの実値>/patch.diff --repo-root <$PRWTの実値> --context <$RUNDIRの実値>/context.md > <$RUNDIRの実値>/checks.json
python3 - <<'EOF'
import json; j=json.load(open("<$RUNDIRの実値>/checks.json")); json.dump(j["deterministic"], open("<$RUNDIRの実値>/detchecks.json","w"), ensure_ascii=False, indent=1)
EOF
python3 <$CANONの実値>/.claude/skills/moores-code-review/scripts/split_chunks.py <$RUNDIRの実値>/patch.diff > <$RUNDIRの実値>/chunks.tsv
python3 <$CANONの実値>/.claude/skills/moores-code-review/scripts/build_workflow_args.py --run-dir <$RUNDIRの実値> --patch <$RUNDIRの実値>/patch.diff --context <$RUNDIRの実値>/context.md --repo-root <$PRWTの実値> --base-ref <BASE_REFを解決したSHA> --report-only --detchecks <$RUNDIRの実値>/detchecks.json
```

- **`--repo-root` は `$PRWT`、スクリプトは `$CANON`** — ADR参照の解決と200行判定はPR側の木の実体を見る必要がある。非対称は意図的
- `--context` は必須（Step 4の出所ラベル・`##` 見出し検査はこれが無いと走らない）
- `--report-only --detchecks` で Apply フェーズが省かれ、post-check（comment-rationale-guard / comment-convention-guard）は
  patch＋detchecks.json で発火し、contract.md に report-only の前提が付く。修正適用が無いので最終diff＝Step 3のpatchであり、
  決定論チェックの再実行はしない。convention-guardの「機械的は自動適用」も指摘として出す
- Workflow には `workflow-args.json` の中身をそのまま `args` に渡す。Workflow が `Repo root`（`$PRWT`）と `Skill root`（`$CANON`）を
  全subagentのpromptへ注入する。**Workflow不可でsonnet委譲へフォールバックする場合のみ**、全prompt（reviewer・Fable全般・
  verifier・post-checks）の `Read this :` / `Candidates :` / `Patch path :` / `User prompt :` を `$CANON` / `$RUNDIR` の絶対パスで書き、
  次の2行を足す: 「対象コードのルート: <$PRWTの実値>。コードのReadは必ずこの配下」「スキル・reviewer・post-checks・統合ルールのReadは <$CANONの実値> 配下」
- AskUserQuestionは使わない。設計判断もダイジェストの裁定カードへ。本体Step 7の記録（`$LOGS/harness/moores-code-review/records/`・`eval-log.md`）は書かない
- 統合結果は `integrated.md` を読む。指摘は全部ダイジェストへ

### Codex外部監査の起動手当て

codexはプロンプトのテキストしか受け取らず差分を**自分のcwdで**解決する。cwdを `$CANON` にすると無関係なコードを監査し、
`$PRWT` へ `cd` するとPR側の `AGENTS.md` / `CLAUDE.md` / `.codex/` を上位指示として読む（自己弱体化経路）。

- **中立ディレクトリ（`/tmp` 等・リポジトリ外）から起動し、対象は全部プロンプト内の絶対パスで渡す**。`/tmp` はcwdとして使うだけで
  ファイル置き場ではない（`$RUNDIR` は `$LOGS` 配下＝git repo内なのでcwdにしない）:

      cd /tmp && codex exec --sandbox read-only --skip-git-repo-check -o <$RUNDIRの実値>/codex-audit.final.md - < <$RUNDIRの実値>/codex-audit.md > <$RUNDIRの実値>/codex-audit.out.md 2>&1

- 起動前に `python3 <$CANONの実値>/.claude/skills/moores-code-review/scripts/codex_preflight.py` で実体パスと auth.json を解決する
  （`which codex` は使わない）。exit 10（バイナリ不在）/ 11（認証ファイル不在）ならスキップし、`status` 文字列つきで折りたたみ参考節に縮退として明記
- **`-o` は必須で結論の正本は `.final.md`**。空・不在なら欠員と断定する前に
  `python3 <$CANONの実値>/.claude/skills/moores-code-review/scripts/codex_recover.py --prompt <$RUNDIRの実値>/codex-audit.md --out <$RUNDIRの実値>/codex-audit.out.md`
  を走らせる（exit 0=回収成功 / 3=未完走 / 4=起動失敗＝真の欠員 / 5=認証失効＝環境起因の欠員。「codex不在」とは書かない）
- **audit-templateの差分指定欄を書き換える** — `<$CANONの実値>/.claude/skills/moores-code-review/scripts/codex-audit-template.md` の
  2行目「レビュー対象は、このセッションで私が作業した成果物だけです。」を「レビュー対象は PR #<番号> の差分だけです。」に、
  続くコミット済み／staged／unstaged の3行を `- 差分: git -C <$PRWTの実値> diff <BASE_REF>...HEAD` の1行に置き換える。
  1行目の役割宣言はそのまま。staged/unstaged 行を残すと「変更なし＝問題なし」の誤結論を誘発し、`-C` を省くと差分が1行も取れない
- `## 目指す / 目指さない / 許容するトレードオフ / 尊重すべき制約` 欄にはStep 4のcontextをそのまま貼る

## Step 7: ダイジェスト生成（digest.md → コンバータ）

sonnet subagentに `<$RUNDIRの実値>/digest.md` を**Markdownで**生成させる。フォーマットの正本は
`<$CANONの実値>/.agents/skills/pr-independent-review/README-digest-format.md`（生成subagentの参照先はこの1本のみ）。

- 生成後 `python3 <$CANONの実値>/.agents/skills/pr-independent-review/scripts/digest_build.py <$RUNDIRの実値>`。
  非0終了なら **digest.mdを直して再実行する**（HTMLを手で直すのは禁止。エラーメッセージが欠けたキー・見出しを指す）。
  コンバータは `$RUNDIR/patch.diff` を読む
- `digest.html` は置いておくだけでよい。閲覧経路は裁定サイト（`https://review.moores.tech/pr/<番号>`）だけ。
  **`open` でローカル表示しない・`http.server`＋トンネル等で別途公開しない**
- 生成subagentへ引き継ぐ規約:
  - `must_read: true` の条件: (a)指摘系統の一致数が多い (b)裁定がCriticalの直し方を左右する (c)ゲームプレイ・アーキテクチャの方向を変える
  - 一言サマリ: 欠陥・裁定対象そのものを主語にした短文1つ（目安20字前後）。免責の仕組み・出所ラベル・系統数・規約条番号は書かない
  - コード抜粋は全カード必須（`code-card` フェンス）。patchから機械的に転記する（創作・要約禁止）。置換なら削除行 `-<旧行番号>|<コード>` も
    必ず転記する（コンバータが `patch.diff` と照合する）。1カードには単一ファイルの抜粋だけ（言語は `files` 先頭の拡張子で判定）
  - `# 折りたたみ参考` に必ず入れる5項目: Criticalの修正方針詳細／Warning全件（1件1行・出所系統つき・間引き禁止）／Info一覧（圧縮列挙可）／
    参考扱いのnew_edges／各系統の生所見要約
  - 案はカード本文へ手で書かない。`options:` へ書けばコンバータが案A/案B…として描き先頭へ推奨マークを付ける。
    本文に「代替案」や `recommendation` を書くとエラーで落ちる
- HTML手組みの細則はすべてコンバータの責務で、生成subagentへ指示しない

## Step 7.5: findings.json

`findings.json` はコンバータが `$RUNDIR` 直下に生成する。**手で書かない・直さない**。スキーマと採番規則は `references/record-format.md`。

## Step 8: 記録

書式はすべて `references/record-format.md` に従う。

- `$LOGS/harness/pr-independent-review/records/pr-<番号>.md`（再レビューは `-r2`…）を固定書式で保存する
- シャドー台帳 `records/shadow-ledger.md` に1行追記する
- **完了報告には裁定サイトのURL `https://review.moores.tech/pr/<番号>` を1行で添える**（無人起動ではpollerが同じURLをPRコメントへ投稿する）
- **本Step完了の最後に `$RUNDIR/session-done.marker` を書く**（中身は空でよい）。pollerは「ターン終了＝プロセス終了」の合図を持たないため、
  `findings.json ∧ session-done.marker` の両方が揃うまでワークスペースを閉じない。Step 8が本当に終わってから書く
  (This marker is the sole "safe to close" signal for the poller's foreground cmux launch)

## Step 9: 修正モード（「修正して」と言われたときだけ）

レビューは report-only なので、指示があるまでPRのコードには触らない。作業場所は `$PRWT`。

1. **裁定済みであることを確認する** — 直し方に選択肢がある指摘は、ダイジェストの設計判断カードでユーザーが選んだ案が確定していること。
   未裁定のまま実装しない（ダイジェストの設計判断がgrillの場を兼ねるので改めてgrillは起動しない）
2. **PR headが動いていないか確認する** — records の `- head:` と `git -C <$PRWTの実値> rev-parse HEAD` を比べ、動いていたら差分を読み
   指摘がまだ成立するか確認してから直す。成立しなくなった指摘は直さず報告する
3. **修正を適用する**（subagentへ委譲してよい。作業ディレクトリを `$PRWT` に限定し `$ORIGIN` / `$CANON` / 他のworktreeを編集しないことをpromptに明記）
4. **コンパイル**（`.cs` を触ったら必須）: `uloop compile --project-path <$PRWTの実値>/moorestech_client`。
   `$PRWT` は baseline clone から生やした worktree で `Library/` が無く初回は膨大な再インポートが走るので、
   `$ORIGIN` の `moorestech_client/Library` を `cp -Rc`（APFS clonefile。数分・容量消費なし）でコピーしてから叩く
5. **テスト**: `uloop run-tests --project-path <$PRWTの実値>/moorestech_client --filter-type regex --filter-value "<対象>"` で修正箇所と回帰テストに絞る
6. **コミット**。レビュー由来の修正であることが後から分かるメッセージにする
7. **push**: `git -C <$PRWTの実値> push`。**pushは外向きの操作なので、明示の指示がない限り行わない**（「修正して」だけならcommitで止めて可否を確認）
- コンパイル・テストを実行できなかった場合は、やっていないことを報告に明記する

## Step 10: 対応完了ラベル（レビューと対応が両方終わった時のみ）

`独立レビュー&対応完了` は「人間はマージ判断だけすればよい」の合図で、対応未実施のPRに付くと未修正のCriticalがマージされる。
付与条件の判定と `gh pr edit`（`--add-label "独立レビュー&対応完了" --remove-label "独立レビュー待ち"`）はスクリプトが行う。
`gh pr edit` を直接叩かない:

    python3 <$CANONの実値>/.agents/skills/pr-independent-review/scripts/label_gate.py \
      --pr <番号> --rundir <$RUNDIRの実値> [--prwt <$PRWTの実値> --fixed-commit <SHA> ...]

| verdict | 通る条件 | 通らないとき |
| --- | --- | --- |
| 自動マージ可 | 無条件（Step 8の記録まで済んでいること） | — |
| Critical差し戻し | 全Criticalへの修正コミットを `--fixed-commit` で渡し、すべてが現在のPR headの祖先（push済み） | exit 30。未実施・ローカルのみなら付けない |
| 新形につき裁定行き | `$RUNDIR/adjudications.json` が `completed:true`（裁定が出ている）。裁定に伴う対応があればそのコミットも `--fixed-commit` で渡す | exit 31。裁定待ちの間は付けない |
| 未測定（スタブ） | 付けない | exit 32 |

exit 33（findings.json が読めない）/ 34（gh 不在・未認証）は環境の問題なので理由を報告して付けずに終える。

## reconcileモード（人間レビューとの突き合わせ・改善発火）

`/pr-independent-review reconcile <番号>` で単独起動、またはStep 0.5から強制実行される。**手順は `references/reconcile-mode.md` を Read して従う。**

## verdict判定規則

- **Critical差し戻し**: 統合後Criticalが1件以上（決定論チェックの `confirmed` を含む。**200行超過（file-too-long）は除外**＝努力目標・
  **`context_source_label` も除外**）
  - `context_source_label` はStep 4で自分が書いたcontextの書式欠落であってPR側の欠陥ではない。context.md を直して
    `deterministic_checks.py` を再実行し、消えたことを確認してから進む。verdictには一切数えない
- **新形につき裁定行き**: Criticalなし、かつ新形フラグ or `設計判断: あり` が1件以上
- **自動マージ可**: 上記いずれも無し
- **未測定（スタブ）**: Step 6を実行していない場合は上の3値を名乗ってはいけない。「Criticalなし」は測定結果ではなく未測定であり、
  台帳上は測定済みの1件として数えられてしまう。配管スモークテスト等で意図的に飛ばした場合は verdict を `未測定（スタブ）`、
  `- 縮退:` に `スタブ（Step 6未実行）` を書き、新形フラグの件数だけ記録する
- suppressedはverdictに影響しない（ダイジェストに全件列挙）
- `data-verdict` 属性の値: `reject` / `ruling` / `auto` / `stub`
