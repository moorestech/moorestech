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
2. **moores設計レンズ群**（`lenses/`・9本）— moorestech固有の設計規約。実PRレビュー指摘（PR978/987/988/996/997/1000）由来。
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

- **`confirmed`**（partial・try-catch・デフォルト引数・SerializeField命名・10ファイル・master_default_fallback・packet_response_root）— 検出正確・裏取り不要。Criticalとして統合に直接載せる（修正の適用可否は §3/§4）。
- **`confirmed` のうち200行超過（file-too-long）は努力目標** — Criticalにせず報告のWarning備考に1行載せるだけ。分割を強制せず、AskUserQuestionにも**絶対に**載せない（ユーザー裁定 2026-07-23）。
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

1. **各発火レンズ**（select_lensesのTSVどおりの `model`）— 3行契約＋共通出力契約:
   ```
   Read this : <レンズの絶対パス>
   Patch path : <PATCH_PATH>
   User prompt : <USER_PROMPT_PATH>

   出力契約（観点本文の出力フォーマットが二値でもこちらが優先）: 重大度3段階で返す。
   Critical: あり/なし — 確信をもって修正すべき違反。ありなら `修正方針: - <ファイル:行>: <直し方>` を列挙
   Warning: 0行以上 — 観点に該当しそうだが確信・裏取りが一段弱い指摘、重大だが裁量余地のある懸念。`- <ファイル:行>: <懸念と根拠>`
   Info: 0行以上 — 対応不要の観察・過検知ガードで落としたが記録価値のある事実。1行ずつ
   設計判断: あり/なし — 従来通り（代替案の具体形・シグネチャ付き比較）
   ```
   `precedent-alignment.md`（always発火）は発火レンズが0件でも必ず起動する。
2. **各reviewer**（select_reviewersのTSVどおりの `model`）— 同じ3行契約＋共通出力契約。
3. **Fable全般レビュー**（常時・`model: "fable"`）— 同じ3行契約＋共通出力契約で `generalists/fable-holistic-review.md` を渡す。
4. **比較演算子verifier**（Step 2の `candidates.comparison_operator` が1件以上のときだけ・`model: "sonnet"`）— 4行契約:
   ```
   Read this : .claude/skills/moores-code-review/verifiers/comparison-operator-verifier.md
   Candidates : /tmp/moores-review-detchecks-<ts>.json
   Patch path : <PATCH_PATH>
   User prompt : <USER_PROMPT_PATH>
   ```

各サブエージェントは上記の共通出力契約（Critical/Warning/Info＋設計判断）で返す。**二値（あり/なし）に潰さず3段階で出させる理由**: Warning/Infoは「とりあえず統合報告のコンテキストに乗る」ことが目的の保険であり、二値だと確信の一段弱い実指摘が `なし` に丸められて消失する（ユーザー裁定 2026-07-23。実例: リプレースファミリーのハードコードを複数レンズが視認しながら二値契約のため無出力で落とした）。`設計判断: あり` はCriticalでも備考でもない第3の出口で、Step 7のAskUserQuestionへ**必ず**載せる（備考落ちで黙殺しない）。reviewer発火が0件でもレンズ群とFableは起動する。

## Step 5: 回収・実コード照合・重複排除 ④

- Step 4の全サブエージェント（レンズ・reviewer・Fable・verifier）の返却を受け取る。
- Step 3のバックグラウンドCodexの出力を回収する（未完了なら完了を待つ）。
- 全部揃うまでStep 6へ進まない。`references/integration-rules.md` §0〜§2 に従い、実コード照合・重複排除する（決定論confirmedは裏取り不要、Codex/Fable/レンズ/reviewerのCriticalはReadで裏取り、複数系統一致は「N系統一致（高確度）」に統合）。
- **Warning/Infoの扱い**（§2.5）: Warningは破棄せず統合報告に必ず載せる（軽い照合のみ。複数系統が同一箇所をWarningした場合と、照合で事実が確定した場合はCriticalへ昇格）。Infoは照合不要で報告末尾に圧縮列挙する。どちらもAskUserQuestionには載せない。

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
5. **convention-guardはラベル分岐（Step 7へは送らない）** — `機械的` は §5 のもと自動適用、`要判断` は**ガード自身の裁定で完結**させる（短縮案が意図を保てるなら適用、例外該当なら残置。結果は報告に1行）。コメント短縮をAskUserQuestionに載せるのは**禁止**（ユーザー裁定 2026-07-23）。同一行で衝突したら**根拠保全を優先**。
6. 両ガードとも `Critical: なし` で再チェックも増分ゼロなら何もせずStep 7へ。

## Step 7: 報告＋AskUserQuestion ⑥

1. **統合報告** — Critical/Warning/Info件数、各指摘の出所（決定論/レンズ名/reviewer名/Codex/Fable/N系統一致）、適用した修正、コンパイル・テスト結果。Warningは1件1行で全件載せる（保険としてコンテキストに乗せるのが目的。黙って落とさない）。Infoは末尾に圧縮列挙。raw出力やレビュー表をそのまま貼らない。Codex/Fableをスキップした場合はその旨を明記。
2. **保留した設計判断だけ**をAskUserQuestionで選択肢付き一括提示（0件ならスキップ）。回答に従い適用（§5の安全規則・検証を再適用）。
   - **載せてよいのは本質的な設計判断のみ**: アーキテクチャ・パターン選択（多態化/型分割/移動先クラス）・スコープ影響・両立不能な指摘、およびサブエージェントの `設計判断: あり` 項目。
   - **載せるの禁止**: コメントの短縮・文体（convention-guardが自己完結）、200行超過・ファイル分割（努力目標・報告のみ）。この2種は選択肢に混ぜた時点で規約違反。
3. **レビュー記録を生成する** — `records/TEMPLATE.md` に従い `records/YYYY-MM-DD-<topic>.md` を書く（対象SHA2つ・系統別1行判定表・適用修正・AskUserQuestion裁定・破棄指摘・セッションID）。diff本体は保存せずbase/head SHAのみ（dirty込みなら注記＋`--stat`要約）。同ブランチの再レビューは`-r2`付き新ファイル。`eval/log.md` に集計1行＋記録への相対リンクを足す。
4. `/tmp` の一時ファイル（patch/context/audit/detchecks×2/最終diff）を削除する（記録生成の**後**に行う）。

## モデル割り当て

| レンズ | 担当（由来PR） | 発火条件 |
|---|---|---|
| domain-boundary | 汎用基盤へのドメイン語彙漏れ・Update()ポーリング・共通サービス委譲漏れ（978/1000） | Game.Block/Gear/EnergySystem等の.cs |
| server-state-sync | サーバー状態同期3点セット・Applier禁止・ハンドシェイク順序（988） | Server.Protocol/Server.Event/Client.Network |
| datastore-access-separation | Lookup/Mutation分離・static変更露出（988） | DataStore系キーワード |
| master-data-defense | optional濫用・??フォールバック・ローダープリフィル（978） | VanillaSchema/Core.Master/BlockTemplate |
| type-driven-structure | 共用体struct・god-context・N択1役割の型排除・DTO配置・振る舞い型switchの多態化漏れ（987/996/997/1045） | struct/Context/interface系キーワード |
| redundant-member-duplication | バッキングフィールド＋素通しプロパティの二重保持・同値別名メンバーの排除（sonnet） | プロパティ/フィールド宣言を含む.cs |
| implicit-cardinality-assumption | マスタ/ドメイン集合の単一要素決め打ち（`[0]`/`First`）で暗黙に単数を仮定（1017） | MasterHolderを読む.cs |
| set-once-dependency-injection | 生成時に確定するset-once依存の可変setter注入（コンストラクタ注入漏れ）（1027） | `public void Set`追加を含む.cs |
| hardcoded-content-enumeration | コンテンツ集合のコード内列挙→マスタ駆動化（2026-07-23リプレースファミリー指摘） | TypeConst/KindConst/GUIDリテラルを含む.cs |
| precedent-alignment | 前例一致（全PR横断・役割で前例を選ぶ） | 常時 |
- **レンズ** — `select_lenses.py` の2列目（各レンズ先頭YAMLの `model`）をそのまま渡す。
- **reviewer** — `select_reviewers.py` の2列目（正は `scripts/model_map.json`。未記載reviewerはopus、`sonnet` 記載のみsonnet）。
- **Fable全般** — `model: "fable"` 固定。**比較演算子verifier・comment-convention-guard** — `sonnet`。**comment-rationale-guard** — `opus`（WHY判定は高ステークス）。
- Codex監査は別CLIなので対象外。

## 有効性の測定（eval/）

- **リプレイ評価**: `eval/fixtures.tsv` + `eval/make-fixture.sh` でレビュー当時のdiffを再生成し `eval/expected-findings.md` の期待検出と突合する。レンズ・reviewer・スクリプトを変更したら必ず1回流す。手順は `eval/README.md`。
- **前向きログ**: マージ済みPRごとに `eval/log.md` へ「人間指摘数・分類・ハーネス事前検出の有無・却下数」を記録する。
- **観点新設・改稿時の検証（3段階を全て完了するまで「検証済み」と報告しない）**:
  1. **セレクタ発火確認** — `select_lenses.py`/`select_reviewers.py` のTSVに対象diffで現れること。
  2. **由来diffサニティ** — 由来PRのdiffに本番同様の3行契約で起動しCriticalが出ること。ただしこれは配管確認に過ぎない。
  3. **ブラインド汎化検証** — 観点本文とドメイン語彙（クラス名・メソッド名・タグ・ディレクトリ名）が一切重ならないfixtureを2本作り、同じ3行契約で起動する: **陽性**（別ドメインで同じ意味構造を持つdiff→Criticalが出るべき）と**陰性**（過検知ガード対象のパターンのみのdiff→`Critical: なし`であるべき）。どちらかが期待と違えば観点本文を直してから再検証する。

## Gotchas

- **4カテゴリcontextを埋めないとレンズ/reviewerが誤検知する** — 空contextは「合意なし」と解釈され既定Criticalが出る。
- **「並列」の実体はバックグラウンド起動** — Codexを `run_in_background` で先に投げ、完了を待たずにレンズ・reviewer・Fableを起動する。
- **`codex exec` のフラグ順序** — `--sandbox` `--skip-git-repo-check` はサブコマンドより**前**に置く。監査プロンプトは/tmpに置く（リポジトリ内は誤コミットの恐れ）。
- **verifierは候補ゼロなら起動しない** — `candidates.comparison_operator` が空なら比較演算子verifierは不要（0トークン）。
- **文字数はスクリプトの値が正** — LLMに日本語の文字数を数え直させない。convention-guardは `count` を信頼し例外判定と短縮案だけ行う。
- **post-checksはreviewerではない** — `post-checks/` はStep 6.5専用でセレクタのglobに含まれない。
- **Agent起動時に必ずmodel列を渡す（モデル継承事故の防止）** — Agentツールは `model` を省略すると**親（＝あなた＝オーケストレータ）のモデルを継承**する。あなた自身がfableで走っていると、model未指定のサブエージェントが誤ってfableで起動しうる。両セレクタはTSV2列目に**常に具体値**を出す（`select_lenses.py` はmodel未記載lensを `opus` に、`select_reviewers.py` は未記載reviewerを `default:opus` に具体化。空欄は絶対に出さない）。この2列目を**必ずそのまま** Agentの `model` に渡すこと。fableが正になるのは `precedent-alignment` レンズ（YAMLに `model: fable`）とFable全般（prose指定）だけで、それ以外にfableは現れない。
- **AskUserQuestionは末尾だけ** — 確定修正の途中で割り込まない。
- **観点の更新は指摘駆動で行う** — 新しい人間レビュー指摘が出たら、**先に `eval/README.md` の「検知可否のフォレンジック・リプレイ」で当時diff×全系統の検知マトリクスを取り**（当時コミットへピンしたworktree＋忠実context。診断なしの対策は禁止）、診断結果に応じてそれを防げたはずのレンズ/reviewer（無ければ新規）へ実例として追記し、`eval/expected-findings.md` にも行を足す。
- **検知の主担保はopus/sonnet側のレンズ・reviewerに置く** — fable（precedent-alignment・Fable全般）とCodexは高コストな最後のセーフティネットであり、特定の指摘クラスの検知をそこに恒常依存させない。**fable/Codexだけが検知した指摘（他系統が全滅）が出たら、それはopus/sonnet側の観点欠落のシグナル**: その指摘クラスをopus/sonnetモデル指定の既存レンズ/reviewerへ実例追記するか専用観点を新設し、3段階検証を通す（実例: リプレースファミリーのBlockTypeハードコード→マスタ駆動化はfable precedent-alignmentのみが検知し、opus/sonnetの9系統は素通しした。2026-07-23リプレイ検証）。新設・改稿する観点は原則opus以下で検知が成立する具体性で書く。
- **由来PRへの再発火はサニティに過ぎない** — レンズ/reviewer本文には由来指摘の実名（クラス・メソッド名）が載るため、由来diffでの検出は名前照合でも通ってしまい汎化の証明にならない。新設・改稿した観点は「有効性の測定」の3段階検証（特にドメイン語彙非重複のブラインド陽性/陰性fixture）を完了するまで有効と報告しない。
