---
name: pr-adjudicated-apply
description: |
  人間の裁定結果（adjudications.json）に基づき、pr-independent-reviewが出力したfindings.jsonのうち
  reject以外の裁定（案キーA〜F・other）が付いた指摘だけをPRブランチへ実装・検証・pushする無人実行スキル。PR番号を受け取り、
  メインクローンでPRのheadブランチへcheckoutして修正し、コンパイル・関連テストで検証してからpushする。
  checkout後は修正前にsubagentを無条件発火してmasterとのコンフリクトを検査し、あれば逆マージで事前解消する。
  裁定未完了時は即座にfailureとして終了し、却下された指摘・新規発見の問題には一切触れない。
  Use When:
  1. 「/pr-adjudicated-apply <PR番号>」で起動された時
  2. 「裁定結果をPRに適用して」「adoptされた指摘を直してpushして」と言われた時
  3. pr-independent-reviewの裁定サイトで裁定が完了したPRへ、無人で対応を反映させる時
---

# pr-adjudicated-apply — 裁定結果のPR適用（無人実行）

**このスキルは無人パイプラインの一部として動く。AskUserQuestionは使わない**（禁止事項参照）。
ユーザーに確認を求めたくなった判断は、実装せずapply-result.jsonのsummaryへ記載して終える。

**入出力の置き場**: `$LOGS` はメインリポジトリの兄弟にあるprivateログrepo `../moorestech_logs`
（`git rev-parse --show-toplevel` の親ディレクトリ直下）、
`$RUNDIR = $LOGS/harness/pr-independent-review/runs/pr-<番号>/`（再レビューが存在する場合は
最大のrNを持つ `pr-<番号>-rN/` が最新run。最新runを使う）。
`$LOGS` / `$RUNDIR` は本ドキュメント上のプレースホルダでありシェル変数ではない。
コマンド・ファイルパスへ渡すときは必ず実値の絶対パスへ展開して書く。

**$REPO（メインクローン）**: このSKILL.mdを実行しているセッションのリポジトリルート
（`git rev-parse --show-toplevel` の出力）。pr-independent-reviewの専用レビューworktree
（`~/moorestech-worktrees/pr-review`）は使わない — 本スキルはPRブランチへ実際にcommit・pushするため、
そのブランチの本来の置き場であるメインクローンで作業する。`$REPO` も実値の絶対パスへ展開して書く。

## Step 1: 入力読み込み・裁定完了ゲート（最初に必ず通る）

1. `<$RUNDIRの実値>/findings.json` と `<$RUNDIRの実値>/adjudications.json` をReadする。
2. **次のいずれかに該当したら、その時点で即座に失敗として終了する**（後続Stepへ進まない。
   ブランチ操作はまだ行っていないので後片付けは不要）:
   - `findings.json` が存在しない（レビュー未実施）
   - `adjudications.json` が存在しない（裁定未着手）
   - `adjudications.json` は存在するが、トップレベルの `completed` が `true` でない（裁定作業中）
   - `adjudications.json` の `items` 配列内に、`findings.json` に存在しないidへの参照がある（データ不整合）
   - `findings.json` の非suppressed findingのいずれかに対応する `items` エントリが無い（裁定漏れ。
     `completed:true` の自己申告を信用せず、実データで裏取りする）

   失敗時は `<$RUNDIRの実値>/apply-result.json` に次を書いて終了する:

       {"status": "failure", "pushed_commits": [], "summary": "裁定未完了", "tests": ""}

   （理由が「裁定未完了」以外の不整合の場合は `summary` にその具体理由を書く。書式は「裁定未完了」固定ではなく
   実際の理由を簡潔に記す）

3. `adjudications.json` の期待スキーマ（裁定サイトの出力契約）:

       {
         "pr": <PR番号>,
         "completed": true,
         "completed_at": "<ISO8601>",
         "items": [
           {"id": "F01", "decision": "<案キー(A〜F)|other|reject>", "comment": "<人間の補足指示（otherでは必須）>",
            "auto_recommended": false}
         ]
       }

   decisionの意味: 案キー（`A`〜`F`）＝findings.jsonの `options` またはdigestカード記載の当該案を実装する ／
   `other`＝commentに書かれた自由指示を実装する ／ `reject`＝一切触らない

   `auto_recommended: true` は、人間が完了ボタンを押した時点で未裁定だった指摘を**推奨案で一括採用**した印
   （`decision` は推奨案のキー・`comment` は空・`other`/`reject` には付かない）。
   **実装上の扱いは明示裁定と同じ**（decisionの案をそのまま実装する）。個別に読まれていない可能性があるため、
   実装が推奨案の想定と食い違ったときに勝手に別案へ寄せず、apply-result.jsonの `summary` に
   「推奨一括採用のFxxで想定と差異: <内容>」と記して人へ返すこと

## Step 2: スコープ確認（adopt以外には絶対に触れない）

- `items` のうち `decision != "reject"` のものだけを対象findingとして抽出する。`reject` は一切触らない
- 実装内容の決定順: `decision` が案キーなら **その案**（findings.jsonの `options` の該当summary、
  無ければdigestカード記載の当該案）を実装する。`other` なら `comment` の自由指示を実装する
- **各対象findingの `comment` は人間からの補足指示として尊重する** — 選択された案や `recommendation` と
  矛盾する場合は `comment` を優先する（人間が最新の判断を書いているため）
- 対象findingが0件（全件reject）の場合、Step 3以降（ブランチ操作・修正・push）は一切行わず、
  Step 7の出力へ進む（`status: "success"`、`pushed_commits: []`、summaryに「採用指摘0件、変更なし」と記載）
- **実装中に気づいた「reject指摘の再燃」や「新たに見つけた別の問題」は絶対に修正しない**。
  見つけた場合はapply-result.jsonの `summary` に「見送り: <内容>」として記載するに留める
  （このスキルの責務は裁定の反映のみ。新たなレビューの実施はpr-independent-reviewの責務）

## Step 3: 作業ブランチ準備

対象findingが1件以上ある場合のみ実行する（Step 2で0件なら本Stepはスキップ）。

1. **dirtyの分類を最初に行う**: `git -C <$REPOの実値> status --porcelain --untracked-files=no` を実行する。
   出力が空なら手順2へ進む。非空でも**中止しない** — 次の分類を行ってから続行する
   （自動生成の痕跡1件で無人パイプラインを止めないため。ユーザー裁定 2026-08-15）:

   1. dirtyな各パスについて `git -C <$REPOの実値> diff -- <パス>` を読み、2つに分類する。
      **特定ファイル名のallowlistは持たない**（列挙は必ず陳腐化し、載っていないだけで止まるため）。
      判定基準は「そのdiffが人間・エージェントの意図を1つでも表しているか」:
      - **意味のない自動変更** — 意図を表さない、ツールが実行のたびに書き換える痕跡
        （例: コンパイルトリガーの連番、兄弟クローンのHEADへ追随しただけの外部リビジョンピン）
      - **意味のある変更** — それ以外すべて（他セッションのコミット漏れ等）。判定に迷ったらこちらへ倒す
   2. 意味のない自動変更は `git -C <$REPOの実値> checkout -- <パス>` で破棄する
   3. 意味のある変更は破棄も退避もしない。そのまま手順4のcheckoutでPRブランチへ持ち越し、
      Step 6のcommitに含めてPRの一部とする（ユーザー裁定 2026-08-15。厳密な保全より続行を優先する）。
      持ち越すパスを `CARRIED_PATHS` として控え、Step 7のsummaryへ
      「持ち越し: <パス> — <diffの内容1行>」を、破棄したパスを「破棄: <パス>」として書く
   4. 未追跡ファイルは分類対象外（`--untracked-files=no` のため。checkoutを妨げないが、
      パス衝突時はcheckout自体が失敗して手順4で止まる）
   5. **持ち越しはcheckoutが拒否することがある**。tracked な未コミット変更が持ち越せるのは、
      現HEADとFETCH_HEADで**そのファイルの中身が同一の場合だけ**であり、PRブランチ側でも
      同じファイルが変更されていると手順4は
      `error: Your local changes to the following files would be overwritten by checkout` で失敗する。
      さらに、手順2の破棄から手順4のcheckoutまでの間に常駐Unityが同じファイルを書き戻すレースもある
      （ピンは5〜30秒毎に書き換わる）。手順4が失敗したら**本手順1へ戻って分類をやり直し**、
      意味のない自動変更を破棄したうえでcheckoutを1回だけリトライする。
      それでも失敗したら失敗として終了し（ブランチは切り替わっていないので後片付け不要）、summaryへ
      「checkout失敗: <パス> — PRブランチ側と衝突する持ち越しのため中止（持ち越し分は未commitのまま保全）」と書く
2. **元ブランチを記録する**（Step 8の後片付けで使う）:
   - `git -C <$REPOの実値> symbolic-ref --short -q HEAD` が値を返せばそれが `ORIGINAL_REF`（ブランチ名）
   - 値が空（detached HEAD）なら `git -C <$REPOの実値> rev-parse HEAD` の出力を `ORIGINAL_REF` とする
3. PRのheadRefNameを取得する: `gh pr view <番号> --repo moorestech/moorestech --json headRefName,headRefOid`
4. PRのheadをfetchしてローカルブランチへ反映する（既存の同名ローカルブランチがあってもPRの最新headへ揃える）:

       git -C <$REPOの実値> fetch origin pull/<番号>/head && \
         git -C <$REPOの実値> checkout -B <headRefName> FETCH_HEAD

5. checkout後、`git -C <$REPOの実値> rev-parse HEAD` が手順3の `headRefOid` と一致することを確認する。
   不一致なら即座に失敗として終了し（Step 8で元ブランチへ戻ってから）、理由をsummaryに記す

**この時点から先でどのように終了しても、Step 8（後片付け）を必ず実行してからapply-result.jsonを書く。**

## Step 3.5: masterコンフリクト事前解消（subagent委譲・無条件発火）

checkout成功後、修正実装に入る前に、**コンフリクトの有無を自分で調べず**、必ずsubagent
（Agentツール、general-purpose）を1体発火して委譲する。目的はメインエージェントのコンテキストを
コンフリクト解消の詳細（差分・両側の変更内容）で消費しないこと。メインエージェントが渡してよい情報は
`$REPO` の実値・`headRefName`・PR番号のみで、事前に `git merge-tree` や diff での予備調査は行わない。

subagentへの指示内容（プロンプトに含める）:

1. `git -C <$REPOの実値> fetch origin master` を実行する
2. PRブランチ上で `git -C <$REPOの実値> merge --no-commit --no-ff origin/master` を試みる
   - **Already up to date / コンフリクトなしで成功した場合**: `git -C <$REPOの実値> merge --abort`
     （abort対象が無ければ `git reset --merge`）でマージ状態を破棄し、「コンフリクトなし」とだけ報告して
     即終了する（クリーンでもマージは残さない。master取り込み自体はこのスキルの責務ではない）
   - **コンフリクトが発生した場合**: 各コンフリクトファイルについて両側の変更意図を読み取り、
     両方の意図を保つ形で解消する（機械的にours/theirsを選ばない）。解消後 `git add` し、
     標準のマージメッセージ＋`Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` トレーラーで
     マージコミットを作成する。`.cs` を解消で触った場合は `uloop compile --project-path ./moorestech_client`
     でコンパイルが通ることまで確認してからコミットする
   - **自信を持って解消できないコンフリクトがある場合**: `git merge --abort` で完全に元へ戻し、
     「解消不能」と対象ファイル一覧を報告して終了する（中途半端な解消状態を残さない）
3. 報告は次の3値のいずれか＋最小限の情報のみとする（差分の中身は報告に含めない）:
   「コンフリクトなし」／「解消済み: <マージコミットSHA> <解消ファイルパス一覧>」／「解消不能: <ファイル一覧と理由1行>」

メインエージェント側の後続処理:

- 「コンフリクトなし」→ そのままStep 4へ進む
- 「解消済み」→ マージコミットSHAをStep 7の `summary` に記録し（`pushed_commits` にも含める）、Step 4へ進む。
  解消内容の再検証・diff閲覧は行わない（Step 5のコンパイル・テストが実効的な検証になる）
- 「解消不能」→ 失敗として終了する（Step 8で元ブランチへ戻り、summaryに解消不能ファイルを記載）

## Step 4: 修正実装

対象finding（Step 2で抽出したadopt分）それぞれについて、`recommendation` と `comment`（あれば）に従って
`files` の指すコードを修正する。

編集・新規作成したファイルのパスを `EDITED_PATHS` として控える（Step 6のadd対象・Step 8の破棄対象がこれに限定されるため）。既存ファイルの編集か新規作成かも区別して控えること（Step 8で始末の仕方が変わる）。

AGENTS.mdの規約を遵守する:

- コメントは日本語→英語の2行セット（3〜10行ごと）、`#region Internal` はローカル関数用途限定
- `partial` 禁止、`Func<>` 禁止、デフォルト引数禁止、単純getter/setterプロパティ禁止
- 命名は実処理と一致させる、初期化メソッド名は `Initialize` 固定
- イベント発火に `Action` を使わない（UniRx）

修正がAGENTS.mdの規約と衝突する場合（例: recommendationがpartial化を示唆している等）は、
規約を優先しrecommendationの意図を保ったまま規約準拠の形で実装する。それでも両立できない場合は
その finding をStep 7のsummaryに「見送り: 規約と衝突」として記録し、実装しない。

## Step 5: 検証

- **.csファイルを1つでも変更したら** `cd <$REPOの実値> && uloop compile --project-path ./moorestech_client` を必ず実行する
  （Step 3.5でマージコミットが作られた場合も、masterから流入した変更を含めた検証としてコンパイル必須）
- 修正箇所に関連するテストを `cd <$REPOの実値> && uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<関連regex>"` で実行する
  （`<関連regex>` は修正したクラス・機能に対応するテストクラス名から組み立てる）
- **コンパイルまたはテストが失敗し、かつStep 4の範囲内で直しきれない場合は、pushせず失敗として終了する**
  （Step 8で元ブランチへ戻ってから、apply-result.jsonの `status` を `"failure"`、`tests` に失敗内容を書く）
- ドメインリロード中のエラー（「Unity is reloading」）はAGENTS.md記載どおり45秒待ってリトライする

## Step 6: commit & push

- 採用finding単位、または意味的にまとまる単位でcommitする。**`git add` は必ずパスを明示する** —
  対象は「Step 4で編集したパス（`EDITED_PATHS`）」と「Step 3で控えた `CARRIED_PATHS`」だけ。
  `git add -A` / `git add .` / `git commit -a` は禁止。
  apply実行中もUnityがdirtyを作り続けるため（Step 5の `uloop compile` はコンパイルトリガーを必ず書き換え、
  外部リビジョンピンは常駐Unityが数十秒ごとに書き換える）、全体addすると実行中に湧いた痕跡がPRのcommitへ混入する。
  コミットメッセージ末尾に必ず次を含める:

      Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>

- 全commit後、PRブランチへpushする: `git -C <$REPOの実値> push origin HEAD:<headRefName>`
- pushした各commitのSHAとsubjectを控えておく（Step 7の `pushed_commits` に使う）

## Step 7: 出力

`<$RUNDIRの実値>/apply-result.json` を書く（Step 8の後片付けの後に書いても先に書いてもよいが、
このファイルを書かずに終了することは禁止）:

    {
      "status": "success|failure",
      "pushed_commits": ["<sha> <subject>", "..."],
      "summary": "<何を直したか・何を見送ったか（対象外finding・規約衝突による見送り等）を簡潔に>",
      "tests": "<実行したコンパイル・テストコマンドと結果>"
    }

**PRへのコメント投稿・ラベル操作は一切行わない**（poller側の責務）。

## Step 8: 後片付け（成功・失敗を問わず必ず実行）

Step 3でブランチを切り替えた場合（＝対象findingが1件以上あった場合）は、
Step 3〜7のどこで終了するとしても、apply-result.jsonを書く前に必ず元ブランチへ戻る:

    git -C <$REPOの実値> checkout <ORIGINAL_REFの実値>

失敗終了で未commitの変更が残っている場合、**`git reset --hard` を使ってはならない**。
自分が作った変更だけを、既存ファイルと新規ファイルで**書き分けて**始末してから戻る:

    # Step 4で既存ファイルを編集した分
    git -C <$REPOの実値> checkout -- <EDITED_PATHSのうち既存ファイルの各パス>
    # Step 4で新規作成した分（未追跡なので checkout では消せない）
    rm -f <EDITED_PATHSのうち新規作成ファイルの各パス>

新規作成ファイルを `git checkout --` のpathspecに混ぜてはならない。未追跡パスは
`error: pathspec ... did not match any file(s) known to git` でコマンド**全体**が失敗し、
同時に指定した既存ファイルの復元まで行われない（`reset --hard` はtracked分を戻していたので機能的後退になる）。

`CARRIED_PATHS` は破棄しない。未commitのまま元ブランチへ戻れば、apply前と同じ位置にそのまま復元される
（他セッションのコミット漏れを無告知で消さないため。ユーザー裁定 2026-08-15）。
push済みでない失敗applyの変更は再実行時にゼロから作り直すため、残す価値がない。

Step 1・Step 2（対象0件）で終了した場合はブランチ操作自体を行っていないため、本Stepは不要。

## 禁止事項

- **AskUserQuestionの使用禁止**（無人実行前提。判断に迷ったら実装せずapply-result.jsonのsummaryへ記載する）
- **レビューのやり直し禁止**（findings.jsonの再収集・追加所見の指摘出しはpr-independent-reviewの責務であり、
  本スキルはStep 2で触れないと決めたものを勝手に洗い直さない）
- **`decision:"reject"` の指摘と新規発見の問題への変更禁止**（実装で触らない。Step 2参照）
- **masterへの直接push禁止**（push先は常にPRのheadRefName。`git push origin HEAD:master` 等は行わない）
- `findings.json` / `adjudications.json` は入力として扱い、書き換えない（出力は `apply-result.json` のみ）
