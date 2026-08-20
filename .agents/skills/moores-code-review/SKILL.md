---
name: moores-code-review
description: |
  moorestechのPR作成前・マージ前レビューを単体で完結させる統合スキル。6系統を並列実行する:
  ①決定論チェック（汎用+moorestech固有の機械判定）②moores設計レンズ群（ドメイン境界・サーバー状態同期3点セット・
  DataStore分離・マスタデータ防御・型構造・前例一致）③汎用reviewer群（汎用コード品質の採用実績ある観点＋webui向けts/tsx設計観点）
  ④Codex外部監査 ⑤Fable全般レビュー ⑥分割深掘り調査（大規模PR時のみ・10-15ファイル/チャンクで全文精読）。
  指摘を実コード照合・重複排除のうえ統合し、機械的修正を自動適用、
  設計判断だけ末尾でAskUserQuestion。設計レンズと汎用レビュー機構を1本に束ね、これ単体でレビューが完結する。
  既定ではStep 3.5〜6.5をWorkflowツール（scripts/review_workflow.js）で決定論的に実行する（2026-08-20。本体は対象確定・機械チェック・Codex起動・AskUserQuestionのみ。sonnet委譲はWorkflow不可時のフォールバック）。
  Use when:
  1. moorestechでPR作成前・マージ前のレビューを行う時（pr-create前に必ず1パス）
  2. subagent-driven-development の最終ブランチレビューを行う時
  3. 「moores-code-reviewで」「moorestechレンズでレビュー」「設計レンズを通して」「コードレビューして」と言われた時
---

# moores-code-review

moorestechのコードレビューを **決定論チェック → 6系統の並列レビュー → 実コード照合・重複排除 → 自動適用 → 報告** の順で単体完結させる（外部スキルへの依存なし）。

**この SKILL.md は本体セッション用のディスパッチャである**（2026-08-18 分割・2026-08-20 Workflow化）。本体がやるのは Step 0〜2（対象確定・機械チェック・Codex起動・Workflow args）・Workflow 起動・Step 7（報告と AskUserQuestion）だけで、**Step 3〜6.5 の実行手順・6系統の詳細・モデル割り当て・実行系 Gotchas の正本は `references/orchestrator-steps.md`**、その実行形が `scripts/review_workflow.js` にある。本体が orchestrator-steps.md を通読するのはインライン実行(後述)の場合のみ。

系統の要約（詳細は orchestrator-steps.md）: ①決定論チェック(check_all.py・0トークン) ②mooresレンズ11本 ③汎用reviewer 30本 ④Codex外部監査3本 ⑤Fable全般 ⑥分割深掘り調査(16ファイル以上のみ) + 条件発火verifier + post-checks 2本 + opus integrator。

## Workflow実行（既定・2026-08-20）

**既定では Step 2 を本体が回し、Step 3.5〜6.5（系統の並列発火→統合→自動適用→post-check）を Workflow ツール（`scripts/review_workflow.js`）で実行する。** 2026-08-18〜20 の sonnet オーケストレータ委譲は、系統群 $164〜225/回 に対し **オーケストレータ1体が待機だけで $194〜240/回**（590〜625ターン・毎ターン25万トークン再送・`Concurrent subagent limit` の再起動16〜34回）を燃やしていた（`docs/research/2026-08-20-moores-code-review-diet-assessment.md`）。Workflow は待機が JS の `await` なのでこの項目が消え、「1メッセージ12体」「全員に model 明示」「起動失敗の再起動」「欠員の申告」「fable quota 時の opus fallback」が散文でなくコードで強制される。同じ `args` での再実行（`resumeFromRunId`）は完了済みの体をキャッシュから返すので、上限死からの再開で全系統をやり直さない。

**Workflow を使わず sonnet オーケストレータ委譲（旧既定）やインライン実行に落としてよいのは次の場合のみ**（報告冒頭に理由を明記。黙って切り替えない）: (a) Workflow ツールがこのセッションで使えない、(b) ユーザーが「委譲で」「インラインで」等を明示、(c) 自分が委譲オーケストレータとして派遣された側である。委譲時の派遣プロンプトは末尾「旧既定: sonnet 委譲」を、インライン時は `references/orchestrator-steps.md` を Read して Step 2〜6.5 を自分で実行する。

- **本体は Workflow 完了まで対象リポジトリを編集しない**（Apply フェーズで修正が適用されるため衝突する）。
- Codex 3本は Workflow の外（本体の Bash `run_in_background`）で先に投げる。スクリプトはシェルを持たないため。
- Workflow の同時実行数はランタイムが `min(16, CPU-2)` でキューイングする（Mac mini=8）。体数は減らさず所要時間だけ伸びる。

### 回収時の検死（本体・毎回必須）

Workflow の返り値（`systems.launched/recovered/missing/fallbacks`・`integrated`・`apply`・`postChecks`）を受けたら、報告する前に次を突き合わせる —
1. `systems.launched` が `workflow-args.json` の `systems` 数と一致し、`missing` が空か（空でなければ欠員として報告に転記。`agents/` は残っているので、欠員分だけ Agent で再起動→integrator だけ再派遣してよい）
2. `integrated.md` の「系統別回収状況」に欠員・未回収がないか。**Codex の欠員申告だけは転記前に裏を取る** — `codex_recover.py` の終了コード（3/4/5）が添えられていなければ自分で1コマンド走らせて確認し、exit 0 なら欠員ではないので integrator を再実行させる
3. `$RUNDIR` に規定の成果物（checks.json / workflow-args.json / contract.md / codex `.final.md` ×3 / `agents/` / integrated.md / final.diff / checks-final.json / design.md）が揃っているか

**何か変なこと（体数不一致・モデル割り当てが指定と違う・成果物の欠落・integrated.md 不在等）があれば、修正適用や再派遣を重ねる前に一旦止めて調査する。** 手順: セッション transcript（`~/.claude/projects/<プロジェクト>/<セッションID>/subagents/*.meta.json` で起動数とモデルを実測）→ 原因特定→再開の要否。原因がスキル記述の穴なら `references/skill-improvement.md` の手順で恒久対応する。異常のまま結果だけ採用しない（欠員のある統合結果は「全系統レビュー済み」を偽装する）。

## Step 0: 実行ディレクトリ `$RUNDIR` を作る

1回のレビューが作る生成物（patch・context・codex監査プロンプト3本・check_all出力・chunks・最終diff・最終detchecks）は
**すべて** `$LOGS/harness/moores-code-review/runs/<ts>/` 配下に置く。以下これを `$RUNDIR` と呼ぶ
（`$LOGS` は記録repo `../moorestech_logs`。`<ts>` は `YYYY-MM-DD-HHMM` 形式でレビュー1回につき1つ）。

    mkdir -p <$RUNDIRの実値>

- **`/tmp` には置かない** — OSに掃除されて消える。これらは記録（Step 7）が指すverdictの実入力であり、
  後から「何をどう測ってその結論になったか」を再現する唯一の材料。pr-independent-reviewのreconcileも
  ここを読む（あちらは `$LOGS/harness/pr-independent-review/runs/pr-<番号>/` を使う。混ぜない）
- ファイル名は固定: `patch.diff` / `context.md` / `checks.json` / `codex-audit.md` / `codex-bughunt.md` /
  `codex-design.md`（各Codexの**結論**は同名の `.final.md`＝`-o` の出力が正本、stdoutログは `.out.md`） / `chunks.tsv` / `agents/<名前>.md` / `integrated.md` /
  `final.diff` / `checks-final.json`
- `$RUNDIR` 配下はStop/SessionEnd hook（`.dev-hooks/logs-sync.mjs`）でlogs repoへ自動commit・pushされる。
  セッション側で `git commit` しない

## Step 1: レビュー対象と4カテゴリcontextを確定する

セッション文脈（何を作業したか・どんな裁定があったか）を知るのは本体だけなので、この Step は委譲できない。

1. **作業範囲を特定** — このセッションで生成・変更した成果物をコミット範囲・staged・unstagedから確定し、統合unified diffを `<$RUNDIRの実値>/patch.diff` に書く（**PATCH_PATH**）。`git diff <base>^..<last>` + `git diff --cached` + `git diff` を連結。ユーザーがレビュー範囲を明示したらそれを優先。
   - **プレイテストシナリオの除外（省略禁止）** — 各 `git diff` に必ず次のpathspecを付け、
     `unity-playmode-recorded-playtest` 配下の `.cs` をpatchへ入れない:

         -- . ':(exclude,glob)**/unity-playmode-recorded-playtest/**/*.cs'

     シナリオは実プレイを踏ませるための使い捨ての操作台本であり、プロダクトコードの規約（重複排除・
     命名・行数）で裁く対象ではない。指摘しても設計判断の裁定コストだけが増える
     （ユーザー裁定 2026-08-16 / PR#1137-F12）。`Client.Playtest` のDSL本体はこのパス外なので通常どおり見る
2. **4カテゴリcontextを書く** — `<$RUNDIRの実値>/context.md`（**USER_PROMPT_PATH**）に埋める。埋め忘れるとレンズ/reviewerがfalse-positiveを量産する:
   - **目指す（ゴール）** / **目指さない（非目標）** / **許容するトレードオフ** / **尊重すべき制約**
   - **4カテゴリは必ず `##` 見出しで書く**（太字箇条書き形式は出所ラベル検査の対象外になり沈黙故障する。見出しゼロはfail-closedでconfirmedになる）。
   - **「許容するトレードオフ」「非目標」の各行に出所ラベル必須**: `[ユーザー裁定: "発言引用" または AskUserQuestion結果 YYYY-MM-DD]` / `[ADR: <spec名>#<台帳項目>]` / `[agent前提]`。ラベル無し・引用不能な行は自動的に `[agent前提]` 扱いで免責力を持たない（`references/integration-rules.md` §6）。ユーザー裁定の出所はspec/planの判断台帳（ADRセクション）から引く（台帳がSSOT）。

## Step 2: 機械チェック＋Codex起動＋Workflow args（本体）

1. **機械チェック統一窓口**（orchestrator-steps.md Step 2 と同じ1コマンド。`summary.errors` が空でないまま先へ進まない）:

       python3 .claude/skills/moores-code-review/scripts/check_all.py "<PATCH_PATH>" --repo-root "$(pwd)" --context "<USER_PROMPT_PATH>" > <$RUNDIRの実値>/checks.json
       python3 .claude/skills/moores-code-review/scripts/split_chunks.py "<PATCH_PATH>" > <$RUNDIRの実値>/chunks.tsv

2. **Codex 3本をバックグラウンド起動**（orchestrator-steps.md Step 3 のとおり。`codex_preflight.py` で実体パスを解決し、不在/認証ファイル不在なら理由つきで縮退を報告）。
3. **Workflow args を組み立てる**（選択・命名・contract.md 生成はここで完結。`--base-ref` は Step 1 の base コミット）:

       python3 .claude/skills/moores-code-review/scripts/build_workflow_args.py --run-dir <$RUNDIRの実値> --patch "<PATCH_PATH>" --context "<USER_PROMPT_PATH>" --repo-root "$(pwd)" --base-ref <base SHA>

   report-only（pr-independent-review）では `--report-only --detchecks <detchecks.json>` を足す。

## Step 3.5〜6.5: Workflow で実行

`workflow-args.json` の中身（JSONオブジェクト）を `args` に渡して起動する（`scriptPath` は絶対パス）:

    Workflow({ scriptPath: "<リポジトリ絶対パス>/.agents/skills/moores-code-review/scripts/review_workflow.js", args: <workflow-args.json の中身> })

完了通知を受けたら **`integrated.md` を Read する（この1ファイルだけ）**。`agents/`・Codex `.out.md` は読まない（疑義のある個別件の再確認のみ例外）。返り値の `missing`・`fallbacks`・`apply.compile` は Step 7 の報告へ転記する。Workflow が例外で止まった場合（integrator/apply の応答なし）は `$RUNDIR` の残骸を引き継ぎ、同じ `scriptPath`＋`args` に `resumeFromRunId` を付けて再起動する（完了済みの体はキャッシュ）。最初からやり直さない。

### 旧既定: sonnet 委譲（Workflow 不可時のフォールバック・2026-08-18）

派遣プロンプト（テンプレをそのまま埋める。Agent ツール・`model: "sonnet"` 明示・1体。派遣は subagent 深度を1消費する）:

```
moores-code-review のオーケストレータとして動け。
Read this : <リポジトリ絶対パス>/.claude/skills/moores-code-review/references/orchestrator-steps.md
実行範囲 : Step 2〜6.5(Step 0〜1 は完了済み。Step 7 の報告・AskUserQuestion・記録は親が行う)
Run dir : <$RUNDIRの実値>
Patch path : <PATCH_PATH>
User prompt : <USER_PROMPT_PATH>
Repo root : <リポジトリ絶対パス>
追加契約 :
- orchestrator-steps.md の手順・出力契約・「1 メッセージ最大 12 体」「全員に model 明示」・Gotchas を逐語で守る。系統のモデルを自分の判断で変えない。
- 自分は委譲された側なので再委譲しない。
- Step 6 の自動適用・uloop compile・Step 6.5 の再チェックと post-checks まで実施する。設計判断は適用せず保留。
- 設計判断を <$RUNDIRの実値>/design.md に書く(1 件ごとに 症状→原因→推奨と選択肢。コードを開かずに選べる形)。0 件なら「なし」とだけ書く。
- $RUNDIR 配下のファイルは削除しない。
- 待機は Monitor の無出力 until ループ1回で行い、echo/sleep の連打でターンを回さない。
- 返答は 10 行以内: 系統数(起動・回収・欠員) / Critical・Warning・Info・suppressed 件数 / 適用した修正数 / コンパイル・テスト結果 / integrated.md と design.md の 2 パス。生の指摘本文は返答に書かない。
```

オーケストレータが返答せず死んだら、$RUNDIR の残骸を引き継いで再派遣する（テンプレに「$RUNDIR 内の完了済み工程はスキップして続きから」と1行足す）。

## Step 7: 報告＋AskUserQuestion ⑥

1. **統合報告** — Critical/Warning/Info件数、各指摘の出所（決定論/レンズ名/reviewer名/Codex/Fable/N系統一致）、適用した修正、コンパイル・テスト結果。Warningは1件1行で全件載せる（保険としてコンテキストに乗せるのが目的。黙って落とさない）。Infoは末尾に圧縮列挙。raw出力やレビュー表をそのまま貼らない。Codex/Fableをスキップした場合はその旨を明記。
   - **「免責で消された指摘」セクション必須**: 各観点の `suppressed:` 節を固定形式 `- [Critical|Warning] <指摘要約> — suppressed-by: <トレードオフ1行, 出所ラベル>` で列挙する（元の重大度を行頭に保持。0件なら「suppressed: 0件」と明記）。§2.6参照。
2. **保留した設計判断だけ**をAskUserQuestionで選択肢付き一括提示（0件ならスキップ）。回答に従い適用（§5の安全規則・検証を再適用）。裁定結果の適用は、1〜2箇所の機械的な直しなら本体が最小Edit、まとまった量なら fix subagent（`model: "sonnet"`）1体に design.md のパス+裁定を渡す。
   - **載せてよいのは本質的な設計判断のみ**: アーキテクチャ・パターン選択（多態化/型分割/移動先クラス）・スコープ影響・両立不能な指摘、およびサブエージェントの `設計判断: あり` 項目。
   - **載せるの禁止**: コメントの短縮・文体（convention-guardが自己完結）、200行超過・ファイル分割（努力目標・報告のみ）。この2種は選択肢に混ぜた時点で規約違反。
   - **設問は「症状 → 原因 → 推奨」の順で書く（ユーザー裁定 2026-08-03）**。設問本文の書き出しは**ゲーム上・開発上で実際に何が起きるか**にする。「列車に乗ったままゲームを終了して起動すると、自機が列車の上ではなく地面に落ちていて、その位置がセーブされる」のように、**コードを読まなくても分かる症状**から始めること。原因は1〜2行に圧縮する。
     - **推奨を必ず第1選択肢に置き、ラベル末尾に `（推奨）` を付ける**。各選択肢の説明には「これを選ぶと症状がなぜ消えるか」を1行入れる。トレードオフだけを並べない。
     - **禁止**: 観点名・レビュアー名（`caller-orchestration` 等）・レンズ用語・「N系統一致」を設問本文の**主役**にすること。出所は報告本文へ書き、設問には持ち込まない。メソッド名や行番号の羅列だけで問題を説明したことにしない。
     - **症状を1文で書けない指摘は設問にしない** — 報告本文のWarningへ落とす。「将来こう書き換えると壊れる」型は、症状（何が壊れるか）と再現条件を書けるときだけ設問にしてよい。
     - 判定基準: **その設問だけを読んだ人が、コードを開かずに選べるか**。選べないなら書き直す。
3. **レビュー記録を生成する** — 記録はコードrepoでなく記録repo `$LOGS`（`../moorestech_logs`）へ書く（featureブランチが記録に触れてマージ衝突する構造を断つため。コードrepo側へ書き戻さない）。`$LOGS/harness/moores-code-review/records/TEMPLATE.md` に従い `$LOGS/harness/moores-code-review/records/YYYY-MM-DD-<topic>.md` を書く（対象SHA2つ・系統別1行判定表・適用修正・AskUserQuestion裁定・破棄指摘・セッションID）。diff本体は保存せずbase/head SHAのみ（dirty込みなら注記＋`--stat`要約）。同ブランチの再レビューは`-r2`付き新ファイル。`$LOGS/harness/moores-code-review/eval-log.md` に集計1行＋記録への相対リンクを足す。
4. **`$RUNDIR` 配下は削除しない**（旧版は `/tmp` の一時ファイルを消す規定だった）。patch/context/audit×3/checks×2/最終diffは、記録が主張するverdictの実入力であり、消すと後から「何をどう測ってその結論に至ったか」を再現できない。記録本文に `- rundir: runs/<ts>/` の1行を入れて、記録から実入力へ辿れるようにする。

## Gotchas（本体側）

- **4カテゴリcontextを埋めないとレンズ/reviewerが誤検知する** — 空contextは「合意なし」と解釈され既定Criticalが出る。
- **AskUserQuestionは末尾だけ** — 確定修正の途中で割り込まない。
- **人間指摘の見逃しが出たら** — その場で観点をいじらず `references/skill-improvement.md` の手順（フォレンジック・リプレイ診断→対策→4段階検証）に従う。
- 実行系のGotchas（codexフラグ順序・verifier発火条件・モデル継承事故・fableクォータ・生出力を読まない等）の正本は `references/orchestrator-steps.md` — インラインで回すときは必ずそちらを読む。

## スキル自体の改善

観点の追加・改稿・人間指摘の見逃しへの対応・有効性測定は `references/skill-improvement.md` を読む（通常のレビュー実行では読まない）。
