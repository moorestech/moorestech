---
name: pr-adjudicated-apply
description: |
  人間の裁定結果（adjudications.json）に基づき、pr-independent-reviewが出力したfindings.jsonのうち
  reject以外の裁定（案キーA〜F・other）が付いた指摘だけをPRブランチへ実装・検証・pushする無人実行スキル。PR番号を受け取り、
  apply専用worktreeでPRのheadをdetached checkoutして修正し、コンパイル・関連テストで検証してからpushする。
  checkout後は修正前にsubagentを無条件発火してmasterとのコンフリクトを検査し、あれば逆マージで事前解消する。
  裁定未完了時は即座にfailureとして終了し、却下された指摘・新規発見の問題には一切触れない。
  Use When:
  1. 「/pr-adjudicated-apply <PR番号>」で起動された時
  2. 「裁定結果をPRに適用して」「adoptされた指摘を直してpushして」と言われた時
  3. pr-independent-reviewの裁定サイトで裁定が完了したPRへ、無人で対応を反映させる時
hooks:
  # 無人実行の関所。スキル発動中だけ有効（repo横断のsettings.jsonに置くと開発者の通常セッションまで巻き込む）
  # Gate for unattended runs; active only while this skill runs, unlike a repo-wide settings.json hook
  PreToolUse:
    - matcher: "AskUserQuestion"
      hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py ask"
  Stop:
    - hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py stop apply"
---

# pr-adjudicated-apply — 裁定結果のPR適用（無人実行）

**このスキルは無人パイプラインの一部として動く。AskUserQuestionは使わない**（禁止事項参照）。
ユーザーに確認を求めたくなった判断は、実装せずapply-result.jsonのsummaryへ記載して終える。

## 最重要: 無人起動でも「apply-result.json で終える」

このスキルは poller から cmux ワークスペース上の**対話モード** claude でフォアグラウンド起動されている
（ADR 0023。2026-08-20 までは `claude -p` だった）。対話モードではターンを終えてもプロセスは消えないが、
**poller は `apply-result.json` の存在とプロセス生存（`pgrep -f "session-id <id>"`）であなたを監視している**。
apply向けpollerはidle検知を行わない（session/subagentsのtranscript更新は見ない）。
プロセスが死んで `apply-result.json` も無ければ、新しいセッション・新しいワークスペースで
1回だけ作り直しリトライする（`MAX_APPLY_RETRY=1`）。死亡＝retryで足りるという設計裁定であり、
長時間の無応答を待って救済する仕組みは無い。

したがって:

- **待機は同一ターン内でブロッキングして行う**。`uloop run-tests` は結果が返るまでそのターンで待ち切る。
  完了に数分かかっても、待つこと自体がこのスキルの仕事である
- **「wakeupをスケジュールしたので待つ」「後で結果を確認する」と述べてターンを閉じることを禁止する**。
  スケジュールされた再開はこの実行環境に存在しない
- **終了はStep 7の `apply-result.json` を書いた直後だけ**。書く前に終わる終わり方は、成功・失敗いずれの意図であっても
  バグである。行き詰まったなら `status: "failure"` で理由を書いて終える
- **session limit に当たったら何もしなくてよい**。poller が reset 時刻まで待ち、同じペインへ継続指示を送る
  （リトライ予算は消費しない）。weekly limit は失敗ラベルにして人を呼ぶ

**入出力の置き場**: `$LOGS` はprivateログrepo `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs`
（apply専用worktreeからは兄弟symlink `../moorestech_logs` でも到達できるが、絶対パスで書くこと）、
`$RUNDIR = $LOGS/harness/pr-independent-review/runs/pr-<番号>/`（再レビューが存在する場合は
最大のrNを持つ `pr-<番号>-rN/` が最新run。最新runを使う）。
`$LOGS` / `$RUNDIR` は本ドキュメント上のプレースホルダでありシェル変数ではない。
コマンド・ファイルパスへ渡すときは必ず実値の絶対パスへ展開して書く。

**$REPO（apply専用worktree）**: このSKILL.mdを実行しているセッションのリポジトリルート
（`git rev-parse --show-toplevel` の出力。pollerはapplyスロットworktree — `~/moorestech-worktrees/pr-apply` /
`pr-apply-2` 等のスロットプールから空きを1つ選ぶ — をcwdとして起動する。並列applyのためスロットは複数ある）。
`$REPO` も実値の絶対パスへ展開して書く。

このworktreeはapply専用であり、他セッションの作業物は存在しない前提で扱ってよい
（メインクローンで走らせていた頃は、apply実行中に別セッションがブランチを切り替える事故が起きた。
ユーザー裁定 2026-08-17）。したがって作業前の残骸は保全せず破棄する（Step 3）。
一方、**ブランチのcheckoutは必ずdetachedで行う** — 同じブランチが他のworktreeでcheckout済みだと
`fatal: '<branch>' is already used by worktree at ...` で失敗するため、ブランチ名を持たずに作業してpush時だけ名指す。

この規律は本スキルのfrontmatter hooks（`pr-independent-review/scripts/unattended-gate.py`）が機械的に守らせる。
起動プロンプトに `【無人起動】` がある場合に限り、`$RUNDIR/apply-result.json` も `abort.json` も無いまま
ターンを終えようとすると Stop がブロックされ、AskUserQuestion はdenyされる（同一セッション2回でフェイルオープン）。

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

1. **前回の残骸を無条件に破棄する**。ここはapply専用worktreeであり、他セッションの作業物は存在しない
   （前回applyの未pushな変更・Unityが書いた痕跡しか残らず、どちらも残す価値がない）:

       git -C <$REPOの実値> checkout -- .
       git -C <$REPOの実値> clean -fd

   `clean -fd` に `-x` を付けてはならない — `Library/` はgitignoreされており、消すと再インポートに数十分かかる。
2. PRのheadRefNameを取得する: `gh pr view <番号> --repo moorestech/moorestech --json headRefName,headRefOid`
3. PRのheadをfetchし、**detachedでcheckoutする**（ブランチ名を作らない。他worktreeとの二重checkout衝突を避ける）:

       git -C <$REPOの実値> fetch origin pull/<番号>/head && \
         git -C <$REPOの実値> checkout --detach FETCH_HEAD

4. checkout後、`git -C <$REPOの実値> rev-parse HEAD` が手順2の `headRefOid` と一致することを確認する。
   不一致なら即座に失敗として終了し、理由をsummaryに記す

## Step 3.5: masterコンフリクト事前解消（subagent委譲・無条件発火）

checkout成功後、修正実装に入る前に、**コンフリクトの有無を自分で調べず**、必ずsubagentを1体発火して委譲する
（メインのコンテキストをコンフリクト詳細で消費しないため。`git merge-tree` やdiffでの予備調査も行わない）。

`references/conflict-preflight-agent.md` をReadし、`{{REPO}}`・`{{HEAD_REF_NAME}}`・`{{PR_NUMBER}}` を
実値に置換して、Agentツール（`model: "opus"`）のプロンプトとして丸ごと渡す。

subagentの報告（コンフリクトなし／解消済み／解消不能）への後続処理:

- 「コンフリクトなし」→ そのままStep 4へ進む
- 「解消済み」→ マージコミットSHAをStep 7の `summary` に記録し（`pushed_commits` にも含める）、Step 4へ進む。
  解消内容の再検証・diff閲覧は行わない（Step 5のコンパイル・テストが実効的な検証になる）
- 「解消不能」→ 失敗として終了する（summaryに解消不能ファイルを記載）。
  ただし**報告された解消不能ファイルが機械生成・緩い運用のファイルだけ**の場合
  （`.moorestech-external-revisions.json` / `_CompileRequester.cs` / `moorestech_client/.uloop/tools.json` /
  `.superpowers/**`・`docs/superpowers/**` の記録類）は、subagentがreferenceの
  「機械的に解消するファイル」節を守れていない。失敗させず、その節の解消方法を明示した指示で
  **subagentを1回だけ再発火する**。再発火後もなお同じ報告なら失敗として終了する
  — 外部リビジョンピンの分岐はapplyの中止理由にしない（ユーザー裁定 2026-08-19。
  `.decisions/2026-08-19-applyのピン衝突はPR側を採って続行する.md`）

## Step 4: 修正実装

対象finding（Step 2で抽出したadopt分）それぞれについて、`recommendation` と `comment`（あれば）に従って
`files` の指すコードを修正する。

編集・新規作成したファイルのパスを `EDITED_PATHS` として控える（Step 6のadd対象がこれに限定されるため）。

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
- 修正箇所に関連するテストを
  `cd <$REPOの実値> && uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<関連regex>"`
  で実行する（`<関連regex>` は修正したクラス・機能に対応するテストクラス名から組み立てる）。
  **`--test-mode EditMode` を省いてはならない** — uloopの既定は PlayMode であり、
  ユニットテストのつもりで投げるとEditorがPlayModeへ入ったまま固着し、以後のuloopコマンドが全て180秒でタイムアウトする。
  固着したら `uloop control-play-mode --project-path ./moorestech_client --action stop` で解除してからやり直す
- **コンパイルまたはテストが失敗し、かつStep 4の範囲内で直しきれない場合は、pushせず失敗として終了する**
  （apply-result.jsonの `status` を `"failure"`、`tests` に失敗内容を書く）
- ドメインリロード中のエラー（「Unity is reloading」）はAGENTS.md記載どおり45秒待ってリトライする
- Unityがこのworktreeで起動していなければ `cd <$REPOの実値> && uloop launch ./moorestech_client` で起動する
  （apply専用worktreeは常駐対象ではないため、接続できない状態から始まることがある。
  `--project-path` は `launch` には無く位置引数で渡す。起動後 `uloop compile` が通るまで45秒間隔でリトライする）。
  `Unity CLI Loop is not installed in this project` が出たら
  `moorestech_client/UserSettings/UnityMcpSettings.json` が無い状態。本来スロット配備時に固有ポートで
  設置済みのはずのファイルなので、メインクローンの同ファイルをコピーし `customPort` を
  **このスロット固有の値**（他worktreeの `UnityMcpSettings.json` と重複しない未使用ポート）へ書き換えてから起動し、
  復旧した事実と使用ポートをapply-result.jsonのsummaryに記載する
  — ポートを他worktreeと共有すると別プロジェクトのEditorへコマンドが飛ぶ
- **テストの完了は必ずこのターン内で待ち切る**。7分かかっても待つ。
  「実行を投げてターンを終える」は結果を捨てるのと同じである（冒頭の最重要事項を再読すること）

## Step 6: commit & push

- 採用finding単位、または意味的にまとまる単位でcommitする。**`git add` は必ずパスを明示する** —
  対象は「Step 4で編集したパス（`EDITED_PATHS`）」だけ。
  `git add -A` / `git add .` / `git commit -a` は禁止。
  apply実行中もUnityがdirtyを作り続けるため（Step 5の `uloop compile` はコンパイルトリガーを必ず書き換え、
  外部リビジョンピンは常駐Unityが数十秒ごとに書き換える）、全体addすると実行中に湧いた痕跡がPRのcommitへ混入する。
  コミットメッセージ末尾に必ず次を含める:

      Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>

- 全commit後、PRブランチへpushする: `git -C <$REPOの実値> push origin HEAD:<headRefName>`
- pushした各commitのSHAとsubjectを控えておく（Step 7の `pushed_commits` に使う）

## Step 7: 出力

`<$RUNDIRの実値>/apply-result.json` を書く（**このファイルを書かずに終了することは禁止**。
書き終えるまでターンを閉じない）:

    {
      "status": "success|failure",
      "pushed_commits": ["<sha> <subject>", "..."],
      "summary": "<何を直したか・何を見送ったか（対象外finding・規約衝突による見送り等）を簡潔に>",
      "tests": "<実行したコンパイル・テストコマンドと結果>"
    }

**PRへのコメント投稿・ラベル操作は一切行わない**（poller側の責務）。

## Step 8: 後片付け（不要）

apply専用worktreeで作業しているため、終了時の後片付けは行わない。失敗して未commitの変更が残っても、
次回applyのStep 3手順1が無条件に破棄する。元ブランチへ戻す操作も不要（detachedのまま放置してよい）。

**やってはいけないこと**: 後片付けのために `apply-result.json` を書く前へ手数を増やすこと。
出力を書かずに死ぬ方がはるかに高くつく（冒頭「最重要: 無人起動でも『apply-result.json で終える』」参照）。

## 禁止事項

- **AskUserQuestionの使用禁止**（無人実行前提。判断に迷ったら実装せずapply-result.jsonのsummaryへ記載する）
- **レビューのやり直し禁止**（findings.jsonの再収集・追加所見の指摘出しはpr-independent-reviewの責務であり、
  本スキルはStep 2で触れないと決めたものを勝手に洗い直さない）
- **`decision:"reject"` の指摘と新規発見の問題への変更禁止**（実装で触らない。Step 2参照）
- **masterへの直接push禁止**（push先は常にPRのheadRefName。`git push origin HEAD:master` 等は行わない）
- `findings.json` / `adjudications.json` は入力として扱い、書き換えない（出力は `apply-result.json` のみ）
