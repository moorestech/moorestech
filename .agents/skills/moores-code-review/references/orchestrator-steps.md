# Step 2〜6.5 実行手順（正本・委譲オーケストレータが読む）

moores-code-review の実行本体。**既定（2026-08-20）ではこの手順書の Step 3.5〜6.5 を `scripts/review_workflow.js`（Workflow ツール）が実行する** — 本体が Step 2（check_all.py・split_chunks・Codex起動・`build_workflow_args.py`）まで行い、Workflow が系統の並列発火→統合→適用→post-check を決定論的に回す。この手順書は Workflow スクリプトの**仕様の正本**であり、JS を変えるときは先にここを直す。sonnet オーケストレータ委譲（旧既定）・インライン実行の場合は、派遣プロンプトの `Run dir` / `Patch path` / `User prompt` / `Repo root` を前提に Step 2 から自分で始める（Step 0〜1 は親が完了済み）。Step 7（報告・AskUserQuestion・記録）は親が行う — このファイルには含まれない。

## 6系統の構成

1. **決定論チェック**（`scripts/deterministic_checks.py`）— AGENTS.md・moorestech規約の機械判定分（partial・try-catch・Func・200行・10ファイル・デフォルト引数・SerializeField命名・比較演算子・コメント長・region・master_default_fallback・packet_response_root・server_realtime_api・server_elapsed_time・init_method_naming・schema_optional_true・event_tag_sync・try_catch_boundary）。0トークン。
2. **moores設計レンズ群**（`lenses/`・11本）— moorestech固有の設計規約。実PRレビュー指摘（PR978/987/988/996/997/1000/1095/1108）由来。
3. **汎用reviewer群**（`reviewers/`・30本）— 言語横断のコード品質。全数調査（63セッション/1029起動）で採用実績のある観点のみ採録（採用0/冗長の20本と決定論代替1本は除外、2026-08-16再監査で採用ゼロの2本を追加削除。根拠は `scripts/model_map.json` の `_excluded_from_port`）。加えて、`.cs` ゲートの設計レンズ5本（speculative-abstraction・type-driven-structure・hardcoded-content-enumeration・default-resolution-ownership・implicit-cardinality-assumption）の **ts/tsx翻案版**を採録し、webui差分にも同じ意味構造の検査を当てる（`_ts_lens_ports`・2026-08-04逆輸入）。
4. **Codex外部監査**（`scripts/codex-audit-template.md`）— 別モデルCLIの独立第三者視点。
5. **Fable全般レビュー**（`generalists/fable-holistic-review.md`）— チェックリスト非依存の俯瞰監査。自己裏取り契約。
6. **分割深掘り調査**（`investigators/`・3観点）— 変更ファイル（テスト・非コード除外後）が16以上の大規模PRのみ発火。`scripts/split_chunks.py` がドメイン単位で10-15ファイルのチャンクに分割し、チャンクごとに深読みバグ狩り・縫い目統合・チャンク内一貫性の3エージェントが**変更後ファイル全文**をagenticにReadする（全体diff一括の系統では希釈される注意を担保）。テストは完全隔離＝チャンク割当もReadも禁止（ユーザー裁定 2026-08-03）。

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
- **`status: skipped`** — ScriptAssemblies不在（素のレビューworktree等）、または dotnet 不在（`note` に「dotnet不在」と出る。PATH/`DOTNET_ROOT`/`~/.dotnet` で解決できなかった）。どちらも縮退として報告に1行明記し（理由は `note` を転記）、dead-scope reviewer（LLM）の参照勘定が唯一の担保になる旨を記録する。

## Step 2.6: webui死コード・テスト専用参照ゲート（knip） ①.6

**check_all.py が同時実行済み**（出力JSONの `ts_dead_code` 節。単体で再実行したい時だけ `scripts/ts_dead_code_gate.py "<PATCH_PATH>" --repo-root "$(pwd)"`）。実体は `moorestech_web/webui` の knip（設定は `webui/knip.json` が正）で、**patchが触った .ts/.tsx のもの**を `candidates.ts_dead_code` として出す。C#側 DeadMemberAudit（Step 2.5）のts/tsx対称形（2026-08-16導入・弱かった `core-ts_tsx-dead-code-and-scope` reviewerの機械化代替）。rule: `ts-dead-file` / `ts-dead-export`（参照ゼロ）、`ts-nonproduction-file` / `ts-nonproduction-export`（テスト・e2e・開発コードからのみ参照）。

- **`status: ok`** — candidatesが1件以上あればStep 4でts死コードverifier（sonnet・`verifiers/ts-dead-code-verifier.md`）を並列起動。0件なら起動しない。
- **`status: skipped`** — webuiのts/tsx変更なし（knip自体を実行しない・0秒）、またはknip未インストール（後者は報告に1行明記）。

## Step 3: Codex外部監査を3本バックグラウンド起動する ②

**起動前に実体パスと認証ファイルを解決する**（`which codex` は使わない — 封じ込めPATHでは失敗し、実体が `~/.local/bin` にあるのに「codex不在」と誤診して10本連続で縮退した・2026-08-20）:

```bash
python3 .claude/skills/moores-code-review/scripts/codex_preflight.py   # {"status":"ok","codex":"<実体パス>",...} / exit 10=バイナリ不在 / 11=認証ファイル不在（報告は status 文字列で転記）
```

exit 0 なら以下の `codex` を出力の `codex` 実体パスに読み替えて起動する。exit 10/11 なら3本ともスキップし、**理由（バイナリ不在 / `$CODEX_HOME/auth.json` 不在）を区別して**最終報告と `integrated.md` の系統別回収状況に明記する（「不在」と一括りにしない）。

3種のテンプレートを埋めて監査プロンプトを `$RUNDIR` に書き、**3本ともバックグラウンドで並列起動する**:

1. **俯瞰監査** — `scripts/codex-audit-template.md`（3観点同梱・従来どおり。ユーザーが観点を指定したらここに差し替える）→ `<$RUNDIRの実値>/codex-audit.md`
2. **バグ狩り専任** — `scripts/codex-bughunt-template.md`（不具合のみ・設計への言及禁止・修正提案は最小差分）→ `<$RUNDIRの実値>/codex-bughunt.md`
3. **設計整合専任** — `scripts/codex-design-template.md`（設計のみ・**過剰設計提案の抑制付き**: 新抽象の推奨は既存前例が現にその形の場合に限る）→ `<$RUNDIRの実値>/codex-design.md`

```bash
codex exec --sandbox read-only --skip-git-repo-check -o <$RUNDIRの実値>/codex-audit.final.md   - < <$RUNDIRの実値>/codex-audit.md   > <$RUNDIRの実値>/codex-audit.out.md 2>&1
codex exec --sandbox read-only --skip-git-repo-check -o <$RUNDIRの実値>/codex-bughunt.final.md - < <$RUNDIRの実値>/codex-bughunt.md > <$RUNDIRの実値>/codex-bughunt.out.md 2>&1
codex exec --sandbox read-only --skip-git-repo-check -o <$RUNDIRの実値>/codex-design.final.md  - < <$RUNDIRの実値>/codex-design.md  > <$RUNDIRの実値>/codex-design.out.md 2>&1
```

**`-o`（`--output-last-message`）は必須** — 結論の正本は `.final.md` であり、`.out.md`（stdout）はツール実行ログ込みの副産物にすぎない。stdoutは完走しても最終回答まで届かないことがある（2026-08-18実測：3本ともtask_completeまで完走したのに`.out.md`はツールログの途中で終端し、integratorが「Codex全滅」と誤判定した）。

それぞれBashの `run_in_background: true` で起動しシェルIDを控える。**出力は必ず `.out.md` へリダイレクトする** — 完了確認はシェル状態だけで行い、監査本文をオーケストレータのコンテキストへ読み込まない（回収はStep 5のintegratorが行う）。狭域専任2本は単発同梱プロンプトで注意が3分割される問題への対策（recall向上）で、俯瞰が残り全部の受け皿。**同一モデルの3起動は独立系統ではない** — 回収時、codex間で重複した指摘は1件に畳み、出所は「Codex」1系統として扱う（integration-rules §2）。preflight が exit 10/11 なら本Stepを3本ともスキップし、その理由（`status` 文字列）を最終報告に明記する（黙って縮退しない）。

**完了確認は「`.final.md` が非空か」で行う（`.out.md` の中身では判定しない）。** 空・不在なら**欠員と断定する前に必ず**回収スクリプトを走らせる（codexは `$CODEX_HOME/sessions/**/rollout-*.jsonl` に結論を必ず残すので、そこが最後の正本）:

```bash
python3 .claude/skills/moores-code-review/scripts/codex_recover.py \
  --prompt <$RUNDIRの実値>/codex-<名前>.md --out <$RUNDIRの実値>/codex-<名前>.out.md
```

終了コードで系統の扱いが決まる: `0`=結論あり（`.final.md` を回収して**通常どおり1系統として数える**）/ `3`=セッションはあるが未完走（再実行）/ `4`=セッション自体が無い（起動失敗＝真の欠員）/ `5`=認証失効（`.out.md` に 401・`Please log in again`。この `CODEX_HOME` で `codex login` が必要＝環境起因の欠員として報告し「codex不在」とは書かない）。**「完走したが回収に失敗した」を「Codexが失敗した」と報告するのは禁止**（前者は結論が現に存在する）。

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

**回収はファイルハンドオフで行う（オーケストレータのコンテキストを空けるため）。** 起動前に共通出力契約（正本は `references/output-contract.md`。Workflow 既定では `build_workflow_args.py` が書く）を `<$RUNDIRの実値>/contract.md` へ1本だけ書き、各エージェントのプロンプトは `Read this` / `Patch path` / `User prompt` / `Output contract`（contract.mdのパス） / `Write full report to`（`<$RUNDIRの実値>/agents/<名前>.md`）の5行に畳む。**返答は3行以内（Critical件数・設計判断あり/なし・一行要約）に制限し、詳細は返答に書かせずファイルへ書かせる。** `agents/` 配下のファイル群の回収・照合はStep 5のintegratorが行う（オーケストレータはgrep・集計しない）。起動数が多い場合はwaveに分けてよい（1メッセージ内の並列は各wave内で守る）。**コンテキスト残量を理由にこの工程を中断してはならない** — 詰まるのは実行可否ではなく回収の設計であり、この方式で消費はほぼゼロになる（ユーザー裁定 2026-08-14・[[2026-08-14-大規模ファンアウトは回収方式を変えて完走する]]）。

各サブエージェントは上記の共通出力契約（Critical/Warning/Info＋設計判断）で返す。**二値（あり/なし）に潰さず3段階で出させる理由**: Warning/Infoは「とりあえず統合報告のコンテキストに乗る」ことが目的の保険であり、二値だと確信の一段弱い実指摘が `なし` に丸められて消失する（ユーザー裁定 2026-07-23。実例: リプレースファミリーのハードコードを複数レンズが視認しながら二値契約のため無出力で落とした）。`設計判断: あり` はCriticalでも備考でもない第3の出口で、Step 7のAskUserQuestionへ**必ず**載せる（備考落ちで黙殺しない）。reviewer発火が0件でもレンズ群とFableは起動する。

## Step 5: 回収・統合（integratorへ委譲） ④

- Step 4の全サブエージェント（レンズ・reviewer・Fable・investigator・verifier）の**完了**と、Step 3のバックグラウンドCodex3本の**完了**を確認する（未完了なら待つ。Workflow 経路では `review_workflow.js` が haiku の待機係1体（until ループ・最大 `codexWaitMaxMinutes` 分）で `.final.md` 非空を待ち、期限切れ分は `codex_recover.py` の終了コードを integrator へ渡す）。各返答は3行契約なのでそのまま受けるが、**生の報告本文・Codex出力をオーケストレータが読むのは禁止** — 中身の回収と照合はintegratorが行う。
- 全部揃ったら**統合integrator**を1体起動する（`model: "opus"` 明示・5行契約）:
  ```
  Read this : .claude/skills/moores-code-review/integrators/finding-integrator.md
  Run dir : <$RUNDIRの実値>
  Patch path : <PATCH_PATH>
  User prompt : <USER_PROMPT_PATH>
  Write integrated report to : <$RUNDIRの実値>/integrated.md
  ```
  integratorは `agents/` 配下・Codex結論3本（`.final.md`。不在なら回収スクリプト実行後の同ファイル。`.out.md` は疑義がある時だけ補助的に見る）・`checks.json` を読み、`references/integration-rules.md` §0〜§2.7（実コード照合・棄却の挙証責任・重複排除・Warning/Info統合・suppressed統合・同型掃引・系統間矛盾の検証）を適用して `integrated.md` に統合結果を書く。各Criticalには適用区分（自動適用可 §3/§3.5 | 設計判断 §4）が付く。返答は件数サマリのみ。
- integratorの返答を受けたら `integrated.md` をReadしてStep 6へ。生のagentsファイル・Codex出力へ戻ってよいのは、integratorの結論に疑義がある個別件の再確認だけ（全量の読み直しは統合の二重実行であり禁止）。
- `integrated.md` の「系統別回収状況」に欠員（起動失敗・weekly limit・Codexスキップ）があれば、Gotchasの再起動規則に従い、必要なら該当系統を再起動してintegratorを再実行する。**Codexの欠員申告は `codex_recover.py` の終了コードを添えていない限り受け付けない** — 回収漏れを欠員として通すと「外部監査の観点が効いていない」という偽の縮退申告がPR本文とレビュー記録に残る（2026-08-18 PR#1167 実害）。

## Step 6: 確定修正の自動適用＋コンパイル ⑤

適用対象と区分は `integrated.md` の「採用Critical」の適用区分どおり（区分に疑義がある件だけ§3/§4の条文で判定し直す）。`references/integration-rules.md` §3〜§5 に従う。要点:
- 具体名（ファイル/クラス/メソッド）と修正方針が挙がっていて選択の余地が無い機械的修正・単独系統cosmeticは、確認を挟まず自動適用する（デフォルト動作）。
- 設計判断（複数の妥当な選択肢・スコープ影響・アーキテクチャ変更・両立不能な指摘・decisionを要するCodex High/Medium）は適用せずStep 7へ保留。
- .csを修正したら `uloop compile --project-path ./moorestech_client` を実行しエラー0を確認する。
- **Read規律（オーケストレータのコンテキスト節約・2026-08-18）**: 修正適用のためのReadはEdit対象の該当範囲だけを `offset`/`limit` で読む（ファイル全文Readしない）。疑義照合で実コードへ戻るときも該当関数の範囲だけに絞る。裏取りはintegratorが済ませており、オーケストレータの再Readは「これからEditする箇所の現物確認」が目的。

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
6. 両ガードとも `Critical: なし` で再チェックも増分ゼロなら何もせず完了（委譲時はここで親へ返答する）。

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
- **統合integrator**（`integrators/finding-integrator.md`）— `opus` 固定（棄却の挙証責任・系統間矛盾の裁定・適用区分の判定は高ステークス）。
- **investigator（分割深掘り調査）** — 各 `investigators/*.md` 先頭YAMLの `model` が正（2026-08-16裁定で3観点ともsonnetへ降格。経緯は `$LOGS/harness/moores-code-review/analysis/2026-08-16-agent-efficiency-reaudit.md`）。
- Codex監査は別CLIなので対象外。

## Gotchas（実行系）

- **「並列」の実体はバックグラウンド起動** — Codexを `run_in_background` で先に投げ、完了を待たずにレンズ・reviewer・Fableを起動する。
- **`codex exec` のフラグ順序** — `--sandbox` `--skip-git-repo-check` はサブコマンドより**前**に置く。監査プロンプトは `$RUNDIR`（logs repo側）に置く（コードrepo内は誤コミットの恐れ・`/tmp` は消える）。
- **verifierは候補ゼロなら起動しない** — `candidates.comparison_operator` / `candidates.try_catch_boundary` / `candidates.server_elapsed_time` / `candidates.dead_member` が空なら対応verifierは不要（0トークン）。
- **try-catchの免除はverifierだけが出せる** — オーケストレータが「根拠コメントがあるからAGENTS.md例外を充足」と判断してCritical計上から外すのは禁止（PR1095の較正ミスそのもの）。コメントは検証対象であって証拠ではない。
- **分割深掘り調査は閾値未満なら起動しない** — split_chunksが `below-threshold` を返したら第6系統は丸ごと不発火（0トークン）。閾値を無視してinvestigatorを手動起動しない（小PRでは既存系統と重複するだけ）。
- **investigatorにテストを絶対に見せない** — チャンク割当除外だけでなくRead自体が禁止（ユーザー裁定 2026-08-03 完全隔離）。テスト不足の検知はtest系reviewerの担当のまま。
- **文字数はスクリプトの値が正** — LLMに日本語の文字数を数え直させない。convention-guardは `count` を信頼し例外判定と短縮案だけ行う。
- **post-checksはreviewerではない** — `post-checks/` はStep 6.5専用でセレクタのglobに含まれない。
- **Agent起動時に必ずmodel列を渡す（モデル継承事故の防止）** — Agentツールは `model` を省略すると**親（＝あなた＝オーケストレータ）のモデルを継承**する。委譲時のあなたはsonnetなので、model未指定のサブエージェントが誤ってsonnetで起動しうる（opus/fable指定系統の無言降格）。両セレクタはTSV2列目に**常に具体値**を出す（`select_lenses.py` はmodel未記載lensを `opus` に、`select_reviewers.py` は未記載reviewerを `default:opus` に具体化。空欄は絶対に出さない）。この2列目を**必ずそのまま** Agentの `model` に渡すこと。fableが正になるのは `precedent-alignment` レンズ（YAMLに `model: fable`）とFable全般（prose指定）だけで、それ以外にfableは現れない。
- **残量不足を理由に系統を間引かない／中断しない** — レポートはファイルへ書かせ返答は3行に絞る（Step 4の回収方式）。系統を落とすなら報告に明記する。
- **Codexの `.out.md` が途中で切れていても失敗ではない** — 判定材料は `.final.md`（`-o` の出力）と `codex_recover.py` の終了コードだけ。`.out.md` を `grep` して「結論が無い＝失敗」と断じない（stdoutにはツール実行ログしか残っていないことがある）。真の失敗は「rollout にセッションが無い（exit 4）」「task_complete が無い（exit 3）」「認証失効（exit 5。rollout に結論が無いときだけ `.out.md` 両端の codex ERROR 行で判定）」の3つだけ。
- **オーケストレータは生出力を読まない** — `agents/*.md`・Codexの`.out.md`の全量読み・grep集計は統合の二重実行。照合・重複排除はintegratorの担当で、オーケストレータが読むのは `integrated.md` と、疑義のある個別件の該当ファイルだけ。
- **fableクォータ切れは黙って欠員にしない** — fable指定の系統（precedent-alignment・Fable全般）が「weekly limit」等の失敗応答を返したら、その系統を `model: "opus"` で再起動する（2026-07〜08で14起動が無言消失した実測より）。再起動した事実は最終報告に1行明記。
