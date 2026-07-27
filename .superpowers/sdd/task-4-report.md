# Task 4 報告 — 実PRスモークテスト（レビューエンジン以外の全配管）

（注: このファイルには別プロジェクト「クライアントコレクタのアダプタ化」の旧内容が残っていたため、
現行ブリーフ `task-4-brief.md`（pr-independent-review スモークテスト）の内容で上書きした）

- 実行日: 2026-07-27
- worktree: `/Users/katsumi/moorestech/.claude/worktrees/pr-independent-review`（ブランチ `worktree-pr-independent-review`）
- `$CANON` 実値: `/Users/katsumi/moorestech/.claude/worktrees/pr-independent-review`
- レビューworktree: `/Users/katsumi/moorestech-worktrees/pr-review`（本タスクで新規作成）
- 対象PR: **#1041** `feat: tick中のブロック破壊を予約制にしtick末尾で一括確定`（+146 / -7）

## 対象PRの選定

ブリーフどおり `gh pr list --repo moorestech/moorestech --state merged --limit 40 --json number,title,additions,deletions` から
additionsの小さいものを列挙し、各候補の `files` を確認して選定した。

| PR | 規模 | 却下/採用理由 |
|---|---|---|
| #1080 / #1075 / #1072 / #1071 | 小〜中 | `Sync skills directory`。配管検証にならず却下 |
| #1079 | +5/-5 | `.ts` 1ファイル＋`.md` 2件。`.cs`/`.yml` を含まないため却下 |
| #1070 / #1059 | 小 | `.claude/skills/**/*.md` のみ（ドキュメント）。却下 |
| #1047 | +2/-2 | manifest系のみ。却下 |
| **#1041** | +146/-7 | **採用**。全11ファイルが `.cs`（うち `.meta` 3）。新規interface＋新規service＋DI登録＋テストを含み、新規性ゲートの `grammar` / `new_edges` 経路を実際に踏む |

## $CANON決定（SKILL.md冒頭）

SKILL.mdの実パスから `/.claude/skills/pr-independent-review/SKILL.md` を除去して
`/Users/katsumi/moorestech/.claude/worktrees/pr-independent-review` を得た。
指定どおり `ls <実値>/.claude/skills/pr-independent-review/scripts/novelty_gate.py` で実在確認 → OK。
**この手順は問題なく機能した。**

## Step 1: PR取得 — OK

```
gh pr view 1041 --repo moorestech/moorestech --json number,title,body,baseRefName,headRefName,additions,deletions,files
```
exit 0。`baseRefName=master` / `headRefName=feature/BlockRemovalReservation` / `+146 -7` を取得。

## Step 2: レビューworktreeへcheckout — 初回作成はOK / checkoutはNG

初回作成経路（検証対象）:
```
git -C /Users/katsumi/moorestech/.claude/worktrees/pr-independent-review worktree add ~/moorestech-worktrees/pr-review origin/master --detach
```
→ exit 0。`Preparing worktree (detached HEAD c438c4a3d)` / 15473ファイル展開。
**worktreeからの `worktree add` は正常に動く**（`$CANON` が別worktreeでも問題なし）。

リセット:
```
git -C ~/moorestech-worktrees/pr-review reset --hard && git -C ~/moorestech-worktrees/pr-review clean -fd
```
→ exit 0。

checkout（**失敗**）:
```
cd ~/moorestech-worktrees/pr-review && gh pr checkout 1041 --detach
fatal: couldn't find remote ref refs/heads/feature/BlockRemovalReservation
failed to run git: exit status 128
```

**回避策**（適用）:
```
git fetch origin pull/1041/head
git checkout --detach FETCH_HEAD   # -> a28a8aff1
```

base最新化 `git fetch origin master` → exit 0。

## Step 3: patch生成 — 回避策込みでOK

まず SKILL.md 記載どおり `git diff origin/master...HEAD` を試した結果 **完全に空**。
`git merge-base origin/master HEAD` = `a28a8aff1`（HEAD自身）。マージ済みPRではHEADが `origin/master` の
祖先になるため、三点diffのmerge-baseがHEADに一致し差分が消える。

**回避策**: マージコミット `8ce6f4ddae1a0d1c03059d3e3ac6d8acb994de80`（`gh pr view 1041 --json mergeCommit` で取得）の
第1親をbaseに使う。`git merge-base 8ce6f4dd^1 HEAD` = `2ed9ed858`（fork point）。

```
git -C ~/moorestech-worktrees/pr-review diff 8ce6f4dd^1...HEAD -- . ':(exclude)*.meta' ... > /tmp/pr-review-1041-patch.diff
```
→ exit 0 / `grep -c '^diff'` = **8** / 266行。`--stat` は +146 -7（PRのadditions/deletionsと完全一致）。
`.meta` 3件は除外済み。**期待値（1以上）を満たす。**

## Step 4: 4カテゴリcontext — OK

`docs/superpowers/specs/` を `BlockRemovalReservation` / `破壊予約` / `予約制` でgrep → **0件**。
本PR対応のADRは存在しないため、全項目を `[agent前提]` として `/tmp/pr-review-1041-context.md` に記述。
`## 目指す（ゴール）` / `## 目指さない（非目標）` / `## 許容するトレードオフ` / `## 尊重すべき制約` の
4本を `##` 見出しで作成（`checks_context.py` のfail-closed回避）。
※ Step 6を実行していないため `checks_context.py` の実判定は走っていない（この書式が実際に通るかは未検証）。

## Step 5: 新規性ゲートL1 — 沈黙故障を踏んだ後、回避策込みでOK

SKILL.md記載どおりの第1回:
```
python3 "<CANON>/.claude/skills/pr-independent-review/scripts/novelty_gate.py" ~/moorestech-worktrees/pr-review origin/master
{ "new_edges": [], "asmdef_refs": [], "grammar": [] }
EXIT=0
```
→ **exit 0 で全空JSON**。Step 3と同じmerge-base問題。SKILL.mdの「非ゼロexitは即エラー終了」規約では捕捉できず、
そのまま進めば verdict が「自動マージ可」に化ける（＝SKILL.mdが最も警戒している沈黙故障そのもの）。

base差し替え後（第2回・採用）:
```
python3 ".../novelty_gate.py" ~/moorestech-worktrees/pr-review 8ce6f4dd^1 > /tmp/pr-review-1041-novelty.json
EXIT=0
```
出力:
- `new_edges` 1件 — `Tests/CombinedTest/Game/BlockRemovalReservationTest.cs:5` `using Game.World.Interface.DataStore`（**`generic_origin=false`** → 参考情報へ）
- `asmdef_refs` 0件
- `grammar` 3件 — `new_datastore_file` × 2（`IBlockRemovalReservationService.cs:1` / `BlockRemovalReservationService.cs:1`）、`interface` × 1（`IBlockRemovalReservationService.cs:11`）

**新形フラグ集計 = 3**（generic_origin=trueのnew_edges 0 + asmdef_refs 0 + grammar 3）。
`.claude/` `.agents/` `.codex/` 配下の除外対象は0件。

## Step 6 — 意図的に未実行（ブリーフ指示）

## Step 7: ダイジェストHTML生成 — OK

`assets/digest-template.html`（951行）をReadし、カード内容仕様をscratchpadに起こしてから
**sonnet subagent** に `/tmp/pr-review-1041/index.html` を生成させた（1079行）。SKILL.md指示の充足を自分で再検証:

| 指示 | 検証方法 | 結果 |
|---|---|---|
| プレースホルダ置換（`{{TITLE}}` 3箇所・`{{DATE}}`・`{{SUBTITLE}}`） | `grep -c '{{'` | 0 |
| `<title>` の置換漏れなし | `grep -n '<title>'` | 実値入り |
| `<footer>` の置換 | `grep -n '<footer>'` | 実値入り |
| `<h1>` は1個・verdictヘッダはh2へ降格 | `grep -c '<h1'` | 1 |
| 使い方コメントブロック削除 | `grep -c '使い方:'` | 0 |
| CONFIG固有化 | `grep 'var STORAGE_KEY\|var COPY_TITLE'` | `pr-review-1041-comments-v1` / `PR #1041 独立レビュー裁定` |
| `REPLACE_WITH_*` 残存なし | `grep -c 'REPLACE_WITH'` | 0 |
| 絵文字不使用 | 絵文字レンジ正規表現 | 0 |
| CSS・コメント機能JSのverbatim維持 | テンプレとの `diff`（`<style>` ブロック / `<script>` 以降） | CSS完全一致・JSはCONFIG 2行のみ差分 |
| カード間の視覚分離 | 目視 | 各 `verdict-card` / `suppressed-card` にinline background+border付与済 |
| 実コード抜粋が機械転記 | worktree実ファイルの該当行を `awk` で照合 | `IBlockRemovalReservationService.cs:10-14` / `MoorestechServerDIContainerGenerator.cs:259-261` とも完全一致。ジェネリクスはHTMLエスケープ済 |
| ダミーテンプレ残存なし | `grep 'PR #0000\|CommonBlockPlaceSystem\|blocks.yml:120'` | 0 |
| 構成順 | 目視 | verdictヘッダ → 裁定カード4 → suppressed 0件 → 判断台帳 → `<details>` 6項目 |

`open /tmp/pr-review-1041/index.html` → exit 0。

### コメント機能の実走確認

`claude-in-chrome` 拡張が未接続（`Browser extension is not connected`）だったため、
リポジトリ同梱の `@playwright/test`（`moorestech_web/webui/node_modules`）でheadless実走検証した:

```json
{ "title": "独立レビュー: PR #1041 feat: tick中のブロック破壊を予約制にしtick末尾で一括確定",
  "h1Count": 1, "figureCount": 5, "detailsCount": 6,
  "panelCountAfterAdd": "1", "copyBtnLabel": "すべてコピー（1件）", "copyBtnDisabled": false,
  "storageKeys": ["pr-review-1041-comments-v1"], "storedCount": 1,
  "copiedMarkdown": "# PR #1041 独立レビュー裁定（1件）\n\n## コメント 1（図）\n> IBlockRemovalReservationServiceインターフェース…の裁定カード（実コード抜粋つき）\n\nスモークテスト: 図コメント1\n",
  "panelCountAfterReload": "1" }
```

図コメント追加 → パネル件数反映 → localStorage永続化（固有キー）→「すべてコピー」のMarkdown出力
（`COPY_TITLE` 反映・`data-label` が引用として出る）→ リロード後も保持、まで**全て動作**。
検証スクリプトは実行後に削除済（`moorestech_web/webui` に一時配置したものも撤去・`git status` clean確認済）。

## Step 8: 記録 — OK

- `<CANON>/.claude/skills/pr-independent-review/records/pr-1041.md` を新規作成。冒頭に
  「**このレビューはスタブである**（Step 6未実行・Critical/Warning/Info/suppressed未収集・品質評価として引用禁止）」
  のブロック引用を明記
- `records/shadow-ledger.md` に1行追記（verdict欄に `**Step 6未実行のスタブ**・配管スモークテスト` と明記、
  suppressed欄は `0（未収集）`）
- SKILL.md本文は一切書き換えていない

---

# 詰まった箇所・曖昧だった箇所（本タスクの主要成果物）

## A. 致命度: 高 — マージ済みPRで配管が沈黙故障する（3箇所連鎖）

SKILL.mdは暗黙にオープンPRだけを想定している。マージ済みPRを投げると次の3つが連鎖して壊れる:

| # | 箇所 | 症状 | 検知可否 |
|---|---|---|---|
| A-1 | Step 2 `gh pr checkout <番号> --detach` | headブランチ削除済みで `fatal: couldn't find remote ref` / exit 128 | **騒がしく落ちる**（エラー処理節でカバー済み。ただし回避策の記載が無い） |
| A-2 | Step 3 `git diff origin/<base>...HEAD` | HEADが `origin/<base>` の祖先になり三点diffのmerge-baseがHEAD自身 → **patchが空・exit 0** | **無検知**（`grep -c '^diff'` の確認手順がSKILL.mdに無い） |
| A-3 | Step 5 `novelty_gate.py <repo> origin/<base>` | 同上で **全空JSON・exit 0** | **無検知**。SKILL.mdの「非ゼロexitは即エラー終了」規約は素通りする |

A-2・A-3が揃うと verdict は「Critical 0・新形0 → **自動マージ可**」に化ける。
SKILL.mdが最も警戒している「沈黙故障でverdictが自動マージ可に化け、見逃し率実測が壊れる」が、
まさにこの経路で成立する。

**推奨修正（SKILL.mdへ）**:
1. Step 2に「マージ済みPRの場合」の分岐を追加。
   `gh pr view <番号> --json state,mergeCommit` で `state=MERGED` を判定し、
   checkoutは `git fetch origin pull/<番号>/head && git checkout --detach FETCH_HEAD`、
   **base参照は `origin/<baseRefName>` ではなく `<mergeCommit>^1` を使う**（以降のStep 3/5/6で共通の `BASE_REF` として持ち回る）
2. Step 3の直後に**必須ガード**を追加: `grep -c '^diff' /tmp/pr-review-<番号>-patch.diff` が0なら即エラー終了。
   「patch空＝レビュー対象なし」で先へ進むのを禁止する（A-2の唯一の検知点）
3. Step 5に**空出力ガード**を追加: `new_edges` / `asmdef_refs` / `grammar` が全部空かつ patch が非空、
   という組み合わせは base 指定ミスの可能性が高い旨を明記し、base参照の一致確認を要求する
4. Step 3/5/6でbase参照を `origin/<baseRefName>` とベタ書きしている箇所を、Step 2で確定した `BASE_REF` 参照に統一する
   （現在は3箇所に散っており、片方だけ直す事故が起きやすい）

## B. 致命度: 中 — base参照がStep間で名前を持っていない

Step 3は `origin/<baseRefName>`、Step 5も `origin/<baseRefName>`、Step 6のcodexテンプレも
`git diff origin/<baseRefName>...HEAD` と、同じ値を3回別々に書かせている。Aの修正を入れるなら、
Step 2の末尾で「以降 `BASE_REF` と呼ぶ」と一度だけ定義する形が事故を減らす。

## C. 致命度: 中 — Step 3のpatch妥当性チェックが無い

Step 3は生成コマンドだけで、成功条件が書かれていない。ブリーフ側には
「`grep -c '^diff'` が1以上」という期待値があるのに、SKILL.md本文には無い。
Step 5には非ゼロexitガードがあるのに、その前段のStep 3が無防備なのは非対称。

## D. 致命度: 低 — `cd` 前提のコマンドがagent実行に馴染まない

Step 2の `cd ~/moorestech-worktrees/pr-review && gh pr checkout ...` は、
サブエージェント実行系ではbash呼び出し間でcwdがリセットされるため、
`cd` を毎回同じコマンド内に含める必要がある。`git -C` 形式に揃っている他Stepと書式が不統一。
`gh pr checkout` はリポジトリコンテキストを要求するので `-C` にできない事情はあるが、
「同一コマンド内で `cd` すること」を1行注記しておくと安全。

## E. 致命度: 低 — Step 7の「suppressed 0件」の扱いが未定義

SKILL.mdは「suppressedカード（全件・同形式）」としか書いておらず、0件のときに
セクションごと省くのか1行置くのかが不明。今回は「0件」の1行カードを置いた（verdict件数と整合を取るため）。
0件時も明示的に「0件」と出す方針を1行書いておくと、生成subagentの判断ブレが消える。

## F. 致命度: 低 — Step 7のカード見出しバッジの語彙が2種類しかない

テンプレートのバッジは `badge-new`（新形）/ `badge-sup`（suppressed）の2種。
verdict判定規則には「設計判断: あり」という第3の分類があるが、対応するバッジが無い。
今回は `badge-new` を流用しバッジ文言だけ「設計判断（ダミー）」にした。
`badge-design` 相当を足すか、「設計判断は `badge-new` を流用しラベルで区別する」と明記すべき。

## G. 致命度: 低 — Step 8のmd版サマリの書式が未規定

`records/pr-<番号>.md` は「verdict・裁定/suppressed/新形の各明細のテキスト縮約（grep用）」としか
書かれておらず、見出し構成が生成ごとにばらつく。grep用途を謳うなら、最低限
「1行目に `# 独立レビュー記録 — PR #<番号>`」「verdict行の書式」くらいは固定したい。

## H. 観察 — 問題なく機能した箇所（回帰の基準として記録）

- `$CANON` 決定手順（3ステップ＋`novelty_gate.py` での実在確認）: 曖昧さなし
- worktreeからの `git -C "$CANON" worktree add ...` 初回作成: 正常
- `$CANON` をリテラルで渡す罠への注意書き: 明示的で守りやすい
- Step 5の3系統別の採用基準（`new_edges` は `generic_origin=true` のみ / 他は全件）: 混同せず適用できた
- Step 7のテンプレ固有指示（h1重複・使い方コメント削除・CONFIG置換・絵文字禁止・カード視覚分離）:
  subagentへの指示に落とすのに十分具体的で、全項目が一発で通った
- `novelty_gate.py` 自体の検出精度: 新規DataStoreファイル2件とinterface宣言1件を正しく拾い、
  テストからの新規usingを `generic_origin=false` に落として参考情報へ回す挙動も設計どおり

---

## 生成物一覧

| パス | 内容 |
|---|---|
| `/tmp/pr-review-1041-patch.diff` | Step 3 patch（266行・8ファイル） |
| `/tmp/pr-review-1041-context.md` | Step 4 4カテゴリcontext |
| `/tmp/pr-review-1041-novelty.json` | Step 5 新規性ゲート実出力 |
| `/tmp/pr-review-1041/index.html` | Step 7 ダイジェスト（1079行） |
| `.claude/skills/pr-independent-review/records/pr-1041.md` | Step 8 md版サマリ（スタブ明記） |
| `.claude/skills/pr-independent-review/records/shadow-ledger.md` | Step 8 台帳1行追記 |
| `/Users/katsumi/moorestech-worktrees/pr-review` | レビューworktree（新規作成・PR #1041 のheadをdetachedで保持） |
