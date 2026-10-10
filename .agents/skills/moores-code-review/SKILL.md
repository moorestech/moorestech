---
name: moores-code-review
description: |
  moorestechのPR作成前・マージ前レビューを単体で完結させる統合スキル。5系統を並列実行する:
  ①決定論チェック（汎用+moorestech固有の機械判定）②reviewer群（moores-*: ドメイン境界・サーバー状態同期3点セット・
  DataStore分離・マスタデータ防御・型構造・前例一致などmoorestech固有の設計規約／core-*: 汎用コード品質の採用実績ある観点＋webui向けts/tsx設計観点）
  ③Codex外部監査 ④Fable全般レビュー ⑤分割深掘り調査（大規模PR時のみ・10-15ファイル/チャンクで全文精読）。
  指摘を実コード照合・重複排除のうえ統合し、機械的修正を自動適用、
  設計判断だけ末尾でAskUserQuestion。moorestech固有の設計規約と汎用レビュー機構を1本に束ね、これ単体でレビューが完結する。
  既定ではStep 3.5〜6.5をWorkflowツール（scripts/review_workflow/ を結合した $RUNDIR/review_workflow.js）で決定論的に実行する（2026-08-20。本体は対象確定・機械チェック・Codex起動・AskUserQuestionのみ。sonnet委譲はWorkflow不可時のフォールバック）。
  裁定反映の後、PR作成前に正しさ系だけの最終バグ確認（Step 7.5 bug-pass）を1回回す。
  Use when:
  1. moorestechでPR作成前・マージ前のレビューを行う時（pr-create前に必ず1パス）
  2. moores-subagent-driven-development の最終ブランチレビューを行う時
  3. 「moores-code-reviewで」「moorestechの設計規約でレビュー」「コードレビューして」と言われた時
---

# moores-code-review

moorestechのコードレビューを **決定論チェック → 5系統の並列レビュー → 実コード照合・重複排除 → 自動適用 → 報告** の順で単体完結させる（外部スキルへの依存なし）。

**この SKILL.md は本体セッション用のディスパッチャである**（2026-08-18 分割・2026-08-20 Workflow化）。本体がやるのは Step 0〜2（対象確定・機械チェック・Codex起動・Workflow args）・Workflow 起動・Step 7（報告と AskUserQuestion）・Step 7.5（最終バグ確認の起動）だけで、**Step 3〜6.5 の実行手順・5系統の詳細・モデル割り当て・実行系 Gotchas の正本は `references/orchestrator-steps.md`**、その実行形が `scripts/review_workflow/*.js`（`build_workflow_args.py` が結合して `$RUNDIR/review_workflow.js` を書く）にある。本体が orchestrator-steps.md を通読するのはインライン実行(後述)の場合のみ。

**Codex（Workflow ツールも Agent ツールも無いホスト）で実行している場合**: Step 0〜2 と Step 7 は同じく自分で行い、Step 3.5〜6.5 は下の「Codex ホストでの実行」節の runner で回す。選ばれた系統を自分で代行・要約・領域分割に置き換えるのは禁止（発火数が Claude 実行と一致しなくなる）。

系統の要約（詳細は orchestrator-steps.md）: ①決定論チェック(check_all.py・0トークン) ②reviewer 41本（moores-* 12・core-* 29） ③Codex外部監査3本 ④Fable全般 ⑤分割深掘り調査(16ファイル以上のみ) + 条件発火verifier + post-checks 2本（コメント保全）+ Refix（反映diff再レビュー `applied-diff-correctness.md`・`scripts/refix_snapshot.py` の snapshot 間 diff・最大3周）+ opus integrator。裁定反映の後に Step 7.5 の bug-pass（正しさ系だけ・同じ Workflow を `mode=bug-pass` で）。

## Workflow実行（既定・2026-08-20）

**既定では Step 2 を本体が回し、Step 3.5〜6.5（系統の並列発火→統合→自動適用→post-check）を Workflow ツール（`build_workflow_args.py` が `scripts/review_workflow/*.js` を結合し args を埋め込んだ `$RUNDIR/review_workflow.js`）で実行する。** 2026-08-18〜20 の sonnet オーケストレータ委譲は、系統群 $164〜225/回 に対し **オーケストレータ1体が待機だけで $194〜240/回**（590〜625ターン・毎ターン25万トークン再送・`Concurrent subagent limit` の再起動16〜34回）を燃やしていた（`docs/research/2026-08-20-moores-code-review-diet-assessment.md`）。Workflow は待機が JS の `await` なのでこの項目が消え、「全員に model 明示」「起動失敗の再起動」「欠員の申告」「fable quota 時の opus fallback」「Codex 完了待ち」が散文でなくコードで強制される（「1メッセージ12体」は Agent 直起動時の規律で、Workflow では同時数をランタイムがキューイングする）。同じスクリプト（args 埋め込み済み）での再実行（`resumeFromRunId`）は完了済みの体をキャッシュから返すので、上限死からの再開で全系統をやり直さない。

**Workflow を使わず sonnet オーケストレータ委譲（旧既定）やインライン実行に落としてよいのは次の場合のみ**（報告冒頭に理由を明記。黙って切り替えない）: (a) Workflow ツールがこのセッションで使えない、(b) ユーザーが「委譲で」「インラインで」等を明示、(c) 自分が委譲オーケストレータとして派遣された側である。委譲時の派遣プロンプトは末尾「旧既定: sonnet 委譲」を、インライン時は `references/orchestrator-steps.md` を Read して Step 2〜6.5 を自分で実行する。

- **本体は Workflow 完了まで対象リポジトリを編集しない**（Apply フェーズで修正が適用されるため衝突する）。
- Codex 3本は Workflow の外（本体の Bash `run_in_background`）で先に投げる。スクリプトはシェルを持たないため。
- Workflow の同時実行数はランタイムが `min(16, CPU-2)` でキューイングする（Mac mini=8）。体数は減らさず所要時間だけ伸びる。CPU 18コア以上のホストへ移すなら 12体/波の分割を JS に足す（同時20体上限の「起動が黙って消える」対策）。
- **Workflow の可用性は保証されない**（2026-08-20 実測: session limit 到達→課金切替の後、同一セッションで「Workflow is disabled for this session」となり消えた）。不可なら (a) のフォールバックへ落ち、その事実を報告冒頭に書く。

### 回収時の突合（本体）

Workflow の返り値を報告へ転記する前に、**`python3 .claude/skills/moores-code-review/scripts/s5_shape_gate.py $RUNDIR` を必ず走らせる**（user-intent reviewer の §5 が決定ごとに「読み1／読み2／両立判定」を揃えているか・形の欠けた §5 を integrated.md が回収扱いしていないかの形式検査。中身は判定しない）。終了コード 1 なら stderr の欠け一覧を添えて core-any-user-intent-fulfillment だけ Agent で再起動（差し戻し）→ integrator だけ再派遣し、再度ゲートを通す。2回目も 1 なら報告冒頭に「§5 形式欠落（差し戻し後も未解消）」と書き、user-intent 系統を欠員として扱う。続けて `systems.planned` が `checks.json` 由来の `systems.expected.total` と一致し `systems.missing` が空かを見る。不一致・欠員は報告に転記し、欠員分だけ Agent で再起動→integrator だけ再派遣してよい。Codex の欠員申告は `codex_recover.py` の終了コード付きでなければ受け付けない（orchestrator-steps.md Step 5）。`postfix.warnings/infos` と `postCheckSelection.note` は integrated.md に載らないので Step 7 の報告へ転記する。report-only では final.diff / checks-final.json / design.md は生成されない。 `refix` は、`refix.scope` が `source` なら 1 周以上の round と各 `report` の実在を確認する。`refix.unresolved` が true なら最終 round の Critical は直っていないので、報告冒頭に「反映 diff 再レビュー未収束（N 周）」と書き、Step 7 の AskUserQuestion に「手で直す / 未修正のまま進める」を載せる（黙って収束扱いにしない）。各 round の `warnings/infos` を報告へ転記する（収束した最終周は `agents/refix-correctness-r<N>.md` の Warning/Info 節を本体が Read してよい）。`scope` が `non-source`/`none` なら「反映 diff は doc/テスト/コメントのみ（再レビュー不要）」と 1 行書く。体数不一致・モデル割り当て違い・成果物欠落があれば、再派遣を重ねる前に transcript（`~/.claude/projects/<プロジェクト>/<セッションID>/subagents/*.meta.json`）で実測し、スキル記述の穴なら `references/skill-improvement.md` で恒久対応する。欠員のある統合結果は「全系統レビュー済み」を偽装するので採用しない。

## Step 0: 実行ディレクトリ `$RUNDIR` を作る

1回のレビューが作る生成物（patch・context・codex監査プロンプト3本・check_all出力・chunks・最終diff・最終detchecks）は
**すべて** `$LOGS/harness/moores-code-review/runs/<ts>/` 配下に置く。以下これを `$RUNDIR` と呼ぶ
（`$LOGS`＝`../moorestech_logs`。`<ts>`＝レビューごとに新規 `YYYY-MM-DD-HHMM-<ブランチslug>-<UUID>`。既存ディレクトリ再利用は同一レビューの中断復旧時のみ。）

    mkdir -p <$RUNDIRの実値>

- **`/tmp` には置かない** — OSに掃除されて消える。これらは記録（Step 7）が指すverdictの実入力であり、
  後から「何をどう測ってその結論になったか」を再現する唯一の材料。pr-independent-reviewのreconcileも
  ここを読む（あちらは `$LOGS/harness/pr-independent-review/runs/pr-<番号>/` を使う。混ぜない）
- ファイル名は固定: `patch.diff` / `context.md` / `checks.json` / `codex-audit.md` / `codex-bughunt.md` /
  `codex-design.md`（各Codexの**結論**は同名の `.final.md`＝`-o` の出力が正本、stdoutログは `.out.md`） / `chunks.tsv` / `agents/<名前>.md` / `integrated.md` /
  `final.diff` / `checks-final.json` / `refix/s<N>.sha`（snapshot commit）/ `refix/round<N>.diff`（反映 diff）/ `agents/refix-correctness-r<N>.md`
- `$RUNDIR` 配下はStop/SessionEnd hook（`.dev-hooks/logs-sync.mjs`）でlogs repoへ自動commit・pushされる。
  セッション側で `git commit` しない

## Step 1: レビュー対象と4カテゴリcontextを確定する

セッション文脈（何を作業したか・どんな裁定があったか）を知るのは本体だけなので、この Step は委譲できない。

1. **作業範囲を特定** — このセッションで生成・変更した成果物をコミット範囲・staged・unstagedから確定し、統合unified diffを `<$RUNDIRの実値>/patch.diff` に書く（**PATCH_PATH**）。ユーザーがレビュー範囲を明示したらそれを優先。
   - **diff は必ず `scripts/review_diff.py` 経由で取る（素の `git diff` 禁止）** — 引数は `git diff` にそのまま渡り、レビューに入れないパスの除外が必ず付く:

         python3 .claude/skills/moores-code-review/scripts/review_diff.py <base>^..<last> >  <PATCH_PATH>
         python3 .claude/skills/moores-code-review/scripts/review_diff.py --cached       >> <PATCH_PATH>
         python3 .claude/skills/moores-code-review/scripts/review_diff.py                >> <PATCH_PATH>

     除外の正本は `review_diff.py` の1箇所で、pr-independent-review の `make_patch.py` も同じものを使う。外すのは
     手を入れない外部物（NuGet の同梱パッケージ・ロックファイル）・生成物（DO NOT EDIT の自動生成コード）・バイナリ・
     Unity のシリアライズ資産・`unity-playmode-recorded-playtest` 配下の `.cs`。NuGet を足す PR ではパッケージ本体だけで
     数万行・数十MBになり、レビューが埋もれる（PR#1436 で +14万行のうち14万行がパッケージだった）。
     プレイテストシナリオは実プレイを踏ませるための使い捨ての操作台本であり、プロダクトコードの規約（重複排除・
     命名・行数）で裁く対象ではない。指摘しても設計判断の裁定コストだけが増える
     （ユーザー裁定 2026-08-16 / PR#1137-F12）。`Client.Playtest` のDSL本体はこのパス外なので通常どおり見る。
     `Assets/Dependencies` の `.cs` はチームが手を入れるので外さない
2. **4カテゴリcontextを書く** — `<$RUNDIRの実値>/context.md`（**USER_PROMPT_PATH**）に埋める。埋め忘れるとreviewerがfalse-positiveを量産する:
   - **目指す（ゴール）** / **目指さない（非目標）** / **許容するトレードオフ** / **尊重すべき制約**
   - **4カテゴリは必ず `##` 見出しで書く**（太字箇条書き形式は出所ラベル検査の対象外になり沈黙故障する。見出しゼロはfail-closedでconfirmedになる）。
   - **「許容するトレードオフ」「非目標」の各行に出所ラベル必須**: `[ユーザー裁定: "発言引用" または AskUserQuestion結果 YYYY-MM-DD]` / `[ADR: <spec名>#<台帳項目>]` / `[agent前提]`。ラベル無し・引用不能な行は自動的に `[agent前提]` 扱いで免責力を持たない（`references/integration-rules.md` §6）。ユーザー裁定の出所はspec/planの判断台帳（ADRセクション）から引く（台帳がSSOT）。

## Step 2: 機械チェック＋Codex起動＋Workflow args（本体）

1. **機械チェック統一窓口**（orchestrator-steps.md Step 2 と同じ1コマンド。`summary.errors` が空でないまま先へ進まない）:

       python3 .claude/skills/moores-code-review/scripts/check_all.py "<PATCH_PATH>" --repo-root "$(pwd)" --context "<USER_PROMPT_PATH>" > <$RUNDIRの実値>/checks.json
       python3 .claude/skills/moores-code-review/scripts/split_chunks.py "<PATCH_PATH>" > <$RUNDIRの実値>/chunks.tsv

2. **Codex 3本をバックグラウンド起動** — orchestrator-steps.md の **Step 3 節だけ**を Read して実行する（`codex_preflight.py` で実体パスを解決 → 3テンプレを埋めて `codex exec --sandbox read-only --skip-git-repo-check -o <$RUNDIR>/codex-<名前>.final.md - < <$RUNDIR>/codex-<名前>.md > <$RUNDIR>/codex-<名前>.out.md` を `run_in_background`。preflight が exit 10/11 なら `status` 文字列つきで縮退を報告）。完了待ちは Workflow 内の待機係が行う。
3. **Workflow args を組み立てる**（選択・命名・contract.md 生成はここで完結。`--base-ref` は Step 1 の base コミット）:

       python3 .claude/skills/moores-code-review/scripts/build_workflow_args.py --run-dir <$RUNDIRの実値> --patch "<PATCH_PATH>" --context "<USER_PROMPT_PATH>" --repo-root "$(pwd)" --base-ref <base SHA>

   report-only（pr-independent-review）では `--report-only --detchecks <detchecks.json>` を足す。

   `build_workflow_args.py` は `--run-dir` に `workflow-args.json` と、それを埋め込んだ Workflow スクリプト `review_workflow.js` を生成する。`> workflow-args.json` 禁止。Workflow スクリプトはファイルシステムも import も持たないため、部品 `scripts/review_workflow/*.js` を結合した `$RUNDIR/review_workflow.js` を `scriptPath` にする（部品を直接渡さない）。

## Step 3.5〜6.5: Workflow で実行

Step 2-3 が書いた結合済みスクリプトを `scriptPath`（絶対パス）に渡して起動する。args は埋め込み済みなので渡さない（巨大な JSON を会話に載せない）:

    Workflow({ scriptPath: "<$RUNDIRの実値>/review_workflow.js" })

完了通知を受けたら **`integrated.md` を Read する（この1ファイルだけ）**。`agents/`・Codex `.out.md` は読まない（疑義のある個別件の再確認のみ例外）。返り値の `missing`・`fallbacks`・`apply.compile`・`postCheckSelection.note`・`postfix.warnings/infos` は Step 7 の報告へ転記する。Workflow が例外で止まった場合（integrator/apply の応答なし）は `$RUNDIR` の残骸を引き継ぎ、同じ `scriptPath` に `resumeFromRunId` を付けて再起動する（完了済みの体はキャッシュ。`build_workflow_args.py` を再実行して書き直すとキャッシュが外れうるので、再開時は再実行しない）。最初からやり直さない。返り値の `apply.verify_note` と `refix.rounds[].verify`（機械的動作確認の結果）も Step 7 の報告へ転記する。

### Codex ホストでの実行（Workflow の代替ランタイム）

Codex には Workflow も Agent も無いため、同じ `$RUNDIR/review_workflow.js` を `scripts/codex_workflow_runner/run.mjs` で実行する。runner はスクリプト本文をそのまま評価し、`agent()` 1回を `codex exec` 1プロセス（`--output-schema` で構造化出力・`-o` で結論）に置き換えるだけなので、系統の選択・発火数・再起動・統合・適用は Claude の Workflow 実行と一致する（`tests/test_codex_workflow_runner.py` が模擬実行と突き合わせる）。モデル階層（opus/fable/sonnet/haiku）は `codex_model_map.json` で codex の推論強度へ写す。

1. Step 2-2 は Codex 3本のプロンプトを `$RUNDIR/codex-<名前>.md` に書くところまで行い、起動しない（runner が未起動のものだけ切り離して起動する）。
2. 子の codex exec が対象リポジトリと `$RUNDIR` へ書き、さらに codex を起動するため、本体はサンドボックス外（`danger-full-access`）で動いている必要がある。サンドボックスで子の起動が拒否されたら縮退せず止め、その旨を報告する。
3. 前景で1コマンド実行し、終わるまで待つ（所要は系統数と同時数次第で数十分。バックグラウンドに回してポーリングしない）:

       node .agents/skills/moores-code-review/scripts/codex_workflow_runner/run.mjs <$RUNDIRの実値>/review_workflow.js

4. 標準出力（＝`$RUNDIR/codex-runner/result.json`）が Workflow の返り値にあたる。`fireCount`・`failedFires` と `result.systems` を「回収時の突合」と同じ規則で見て、Step 7 の報告冒頭に「runner: 発火 N 体（失敗 M）」を書く。終了コード 3 は Workflow 本文の例外（`error` に理由）で、`$RUNDIR` を残したまま同じコマンドで再実行してよい（キャッシュは無いので全系統が再発火する）。
5. Step 7 の AskUserQuestion が使えない非対話実行（`codex exec`）では、設計判断と `[解釈]` Warning を選択肢付きで報告に列挙して止める（自分で裁定しない）。

### 旧既定: sonnet 委譲（Workflow 不可時のフォールバック・2026-08-18）

派遣プロンプト（Agent ツール・`model: "sonnet"` 明示・1体。派遣は subagent 深度を1消費する）:

```
moores-code-review のオーケストレータとして動け。
Read this : <リポジトリ絶対パス>/.claude/skills/moores-code-review/references/orchestrator-steps.md
実行範囲 : Step 2〜6.5（Step 0〜1 は完了済み。Step 7 の報告・AskUserQuestion・記録は親が行う）
Run dir : <$RUNDIRの実値> / Patch path : <PATCH_PATH> / User prompt : <USER_PROMPT_PATH> / Repo root : <リポジトリ絶対パス>
追加契約 : 手順書のモデル割当・出力契約・Gotchas に従い、再委譲しない。設計判断は適用せず <$RUNDIRの実値>/design.md に「症状→原因→推奨と選択肢」で書く（0件なら「なし」）。$RUNDIR 配下は削除しない。
返答 : 系統数(起動・回収・欠員) / Critical・Warning・Info・suppressed 件数 / 適用修正数 / コンパイル・テスト結果 / integrated.md と design.md のパス。指摘本文は書かない。
```

オーケストレータが返答せず死んだら、$RUNDIR の残骸を引き継いで再派遣する（テンプレに「$RUNDIR 内の完了済み工程はスキップして続きから」と1行足す）。

## Step 7: 報告＋AskUserQuestion ⑥

1. **統合報告** — Critical/Warning/Info件数、各指摘の出所（決定論/reviewer名/Codex/Fable/N系統一致）、適用した修正、コンパイル・テスト結果。Warningは1件1行で全件載せる（保険としてコンテキストに乗せるのが目的。黙って落とさない）。Infoは末尾に圧縮列挙。raw出力やレビュー表をそのまま貼らない。Codex/Fableをスキップした場合はその旨を明記。
   - **「免責で消された指摘」セクション必須**: 各観点の `suppressed:` 節を固定形式 `- [Critical|Warning] <指摘要約> — suppressed-by: <トレードオフ1行, 出所ラベル>` で列挙する（元の重大度を行頭に保持。0件なら「suppressed: 0件」と明記）。§2.6参照。
   - **Warning の行き先を必ず決める**: 行頭 `[解釈]` の Warning（依頼の解釈そのものを疑うもの・`references/integration-rules.md` §2.5）は、PR 作成前に必ず下の 2. の AskUserQuestion で確認する。**自律モード（AGENTS.md の「確認を求めず自律的に」・無人実装の質問禁止を含む）でも聞く対象**で、「後で聞く」「報告に載せるだけ」に回さない（AskUserQuestion 自体が使えない無人経路では PR 本文冒頭に「依頼解釈 未確認」として書く）。それ以外の Warning は Step 7.5 の bug-pass へ持ち込まれ、実害を再現できれば Critical として直され、できなければ落ちる（どちらでも報告からは消さない）。
2. **保留した設計判断と `[解釈]` Warning だけ**をAskUserQuestionで選択肢付き一括提示（0件ならスキップ。`[解釈]` Warning の設問の症状は「依頼の意図が逆だった場合に起きること」）。回答に従い適用（§5の安全規則・検証を再適用）。裁定結果の適用は、1〜2箇所の機械的な直しなら本体が最小Edit、まとまった量なら fix subagent（`model: "sonnet"`）1体に design.md のパス+裁定を渡す。
   - **例外: SDD の単一subagent実装モードから呼ばれた場合**（`moores-subagent-driven-development` の規模ゲート未満の派遣を経てこのレビューに来た場合）は、**裁定反映の fix subagent を `model: "opus"` とし、本体による最小Editは行わない**（量が1〜2箇所でも fix subagent に渡す）。ADR 0053「本体セッションは実装コードを書かない」を最終レビュー局面でも守り切るため。通常の呼び出しでは従来どおり本体の最小Edit or fix subagent（`sonnet`）。
   - **裁定反映 diff の再レビュー（Refix・Step 6 と同じ手順）**: 裁定を適用する**前**に `python3 .claude/skills/moores-code-review/scripts/refix_snapshot.py snapshot --repo-root "$(pwd)" --run-dir $RUNDIR --name w7-s0` を取り、続けて `python3 .claude/skills/moores-code-review/scripts/applied_diff_checks.py record --repo-root "$(pwd)" --run-dir $RUNDIR --name w7-s0 --if-missing`（反映前の録画シナリオ診断）を記録し、適用後に `--name w7-s1` → `refix_snapshot.py diff --from w7-s0 --to w7-s1 --out $RUNDIR/refix/w7-round1.diff` で反映 diff と `scope` を得て、その diff に `references/orchestrator-steps.md` の「反映 diff の機械的動作確認」を回す（`check --from w7-s0 --to w7-s1` で録画シナリオ全件の反映前後のコンパイル診断を比べ、増えた診断だけ追従修正・セーブ往復テスト。直し直すたびに取る `w7-round<N>.diff` にも `--from w7-s<N-1> --to w7-s<N>` で回す）。`source` なら `post-checks/applied-diff-correctness.md`（`model: "opus"`・Step 4 と同じ5行契約＋`Refix of : design.md の該当裁定`・Patch path = その diff・報告先 `agents/refix-correctness-w7-r1.md`）を1体起動する（理由は同ファイル冒頭。Step 6 側は Workflow の Refix フェーズが同じ手順を回す）。Critical は直して `w7-s2` を取り直し直した差分だけで再実行（最大3周。機械的でなければ再度 AskUserQuestion）、上限超過・適用0件は未収束として報告冒頭に明記、Warning/Info は最終報告へ。`non-source`/`none` なら起動せず報告に1行。
   - **設計判断を反映した diff の構造レビュー（2026-09-20 導入）**: 上の反映 diff が型・スキーマ・公開シグネチャを新設または変更していれば、`reviewers/core-cs-centralization-duplication.md`（`.ts`/`.tsx` のみなら `core-ts_tsx-centralization-duplication.md`）を同じ diff・同じ5行契約で opus 1体起動する（報告先 `agents/refix-structure-w7-r1.md`）。裁定を反映した diff は新しい設計そのものだが、applied-diff-correctness は設計に言及しない。Critical は上と同じ扱い、`設計判断: あり` は最終報告へ案の形ごと載せる。根拠（2026-09-20 実測・後知恵なし opus 各1体）: 当時の反映diffへこの reviewer を当てるリプレイ4回のうち3回が、本番レビューが見逃してマージ済みの実在Critical（`BiomeObjectConfigRuntimeApplier` がバイオーム列挙を4箇所目に増やし、「1箇所化」という導入理由を導入物自身の文字列 switch が打ち消している件）を独立に検出した。一方この工程の契機となった bands 二重定義そのものは 4回中1回しか出ない — **特定の指摘の再発防止ではなく、反映diffに残る設計欠陥一般への網として入れている**（reviewer の焦点は毎回揺れるので、1回の検出を当てにしない）。
   - **載せてよいのは本質的な設計判断のみ**（アーキテクチャ・パターン選択・スコープ影響・両立不能な指摘・サブエージェントの `設計判断: あり`）。**載せるの禁止**: コメントの短縮・文体（convention-guard が自己完結）、200行超過・ファイル分割（努力目標・報告のみ）。混ぜた時点で規約違反（ユーザー裁定 2026-07-23）。
   - **design.md に無い選択肢を本体が足すのも禁止** — 「現状維持」「別issueへ」を付け足さない。推奨は design.md の「（推奨）」に揃える（推奨の決め方の正本は `references/integration-rules.md` §4）。
   - **設問は「症状 → 原因 → 推奨」の順**（ユーザー裁定 2026-08-03）。書き出しはコードを読まなくても分かるゲーム上・開発上の症状、原因は1〜2行、推奨を第1選択肢に置き末尾に `（推奨）`、各選択肢に症状が消える理由を1行。観点名・レビュアー名・「N系統一致」は設問の主役にせず報告本文へ。症状を1文で書けない指摘は設問にせず Warning へ落とす。判定基準: **その設問だけを読んだ人が、コードを開かずに選べるか**。
3. **レビュー記録を生成する**（Step 7.5 の bug-pass が終わってから。bug-pass の結果も同じ記録に書く） — 記録はコードrepoでなく記録repo `$LOGS`（`../moorestech_logs`）へ書く（featureブランチが記録に触れてマージ衝突する構造を断つため。コードrepo側へ書き戻さない）。`$LOGS/harness/moores-code-review/records/TEMPLATE.md` に従い `$LOGS/harness/moores-code-review/records/YYYY-MM-DD-<topic>.md` を書く（対象SHA2つ・系統別1行判定表・適用修正・AskUserQuestion裁定・破棄指摘・セッションID）。diff本体は保存せずbase/head SHAのみ（dirty込みなら注記＋`--stat`要約）。同ブランチの再レビューは`-r2`付き新ファイル。`$LOGS/harness/moores-code-review/eval-log.md` に集計1行＋記録への相対リンクを足す。
4. **`$RUNDIR` 配下は削除しない**（旧版は `/tmp` の一時ファイルを消す規定だった）。patch/context/audit×3/checks×2/最終diffは、記録が主張するverdictの実入力であり、消すと後から「何をどう測ってその結論に至ったか」を再現できない。記録本文に `- rundir: runs/<ts>/` の1行を入れて、記録から実入力へ辿れるようにする。

## Step 7.5: 最終バグ確認（bug-pass）

Step 7 の裁定反映（w7 の Refix を含む）が収束した後、記録・PR 作成の前に**1回だけ**回す。裁定0件でも回す。report-only（pr-independent-review からの呼び出し）では回さない。最終 head の PR 全体 diff を、正しさ（誤動作）を見る系統だけで見直し、context の免責を効かせず、前周の Warning を「再現できれば Critical・できなければ落とす」で判定し直す（根拠: `$LOGS/harness/pr-independent-review/experiments/2026-10-06-value-audit/report.md`）。系統の選択は `scripts/workflow_args/bug_pass.py`、統合規則は `references/integration-rules.md` §7、Workflow 内の差分は orchestrator-steps.md「bug-pass モードの差分」が正本。

1. `BPDIR=<$RUNDIRの実値>/bug-pass` を作り、Step 1 と同じ base で末尾を最終 HEAD にした PR 全体 diff を `review_diff.py` 経由で `$BPDIR/patch.diff` に書く（未コミット分があれば `--cached`・引数なしも連結。修正コミットだけに絞らない）。
2. Codex はバグ狩り1本だけ: `scripts/codex-bughunt-template.md` を最終 diff で埋めて `$BPDIR/codex-bughunt.md` に書き（4カテゴリ context は「目指す」「尊重すべき制約」だけ貼る）、Step 2-2 と同じ形で `-o $BPDIR/codex-bughunt.final.md` を付けて `run_in_background` で起動する。
3. args とスクリプトを作る:

       python3 .claude/skills/moores-code-review/scripts/build_workflow_args.py --bug-pass --carry-from <$RUNDIRの実値> --run-dir $BPDIR --patch $BPDIR/patch.diff --context <USER_PROMPT_PATH> --repo-root "$(pwd)" --base-ref <base SHA>

4. `Workflow({ scriptPath: "$BPDIR/review_workflow.js" })` で起動し、完了後 `$BPDIR/integrated.md` を Read する。回収時の突合は上と同じ（`systems.expected` は bug-pass の選択から計算される。s5_shape_gate は対象外）。`integrated.design_items` は常に0で、AskUserQuestion は出さない。
5. Critical（`再現:` 付き）は Workflow の Apply → Refix で直っている。報告に追記するのは、bug-pass の Critical と適用結果・持ち込み Warning の格上げ/破棄の件数・残った Warning（1件1行）・`verify_note`。`refix.unresolved` が true なら Step 7 と同じく報告冒頭に明記し AskUserQuestion「手で直す / 未修正のまま進める」を載せる。

## Gotchas（本体側）

- **4カテゴリcontextを埋めないとreviewerが誤検知する** — 空contextは「合意なし」と解釈され既定Criticalが出る。
- **AskUserQuestionは末尾だけ** — 確定修正の途中で割り込まない。
- **人間指摘の見逃しが出たら** — その場で観点をいじらず `references/skill-improvement.md` の手順（フォレンジック・リプレイ診断→対策→4段階検証）に従う。
- 実行系のGotchas（codexフラグ順序・verifier発火条件・モデル継承事故・fableクォータ・生出力を読まない等）の正本は `references/orchestrator-steps.md` — インラインで回すときは必ずそちらを読む。

## スキル自体の改善

観点の追加・改稿・人間指摘の見逃しへの対応・有効性測定は `references/skill-improvement.md` を読む（通常のレビュー実行では読まない）。
