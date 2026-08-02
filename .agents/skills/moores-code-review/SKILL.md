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

1. **決定論チェック**（`scripts/deterministic_checks.py`）— AGENTS.md・moorestech規約の機械判定分（partial・try-catch・Func・200行・10ファイル・デフォルト引数・SerializeField命名・比較演算子・コメント長・region・master_default_fallback・packet_response_root・server_realtime_api・server_elapsed_time・init_method_naming・schema_optional_true・event_tag_sync・try_catch_boundary）。0トークン。
2. **moores設計レンズ群**（`lenses/`・11本）— moorestech固有の設計規約。実PRレビュー指摘（PR978/987/988/996/997/1000/1095）由来。
3. **汎用reviewer群**（`reviewers/`・24本）— 言語横断のコード品質。全数調査（63セッション/1029起動）で採用実績のある観点のみ採録（採用0/冗長の20本と決定論代替1本は除外。根拠は `scripts/model_map.json` の `_excluded_from_port`）。
4. **Codex外部監査**（`scripts/codex-audit-template.md`）— 別モデルCLIの独立第三者視点。
5. **Fable全般レビュー**（`generalists/fable-holistic-review.md`）— チェックリスト非依存の俯瞰監査。自己裏取り契約。

## 実行順序（厳守）

> **① 機械チェック統一窓口 `check_all.py`（決定論＋死にメンバーゲート＋セレクタを1コマンドで同時実行） → ② Codex監査をバックグラウンド起動 → ③ レンズ群＋reviewer群＋Fable全般＋（`verifiers_to_launch`にあるverifier）を1メッセージで並列起動 → ④ 全系統を回収・実コード照合・重複排除 → ⑤ 機械的修正を自動適用＋コンパイル → ⑤.5 最終diffで決定論再チェック＋コメント保全post-checks 2本 → ⑥ 報告＋設計判断のみAskUserQuestion（末尾集約）**

AskUserQuestionは**最後の報告フェーズに集約**する。修正適用の途中で割り込まない。

## Step 1: レビュー対象と4カテゴリcontextを確定する

1. **作業範囲を特定** — このセッションで生成・変更した成果物をコミット範囲・staged・unstagedから確定し、統合unified diffを `/tmp/moores-review-patch-<ts>.diff` に書く（**PATCH_PATH**）。`git diff <base>^..<last>` + `git diff --cached` + `git diff` を連結。ユーザーがレビュー範囲を明示したらそれを優先。
2. **4カテゴリcontextを書く** — `/tmp/moores-review-context-<ts>.md`（**USER_PROMPT_PATH**）に埋める。埋め忘れるとレンズ/reviewerがfalse-positiveを量産する:
   - **目指す（ゴール）** / **目指さない（非目標）** / **許容するトレードオフ** / **尊重すべき制約**
   - **4カテゴリは必ず `##` 見出しで書く**（太字箇条書き形式は出所ラベル検査の対象外になり沈黙故障する。見出しゼロはfail-closedでconfirmedになる）。
   - **「許容するトレードオフ」「非目標」の各行に出所ラベル必須**: `[ユーザー裁定: "発言引用" または AskUserQuestion結果 YYYY-MM-DD]` / `[ADR: <spec名>#<台帳項目>]` / `[agent前提]`。ラベル無し・引用不能な行は自動的に `[agent前提]` 扱いで免責力を持たない（`references/integration-rules.md` §6）。ユーザー裁定の出所はspec/planの判断台帳（ADRセクション）から引く（台帳がSSOT）。

## Step 2: 機械チェック統一窓口 ①（check_all.py）

**1コマンドで機械層の全観点を同時実行する。** 内部で `deterministic_checks.py`（規約の機械判定）・`dead_member_gate.py`（IL解析）・`select_lenses.py`/`select_reviewers.py`（発火観点とモデル）を全部呼び、単一JSONに束ねる。個別スクリプトを別々に叩かない（呼び忘れ・結果の取りこぼしの温床）:

```bash
python3 .claude/skills/moores-code-review/scripts/check_all.py "<PATCH_PATH>" --repo-root "$(pwd)" --context "<USER_PROMPT_PATH>" > /tmp/moores-review-detchecks-<ts>.json
```

出力JSONの読み方: `deterministic`（confirmed/candidates）・`dead_member`（Step 2.5の節を参照）・`lenses`/`reviewers`（Step 4で使うTSV相当の`{path, model}`一覧）・**`verifiers_to_launch`（候補件数から計算済みの起動すべきverifier一覧 — Step 4はこれに従うだけ）**・`summary`（全体集計と`errors`。errorsが空でないまま先へ進むのは禁止）。

`deterministic` 節の解釈:

- **`confirmed`**（partial・try-catch・Func・デフォルト引数・SerializeField命名・10ファイル・master_default_fallback・packet_response_root・server_realtime_api・init_method_naming・context_source_label）— 検出正確・裏取り不要。Criticalとして統合に直接載せる（修正の適用可否は §3/§4）。`context_source_label`（出所ラベル欠落）はcontextファイルを修正して再実行する。
- **`confirmed` のうち200行超過（file-too-long）は努力目標** — Criticalにせず報告のWarning備考に1行載せるだけ。分割を強制せず、AskUserQuestionにも**絶対に**載せない（ユーザー裁定 2026-07-23）。
- **テストコードは200行/10ファイル規約の適用外** — `*Test(s).cs`・`*.test.ts(x)`・`*.spec.ts`・`Tests`系/`e2e`/`tests`ディレクトリ配下は `file-too-long`/`dir-file-limit` の対象外（スクリプトが除外済み。ユーザー裁定 2026-07-28）。
- **`candidates.comparison_operator`** — 1件以上あればStep 3で比較演算子verifier（sonnet）を並列起動。0件なら起動しない。
- **`candidates.try_catch_boundary`** — 1件以上あればStep 4でtry-catch境界verifier（opus）を並列起動。0件なら起動しない。**根拠コメントの実在を免除として扱ってはならない**（コメントがあるだけの try-catch は `confirmed` のまま。許可された境界3種を主張しているものだけがこの候補に降り、verifierが実コードで裁定する。ユーザー指摘由来の較正 2026-08-02・PR1095）。
- **`candidates.server_elapsed_time`** — 1件以上あればStep 4でサーバDateTime用途verifier（sonnet）を並列起動。0件なら起動しない。サーバ`Game.*`の`DateTime.Now/UtcNow`は「セーブへの実世界時刻記録（正当）」と「ゲーム進行の経過時間ゲート（違反）」が同じ実装形になるため、確定検出にせずverifierが用途を裁定する（PR1095 `MapObjectMiningService` のDateTimeクールダウン由来・2026-08-02）。
- **`candidates.comment_length` / `region_internal`** — この時点では保持のみ（commentはStep 5.5で最終diffに再計測、regionはregion-internal reviewerの裏付け）。
- **`candidates.schema_optional_true`** は master-data-defense レンズ、**`candidates.event_tag_sync`** は server-state-sync レンズの裏付けデータとして渡す（正当な例外がありうるためレンズが裁定）。

## Step 2.5: 死にメンバーゲート（IL解析） ①.5

**check_all.py が同時実行済み**（出力JSONの `dead_member` 節。単体で再実行したい時だけ `scripts/dead_member_gate.py "<PATCH_PATH>" --repo-root "$(pwd)"`）。実体は `tools/DeadMemberAudit`（Mono CecilによるScriptAssembliesのIL解析）で、「参照0」「テスト/デバッグ/エディタ/デフォルト参照のみ」のpublicメンバーのうち**patchが触ったファイルのもの**を `candidates.dead_member` として出す。名前grepと違いオーバーロード単位で参照を厳密に数える（AGENTS.md「デバッグ/テスト専用publicを残さない」のanalyzer化・2026-08-03）。
- **`status: ok`** — candidatesが1件以上あればStep 4で死にメンバーverifier（sonnet・`verifiers/dead-member-verifier.md`）を並列起動。0件なら起動しない。
- **`status: stale`** — 変更.csがDLLより新しい。`uloop compile` を先に実行してからゲートを再実行する（コンパイルはどのみちStep 5で必須）。
- **`status: skipped`** — ScriptAssemblies不在（素のレビューworktree等）。縮退として報告に1行明記し、dead-scope reviewer（LLM）の参照勘定が唯一の担保になる旨を記録する。

## Step 3: Codex外部監査をバックグラウンド起動する ②

`scripts/codex-audit-template.md` を埋めて `/tmp/moores-review-audit-<ts>.md` に書き、バックグラウンド起動する:

```bash
codex exec --sandbox read-only --skip-git-repo-check - < /tmp/moores-review-audit-<ts>.md
```

Bashの `run_in_background: true` で起動しシェルIDを控える。観点デフォルト3つ: (1)アーキテクチャ的不整合・既存パターン乖離 (2)設計妥当性・将来の懸念 (3)致命的不具合・エンバグ・リグレッション。`which codex` が失敗したら本Stepをスキップし、その旨を最終報告に明記する（黙って縮退しない）。

## Step 4: レンズ群＋reviewer群＋Fable全般＋verifierを並列発火する ③

発火対象とモデルは **Step 2のcheck_all.py出力の `lenses` / `reviewers` 節**（`{path, model}` の一覧）をそのまま使う。起動すべきverifierも同出力の `verifiers_to_launch` に計算済み（候補0件の種は載らない＝起動しない）。セレクタを単体で再実行したい時だけ `select_lenses.py` / `select_reviewers.py` にPATCHを渡す（TSV出力）。

**1メッセージ内で並列に** 次を全部Agent起動する（順次起動は禁止）:

1. **各発火レンズ**（select_lensesのTSVどおりの `model`）— 3行契約＋共通出力契約:
   ```
   Read this : <レンズの絶対パス>
   Patch path : <PATCH_PATH>
   User prompt : <USER_PROMPT_PATH>

   出力契約（観点本文の出力フォーマットが二値でもこちらが優先）: 重大度3段階で返す。
   Critical: あり/なし — 確信をもって修正すべき違反。ありなら `修正方針: - <ファイル:行>: <直し方>` を列挙
   ※Criticalを1件出すと決めたら、**その形を patch 全体で数え上げてから**出力する（同型全数掃引・`references/integration-rules.md` §2.7）。1件だけ挙げて同型の残りを黙って落とすのは禁止
   Warning: 0行以上 — 観点に該当しそうだが確信・裏取りが一段弱い指摘、重大だが裁量余地のある懸念。`- <ファイル:行>: <懸念と根拠>`
   Info: 0行以上 — 対応不要の観察・過検知ガードで落としたが記録価値のある事実。1行ずつ
   suppressed: 0行以上 — トレードオフ免責で降格した指摘。`- [Critical|Warning] <ファイル:行>: <指摘要約> / suppressed-by: <トレードオフ1行, 出所ラベル>`。Critical/Warning節には入れない（重大度は行頭表記で保持）
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
5. **try-catch境界verifier**（Step 2の `candidates.try_catch_boundary` が1件以上のときだけ・`model: "opus"`）— 同じ4行契約で `verifiers/try-catch-boundary-verifier.md` を渡す。
6. **サーバDateTime用途verifier**（Step 2の `candidates.server_elapsed_time` が1件以上のときだけ・`model: "sonnet"`）— 同じ4行契約で `verifiers/server-elapsed-time-verifier.md` を渡す。
7. **死にメンバーverifier**（Step 2.5の `candidates.dead_member` が1件以上のときだけ・`model: "sonnet"`）— 同じ4行契約で `verifiers/dead-member-verifier.md` を渡す（候補JSONのパスをpromptに含める）。ILに現れない経路（UnityEvent配線・プレイテストDSL・文字列リフレクション）の実在だけをrgで裁く。

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
2. **決定論チェックを最終diffで再実行** — `deterministic_checks.py` を再度実行し `/tmp/moores-review-detchecks-final-<ts>.json` に書く。自分の修正が新たに生んだ `confirmed`/`comparison_operator` 違反はその場でインライン修正する。**再実行時は `--context` を渡さない**（出所ラベルはStep 2で検査済み。再検出させると/tmpのcontext編集へ誘導され無意味）。
3. **2本のガードを並列起動**（1メッセージ内）:
   - **comment-rationale-guard**（`model: "opus"`・3行契約）— load-bearingな根拠コメントがコード本体を残したまま削除・希薄化されていないか（削除行 `-` が対象）。`Read this : .claude/skills/moores-code-review/post-checks/comment-rationale-guard.md` + Patch path（最終diff）+ User prompt。
   - **comment-convention-guard**（`model: "sonnet"`・4行契約）— スクリプト計測の文字数超過候補の例外判定・短縮案 + 名前重複コメント検出。**文字数はスクリプトの値が正**。`Read this` + `Candidates : /tmp/moores-review-detchecks-final-<ts>.json` + Patch path（最終diff）+ User prompt。
4. **rationale-guardのCriticalはescalate**（自動復元しない）— 削除コメント再挿入は設計判断。復元タグ案を添えてStep 7へ。
5. **convention-guardはラベル分岐（Step 7へは送らない）** — `機械的` は §5 のもと自動適用、`要判断` は**ガード自身の裁定で完結**させる（短縮案が意図を保てるなら適用、例外該当なら残置。結果は報告に1行）。コメント短縮をAskUserQuestionに載せるのは**禁止**（ユーザー裁定 2026-07-23）。同一行で衝突したら**根拠保全を優先**。
6. 両ガードとも `Critical: なし` で再チェックも増分ゼロなら何もせずStep 7へ。

## Step 7: 報告＋AskUserQuestion ⑥

1. **統合報告** — Critical/Warning/Info件数、各指摘の出所（決定論/レンズ名/reviewer名/Codex/Fable/N系統一致）、適用した修正、コンパイル・テスト結果。Warningは1件1行で全件載せる（保険としてコンテキストに乗せるのが目的。黙って落とさない）。Infoは末尾に圧縮列挙。raw出力やレビュー表をそのまま貼らない。Codex/Fableをスキップした場合はその旨を明記。
   - **「免責で消された指摘」セクション必須**: 各観点の `suppressed:` 節を固定形式 `- [Critical|Warning] <指摘要約> — suppressed-by: <トレードオフ1行, 出所ラベル>` で列挙する（元の重大度を行頭に保持。0件なら「suppressed: 0件」と明記）。§2.6参照。
2. **保留した設計判断だけ**をAskUserQuestionで選択肢付き一括提示（0件ならスキップ）。回答に従い適用（§5の安全規則・検証を再適用）。
   - **載せてよいのは本質的な設計判断のみ**: アーキテクチャ・パターン選択（多態化/型分割/移動先クラス）・スコープ影響・両立不能な指摘、およびサブエージェントの `設計判断: あり` 項目。
   - **載せるの禁止**: コメントの短縮・文体（convention-guardが自己完結）、200行超過・ファイル分割（努力目標・報告のみ）。この2種は選択肢に混ぜた時点で規約違反。
3. **レビュー記録を生成する** — `records/TEMPLATE.md` に従い `records/YYYY-MM-DD-<topic>.md` を書く（対象SHA2つ・系統別1行判定表・適用修正・AskUserQuestion裁定・破棄指摘・セッションID）。diff本体は保存せずbase/head SHAのみ（dirty込みなら注記＋`--stat`要約）。同ブランチの再レビューは`-r2`付き新ファイル。`eval/log.md` に集計1行＋記録への相対リンクを足す。
4. `/tmp` の一時ファイル（patch/context/audit/detchecks×2/最終diff）を削除する（記録生成の**後**に行う）。

## モデル割り当て

| レンズ | 担当（由来PR） | 発火条件 |
|---|---|---|
| domain-boundary | 汎用基盤へのドメイン語彙漏れ・Update()ポーリング・共通サービス委譲漏れ（978/1000） | 全ての.cs |
| server-state-sync | サーバー状態同期3点セット・Applier禁止・ハンドシェイク順序（988） | Server.Protocol/Server.Event/Client.Network |
| datastore-access-separation | Lookup/Mutation分離・static変更露出（988） | DataStore系キーワード |
| master-data-defense | optional濫用・??フォールバック・ローダープリフィル（978） | VanillaSchema/Core.Master/BlockTemplate |
| type-driven-structure | 共用体struct・god-context・N択1役割の型排除・DTO配置・振る舞い型switchの多態化漏れ（987/996/997/1045） | struct/Context/interface系キーワード |
| redundant-member-duplication | バッキングフィールド＋素通しプロパティの二重保持・同値別名メンバーの排除（sonnet） | プロパティ/フィールド宣言を含む.cs |
| implicit-cardinality-assumption | マスタ/ドメイン集合の単一要素決め打ち（`[0]`/`First`）で暗黙に単数を仮定（1017） | MasterHolderを読む.cs |
| set-once-dependency-injection | 生成時に確定するset-once依存の可変setter注入（コンストラクタ注入漏れ）（1027） | `public void Set`追加を含む.cs |
| hardcoded-content-enumeration | コンテンツ集合のコード内列挙→マスタ駆動化（2026-07-23リプレースファミリー指摘） | TypeConst/KindConst/GUIDリテラルを含む.cs |
| speculative-abstraction | 受益者なき抽象の排除（単一実装interface・意味なしラッパー/IDisposable・存在意義なしメンバー・不要な新設型）（1095） | 型/interface/Dispose宣言を含む.cs |
| precedent-alignment | 前例一致（全PR横断・役割で前例を選ぶ） | 常時 |
- **レンズ** — `select_lenses.py` の2列目（各レンズ先頭YAMLの `model`）をそのまま渡す。
- **reviewer** — `select_reviewers.py` の2列目（正は `scripts/model_map.json`。未記載reviewerはopus、`sonnet` 記載のみsonnet）。
- **Fable全般** — `model: "fable"` 固定。**比較演算子verifier・サーバDateTime用途verifier・死にメンバーverifier・comment-convention-guard** — `sonnet`。**comment-rationale-guard・try-catch境界verifier** — `opus`（WHY判定・境界の真偽判定は高ステークス）。
- Codex監査は別CLIなので対象外。

## スキル自体の改善

観点の追加・改稿・人間指摘の見逃しへの対応・有効性測定は `references/skill-improvement.md` を読む（通常のレビュー実行では読まない）。

## Gotchas

- **4カテゴリcontextを埋めないとレンズ/reviewerが誤検知する** — 空contextは「合意なし」と解釈され既定Criticalが出る。
- **「並列」の実体はバックグラウンド起動** — Codexを `run_in_background` で先に投げ、完了を待たずにレンズ・reviewer・Fableを起動する。
- **`codex exec` のフラグ順序** — `--sandbox` `--skip-git-repo-check` はサブコマンドより**前**に置く。監査プロンプトは/tmpに置く（リポジトリ内は誤コミットの恐れ）。
- **verifierは候補ゼロなら起動しない** — `candidates.comparison_operator` / `candidates.try_catch_boundary` / `candidates.server_elapsed_time` / `candidates.dead_member` が空なら対応verifierは不要（0トークン）。
- **try-catchの免除はverifierだけが出せる** — オーケストレータが「根拠コメントがあるからAGENTS.md例外を充足」と判断してCritical計上から外すのは禁止（PR1095の較正ミスそのもの）。コメントは検証対象であって証拠ではない。
- **文字数はスクリプトの値が正** — LLMに日本語の文字数を数え直させない。convention-guardは `count` を信頼し例外判定と短縮案だけ行う。
- **post-checksはreviewerではない** — `post-checks/` はStep 6.5専用でセレクタのglobに含まれない。
- **Agent起動時に必ずmodel列を渡す（モデル継承事故の防止）** — Agentツールは `model` を省略すると**親（＝あなた＝オーケストレータ）のモデルを継承**する。あなた自身がfableで走っていると、model未指定のサブエージェントが誤ってfableで起動しうる。両セレクタはTSV2列目に**常に具体値**を出す（`select_lenses.py` はmodel未記載lensを `opus` に、`select_reviewers.py` は未記載reviewerを `default:opus` に具体化。空欄は絶対に出さない）。この2列目を**必ずそのまま** Agentの `model` に渡すこと。fableが正になるのは `precedent-alignment` レンズ（YAMLに `model: fable`）とFable全般（prose指定）だけで、それ以外にfableは現れない。
- **AskUserQuestionは末尾だけ** — 確定修正の途中で割り込まない。
- **人間指摘の見逃しが出たら** — その場で観点をいじらず `references/skill-improvement.md` の手順（フォレンジック・リプレイ診断→対策→4段階検証）に従う。
