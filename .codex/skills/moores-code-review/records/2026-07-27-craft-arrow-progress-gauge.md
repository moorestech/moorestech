# クラフト進捗矢印のゲージ化 レビュー記録 (2026-07-27)

## 対象
- base: `e26f42318`(master) / reviewed head: `54597732e` → 修正適用後 `9d2f3a1ea`
- ブランチ: `feature/craft-arrow-progress-gauge` / PR: なし
- レビュー時点はクリーン（`_CompileRequester.cs` のみセッション外の未コミット変更としてスコープ外）
- context要約
  - ゴール: クラフト画面の矢印グリフ自体が長押しクラフトの進捗ゲージになる。`value=1` で変更前の白矢印と一致。矢印下の緑バー廃止。clipPath id 衝突回避。様式を webui-design へ先行成文化
  - 非目標: 機械ブロックUI側（`shared/ui/ProgressArrow`）の見た目変更 / `shared/ui` への共通部品化 / パリティ既存FAIL 5件の解消 / transition 付与
  - 許容トレードオフ: 待機時に矢印が暗くなる / 充填色が §8.6 既定の `--gauge-fill` でなく `--color-content-primary` / parity の検出方式と目標値の変更 / `topicFixtures.ts` のブロッカー1行同梱（いずれもユーザー裁定 2026-07-27）
  - 制約: webui-design がホワイトリスト（§5 色/寸法直書き禁止・§6 画像アセット禁止・§8.6 ゲージ2トーン・§10 目視QA）/ AGENTS.md の日英2行コメント / lint・build・unit・e2e・parity 全通過

## 系統別判定
| 系統 | Critical | 要旨 |
|---|---|---|
| 決定論チェック | 0 | confirmed 0。comment_length 候補のみ（Step 6.5 の convention-guard が裁定） |
| precedent-alignment（レンズ） | なし | `topicFixtures` の `{}` が実ホスト `NotificationTopic.cs:32` の返り値と一致することを裏取り。SVG 3層+useId は codebase 初だが §8.12 に先行成文化済みで違反でない |
| core-any-file-directory-organization | なし | 新規追加1ファイルのみでゲート未達。`views/` が11ファイル化は既存の実効上限内 |
| core-any-implicit-value-meaning | なし | Warning: `ARROW_TOP`/`ARROW_BOTTOM` が viewBox 値なのに矢印座標を名乗り、座標を `height` に使用 |
| core-any-test-mutation-effectiveness | なし | Warning: クリップ矩形の x/height 未検証、3層の取り違え mutation が全green で通る |
| core-any-user-intent-fulfillment | なし | 依頼動詞を全達成と判定。`--color-content-primary` が不透明 `#f2f3f7` で溝が透けないこと、parity 目標が正本実測であることを独立検証 |
| core-ts_tsx-ai-recurring-mistakes | なし | Warning: テスト側の clamp 再実装、`ARROW_` 接頭辞に別物差しが混在 |
| core-ts_tsx-centralization-duplication | 未回収 | 4回の催促に無応答。矢印path・`useId` の重複が無いことをオーケストレータが自分で確認して代替 |
| core-ts_tsx-dead-code-and-scope | なし | 未使用シンボル・旧クラス残骸なし |
| core-ts_tsx-result-state-propagation | なし | Warning＋設計判断: `value=1` が連続クラフト中は到達不能（完了フレームで `elapsed` が 0 へリセット） |
| core-ts_tsx-single-source-of-truth | なし | Warning: 矢印ジオメトリの出所が path と定数の2つ、テスト側の clamp 再実装 |
| Codex外部監査 | High 1 / Medium 1 / Low 2 | High: `value=1` が画素完全一致していない（AA境界）。Medium: `margin-top` の寸法ハードコード。Low: geometry 三重管理・`useId` のコロン除去 |
| Fable全般 | なし | Warning: `craft-arrow-time` が maxΔ=5/tol=5 の境界ちょうど。フレッシュキャプチャで 42/47 を独立確認 |
| comment-rationale-guard（post-check） | 1 | Step 6 のコメント短縮で「コロンが `url(#…)` を壊す」根拠が消失 |
| comment-convention-guard（post-check） | — | 該当コメントを実ファイルへ短縮適用 |

Codex High はオーケストレータが実画素で再検証: 差分1221px・最大Δ31/255・**塗り内部は差0で全て輪郭のAA画素**（master は白→背景、本実装は白→溝でブレンド）。Codex の合成試算（125px・Δ3）より実差は大きいが、影響は縁1pxに閉じる。

## 適用した修正
- `ARROW_TOP`/`ARROW_BOTTOM` → `VIEWBOX_WIDTH`/`VIEWBOX_HEIGHT` へ改名、`ARROW_SPAN` を `ARROW_RIGHT - ARROW_LEFT` から導出、`viewBox` をテンプレート化（3系統一致: rv-ssot / rv-ai-mistakes / rv-implicit-value ＋ Codex Low） → `9d2f3a1ea`
- クリップ矩形の `x="2" y="0" width="117" height="78"` を固定するテストを追加（rv-test-mutation） → `9d2f3a1ea`
- 3層の役割（clip は充填層のみ・輪郭は最上層で非clip・各層のクラス名）を固定するテストを追加（rv-test-mutation） → `9d2f3a1ea`
- テスト側の clamp 再実装を `it.each` の `valuenow` リテラル列へ置換（2系統一致: rv-ssot / rv-ai-mistakes） → `9d2f3a1ea`
- `useId` のコロン除去の根拠コメントを復元（comment-rationale-guard の Critical。**自分が Step 6 で作り込んだ退行なので escalate せず機械的復元として適用**） → `9d2f3a1ea`
- コメント長超過の短縮（comment-convention-guard が実ファイルへ適用） → `9d2f3a1ea`

## 設計判断（AskUserQuestion裁定）
- Q: `value=1` が画素完全一致しない（AA境界のみ・最大Δ31/255） / 選択肢: 記述を実態に合わせる ／ value=1 だけ旧描画へ切替 / 裁定: **記述を実態に合わせる** / 適用: webui-design §8.12 と parts-eval-criteria §2-6 と CSS コメントを「塗り内部は一致・輪郭のAA画素のみ最大31/255暗い」へ訂正。コード無変更（`9d2f3a1ea`）
- Q: 連続クラフト中に `value=1` が到達不能で完了時に矢印が空へスナップする / 選択肢: 現状維持＋記述明確化 ／ 完了フレームだけ満杯を描く ／ 触らない / 裁定: **現状維持＋記述明確化** / 適用: §8.12 に「`value=1` は基準状態で連続クラフト中は 0→1未満 を周回し完了時 0 へ戻る。到達するのは `craftTime<=0` の即時レシピのみ」を明記（`9d2f3a1ea`）
- Q: 予防的修正3件（複数選択） / 裁定: **3件すべて適用** / 適用:
  - `margin-top: -12.218px` を `--craft-arrow-offset-y` へトークン化（Codex Medium）
  - `craft-arrow-time` の tol を 5→6（Fable Warning。目標値は据え置き、理由をコメント化）
  - `.craftArrowGlyph` の `drop-shadow` に根拠コメント1行（comment-rationale-guard の suppressed）

## 破棄した指摘
- Codex Low「`useId()` のコロン除去が一意性保証を弱める」— React 18.3 の `:r0:` 形式では除去後も一意性は保たれ、複数描画テストで実証済み。生IDを SVG の `url(#…)` に使うとコロンが参照を壊すため除去は必須。実害なしとして破棄（根拠コメントは復元済み）
- rv-dir-org / rv-ai-mistakes の「`views/` が11ファイルで AGENTS.md の10ファイル上限超過」— 200行超過と同様の努力目標扱い。Critical にせず備考のみ
- rv-user-intent / rv-test-mutation の「`value=1` の白一致に自動ガードが無い」— 部分的に解消（クリップ全域被覆テストを追加）。色そのものの自動検証は parity の COLOR_POINTS に矢印の点が無いため未対応のまま。目視QA依存であることを記録に残す

## 事後結果（マージ後追記可）
- 本レビューは PR #1077 としてマージ済み（マージコミット `402ae752e`）。同PRには別セッションのスキット再実装・チュートリアル暗転撤去・ワールドピン矢印が同梱された
- **未回収だった `core-ts_tsx-centralization-duplication` の結果がマージ後に届き、Critical を1件検出した。** `e2e/mock-host/topics/topicFixtures.ts` の `Topics.notification` 分岐が2箇所に存在し、後段は到達不能な死んだ分岐。本ブランチの `e4fda1ed0` と `tree2` の `b6df4c7b3` が同一不具合を独立に修正し、逆マージ `2e678cf92` でコンフリクトにならず両方残ったもの。PR #1079（`6bca7da78`）で後段を削除して解消
- **教訓: 未回収系統を「オーケストレータの自力確認で代替した」扱いにして締めるのは危険。** 今回は代替確認（矢印path・`useId` の重複無し）が当該系統の守備範囲を覆っておらず、その系統だけが見つけられる Critical を素通しした。逆マージ後に顕在化する重複は patch 単体では見えないため、**逆マージを含むブランチでは最終diffだけでなく作業ツリー実体を見る系統を必ず1本は完走させること**
- 未対応で残した Warning 2件（同 reviewer・いずれも本件スコープ外の既存非対称）: `shared/ui/ProgressArrow` が `role="progressbar"`+aria3点を持たず GaugeBar / CraftProgressArrow と割れている / `GaugeBar` が `clamp01` を使わずクランプをインライン再実装している

## メタ
- セッションID: 7c7c2c31-945c-4eb4-b0fa-e42a56ee1cd4
- スキップ系統: なし。ただし **core-ts_tsx-centralization-duplication が無応答で未回収**（オーケストレータが矢印path・`useId` の重複無しを自分で確認して代替）。Codex は read-only サンドボックスのため vitest 再実行不可（lint と Python AST で代替）
- 検証: lint 0 errors（1 warning は無関係な既存分）/ build 成功 / unit 376 passed・66 files / e2e 109 passed / parity 42/47（FAIL 5件は非目標に列挙済みの既存分と完全一致・`craft-arrow-time` PASS）
- 備考: サブエージェントの最終出力が自動では届かず、全系統に `SendMessage` で `to:"main"` への送信を明示する必要があった
