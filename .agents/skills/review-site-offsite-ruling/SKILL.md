---
name: review-site-offsite-ruling
description: |
  独立レビューの裁定サイト（review.moores.tech）の外＝チャット上で確定したユーザー裁定を、
  サイトのadjudications.json・シャドー台帳・.decisions/・PR本文へ、下流の無人applyを誤爆させずに書き戻す。
  提示外の第三案が採られた場合の記録方法と、手で実装済みの裁定を二重適用させない手順を含む。
  Use When —
  1. チャットでの議論の末にレビュー指摘の扱いが決まった時（サイトのボタンを押さずに裁定が出た時）
  2. 「裁定サイトは更新した？」「サイトにも書き戻して」「この裁定を台帳に記録して」と言われた時
  3. レビュー指摘に対し、提示された案A/案Bのどれでもない案を採った時
---

# review-site-offsite-ruling — サイト外で出た裁定の書き戻し

裁定サイトはボタンで裁定する前提の機構だが、実際の裁定はチャットでの議論で決まることが多い。
その差分を放置すると、**サイト・台帳・PRのどれを見ても裁定が見つからない**状態になる
（PR本文と `.decisions/` だけ更新して「裁定を記録した」と報告したのが実際の事故。ユーザーから
「それって裁定サイトは更新したってこと？」と指摘された）。本スキルはその書き戻しだけを担う。

**指摘の是非は判断しない。** 裁定そのものが未確定なら、書き戻す前に moores-grill-with-docs で確定させる。

## 前提

| 記号 | 実体 |
|---|---|
| `$SITE` | `http://127.0.0.1:8931`（`review.moores.tech` はCloudflare Tunnelがここへ流すだけ。**同じデータ**） |
| `$LOGS` | `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs` |
| `$RUNDIR` | `$LOGS/harness/pr-independent-review/runs/pr-<番号>/`（再レビューがあれば最大の `-rN`） |

サイトが落ちていると `curl` が届かない。`services.json` の `pr-review-site`（longrun）が実体で、
落ちていれば supervisor が5秒で上げ直す。到達不能が続くならそこを見る。

## Step 1: 現状を実測する

裁定を書く前に、いま何が記録されているかを必ず読む。**記憶や会話の要約で代替しない**。

```bash
curl -s http://127.0.0.1:8931/api/pr/<番号> | python3 -c "
import json,sys; d=json.load(sys.stdin)
a=d['adjudications'] or {}
print('completed:', a.get('completed'))
print('decided:', [(i['id'], i['decision']) for i in a.get('items', [])])
for f in d['findings']['findings']:
    print(f['id'], f.get('severity'), 'suppressed' if f.get('suppressed') else '',
          f.get('title','')[:60], [o.get('key') for o in (f.get('options') or [])])
"
HOME=/Users/sakastudio gh pr view <番号> --repo moorestech/moorestech --json labels,headRefOid
```

見るもの: 既にサイト上で裁定済みの指摘（人が押した分。**消してはいけない**）／今回の裁定が
どのfinding idに対応するか／PRのラベル（`独立レビュー:裁定待ち` なら poller が完了を待っている）。

## Step 2: decisionの値を決める（下流の挙動から逆算する）

`decision` は案キー（`A`〜`F`）・`other`・`reject` の3系統で、**`reject` 以外はすべて「採用」として
無人apply（`/pr-adjudicated-apply`）の対象になる**。poller は `completed:true` を見た次のtick（120秒間隔）で
採用件数を数え、1件でもあれば apply スロットのworktreeで `claude -p` を起動する。
つまり decision の選択は「文書上の意味」ではなく「誰が直すか」の指定である。

| 状況 | decision | 理由 |
|---|---|---|
| 提示された案をそのまま採り、**まだ直していない** | 案キー（`A` 等） | 無人applyに実装させる |
| 提示外の第三案を採り、**もう手で実装した** | `reject` ＋ 理由コメント | 採用扱いにすると同じ箇所を無人applyがもう一度直しにくる（二重適用） |
| 提示外の第三案を採り、**これから無人applyに任せる** | `other` ＋ 実装指示コメント | `other` はコメント必須。コメントが実装指示そのものになる |
| 指摘を採らない（誤検知・仕様どおり） | `reject` ＋ 理由 | — |
| 案Dのように**指摘の前提ごと消えた** | `reject` ＋ 「案D裁定により前提消滅」 | 指摘は「間違い」ではなく「もう成立しない」。コメントでそう書く |

コメントには**裁定の所在**を必ず入れる — `.decisions/` のファイル名と、実装済みならそのcommit SHA。
サイトのカードだけを見た人が、なぜ却下されたかを追えるようにするため。

## Step 3: サイトへ書き戻す

**POSTはitems配列を全置換する。** 部分POSTすると既存の裁定が消える。同梱スクリプトはGET→マージ→
POST→readbackを行うのでこれを使う。

```bash
cat > /tmp/adj.json <<'JSON'
[
  {"id": "F01", "decision": "reject", "comment": "案D裁定（.decisions/2026-08-19-....md・実装 abd1d1492）により前提ごと解消。..."},
  {"id": "F02", "decision": "reject", "comment": "..."}
]
JSON
python3 .agents/skills/review-site-offsite-ruling/scripts/adjudicate.py --pr 1175 --decisions /tmp/adj.json
```

400が返る典型原因: suppressed指摘に裁定を付けた／`other` なのにコメントが空／findings.jsonに無いid／
`completed:true` なのに未裁定が残っている。エラー本文に理由が出るのでそれを読む。

## Step 4: completed は押さない（押す条件を満たす時だけ押す）

`--complete` を付けると poller が動き出す。次の**両方**を満たす時だけ押す:

1. 全非suppressed指摘の裁定が揃っている（サイト側のゲートでも弾かれる）
2. 採用（`reject` 以外）が付いた指摘を、**無人applyに直させてよい**

手で実装済みの裁定を書き戻しただけの場合、残りの指摘は未裁定のはずなので**押さない**。
ラベルは `独立レビュー:裁定待ち` のまま据え置き、未裁定の指摘一覧をユーザーへ報告して判断を仰ぐ。
「片付いて見えるから完了にしておく」は、無人applyが手直し済みのコードへ重ねて入る事故になる。

**押す前に外部repoピンを確認する。** 無人applyはコード修正の前に `origin/master` とのコンフリクトを
事前解消しようとし、`.moorestech-external-revisions.json` のピンがPR head側とmaster側で互いに祖先関係の
無いコミット（＝未マージのfeatureブランチ上のSHA）を指していると「解消不能」で即failureに落ちる。
関連repo（`moorestech_master` 等）のPRを先にマージしてピンをマージ済みSHAへ更新してから完了にする。

```bash
git -C <PRのworktree> show HEAD:.moorestech-external-revisions.json
git -C <PRのworktree> show origin/master:.moorestech-external-revisions.json
# 両者のcommitHashが違う場合、外部repoで `git merge-base --is-ancestor <A> <B>` が通るか確かめる
```

## Step 5: シャドー台帳へ記録する（`$LOGS`）

`$LOGS/harness/pr-independent-review/records/pr-<番号>.md` の末尾へ節を追記する。既存行は書き換えない
（台帳は追記型）。

```markdown
## 裁定結果（YYYY-MM-DD・<案の呼称>／<全件|部分>裁定）

<決定の要旨。正本は .decisions/<ファイル名>、実装は head <SHA>>

| id | 指摘 | 裁定での扱い |
|---|---|---|
| F01 | <指摘の要点> | <なぜその decision になったか> |

**未裁定で残っている指摘**: <id列挙>。`adjudications.json` は `completed: false` のままで、
pollerは `独立レビュー:裁定待ち` に留まる（理由を1行）。
```

`shadow-ledger.md` の当該PR行の「あなたの実判断」列（記入列）も埋める。他の列は書き換えない。

**logs repoは手でcommitしない。** Stop/SessionEnd の `logs-sync.mjs` が `git add -A` → commit →
`pull --rebase` → push まで行う。手でcommitすると同じ内容を二重に扱うことになる。**書いたら放置が正**。

## Step 6: `.decisions/` へ裁定を記録する（コードrepo）

書式は `.dev-hooks/decisions-format-check.mjs` が強制する — ファイル名 `YYYY-MM-DD-<内容>.md`、
本文に `決定` / `棄却案` / `理由` の行（`リンク` は任意）。違反するとWrite/Editが差し戻される。

- **棄却案に書けるのは実際にユーザーへ提示した案だけ。** レビューが出した案A/案Bは提示済みなので書ける。
  agentが思いついただけの案を棄却として書かない（出所偽装）
- 過去の裁定を覆した場合、**旧ファイルの冒頭へ `**[上書き済み]**` と後継の裁定名を書き、削除しない**。
  経緯が追えなくなるため
- **PRブランチ（`$PRWT`）へ積まない。** レビュー対象コードと記録が混ざる。skill改修・`.decisions/` は
  専用worktree（`moores-wt new <branch> --no-editor`）で完結させる

## Step 7: PR本文・ラベル・bd

- PR本文へ「追記（YYYY-MM-DD・<裁定名>・<SHA>）」節を挿入する。何をどう変えたかではなく
  **どの指摘がどう決着したか**を書く（コード差分はcommitを見れば分かる）
- ラベルは Step 4 の判断に従う。`独立レビュー&対応完了` は**対応がpush済みの時だけ**付ける
- 裁定から派生した後続タスクは `bd create`、経緯は `bd note`

## Step 8: 報告に必ず書くこと

「更新した」だけで終えない。**どこを更新し、どこを更新していないか**を並べる。実際に
「PR本文と `.decisions/` は更新／サイトは未更新」の状態を「裁定を記録した」と報告して指摘された。

- 更新した先（サイト・台帳・`.decisions/`・PR本文・bd）とその状態
- 未裁定で残った指摘のid一覧と、なぜ完了にしていないか
- 公開URL（`https://review.moores.tech/pr/<番号>`）で何がどう見えるか。
  この公開URLはローカル8931のリバースプロキシであり、静的HTMLではなく `adjudications.json` を
  リクエストごとに読むので、POSTした時点で反映済みである

## Gotchas

- **`review.moores.tech` と `127.0.0.1:8931` は同一実体**。「サイトも更新した？」と聞かれたら
  トンネル設定（`services/pr-review/cloudflared-config-moores.yml`）を見て同一性を根拠に答える。
  WebFetchで直接読もうとしてもCloudflare Accessのログイン画面へ302されるので、確認はローカルAPIで行う
- **digest.html を手で書き換えない。** 裁定状態はJSから `adjudications.json` を読んで描画される。
  HTMLを触っても次の再生成で消えるうえ、サイトの表示と実データが食い違う
- **`records/pr-N.md` は最新runを指す。** 再レビュー済みPRは `pr-N-r2` 以降が最新。サイト側も
  `data.py` が最大rNを解決するので、台帳とサイトで別のrunを見ないよう合わせる
- **suppressed指摘には裁定を付けられない**（400）。suppressedを覆したいなら、それはレビュー機構側の
  裁定（`suppressed-by` の根拠を書き換える話）であり、本スキルの範囲外
- **poller は120秒間隔**。`completed:true` を書いた直後にラベルを手で動かすと、pollerの遷移と競合する。
  完了にしたら**ラベルはpollerに任せる**
- **完了は人が先に押すことがある。** 書き戻し作業中にユーザーがサイトで完了を押し、pollerが先に
  `対応中` へ遷移していることがある（PR1175で実際に発生）。`adjudications.json` へ再POSTする際は
  `completed` を**必ず現在値のまま引き継ぐ**（同梱スクリプトはそうする）。falseで上書きすると、
  遷移済みのPRを黙って裁定待ちへ巻き戻す
- **無人applyが失敗しても書き戻しは無駄にならない。** 失敗時は `$RUNDIR/apply-result.json` の
  `summary` に未適用の裁定id一覧が残る。`独立レビュー:失敗` ラベルのPRはpollerが何もしないので、
  原因を潰したうえで人がラベルを付け替えて再開する

## Available scripts

- `scripts/adjudicate.py` — 既存裁定を保ったまま差分をPOSTし、readbackまで行う。
  実行: `python3 scripts/adjudicate.py --help`（`--dry-run` でマージ結果だけ確認できる）
