---
name: moores-code-review
description: |
  moorestechのPR作成前・マージ前レビューを単体で完結させる統合スキル。6系統を並列実行する:
  ①決定論チェック（汎用+moorestech固有の機械判定）②moores設計レンズ群（ドメイン境界・サーバー状態同期3点セット・
  DataStore分離・マスタデータ防御・型構造・前例一致）③汎用reviewer群（汎用コード品質の採用実績ある観点＋webui向けts/tsx設計観点）
  ④Codex外部監査 ⑤Fable全般レビュー ⑥分割深掘り調査（大規模PR時のみ・10-15ファイル/チャンクで全文精読）。
  指摘を実コード照合・重複排除のうえ統合し、機械的修正を自動適用、
  設計判断だけ末尾でAskUserQuestion。設計レンズと汎用レビュー機構を1本に束ね、これ単体でレビューが完結する。
  既定ではStep 2〜6.5をsonnetオーケストレータsubagentに委譲して実行する（委譲実行・2026-08-18。本体は対象確定とAskUserQuestionのみ）。
  Use when:
  1. moorestechでPR作成前・マージ前のレビューを行う時（pr-create前に必ず1パス）
  2. subagent-driven-development の最終ブランチレビューを行う時
  3. 「moores-code-reviewで」「moorestechレンズでレビュー」「設計レンズを通して」「コードレビューして」と言われた時
---

# moores-code-review

moorestechのコードレビューを **決定論チェック → 6系統の並列レビュー → 実コード照合・重複排除 → 自動適用 → 報告** の順で単体完結させる（外部スキルへの依存なし）。

**この SKILL.md は本体セッション用のディスパッチャである**（2026-08-18 分割。委譲が既定になったため、実行手順の厚い正本を本体のコンテキストへ毎回注入しないようにした）。本体がやるのは Step 0〜1（対象確定）・委譲・Step 7（報告と AskUserQuestion）だけで、**Step 2〜6.5 の実行手順・6系統の詳細・モデル割り当て・実行系 Gotchas の正本は `references/orchestrator-steps.md`** にある。本体がそれを読むのはインライン実行(後述)の場合のみ。

系統の要約（詳細は orchestrator-steps.md）: ①決定論チェック(check_all.py・0トークン) ②mooresレンズ11本 ③汎用reviewer 30本 ④Codex外部監査3本 ⑤Fable全般 ⑥分割深掘り調査(16ファイル以上のみ) + 条件発火verifier + post-checks 2本 + opus integrator。

## 委譲実行（既定・2026-08-18）

**既定では Step 2〜6.5 を sonnet オーケストレータ subagent 1 体に委譲する。** 根拠実測（2026-08-18 両Mac transcript調査）: 系統群自体は $54〜139/回だが、派遣・回収の往復を opus/fable 本体で回すと監督だけで $60〜397 かかっていた（mini の実測で本体 $397 に対し実装系統 $3 の回さえある）。sonnet 委譲の初運転では監督 $7.1 で全16系統+integrator を欠員なく回収した。

**インラインで Step 2〜6.5 を自分で回してよいのは次の場合のみ**（報告冒頭に理由を明記。黙って切り替えない）: (a) ユーザーが「インラインで」等を明示、(b) Agent ツールが使えない / subagent 深度上限で系統が起動できない、(c) 自分が委譲オーケストレータとして派遣された側である。インライン時は `references/orchestrator-steps.md` を Read して Step 2〜6.5 を自分で実行する。

- 派遣は **`model: "sonnet"` 明示**・1 体だけ。**sonnet になるのはオーケストレータだけ**で、レンズ・reviewer・verifier・integrator のモデルはセレクタ/YAML/orchestrator-steps.md の指定のまま（オーケストレータの都合で落とすことは決してしない）。
- **本体はオーケストレータ完了まで対象リポジトリを編集しない**（Step 6 で修正が適用されるため衝突する）。
- 委譲は subagent 深度を 1 消費する。本体直下なら 本体→オーケストレータ→系統 の 3 層で収まる。
- オーケストレータが返答せず死んだら、$RUNDIR の残骸を引き継いで再派遣する（テンプレに「$RUNDIR 内の完了済み工程はスキップして続きから」と 1 行足す）。最初からやり直さない。

### 委譲の既知リスクと異常時の対応（導入時点 2026-08-18）

導入時点で委譲構成の実走実績は all-code-review 側の 1 回のみで、**moores 版の委譲は未実走**。同型構成で実証済みなのは subagent からの Agent 入れ子起動・Bash バックグラウンド・ファイルハンドオフ・integrator 委譲まで。moores 版に固有で未検証なのは: (1) codex 3 本のバックグラウンド並列、(2) subagent からの `uloop compile`、(3) **大規模 PR 時の investigator 込み 30 体級を sonnet 監督が wave 規律（1 メッセージ 12 体・「起動が黙って消える」対応・失敗体の再起動）どおり捌けるか**。

**回収時の検死（本体・毎回必須）**: オーケストレータの返答を受けたら、報告する前に次を突き合わせる —
1. 返答の系統数が期待値と一致するか（期待値 = checks.json の `lenses` + `reviewers` + `verifiers_to_launch` + Fable 1 + Codex 3 + 決定論。分割深掘り発火時は + チャンク数×3）
2. `integrated.md` の「系統別回収状況」に欠員・未回収がないか
3. `$RUNDIR` に規定の成果物（checks.json / codex `.out.md` ×3 / `agents/` / integrated.md / final.diff / checks-final.json / design.md）が揃っているか

**何か変なこと（規定数のエージェントが発火していない・モデル割り当てが指定と違う・成果物の欠落・integrated.md 不在・返答が契約と違う等）があれば、修正適用や再派遣を重ねる前に一旦止めて調査する。** 手順: セッション transcript（`~/.claude/projects/<プロジェクト>/<セッションID>/subagents/*.meta.json` で起動数とモデルを実測、`*.jsonl` で該当体の挙動を確認）→ 原因を特定してから再開の要否を決める。原因がスキル記述の穴なら `references/skill-improvement.md` の手順で恒久対応する。異常のまま結果だけ採用しない（欠員のある統合結果は「全系統レビュー済み」を偽装する）。

## Step 0: 実行ディレクトリ `$RUNDIR` を作る

1回のレビューが作る生成物（patch・context・codex監査プロンプト3本・check_all出力・chunks・最終diff・最終detchecks）は
**すべて** `$LOGS/harness/moores-code-review/runs/<ts>/` 配下に置く。以下これを `$RUNDIR` と呼ぶ
（`$LOGS` は記録repo `../moorestech_logs`。`<ts>` は `YYYY-MM-DD-HHMM` 形式でレビュー1回につき1つ）。

    mkdir -p <$RUNDIRの実値>

- **`/tmp` には置かない** — OSに掃除されて消える。これらは記録（Step 7）が指すverdictの実入力であり、
  後から「何をどう測ってその結論になったか」を再現する唯一の材料。pr-independent-reviewのreconcileも
  ここを読む（あちらは `$LOGS/harness/pr-independent-review/runs/pr-<番号>/` を使う。混ぜない）
- ファイル名は固定: `patch.diff` / `context.md` / `checks.json` / `codex-audit.md` / `codex-bughunt.md` /
  `codex-design.md`（各Codex**出力**は同名の `.out.md`） / `chunks.tsv` / `agents/<名前>.md` / `integrated.md` /
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

## Step 2〜6.5: オーケストレータへ委譲

派遣プロンプト（テンプレをそのまま埋める。Agent ツール・`model: "sonnet"` 明示・1 体）:

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
- 返答は 10 行以内: 系統数(起動・回収・欠員) / Critical・Warning・Info・suppressed 件数 / 適用した修正数 / コンパイル・テスト結果 / integrated.md と design.md の 2 パス。生の指摘本文は返答に書かない。
```

回収: 返答を受けたら **`integrated.md` を Read する（この 1 ファイルだけ）**。`agents/`・Codex `.out.md` は読まない（疑義のある個別件の再確認のみ例外）。返答の欠員・縮退は Step 7 の報告へ転記する。

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
