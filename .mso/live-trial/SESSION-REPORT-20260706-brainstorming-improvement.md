# brainstorming スキル改善セッション 統合進捗レポート

日付: 2026-07-06 / 作業ブランチ: hermes/mt-014-docs-plans-blockconnectinfo-refactor (tree1)
結果ビューア: http://localhost:4981

## 0. 出発点（なぜこの作業をしたか）

1. ユーザーが歯車接続・インベントリ接続の誤接続（シャフト側面×歯の直結等）を相談 → メインAI（Fable）は brainstorming スキルを発火させず直接設計提案を返した
2. その提案はユーザーに却下され、別AIとの対話成果である参照プラン（案B: 形状マスタ＋無向互換ペア表＋`BlockConnectorComponent<TTarget, TConnectJudge>` の3段判定）が「あるべきアウトプット」として提示された
3. 本質差の分析（ユーザー承認済み）: (1)自案への反例探索の有無 (2)互換性を正規化された「関係」としてモデル化したか (3)表現不能制約のための注入点＋型安全設計 (4)番号付き前提宣言・拒否権形式 (5)C型質問のみか
4. 指示: run-skill-live-trial で brainstorming スキルを検証・改善する。発火検証も含める。trial 実行は opus（コスト削減）

## 1. 発火検証（Trial A1/A2 — fresh context・素のプロンプト）

| trial | プロンプト | 結果 |
|---|---|---|
| A1 (trigger-q) | 元の質問文そのまま（疑問形「これどうしたらいい？」） | ✅ 2手目で Skill(brainstorming) 自己発火 |
| A2 (trigger-req) | 「防ぐ仕組みを実装したい。設計から一緒に考えてほしい」 | ✅ 自己発火 → AskUserQuestion gate 到達 |

- **fresh context の opus では発火は機能する**。メインセッションで発火しなかったのは「長い対話の途中」×「相談→分析回答と判断した」ことによる条件依存と推定
- 副次発見: 両trialとも既存の connector-shape 設計書（並行セッションが同ブランチにコミット済み）を調査で発見し、コミットハッシュ付き前提宣言→進め方のC型質問という正しい挙動を示した（お題汚染のため対話品質検証は別お題に切替）

## 2. 実走受け入れ（Trial B — 明示呼び出し・汚染なしお題「チェスト自動整列」）

- 対話gate 3回を越えて checklist 9項目を正順で完走、HARD-GATE 違反ゼロ（承認前コード0行）
- 調査で既存ソート実装一式（InventorySortService / SortInventoryProtocol / 既存整理ボタン）を発見し、前提に畳み込み **C型質問1問だけ** を提示
- spec のユーザー配置希望（.mso配下・非コミット）を尊重。evaluator が spec の主張事実をコード実地照合し全て実在確認
- **fresh evaluator 判定: 88/100 PASS**。未達点: specの軽微な誇張（「チェストで検証済み」の実体は機械ブロックのテスト）/ 案数2で下限 / 一括承認 / 質問ツール不統一（plain-text vs AskUserQuestion）

## 3. 第1ラウンド改善（コミット b71e5e746）

1. description に相談形トリガー明記（壁打ち/仕様相談/「どうしたらいい？」型 — 回答が設計提案になるならまず発火。会話途中でも適用）
2. 離散選択肢は AskUserQuestion ツールに統一
3. Spec Self-Review に Evidence scope check 追加（「Xで検証済み」はXそのものを検証したテストがある場合のみ）
4. 小規模設計の一括提示を明文化

## 4. クリーンルーム検証（改善後・本命の受け入れ試験）

条件: 設計書が存在しなかったコミット 1968f7dc6 の隔離 worktree ＋ 改善後スキルのみ重ね ＋ **元の質問文そのまま** ＋ opus。ユーザー役の介入は「選択肢の選択」と「90度反例の指摘」（参照対話でもユーザーが指摘した箇所）のみ。

**fresh evaluator 判定（golden=参照プラン・甘め禁止）: 72/100 — 下限合格**

| 本質差 | 判定 | 要点 |
|---|---|---|
| (1) 反例探索 | 部分達成 | 90度は自力未発見（参照AIもユーザー入力のため公平性で中和）。一方 **goldenにも無い潜在バグ2件を自力発見**（directions==null の無条件接続→NRE温床 / 同一座標後勝ち上書き）。指摘後の処理は優秀（表現限界の認識→BlockDirectionから実行時軸導出→エンジン無知の述語注入→原則引用） |
| (2) 正規化「関係」モデル | **実質未達** | per-connector kind+accepts（分散属性）で設計し、中央互換ペア表＋foreignKeyを案として提示すらせず。**ユーザーが元AIを却下した核心がそのまま再現 — 最大の残債** |
| (3) 注入点＋型安全 | 部分達成 | 述語注入・エンジン無知は達成。型レベル束縛（コンパイルエラー保証）未到達（参照ではユーザー種蒔きのため減点軽減） |
| (4) 前提宣言・拒否権 | 達成 | 毎質問先頭に「前提（自己解決した決定 — 違ったら指摘してください）」 |
| (5) C型のみ・1問ずつ | 部分達成 | B型質問の混入1回（推奨リトマス漏れ）、AskUserQuestion への2問同梱×2回 |

生成された設計自体の内容: kind/accepts 相互同意 ＋ 軸平行チェックの述語注入 ＋ 潜在バグ2件の修繕 ＋ directions からの移行ドラフト生成。参照プランと方向性は同型だが、データモデリングの正規化と型安全で一段浅い。

## 5. 第2ラウンド改善（コミット 8bb45c968）— 72点の未達点への対応

1. **Self-refutation before presenting（必須）節を新設**: 設計提示前に「成立してはいけないのに成立する具体入力」（回転/多セル/境界/スケール）を1件構築して自分で叩く。表現不能なら拡張点追加のシグナル。最強の反例と対処を設計提示に含める。ゲーム仕様断定の前提（例:「直交歯車は噛み合わない」）は反例（ベベルギア）を確認しC型質問に格上げ
2. **一般設計原則7**: N対Nの適合・互換関係は中央の正規化テーブル（ペア表＋foreignKey＋検証）を第一候補に（SSOT）。分散属性案は中央表案と並記必須
3. **一般設計原則8**: 注入点は実行時注入 vs 型レベル束縛の型安全度比較を必須化（「注入ミスがコンパイルエラーになるか」を比較軸に）
4. **1回の AskUserQuestion = 1問** の厳格化 ＋ 送信直前の推奨リトマス最終チェック

~~⚠️ 第2ラウンドの効果は未検証~~ → **検証済み（§5.5参照）: 87/100 強い合格**。

## 5.5 再クリーンルーム検証（2026-07-07・第2ラウンド改善の効果確認）

条件は前回と同一（1968f7dc6 隔離worktree＋改善後スキル＋元質問文＋opus・model一致をjqで機械確認）。詳細: `20260707-020607-brainstorming-r2-cleanroom/report.md`

**fresh evaluator 判定: 87/100 — 強い合格（前回72から+15）**

| 改善項目（狙い） | 結果 |
|---|---|
| 中央表の自力提示（原則7） | ✅ **解消** — 初回フォームで中央適合ペア表＋foreignKeyを分散属性と並記して自力提示（前回最大の残債） |
| 自己反証節の毎回出現 | ✅ 解消（全セクション出現・初回質問を駆動。ただしSection 1の「回転は自動対応」が誤自己反証 → 90度問題は自力未発見のまま。質の課題残） |
| 1呼び出し1問 | ✅ 解消 |
| ゲーム仕様断定のC型格上げ | ✅ 解消 — **ベベルギア例外を自発的にC型質問化**（スキル記載例そのもの） |
| 型安全度比較の必須化（原則8） | ❌ **不発** — 型レベル束縛への言及ゼロ（今回唯一の明確な不発。次ラウンド候補） |

- 本質差5点: (1)部分達成(達成寄り) (2)**達成(強)** (3)部分達成 (4)達成 (5)達成
- 自力発見の上乗せ: null-directionsバイパス（前回も）/gear経路に軸判定不在の実証/クライアントdesync非発生の論証。90度反例への解（alignmentAxis＋外積平行ゲート）はユーザーは問題提起のみで解はtrial独力
- 公平性: ユーザー役介入はヒント注入なしとevaluator確認済み。trialコミットはブランチ `feature/connector-kind-matching-design-r2` に保全

## 6. ハーネス（run-skill-live-trial）への申し送り

- **GATE検知漏れ（2回再現）**: AskUserQuestion の選択待ち中も live-trial-status.sh が BUSY_GENERATING と分類し続け、poll が exit 4 (GATE) しない（gate_ticks=0 のまま stall 進行）。pending tool_use の分類ロジック要修正
- 複数質問フォームは数字キー送信では選択が登録されず、Down/Enter＋Tab＋Submit の操作が必要
- plain-text 質問でターン終了するケースは現仕様どおり GATE 検知不能（STALL 分岐表 #3/#4 で回収）
- **入力欄ゴーストテキスト（2026-07-07新規）**: ターン終了後、trial側Claude Codeの入力欄にsuggested replyらしきテキストが自動出現（複数回観測）。send前に C-u でクリア必須。live-trial-send.sh への C-u 前処理追加が改修候補
- 単一質問フォームは Down/Enter（またはEnterのみ）で選択登録OK

## 7. 成果物・コミット一覧

- コミット: `b71e5e746`（第1R改善）/ `8bb45c968`（第2R改善）— いずれも .claude/skills/brainstorming/SKILL.md のみ
- trial 成果物: `.mso/live-trial/` 配下
  - `20260706-181434-brainstorming-trigger-q|trigger-req|accept/` — 発火検証×2＋実走受け入れ（transcript / pane / report / spec）
  - `20260706-220213-brainstorming-postimprove-cleanroom/` — クリーンルーム検証（produced-spec.md / golden.md / transcript / report.md）
  - `20260707-020607-brainstorming-r2-cleanroom/` — 再クリーンルーム検証87点（produced-spec.md / golden.md / transcript / report.md / reply-1〜6）
- クリーンルーム trial が生成した設計コミットはブランチ `feature/connector-kind-matching-design`（R1）/ `feature/connector-kind-matching-design-r2`（R2）に保全（worktree は削除済み）
- 全 trial の requested/actual model = claude-opus-4-8 一致、tmux セッションは全て掃除済み

## 8. 次アクション候補

1. ~~再クリーンルーム trial で第2ラウンド改善の効果検証~~ → **完了（87/100・§5.5）**
2. 第3ラウンド改善候補（87点の残債。実施判断待ち）: (a)原則8の不発対応 — 型安全比較を対話・specに強制する提示位置/文言の見直し (b)自己反証の質 — 「具体座標・具体入力で構築せよ」の強化
3. run-skill-live-trial の GATE 分類バグ修正 ＋ send前 C-u 前処理追加
4. （本編タスク側）歯車接続の実装は別セッションで進行中（connector-shape-system、Task2以降）
