# brainstorming 第2ラウンド改善 再クリーンルーム検証レポート (2026-07-07)

## 目的

第2ラウンド改善（コミット `8bb45c968`: 自己反証必須化・正規化関係テーブル原則7・注入点型安全比較原則8・1問ずつ厳格化）の効果検証。前回trial（72/100・2026-07-06）と同条件の再クリーンルーム受け入れ試験。

## 条件（前回と同一）

- 設計書が存在しなかったコミット `1968f7dc6` の隔離 worktree（/Users/katsumi/moorestech-worktrees/lt-cleanroom、ブランチ feature/connector-kind-matching-design-r2）
- 改善後スキル（8bb45c968 時点の .claude/skills/brainstorming/）のみ上書き
- 元の質問文そのまま（prompt.md）を送信
- ユーザー役の介入: 選択肢の選択・セクション承認・90度反例の指摘（reply-5、golden側でもユーザー種蒔きの項目）・終了指示のみ。設計ヒントの注入なし（evaluator の公平性チェックで確認済み）

## model 検証（機械 gate）

- requested_model: claude-opus-4-8 / actual_model (jq): claude-opus-4-8 → ✅ 一致

## timeline

- boot 3s (02:06) → 2手目で Skill(brainstorming) 発火 → 調査 → **初手で根本原因 file:line 特定＋前提宣言＋自己反証＋中央ペア表 vs 分散属性の並記質問**（02:14 FORM）→ Section 1〜4 を順次承認 → spec 初版コミット 7f9ac269b (03:22) → ユーザー役が90度反例指摘 (10:23) → 具体座標で検証・alignmentAxis 案導出・**ベベルギア例外を自らC型質問化**（FORM）→ spec 更新コミット 64fae29bd (10:32) → 承認+終了指示 → **DONE (marker 出現) 10:47**
- nudge_count: 0 / gate 応答: 7（フォーム選択2・plain-text応答5=Section1小決定・Section2承認・Section3回答・Section4承認・90度指摘・終了指示のうち reply-1〜6）。対話型 skill の設計上の gate
- 完了マーカー: `{"status": "PASS", "design_doc": ".../lt-cleanroom/docs/superpowers/specs/2026-07-07-connector-kind-matching-design.md"}`

## 成果物

- produced-spec.md（181行）/ golden.md / transcript.jsonl / pane.txt / out/status.json / reply-1〜6.md
- trial 内コミット 7f9ac269b・64fae29bd はブランチ `feature/connector-kind-matching-design-r2` に保全（worktree は掃除済み）

## goal 判定（fresh evaluator, opus, 甘め禁止・golden比較）

**87/100 — 強い合格（≥85）**。前回 72 から +15。

| 本質差 | 前回 | 今回 | 要点 |
|---|---|---|---|
| (1) 反例探索 | 部分達成 | 部分達成（達成寄り） | 自己反証節が毎セクション出現し初回質問を駆動。ただし Section 1 の「回転は自動対応」が誤自己反証で、90度問題は自力未発見（ユーザー指摘後の処理は高品質: 実コード検証→誤り自認→spec明示修正） |
| (2) 正規化「関係」モデル | **実質未達** | **達成（強）** | 中央適合ペア表＋foreignKey を分散属性と並記して**自力提示**（原則7引用）。前回最大の残債が解消 |
| (3) 注入点＋型安全 | 部分達成 | 部分達成 | 関心分離（kind=回転不変/alignmentAxis=回転する幾何）と本体ゲート必須適用は達成。型レベル束縛（TConnectJudge相当）と汎用注入点は未到達 |
| (4) 前提宣言・拒否権 | 達成 | 達成 | 「前提（自己解決した決定—違ったら1行で指摘してください）」形式 |
| (5) C型のみ・1問ずつ | 部分達成 | 達成 | 両フォームともC型・1問。B型のハード混入なし |

### 第2ラウンド改善の効果（狙い→結果）

| 改善項目 | 結果 |
|---|---|
| 中央表の自力提示（原則7） | ✅ 解消 — 初回フォームで並記提示 |
| 自己反証節の毎回出現 | ✅ 解消（ただし1節が誤内容 → 質の課題残） |
| 1呼び出し1問 | ✅ 解消 |
| ゲーム仕様断定のC型格上げ（ベベルギア例） | ✅ 解消 — スキル記載の例そのものを自発的に質問化 |
| 型安全度比較の必須化（原則8） | ❌ 効果なし — 型レベル束縛への言及ゼロ（今回の唯一の明確な不発） |

### 自力発見（golden・ユーザー入力に無い上乗せ）

- **null-directions バイパス経路**（前回も発見）: connector 参照が捨てられ無条件接続になる抜け道 → fail-closed 成立の必須改修に格上げ
- gear 接続経路に軸判定が一切無いことの実証（GearNetworkDatastore/GearNetwork の実コード確認）
- クライアント desync 非発生の論証（プレビューは位置線描画のみ・サーバー権威）
- 90度反例への解（alignmentAxis＋外積ゼロ平行ゲート）はユーザーが問題提起のみ・解は trial 独力

## 残債（次ラウンド候補）

1. **原則8（型安全比較）の不発**: 注入ミス→コンパイルエラーの比較軸が対話にも spec にも一度も出なかった。スキル文言の提示位置・強度の見直し候補
2. 自己反証の「質」: 節は書くが検証が浅いケース（「回転は自動対応」と断定→誤り）。「自己反証は具体座標/具体入力で構築せよ」の強化余地
3. （設計トレードオフ）kind required + fail-closed は golden の optional 方式より移行コスト重（優劣は分かれると evaluator 判定）

## ハーネス申し送り（新規観測）

- **入力欄ゴーストテキスト**: ターン終了後、trial 側 Claude Code の入力欄に「suggested reply」らしきテキスト（例:「enum で始めて、対称でOK」）が自動出現することを複数回観測。send-keys 送信前に C-u でクリアしないと応答文面が汚染される。live-trial-send の前処理に C-u を入れる改修候補
- AskUserQuestion pending の GATE 検知漏れ（既知）は本 trial でも同様（poll は IDLE_TURN_COMPLETE の STALL 検知のみ）。pane 目視ウォッチャーで代替した
- 単一質問フォームは Down/Enter（または Enter のみ）で選択登録OK（複数質問フォームの Tab+Submit は今回不要だった）

## 総合判定

起動✅ / 完走✅（対話gateは設計上のもの、nudge 0）/ goal 87点 → **✅ 合格（強）**。第2ラウンド改善4項目中3項目が明確に発火・解消、1項目（原則8）不発。
