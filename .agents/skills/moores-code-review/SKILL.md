---
name: moores-code-review
description: |
  moorestechのPR作成前・マージ前レビューを単体で完結させる統合スキル。6系統を並列実行する:
  ①決定論チェック（汎用+moorestech固有の機械判定）②moores設計レンズ群（ドメイン境界・サーバー状態同期3点セット・
  DataStore分離・マスタデータ防御・型構造・前例一致）③汎用reviewer群（汎用コード品質の採用実績ある観点＋webui向けts/tsx設計観点）
  ④Codex外部監査 ⑤Fable全般レビュー ⑥分割深掘り調査（大規模PR時のみ・10-15ファイル/チャンクで全文精読）。
  指摘を実コード照合・重複排除のうえ統合し、機械的修正を自動適用、
  設計判断だけ末尾でAskUserQuestion。設計レンズと汎用レビュー機構を1本に束ね、これ単体でレビューが完結する。
  Use when:
  1. moorestechでPR作成前・マージ前のレビューを行う時（pr-create前に必ず1パス）
  2. subagent-driven-development の最終ブランチレビューを行う時
  3. 「moores-code-reviewで」「moorestechレンズでレビュー」「設計レンズを通して」「コードレビューして」と言われた時
---

# moores-code-review

moorestechのコードレビューを **決定論チェック → 6系統の並列レビュー → 実コード照合・重複排除 → 自動適用 → 報告** の順で単体完結させる。汎用コードレビュー機構（reviewer群・Codex監査・Fable全般・post-checksコメント監査）を設計レンズと同居させ、これ1本で完結する（外部スキルへの依存なし）。

## 6系統の構成

1. **決定論チェック**（`scripts/deterministic_checks.py`）— AGENTS.md・moorestech規約の機械判定分（partial・try-catch・Func・200行・10ファイル・デフォルト引数・SerializeField命名・比較演算子・コメント長・region・master_default_fallback・packet_response_root・server_realtime_api・server_elapsed_time・init_method_naming・schema_optional_true・event_tag_sync・try_catch_boundary）。0トークン。
2. **moores設計レンズ群**（`lenses/`・11本）— moorestech固有の設計規約。実PRレビュー指摘（PR978/987/988/996/997/1000/1095/1108）由来。
3. **汎用reviewer群**（`reviewers/`・30本）— 言語横断のコード品質。全数調査（63セッション/1029起動）で採用実績のある観点のみ採録（採用0/冗長の20本と決定論代替1本は除外、2026-08-16再監査で採用ゼロの2本を追加削除。根拠は `scripts/model_map.json` の `_excluded_from_port`）。加えて、`.cs` ゲートの設計レンズ5本（speculative-abstraction・type-driven-structure・hardcoded-content-enumeration・default-resolution-ownership・implicit-cardinality-assumption）の **ts/tsx翻案版**を採録し、webui差分にも同じ意味構造の検査を当てる（`_ts_lens_ports`・2026-08-04逆輸入）。
4. **Codex外部監査**（`scripts/codex-audit-template.md`）— 別モデルCLIの独立第三者視点。
5. **Fable全般レビュー**（`generalists/fable-holistic-review.md`）— チェックリスト非依存の俯瞰監査。自己裏取り契約。
6. **分割深掘り調査**（`investigators/`・3観点）— 変更ファイル（テスト・非コード除外後）が16以上の大規模PRのみ発火。`scripts/split_chunks.py` がドメイン単位で10-15ファイルのチャンクに分割し、チャンクごとに深読みバグ狩り・縫い目統合・チャンク内一貫性の3エージェントが**変更後ファイル全文**をagenticにReadする（全体diff一括の系統では希釈される注意を担保）。テストは完全隔離＝チャンク割当もReadも禁止（ユーザー裁定 2026-08-03）。

## 実行順序（厳守）

> **① 機械チェック統一窓口 `check_all.py`（決定論＋死にメンバーゲート＋ts死コードゲート＋セレクタを1コマンドで同時実行） → ② Codex監査をバックグラウンド起動 → ③ レンズ群＋reviewer群＋Fable全般＋（閾値超なら）分割深掘り調査＋（`verifiers_to_launch`にあるverifier）を1メッセージで並列起動 → ④ 全系統を回収・実コード照合・重複排除 → ⑤ 機械的修正を自動適用＋コンパイル → ⑤.5 最終diffで決定論再チェック＋コメント保全post-checks 2本 → ⑥ 報告＋設計判断のみAskUserQuestion（末尾集約）**

AskUserQuestionは**最後の報告フェーズに集約**する。修正適用の途中で割り込まない。

## Step 0: 実行ディレクトリ `$RUNDIR` を作る

1回のレビューが作る生成物（patch・context・codex監査プロンプト3本・check_all出力・chunks・最終diff・最終detchecks）は
**すべて** `$LOGS/harness/moores-code-review/runs/<ts>/` 配下に置く。以下これを `$RUNDIR` と呼ぶ
（`$LOGS` は記録repo `../moorestech_logs`。`<ts>` は `YYYY-MM-DD-HHMM` 形式でレビュー1回につき1つ）。

    mkdir -p <$RUNDIRの実値>

- **`/tmp` には置かない** — OSに掃除されて消える。これらは記録（Step 7）が指すverdictの実入力であり、
  後から「何をどう測ってその結論になったか」を再現する唯一の材料。pr-independent-reviewのreconcileも
  ここを読む（あちらは `$LOGS/harness/pr-independent-review/runs/pr-<番号>/` を使う。混ぜない）
- ファイル名は固定: `patch.diff` / `context.md` / `checks.json` / `codex-audit.md` / `codex-bughunt.md` /
  `codex-design.md` / `chunks.tsv` / `final.diff` / `checks-final.json`
- `$RUNDIR` 配下はStop/SessionEnd hook（`.dev-hooks/logs-sync.mjs`）でlogs repoへ自動commit・pushされる。
  セッション側で `git commit` しない

## Step 1: レビュー対象と4カテゴリcontextを確定する

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

## Step 2: 機械チェック統一窓口 ①（check_all.py）

**1コマンドで機械層の全観点を同時実行する。** 内部で `deterministic_checks.py`（規約の機械判定）・`dead_member_gate.py`（IL解析）・`select_lenses.py`/`select_reviewers.py`（発火観点とモデル）を全部呼び、単一JSONに束ねる。個別スクリプトを別々に叩かない（呼び忘れ・結果の取りこぼしの温床）:

```bash
python3 .claude/skills/moores-code-review/scripts/check_all.py "<PATCH_PATH>" --repo-root "$(pwd)" --context "<USER_PROMPT_PATH>" > <$RUNDIRの実値>/checks.json
```

出力JSONの読み方: `deterministic`（confirmed/candidates）・`dead_member`（Step 2.5の節を参照）・`ts_dead_code`（Step 2.6の節を参照）・`lenses`/`reviewers`（Step 4で使うTSV相当の`{path, model}`一覧）・**`verifiers_to_launch`（候補件数から計算済みの起動すべきverifier一覧 — Step 4はこれに従うだけ）**・`summary`（全体集計と`errors`。errorsが空でないまま先へ進むのは禁止）。

`deterministic` 節の解釈:

- **`confirmed`**（partial・try-catch・Func・デフォルト引数・SerializeField命名・10ファイル・master_default_fallback・packet_response_root・server_realtime_api・init_method_naming・context_source_label）— 検出正確・裏取り不要。Criticalとして統合に直接載せる（修正の適用可否は §3/§4）。`context_source_label`（出所ラベル欠落）はcontextファイルを修正して再実行する。
- **`confirmed` のうち200行超過（file-too-long）は努力目標** — Criticalにせず報告のWarning備考に1行載せるだけ。分割を強制せず、AskUserQuestionにも**絶対に**載せない（ユーザー裁定 2026-07-23）。
- **既に規約超過しているディレクトリへの1〜2ファイル追加も努力目標** — `dir-file-limit` のうち、そのディレクトリが本ブランチ以前から10ファイルを超えていた場合は `file-too-long` と同様に扱う。報告のWarning備考に1行のみ載せ、**AskUserQuestionには載せない**（ユーザー裁定 2026-08-14・[[2026-08-14-既存超過ディレクトリへの追加はレビューで問わない]]）。本ブランチが新規に作ったディレクトリが超過した場合のみCriticalとして扱う。
- **テストコードは200行/10ファイル規約の適用外** — `*Test(s).cs`・`*.test.ts(x)`・`*.spec.ts`・`Tests`系/`e2e`/`tests`ディレクトリ配下は `file-too-long`/`dir-file-limit` の対象外（スクリプトが除外済み。ユーザー裁定 2026-07-28）。
- **webui（`moorestech_web/webui`）は分割を実施する** — 上の適用外・努力目標の扱いと異なり、webuiでは e2e/tests 含め10ファイル超過を検出したら機能別サブディレクトリへの分割を修正として実施する（報告止まりにしない）。前例: e2e/tests を research/ inventory/ recipe/ へ分割。playwright testDir は再帰globのため設定変更不要（ユーザー裁定 2026-08-04 「基本的にwebuiは分割もするし、コメントの短縮も行う」・[[2026-08-04-e2eテストはサブディレクトリ分割し10ファイル規約を守る]]）。
- **`candidates.comparison_operator`** — 1件以上あればStep 3で比較演算子verifier（sonnet）を並列起動。0件なら起動しない。
- **`candidates.try_catch_boundary`** — 1件以上あればStep 4でtry-catch境界verifier（opus）を並列起動。0件なら起動しない。**根拠コメントの実在を免除として扱ってはならない**（コメントがあるだけの try-catch は `confirmed` のまま。許可された境界3種を主張しているものだけがこの候補に降り、verifierが実コードで裁定する。ユーザー指摘由来の較正 2026-08-02・PR1095）。
- **`candidates.server_elapsed_time`** — 1件以上あればStep 4でサーバDateTime用途verifier（sonnet）を並列起動。0件なら起動しない。サーバ`Game.*`の`DateTime.Now/UtcNow`は「セーブへの実世界時刻記録（正当）」と「ゲーム進行の経過時間ゲート（違反）」が同じ実装形になるため、確定検出にせずverifierが用途を裁定する（PR1095 `MapObjectMiningService` のDateTimeクールダウン由来・2026-08-02）。
- **`candidates.comment_length` / `region_internal`** — この時点では保持のみ（commentはStep 5.5で最終diffに再計測、regionはregion-internal reviewerの裏付け）。
- **`candidates.schema_optional_true`** は master-data-defense レンズ、**`candidates.event_tag_sync`** は server-state-sync レンズの裏付けデータとして渡す（正当な例外がありうるためレンズが裁定）。
- **`candidates.guid_literal`** は hardcoded-content-enumeration レンズ、**`candidates.event_action`**（`event Action`宣言=UniRx規約違反疑い）は domain-boundary レンズ、**`candidates.mutable_auto_property`**（`{ get; set; }`=SetHogeメソッド規約違反疑い）と **`candidates.passthrough_property`** は redundant-member-duplication レンズの裏付けデータとして渡す（DTOシリアライズ・外部interop等の正当例外はレンズが裁定。2026-08-16決定論化第2弾）。

## Step 2.5: 死にメンバー・公開範囲・配置・キャンセルゲート（IL解析） ①.5

**check_all.py が同時実行済み**（出力JSONの `dead_member` 節。単体で再実行したい時だけ `scripts/dead_member_gate.py "<PATCH_PATH>" --repo-root "$(pwd)"`）。実体は `tools/DeadMemberAudit`（Mono CecilによるScriptAssembliesのIL解析）で、**patchが触ったファイルのもの**を `candidates.dead_member` として出す。名前grepと違いオーバーロード単位で参照を厳密に数える（AGENTS.md「デバッグ/テスト専用publicを残さない」のanalyzer化・2026-08-03）。各候補は `rule` で種別が分かれる:

| rule | 検出内容 |
| --- | --- |
| `dead-member-unused` / `dead-member-nonproduction` | 参照0 / テスト・デバッグ・エディタ参照のみ |
| `dead-member-overpublic-private` / `-internal` | 参照は実在するが公開範囲が過剰（宣言型内のみ / 宣言アセンブリ内のみ） |
| `placement-mismatch` / `placement-registration-only` | server宣言でserver側に利用者なし（client参照のみ / DI登録のみ・解決者なし） |
| `ct-not-passed` / `ct-async-void` / `cts-not-released` | CancellationToken未伝搬 / `async void` / CTS作りっぱなし |
| `single-caller-helper` | 同一型の1メソッドからしか呼ばれていないprivateヘルパ（`#region Internal` ローカル関数へ畳む候補） |
| `dead-private-member` | どこからも呼ばれていないprivateメソッド（デリゲート束縛も無い完全な死にコード） |

- **`status: ok`** — candidatesが1件以上あればStep 4で死にメンバーverifier（sonnet・`verifiers/dead-member-verifier.md`）を並列起動。0件なら起動しない。rule別の裁定手順はverifier側に書いてある。
- **`status: stale`** — 変更.csがDLLより新しい。`uloop compile` を先に実行してからゲートを再実行する（コンパイルはどのみちStep 5で必須）。
- **`status: skipped`** — ScriptAssemblies不在（素のレビューworktree等）。縮退として報告に1行明記し、dead-scope reviewer（LLM）の参照勘定が唯一の担保になる旨を記録する。

## Step 2.6: webui死コード・テスト専用参照ゲート（knip） ①.6

**check_all.py が同時実行済み**（出力JSONの `ts_dead_code` 節。単体で再実行したい時だけ `scripts/ts_dead_code_gate.py "<PATCH_PATH>" --repo-root "$(pwd)"`）。実体は `moorestech_web/webui` の knip（設定は `webui/knip.json` が正）で、**patchが触った .ts/.tsx のもの**を `candidates.ts_dead_code` として出す。C#側 DeadMemberAudit（Step 2.5）のts/tsx対称形（2026-08-16導入・弱かった `core-ts_tsx-dead-code-and-scope` reviewerの機械化代替）。rule: `ts-dead-file` / `ts-dead-export`（参照ゼロ）、`ts-nonproduction-file` / `ts-nonproduction-export`（テスト・e2e・開発コードからのみ参照）。

- **`status: ok`** — candidatesが1件以上あればStep 4でts死コードverifier（sonnet・`verifiers/ts-dead-code-verifier.md`）を並列起動。0件なら起動しない。
- **`status: skipped`** — webuiのts/tsx変更なし（knip自体を実行しない・0秒）、またはknip未インストール（後者は報告に1行明記）。

## Step 3: Codex外部監査を3本バックグラウンド起動する ②

3種のテンプレートを埋めて監査プロンプトを `$RUNDIR` に書き、**3本ともバックグラウンドで並列起動する**:

1. **俯瞰監査** — `scripts/codex-audit-template.md`（3観点同梱・従来どおり。ユーザーが観点を指定したらここに差し替える）→ `<$RUNDIRの実値>/codex-audit.md`
2. **バグ狩り専任** — `scripts/codex-bughunt-template.md`（不具合のみ・設計への言及禁止・修正提案は最小差分）→ `<$RUNDIRの実値>/codex-bughunt.md`
3. **設計整合専任** — `scripts/codex-design-template.md`（設計のみ・**過剰設計提案の抑制付き**: 新抽象の推奨は既存前例が現にその形の場合に限る）→ `<$RUNDIRの実値>/codex-design.md`

```bash
codex exec --sandbox read-only --skip-git-repo-check - < <$RUNDIRの実値>/codex-audit.md
codex exec --sandbox read-only --skip-git-repo-check - < <$RUNDIRの実値>/codex-bughunt.md
codex exec --sandbox read-only --skip-git-repo-check - < <$RUNDIRの実値>/codex-design.md
```

それぞれBashの `run_in_background: true` で起動しシェルIDを控える。狭域専任2本は単発同梱プロンプトで注意が3分割される問題への対策（recall向上）で、俯瞰が残り全部の受け皿。**同一モデルの3起動は独立系統ではない** — 回収時、codex間で重複した指摘は1件に畳み、出所は「Codex」1系統として扱う（integration-rules §2）。`which codex` が失敗したら本Stepを3本ともスキップし、その旨を最終報告に明記する（黙って縮退しない）。

## Step 4: レンズ群＋reviewer群＋Fable全般＋verifierを並列発火する ③

発火対象とモデルは **Step 2のcheck_all.py出力の `lenses` / `reviewers` 節**（`{path, model}` の一覧）をそのまま使う。起動すべきverifierも同出力の `verifiers_to_launch` に計算済み（候補0件の種は載らない＝起動しない）。セレクタを単体で再実行したい時だけ `select_lenses.py` / `select_reviewers.py` にPATCHを渡す（TSV出力）。

チャンク分割（第6系統・分割深掘り調査用）はcheck_all.py出力に含まれないため別途実行する:

```bash
python3 .claude/skills/moores-code-review/scripts/split_chunks.py "<PATCH_PATH>" > <$RUNDIRの実値>/chunks.tsv
```

split_chunksの出力が空（stderrに `below-threshold`）なら分割深掘り調査は発火しない（0トークン）。非空なら**CHUNKS_TSV**として保持する。

**並列にAgent起動する。ただし1メッセージ最大12体**（同時実行20体上限に他セッション分を含め当たると起動が黙って消えるため。mac miniで実測17%が消失・2026-08-16再監査）。13体以上になる場合は残りを次のメッセージで**完了を待たずに**続けて起動する。起動失敗（`Concurrent subagent limit`）が返った体は控えておき必ず再起動する。起動対象:

1. **各発火レンズ**（select_lensesのTSVどおりの `model`）— 3行契約＋共通出力契約:
   ```
   Read this : <レンズの絶対パス>
   Patch path : <PATCH_PATH>
   User prompt : <USER_PROMPT_PATH>

   出力契約（観点本文の出力フォーマットが二値でもこちらが優先）: 重大度3段階で返す。
   Critical: あり/なし — 確信をもって修正すべき違反。ありなら `修正方針: - <ファイル:行>: <直し方>` を列挙し、各件に故障シナリオ（入力・状態→誤動作。cleanup系は具体コスト）を1行添える
   ※Criticalを1件出すと決めたら、**その形を patch 全体で数え上げてから**出力する（同型全数掃引・`references/integration-rules.md` §2.7）。1件だけ挙げて同型の残りを黙って落とすのは禁止
   ※ハンクを読むときは囲っている関数全体をReadする（触った関数の未変更行のバグも対象 — このpatchが再露出させる）。半信の候補を黙って落とさない（名指しできる故障シナリオがあればWarningに必ず載せる。finderの自己検閲が見逃しの支配的原因）
   Warning: 0行以上 — 観点に該当しそうだが確信・裏取りが一段弱い指摘、重大だが裁量余地のある懸念。`- <ファイル:行>: <懸念と根拠>`
   Info: 0行以上 — 対応不要の観察・過検知ガードで落としたが記録価値のある事実。1行ずつ
   suppressed: 0行以上 — トレードオフ免責で降格した指摘。`- [Critical|Warning] <ファイル:行>: <指摘要約> / suppressed-by: <トレードオフ1行, 出所ラベル>`。Critical/Warning節には入れない（重大度は行頭表記で保持）
   設計判断: あり/なし — 従来通り（代替案の具体形・シグネチャ付き比較）
   ```
   `precedent-alignment.md`（always発火）は発火レンズが0件でも必ず起動する。
2. **各reviewer**（select_reviewersのTSVどおりの `model`）— 同じ3行契約＋共通出力契約。
3. **Fable全般レビュー**（常時・`model: "fable"`）— 同じ3行契約＋共通出力契約で `generalists/fable-holistic-review.md` を渡す。
4. **分割深掘り調査**（CHUNKS_TSVが非空のときだけ）— チャンクごとに `investigators/` の3観点（chunk-deep-correctness.md / chunk-seam-integration.md / chunk-context-consistency.md）を起動する（起動数 = チャンク数×3）。モデルは各investigator先頭YAMLの `model` を**必ずそのまま**渡す。5行契約＋共通出力契約:
   ```
   Read this : <investigatorの絶対パス>
   Chunk files : <そのチャンクのカンマ区切りファイルリスト（TSV3列目）>
   Chunks TSV : <CHUNKS_TSVの絶対パス>
   Patch path : <PATCH_PATH>
   User prompt : <USER_PROMPT_PATH>
   ```
   テストファイルはチャンクに含まれず、investigatorはReadもしない（完全隔離・ユーザー裁定 2026-08-03）。
5. **比較演算子verifier**（Step 2の `candidates.comparison_operator` が1件以上のときだけ・`model: "sonnet"`）— 4行契約:
   ```
   Read this : .claude/skills/moores-code-review/verifiers/comparison-operator-verifier.md
   Candidates : <$RUNDIRの実値>/checks.json
   Patch path : <PATCH_PATH>
   User prompt : <USER_PROMPT_PATH>
   ```
5. **try-catch境界verifier**（Step 2の `candidates.try_catch_boundary` が1件以上のときだけ・`model: "opus"`）— 同じ4行契約で `verifiers/try-catch-boundary-verifier.md` を渡す。
6. **サーバDateTime用途verifier**（Step 2の `candidates.server_elapsed_time` が1件以上のときだけ・`model: "sonnet"`）— 同じ4行契約で `verifiers/server-elapsed-time-verifier.md` を渡す。
7. **死にメンバーverifier**（Step 2.5の `candidates.dead_member` が1件以上のときだけ・`model: "sonnet"`）— 同じ4行契約で `verifiers/dead-member-verifier.md` を渡す（候補JSONのパスをpromptに含める）。ILに現れない経路（UnityEvent配線・プレイテストDSL・文字列リフレクション）の実在だけをrgで裁く。
8. **ts死コードverifier**（Step 2.6の `candidates.ts_dead_code` が1件以上のときだけ・`model: "sonnet"`）— 同じ4行契約で `verifiers/ts-dead-code-verifier.md` を渡す。import graphに現れない経路（動的import・C#側からの文字列ブリッジ・生成コード）の実在だけをrgで裁く。

**回収はファイルハンドオフで行う（オーケストレータのコンテキストを空けるため）。** 起動前に共通出力契約を `<$RUNDIRの実値>/contract.md` へ1本だけ書き、各エージェントのプロンプトは `Read this` / `Patch path` / `User prompt` / `Output contract`（contract.mdのパス） / `Write full report to`（`<$RUNDIRの実値>/agents/<名前>.md`）の5行に畳む。**返答は3行以内（Critical件数・設計判断あり/なし・一行要約）に制限し、詳細は返答に書かせずファイルへ書かせる。** Step 5の回収は個々の返答ではなく `agents/` 配下のファイル群をgrep・集計して行う。起動数が多い場合はwaveに分けてよい（1メッセージ内の並列は各wave内で守る）。**コンテキスト残量を理由にこの工程を中断してはならない** — 詰まるのは実行可否ではなく回収の設計であり、この方式で消費はほぼゼロになる（ユーザー裁定 2026-08-14・[[2026-08-14-大規模ファンアウトは回収方式を変えて完走する]]）。

各サブエージェントは上記の共通出力契約（Critical/Warning/Info＋設計判断）で返す。**二値（あり/なし）に潰さず3段階で出させる理由**: Warning/Infoは「とりあえず統合報告のコンテキストに乗る」ことが目的の保険であり、二値だと確信の一段弱い実指摘が `なし` に丸められて消失する（ユーザー裁定 2026-07-23。実例: リプレースファミリーのハードコードを複数レンズが視認しながら二値契約のため無出力で落とした）。`設計判断: あり` はCriticalでも備考でもない第3の出口で、Step 7のAskUserQuestionへ**必ず**載せる（備考落ちで黙殺しない）。reviewer発火が0件でもレンズ群とFableは起動する。

## Step 5: 回収・実コード照合・重複排除 ④

- Step 4の全サブエージェント（レンズ・reviewer・Fable・investigator・verifier）の返却を受け取る。
- Step 3のバックグラウンドCodex（3本）の出力を回収する（未完了なら完了を待つ）。
- 全部揃うまでStep 6へ進まない。`references/integration-rules.md` §0〜§2 に従い、実コード照合・重複排除する（決定論confirmedは裏取り不要、Codex/Fable/レンズ/reviewerのCriticalはReadで裏取り、複数系統一致は「N系統一致（高確度）」に統合）。
- **Warning/Infoの扱い**（§2.5）: Warningは破棄せず統合報告に必ず載せる（軽い照合のみ。複数系統が同一箇所をWarningした場合と、照合で事実が確定した場合はCriticalへ昇格）。Infoは照合不要で報告末尾に圧縮列挙する。どちらもAskUserQuestionには載せない。

## Step 6: 確定修正の自動適用＋コンパイル ⑤

`references/integration-rules.md` §3〜§5 に従う。要点:
- 具体名（ファイル/クラス/メソッド）と修正方針が挙がっていて選択の余地が無い機械的修正・単独系統cosmeticは、確認を挟まず自動適用する（デフォルト動作）。
- 設計判断（複数の妥当な選択肢・スコープ影響・アーキテクチャ変更・両立不能な指摘・decisionを要するCodex High/Medium）は適用せずStep 7へ保留。
- .csを修正したら `uloop compile --project-path ./moorestech_client` を実行しエラー0を確認する。

## Step 6.5: 決定論再チェック＋コメント保全post-checks ⑤.5

Step 6の修正適用後に走らせるpost-fixガード群。**人間の変更とStep 6で自分が適用した修正の両方**を検査する。`reviewers/` にもセレクタにも属さない別系統。

1. **最終diffを作り直す** — Step 6適用後の作業ツリーをbaseと比較し `<$RUNDIRの実値>/final.diff` に書く。
2. **決定論チェックを最終diffで再実行** — `deterministic_checks.py` を再度実行し `<$RUNDIRの実値>/checks-final.json` に書く。自分の修正が新たに生んだ `confirmed`/`comparison_operator` 違反はその場でインライン修正する。**再実行時は `--context` を渡さない**（出所ラベルはStep 2で検査済み。再検出させるとcontext編集へ誘導され無意味）。
3. **発火すべきガードをスクリプトで選択し、出力どおりに並列起動**（1メッセージ内。2026-08-16裁定・空振り回の無条件起動を廃止）:

   ```bash
   python3 .claude/skills/moores-code-review/scripts/select_post_checks.py <$RUNDIRの実値>/final.diff <$RUNDIRの実値>/checks-final.json
   ```

   出力は `<post-check絶対パス>\t<モデル>` のTSV（レンズ/レビュアーのセレクタと同形式）。**出力された行だけを起動し、出力が空なら両方スキップ**（=0トークン）。発火条件は選択スクリプトが判定する: rationale-guardは最終diffにコメント削除行があるとき、convention-guardは `candidates.comment_length` が1件以上のとき。手動のgrep判定はしない。
   - **comment-rationale-guard**（3行契約）— load-bearingな根拠コメントがコード本体を残したまま削除・希薄化されていないか（削除行 `-` が対象）。`Read this : .claude/skills/moores-code-review/post-checks/comment-rationale-guard.md` + Patch path（最終diff）+ User prompt。
   - **comment-convention-guard**（4行契約）— スクリプト計測の文字数超過候補の例外判定・短縮案 + 名前重複コメント検出。**文字数はスクリプトの値が正**。`Read this : .claude/skills/moores-code-review/post-checks/comment-convention-guard.md` + `Candidates : <$RUNDIRの実値>/checks-final.json` + Patch path（最終diff）+ User prompt。
   - スキップしたガードは最終報告に「post-checks: <名前> 発火条件未達でスキップ」と1行明記する（黙って縮退しない）。
4. **rationale-guardのCriticalはescalate**（自動復元しない）— 削除コメント再挿入は設計判断。復元タグ案を添えてStep 7へ。
5. **convention-guardはラベル分岐（Step 7へは送らない）** — `機械的` は §5 のもと自動適用、`要判断` は**ガード自身の裁定で完結**させる（短縮案が意図を保てるなら適用、例外該当なら残置。結果は報告に1行）。コメント短縮をAskUserQuestionに載せるのは**禁止**（ユーザー裁定 2026-07-23）。同一行で衝突したら**根拠保全を優先**。
   - **webui（`moorestech_web/webui`）では `要判断` も短縮を適用する** — 数値詳細・数式・設計意図が落ちる場合でも文字数規約を優先して短縮する（詳細はコードとテスト本体が担う）。残置してよいのは「なぜ必要か」型の純粋な根拠コメント（定数選定根拠・防止目的）のみ（ユーザー裁定 2026-08-04・[[2026-08-04-コメント文字数規約は根拠情報より優先する]]）。
6. 両ガードとも `Critical: なし` で再チェックも増分ゼロなら何もせずStep 7へ。

## Step 7: 報告＋AskUserQuestion ⑥

1. **統合報告** — Critical/Warning/Info件数、各指摘の出所（決定論/レンズ名/reviewer名/Codex/Fable/N系統一致）、適用した修正、コンパイル・テスト結果。Warningは1件1行で全件載せる（保険としてコンテキストに乗せるのが目的。黙って落とさない）。Infoは末尾に圧縮列挙。raw出力やレビュー表をそのまま貼らない。Codex/Fableをスキップした場合はその旨を明記。
   - **「免責で消された指摘」セクション必須**: 各観点の `suppressed:` 節を固定形式 `- [Critical|Warning] <指摘要約> — suppressed-by: <トレードオフ1行, 出所ラベル>` で列挙する（元の重大度を行頭に保持。0件なら「suppressed: 0件」と明記）。§2.6参照。
2. **保留した設計判断だけ**をAskUserQuestionで選択肢付き一括提示（0件ならスキップ）。回答に従い適用（§5の安全規則・検証を再適用）。
   - **載せてよいのは本質的な設計判断のみ**: アーキテクチャ・パターン選択（多態化/型分割/移動先クラス）・スコープ影響・両立不能な指摘、およびサブエージェントの `設計判断: あり` 項目。
   - **載せるの禁止**: コメントの短縮・文体（convention-guardが自己完結）、200行超過・ファイル分割（努力目標・報告のみ）。この2種は選択肢に混ぜた時点で規約違反。
   - **設問は「症状 → 原因 → 推奨」の順で書く（ユーザー裁定 2026-08-03）**。設問本文の書き出しは**ゲーム上・開発上で実際に何が起きるか**にする。「列車に乗ったままゲームを終了して起動すると、自機が列車の上ではなく地面に落ちていて、その位置がセーブされる」のように、**コードを読まなくても分かる症状**から始めること。原因は1〜2行に圧縮する。
     - **推奨を必ず第1選択肢に置き、ラベル末尾に `（推奨）` を付ける**。各選択肢の説明には「これを選ぶと症状がなぜ消えるか」を1行入れる。トレードオフだけを並べない。
     - **禁止**: 観点名・レビュアー名（`caller-orchestration` 等）・レンズ用語・「N系統一致」を設問本文の**主役**にすること。出所は報告本文へ書き、設問には持ち込まない。メソッド名や行番号の羅列だけで問題を説明したことにしない。
     - **症状を1文で書けない指摘は設問にしない** — 報告本文のWarningへ落とす。「将来こう書き換えると壊れる」型は、症状（何が壊れるか）と再現条件を書けるときだけ設問にしてよい。
     - 判定基準: **その設問だけを読んだ人が、コードを開かずに選べるか**。選べないなら書き直す。
3. **レビュー記録を生成する** — 記録はコードrepoでなく記録repo `$LOGS`（`../moorestech_logs`）へ書く（featureブランチが記録に触れてマージ衝突する構造を断つため。コードrepo側へ書き戻さない）。`$LOGS/harness/moores-code-review/records/TEMPLATE.md` に従い `$LOGS/harness/moores-code-review/records/YYYY-MM-DD-<topic>.md` を書く（対象SHA2つ・系統別1行判定表・適用修正・AskUserQuestion裁定・破棄指摘・セッションID）。diff本体は保存せずbase/head SHAのみ（dirty込みなら注記＋`--stat`要約）。同ブランチの再レビューは`-r2`付き新ファイル。`$LOGS/harness/moores-code-review/eval-log.md` に集計1行＋記録への相対リンクを足す。
4. **`$RUNDIR` 配下は削除しない**（旧版は `/tmp` の一時ファイルを消す規定だった）。patch/context/audit×3/checks×2/最終diffは、記録が主張するverdictの実入力であり、消すと後から「何をどう測ってその結論に至ったか」を再現できない。記録本文に `- rundir: runs/<ts>/` の1行を入れて、記録から実入力へ辿れるようにする。

## モデル割り当て

| レンズ | 担当（由来PR） | 発火条件 |
|---|---|---|
| domain-boundary | 汎用基盤へのドメイン語彙漏れ・Update()ポーリング・共通サービス委譲漏れ（978/1000） | 全ての.cs |
| server-state-sync | サーバー状態同期3点セット・Applier禁止・ハンドシェイク順序（988） | Server.Protocol/Server.Event/Client.Network |
| datastore-access-separation | Lookup/Mutation分離・static変更露出（988） | DataStore系パス＋変更系語彙の追加行（2026-08-16厳格化） |
| master-data-defense | optional濫用・??フォールバック・ローダープリフィル（978） | VanillaSchema/Core.Master/BlockTemplate＋防御イディオム追加行（2026-08-16厳格化） |
| type-driven-structure | 共用体struct・god-context・N択1役割の型排除・DTO配置・振る舞い型switchの多態化漏れ（987/996/997/1045） | struct/Context/interface系キーワード |
| redundant-member-duplication | バッキングフィールド＋素通しプロパティの二重保持・同値別名メンバーの排除（sonnet） | 素通しプロパティ形（`=> _`等）の追加行（2026-08-16厳格化） |
| implicit-cardinality-assumption | マスタ/ドメイン集合の単一要素決め打ち（`[0]`/`First`）で暗黙に単数を仮定（1017） | MasterHolder＋`[0]`/`First`/`Single`の追加行（2026-08-16厳格化） |
| set-once-dependency-injection | 生成時に確定するset-once依存の可変setter注入（コンストラクタ注入漏れ）（1027） | `public void Set`追加を含む.cs |
| hardcoded-content-enumeration | コンテンツ集合のコード内列挙→マスタ駆動化（2026-07-23リプレースファミリー指摘） | TypeConst/KindConst/GUIDリテラルを含む.cs |
| speculative-abstraction | 受益者なき抽象の排除（単一実装interface・意味なしラッパー/IDisposable・存在意義なしメンバー・不要な新設型）（1095） | 型/interface/Dispose宣言を含む.cs |
| default-resolution-ownership | デフォルト値解決の責務漏れ（public Default公開＋呼び出し側??解決・省略可能性の早期潰し）（1108/1109） | `Default`を含む.cs |
| precedent-alignment | 前例一致（全PR横断・役割で前例を選ぶ） | 常時 |
- **レンズ** — `select_lenses.py` の2列目（各レンズ先頭YAMLの `model`）をそのまま渡す。
- **reviewer** — `select_reviewers.py` の2列目（正は `scripts/model_map.json`。未記載reviewerはopus、`sonnet` 記載のみsonnet）。
- **Fable全般** — `model: "fable"` 固定。**比較演算子verifier・サーバDateTime用途verifier・死にメンバーverifier・ts死コードverifier・comment-convention-guard** — `sonnet`。**comment-rationale-guard・try-catch境界verifier** — `opus`（WHY判定・境界の真偽判定は高ステークス）。
- **investigator（分割深掘り調査）** — 各 `investigators/*.md` 先頭YAMLの `model` が正（2026-08-16裁定で3観点ともsonnetへ降格。経緯は `$LOGS/harness/moores-code-review/analysis/2026-08-16-agent-efficiency-reaudit.md`）。
- Codex監査は別CLIなので対象外。

## スキル自体の改善

観点の追加・改稿・人間指摘の見逃しへの対応・有効性測定は `references/skill-improvement.md` を読む（通常のレビュー実行では読まない）。

## Gotchas

- **4カテゴリcontextを埋めないとレンズ/reviewerが誤検知する** — 空contextは「合意なし」と解釈され既定Criticalが出る。
- **「並列」の実体はバックグラウンド起動** — Codexを `run_in_background` で先に投げ、完了を待たずにレンズ・reviewer・Fableを起動する。
- **`codex exec` のフラグ順序** — `--sandbox` `--skip-git-repo-check` はサブコマンドより**前**に置く。監査プロンプトは `$RUNDIR`（logs repo側）に置く（コードrepo内は誤コミットの恐れ・`/tmp` は消える）。
- **verifierは候補ゼロなら起動しない** — `candidates.comparison_operator` / `candidates.try_catch_boundary` / `candidates.server_elapsed_time` / `candidates.dead_member` が空なら対応verifierは不要（0トークン）。
- **try-catchの免除はverifierだけが出せる** — オーケストレータが「根拠コメントがあるからAGENTS.md例外を充足」と判断してCritical計上から外すのは禁止（PR1095の較正ミスそのもの）。コメントは検証対象であって証拠ではない。
- **分割深掘り調査は閾値未満なら起動しない** — split_chunksが `below-threshold` を返したら第6系統は丸ごと不発火（0トークン）。閾値を無視してinvestigatorを手動起動しない（小PRでは既存系統と重複するだけ）。
- **investigatorにテストを絶対に見せない** — チャンク割当除外だけでなくRead自体が禁止（ユーザー裁定 2026-08-03 完全隔離）。テスト不足の検知はtest系reviewerの担当のまま。
- **文字数はスクリプトの値が正** — LLMに日本語の文字数を数え直させない。convention-guardは `count` を信頼し例外判定と短縮案だけ行う。
- **post-checksはreviewerではない** — `post-checks/` はStep 6.5専用でセレクタのglobに含まれない。
- **Agent起動時に必ずmodel列を渡す（モデル継承事故の防止）** — Agentツールは `model` を省略すると**親（＝あなた＝オーケストレータ）のモデルを継承**する。あなた自身がfableで走っていると、model未指定のサブエージェントが誤ってfableで起動しうる。両セレクタはTSV2列目に**常に具体値**を出す（`select_lenses.py` はmodel未記載lensを `opus` に、`select_reviewers.py` は未記載reviewerを `default:opus` に具体化。空欄は絶対に出さない）。この2列目を**必ずそのまま** Agentの `model` に渡すこと。fableが正になるのは `precedent-alignment` レンズ（YAMLに `model: fable`）とFable全般（prose指定）だけで、それ以外にfableは現れない。
- **残量不足を理由に系統を間引かない／中断しない** — レポートはファイルへ書かせ返答は3行に絞る（Step 4の回収方式）。系統を落とすなら報告に明記する。
- **fableクォータ切れは黙って欠員にしない** — fable指定の系統（precedent-alignment・Fable全般）が「weekly limit」等の失敗応答を返したら、その系統を `model: "opus"` で再起動する（2026-07〜08で14起動が無言消失した実測より）。再起動した事実は最終報告に1行明記。
- **AskUserQuestionは末尾だけ** — 確定修正の途中で割り込まない。
- **人間指摘の見逃しが出たら** — その場で観点をいじらず `references/skill-improvement.md` の手順（フォレンジック・リプレイ診断→対策→4段階検証）に従う。
