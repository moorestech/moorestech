---
name: moores-code-review
description: |
  moorestechのPR作成前・マージ前レビューを単体で完結させる統合スキル。5系統を並列実行する:
  ①決定論チェック（汎用+moorestech固有の機械判定）②moores設計レンズ群（ドメイン境界・サーバー状態同期3点セット・
  DataStore分離・マスタデータ防御・型構造・前例一致）③汎用reviewer群（汎用コード品質の採用実績ある23観点）
  ④Codex外部監査 ⑤Fable全般レビュー。指摘を実コード照合・重複排除のうえ統合し、機械的修正を自動適用、
  設計判断だけ末尾でAskUserQuestion。設計レンズと汎用レビュー機構を1本に束ね、これ単体でレビューが完結する。
  Use when:
  1. moorestechでPR作成前・マージ前のレビューを行う時（pr-create前に必ず1パス）
  2. subagent-driven-development の最終ブランチレビューを行う時
  3. 「moores-code-reviewで」「moorestechレンズでレビュー」「設計レンズを通して」「コードレビューして」と言われた時
---

# moores-code-review

moorestechのコードレビューを **決定論チェック → 5系統の並列レビュー → 実コード照合・重複排除 → 自動適用 → 報告** の順で単体完結させる。汎用コードレビュー機構（reviewer群・Codex監査・Fable全般・post-checksコメント監査）を設計レンズと同居させ、これ1本で完結する（外部スキルへの依存なし）。

## 5系統の構成

1. **決定論チェック**（`scripts/deterministic_checks.py`）— AGENTS.md・moorestech規約の機械判定分（partial・try-catch・200行・10ファイル・デフォルト引数・SerializeField命名・比較演算子・コメント長・region・master_default_fallback・packet_response_root・schema_optional_true・event_tag_sync）。0トークン。
2. **moores設計レンズ群**（`lenses/`・7本）— moorestech固有の設計規約。実PRレビュー指摘（PR978/987/988/996/997/1000）由来。
3. **汎用reviewer群**（`reviewers/`・23本）— 言語横断のコード品質。全数調査（63セッション/1029起動）で採用実績のある観点のみ採録（採用0/冗長の20本と決定論代替1本は除外。根拠は `scripts/model_map.json` の `_excluded_from_port`）。
4. **Codex外部監査**（`scripts/codex-audit-template.md`）— 別モデルCLIの独立第三者視点。
5. **Fable全般レビュー**（`generalists/fable-holistic-review.md`）— チェックリスト非依存の俯瞰監査。自己裏取り契約。

## 実行順序（厳守）

> **① 決定論チェック → ② Codex監査をバックグラウンド起動 → ③ レンズ群＋reviewer群＋Fable全般＋（候補があれば）比較演算子verifierを1メッセージで並列起動 → ④ 全系統を回収・実コード照合・重複排除 → ⑤ 機械的修正を自動適用＋コンパイル → ⑤.5 最終diffで決定論再チェック＋コメント保全post-checks 2本 → ⑥ 報告＋設計判断のみAskUserQuestion（末尾集約）**

AskUserQuestionは**最後の報告フェーズに集約**する。修正適用の途中で割り込まない。

## Step 1: レビュー対象と4カテゴリcontextを確定する

1. **作業範囲を特定** — このセッションで生成・変更した成果物をコミット範囲・staged・unstagedから確定し、統合unified diffを `/tmp/moores-review-patch-<ts>.diff` に書く（**PATCH_PATH**）。`git diff <base>^..<last>` + `git diff --cached` + `git diff` を連結。ユーザーがレビュー範囲を明示したらそれを優先。
2. **4カテゴリcontextを書く** — `/tmp/moores-review-context-<ts>.md`（**USER_PROMPT_PATH**）に埋める。埋め忘れるとレンズ/reviewerがfalse-positiveを量産する:
   - **目指す（ゴール）** / **目指さない（非目標）** / **許容するトレードオフ** / **尊重すべき制約**
   - 自分の判断は「（自分の判断として）」と明記し「ユーザー合意済み」と偽装しない（`references/integration-rules.md` §6）。

## Step 2: 決定論チェック ①

```bash
python3 .claude/skills/moores-code-review/scripts/deterministic_checks.py "<PATCH_PATH>" --repo-root "$(pwd)" > /tmp/moores-review-detchecks-<ts>.json
```

- **`confirmed`**（partial・try-catch・デフォルト引数・SerializeField命名・200行・10ファイル・master_default_fallback・packet_response_root）— 検出正確・裏取り不要。Criticalとして統合に直接載せる（修正の適用可否は §3/§4）。
- **`candidates.comparison_operator`** — 1件以上あればStep 3で比較演算子verifier（sonnet）を並列起動。0件なら起動しない。
- **`candidates.comment_length` / `region_internal`** — この時点では保持のみ（commentはStep 5.5で最終diffに再計測、regionはregion-internal reviewerの裏付け）。
- **`candidates.schema_optional_true`** は master-data-defense レンズ、**`candidates.event_tag_sync`** は server-state-sync レンズの裏付けデータとして渡す（正当な例外がありうるためレンズが裁定）。

## Step 3: Codex外部監査をバックグラウンド起動する ②

`scripts/codex-audit-template.md` を埋めて `/tmp/moores-review-audit-<ts>.md` に書き、バックグラウンド起動する:

```bash
codex exec --sandbox read-only --skip-git-repo-check - < /tmp/moores-review-audit-<ts>.md
```

Bashの `run_in_background: true` で起動しシェルIDを控える。観点デフォルト3つ: (1)アーキテクチャ的不整合・既存パターン乖離 (2)設計妥当性・将来の懸念 (3)致命的不具合・エンバグ・リグレッション。`which codex` が失敗したら本Stepをスキップし、その旨を最終報告に明記する（黙って縮退しない）。

## Step 4: レンズ群＋reviewer群＋Fable全般＋verifierを並列発火する ③

2つのセレクタにPATCHの絶対パスを渡し、発火対象とモデルを得る（出力は `パス<TAB>モデル` のTSV）:

```bash
python3 .claude/skills/moores-code-review/scripts/select_lenses.py "<PATCH_PATH>"
python3 .claude/skills/moores-code-review/scripts/select_reviewers.py "<PATCH_PATH>"
```

**1メッセージ内で並列に** 次を全部Agent起動する（順次起動は禁止）:

1. **各発火レンズ**（select_lensesのTSVどおりの `model`）— 3行契約:
   ```
   Read this : <レンズの絶対パス>
   Patch path : <PATCH_PATH>
   User prompt : <USER_PROMPT_PATH>
   ```
   `precedent-alignment.md`（always発火）は発火レンズが0件でも必ず起動する。
2. **各reviewer**（select_reviewersのTSVどおりの `model`）— 同じ3行契約。
3. **Fable全般レビュー**（常時・`model: "fable"`）— 同じ3行契約で `generalists/fable-holistic-review.md` を渡す。
4. **比較演算子verifier**（Step 2の `candidates.comparison_operator` が1件以上のときだけ・`model: "sonnet"`）— 4行契約:
   ```
   Read this : .claude/skills/moores-code-review/verifiers/comparison-operator-verifier.md
   Candidates : /tmp/moores-review-detchecks-<ts>.json
   Patch path : <PATCH_PATH>
   User prompt : <USER_PROMPT_PATH>
   ```

各サブエージェントは `Critical: あり/なし` + `修正方針: - <ファイル:行>: <何を直すか>` を返す。reviewer発火が0件でもレンズ群とFableは起動する。

## Step 5: 回収・実コード照合・重複排除 ④

- Step 4の全サブエージェント（レンズ・reviewer・Fable・verifier）の返却を受け取る。
- Step 3のバックグラウンドCodexの出力を回収する（未完了なら完了を待つ）。
- 全部揃うまでStep 6へ進まない。`references/integration-rules.md` §0〜§2 に従い、実コード照合・重複排除する（決定論confirmedは裏取り不要、Codex/Fable/レンズ/reviewerのCriticalはReadで裏取り、複数系統一致は「N系統一致（高確度）」に統合）。

## Step 6: 確定修正の自動適用＋コンパイル ⑤

`references/integration-rules.md` §3〜§5 に従う。要点:
- 具体名（ファイル/クラス/メソッド）と修正方針が挙がっていて選択の余地が無い機械的修正・単独系統cosmeticは、確認を挟まず自動適用する（デフォルト動作）。
- 設計判断（複数の妥当な選択肢・スコープ影響・アーキテクチャ変更・両立不能な指摘・decisionを要するCodex High/Medium）は適用せずStep 7へ保留。
- .csを修正したら `uloop compile --project-path ./moorestech_client` を実行しエラー0を確認する。

## Step 6.5: 決定論再チェック＋コメント保全post-checks ⑤.5

Step 6の修正適用後に走らせるpost-fixガード群。**人間の変更とStep 6で自分が適用した修正の両方**を検査する。`reviewers/` にもセレクタにも属さない別系統。

1. **最終diffを作り直す** — Step 6適用後の作業ツリーをbaseと比較し `/tmp/moores-review-final-<ts>.diff` に書く。
2. **決定論チェックを最終diffで再実行** — `deterministic_checks.py` を再度実行し `/tmp/moores-review-detchecks-final-<ts>.json` に書く。自分の修正が新たに生んだ `confirmed`/`comparison_operator` 違反はその場でインライン修正する。
3. **2本のガードを並列起動**（1メッセージ内）:
   - **comment-rationale-guard**（`model: "opus"`・3行契約）— load-bearingな根拠コメントがコード本体を残したまま削除・希薄化されていないか（削除行 `-` が対象）。`Read this : .claude/skills/moores-code-review/post-checks/comment-rationale-guard.md` + Patch path（最終diff）+ User prompt。
   - **comment-convention-guard**（`model: "sonnet"`・4行契約）— スクリプト計測の文字数超過候補の例外判定・短縮案 + 名前重複コメント検出。**文字数はスクリプトの値が正**。`Read this` + `Candidates : /tmp/moores-review-detchecks-final-<ts>.json` + Patch path（最終diff）+ User prompt。
4. **rationale-guardのCriticalはescalate**（自動復元しない）— 削除コメント再挿入は設計判断。復元タグ案を添えてStep 7へ。
5. **convention-guardはラベル分岐** — `機械的` は §5 のもと自動適用、`要判断` はStep 7へ。同一行で衝突したら**根拠保全を優先**。
6. 両ガードとも `Critical: なし` で再チェックも増分ゼロなら何もせずStep 7へ。

## Step 7: 報告＋AskUserQuestion ⑥

1. **統合報告** — Critical/Warning件数、各指摘の出所（決定論/レンズ名/reviewer名/Codex/Fable/N系統一致）、適用した修正、コンパイル・テスト結果。raw出力やレビュー表をそのまま貼らない。Codex/Fableをスキップした場合はその旨を明記。
2. **保留した設計判断だけ**をAskUserQuestionで選択肢付き一括提示（0件ならスキップ）。回答に従い適用（§5の安全規則・検証を再適用）。
3. `/tmp` の一時ファイル（patch/context/audit/detchecks×2/最終diff）を削除する。

## モデル割り当て

- **レンズ** — `select_lenses.py` の2列目（各レンズ先頭YAMLの `model`）をそのまま渡す。
- **reviewer** — `select_reviewers.py` の2列目（正は `scripts/model_map.json`。未記載reviewerはopus、`sonnet` 記載のみsonnet）。
- **Fable全般** — `model: "fable"` 固定。**比較演算子verifier・comment-convention-guard** — `sonnet`。**comment-rationale-guard** — `opus`（WHY判定は高ステークス）。
- Codex監査は別CLIなので対象外。

## 有効性の測定（eval/）

- **リプレイ評価**: `eval/fixtures.tsv` + `eval/make-fixture.sh` でレビュー当時のdiffを再生成し `eval/expected-findings.md` の期待検出と突合する。レンズ・reviewer・スクリプトを変更したら必ず1回流す。手順は `eval/README.md`。
- **前向きログ**: マージ済みPRごとに `eval/log.md` へ「人間指摘数・分類・ハーネス事前検出の有無・却下数」を記録する。

## Gotchas

- **4カテゴリcontextを埋めないとレンズ/reviewerが誤検知する** — 空contextは「合意なし」と解釈され既定Criticalが出る。
- **「並列」の実体はバックグラウンド起動** — Codexを `run_in_background` で先に投げ、完了を待たずにレンズ・reviewer・Fableを起動する。
- **`codex exec` のフラグ順序** — `--sandbox` `--skip-git-repo-check` はサブコマンドより**前**に置く。監査プロンプトは/tmpに置く（リポジトリ内は誤コミットの恐れ）。
- **verifierは候補ゼロなら起動しない** — `candidates.comparison_operator` が空なら比較演算子verifierは不要（0トークン）。
- **文字数はスクリプトの値が正** — LLMに日本語の文字数を数え直させない。convention-guardは `count` を信頼し例外判定と短縮案だけ行う。
- **post-checksはreviewerではない** — `post-checks/` はStep 6.5専用でセレクタのglobに含まれない。
- **Agent起動時に必ずmodel列を渡す（モデル継承事故の防止）** — Agentツールは `model` を省略すると**親（＝あなた＝オーケストレータ）のモデルを継承**する。あなた自身がfableで走っていると、model未指定のサブエージェントが誤ってfableで起動しうる。両セレクタはTSV2列目に**常に具体値**を出す（`select_lenses.py` はmodel未記載lensを `opus` に、`select_reviewers.py` は未記載reviewerを `default:opus` に具体化。空欄は絶対に出さない）。この2列目を**必ずそのまま** Agentの `model` に渡すこと。fableが正になるのは `precedent-alignment` レンズ（YAMLに `model: fable`）とFable全般（prose指定）だけで、それ以外にfableは現れない。
- **AskUserQuestionは末尾だけ** — 確定修正の途中で割り込まない。
- **観点の更新は指摘駆動で行う** — 新しい人間レビュー指摘が出たら、それを防げたはずのレンズ/reviewer（無ければ新規）へ実例として追記し、`eval/expected-findings.md` にも行を足す。
