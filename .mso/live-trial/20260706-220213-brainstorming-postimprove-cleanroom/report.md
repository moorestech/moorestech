# brainstorming 改善後クリーンルーム検証レポート (2026-07-06)

## 目的

「改善後スキルで、元の歯車接続の相談に対し、ユーザーが求める品質のアウトプット（参照プラン=案B相当）が出るか」の受け入れ検証。設計書が存在しなかった時点 (1968f7dc6) の隔離 worktree (/Users/katsumi/moorestech-worktrees/lt-cleanroom) に改善後スキルのみを重ね、**元の質問文そのまま**を送信。

## model 検証

- requested_model: claude-opus-4-8 / actual_model (jq): claude-opus-4-8 → ✅ 一致

## timeline

- boot 3s (22:02) → 発火 (2手目で Skill brainstorming) → 調査 (subagent 2並列) → 前提宣言+方針質問 22:12 → 設計骨子+質問フォーム2回 → spec 初版コミット 22:38 → ユーザー役が90度反例を指摘 22:53 → 軸平行チェック+述語注入で spec 更新コミット 22:54 → 承認+終了指示 → **DONE (exit 0, via jsonl) 23:0x**
- nudge_count: 0 / 対話応答: 5 (方針1・フォーム2・90度指摘1・承認終了1)。対話型 skill の設計上の gate
- 完了マーカー: {"status": "PASS", "design_doc": ".../lt-cleanroom/docs/superpowers/specs/2026-07-06-connector-kind-matching-design.md"}

## 成果物

- produced-spec.md (145行, trial が生成した設計書のコピー) / golden.md (ユーザー提示の参照プラン) / transcript.jsonl / pane.txt / out/status.json
- trial 内コミットは隔離 worktree のブランチ feature/connector-kind-matching-design に保全 (worktree は掃除済み、ブランチは残置)

## goal 判定 (fresh evaluator, opus, 甘め禁止指示)

**72/100 — 合格 (下限・条件付き)**。本質差5点の内訳:

| 観点 | 判定 |
|---|---|
| (1) 自案への反例探索 | 部分達成 — 90度は自力未発見 (参照対話でもユーザー入力のため公平性で中和)。既存潜在バグ2件 (null無条件接続NRE/後勝ち上書き) は**ゴールデンにも無い上乗せ自力発見**。指摘後の処理 (データ表現限界の認識→実行時軸チェック→述語注入→原則引用) は優秀 |
| (2) 互換性の正規化「関係」モデル | 実質未達 — per-connector kind+accepts (非正規化)。中央ペア表+foreignKey を**案として提示すらしなかった**のが最大の残債 |
| (3) 注入点+型安全 | 部分達成 — エンジン無知の述語注入は達成、型レベル束縛 (コンパイルエラー保証) には未到達 (参照でもユーザー種蒔き、減点軽減) |
| (4) 番号付き前提宣言・拒否権形式 | 達成 — 毎質問の先頭に「前提（…違ったら指摘してください）」 |
| (5) C型質問のみ・1問ずつ | 部分達成 — Q2適合ルールが推奨リトマス漏れ (YAGNI根拠推奨=B型を質問化)、AskUserQuestion への2問同梱×2回 |

## 第2ラウンド改善 (評価の未達点→SKILL.md へ適用済み)

1. 一般設計原則に **7: 関係データは中央正規化テーブル第一候補 (SSOT)** / **8: 注入点は実行時注入 vs 型レベル束縛の型安全度比較を必須化** を追加
2. **Self-refutation before presenting (required)** 節を新設 — 設計提示前に「成立してはいけないのに成立する具体入力」を1件構築して自分で叩く。表現不能なら拡張点追加のシグナル。ゲーム仕様断定の前提はC型質問に格上げ
3. AskUserQuestion の **1呼び出し1問** 明文化 + 送信直前の推奨リトマス最終チェック

第2ラウンド改善の効果は未検証 (再クリーンルーム trial が必要)。

## ハーネス申し送り (再確認)

- AskUserQuestion pending の GATE 検知漏れが本 trial でも再現 (gate_ticks=0 のまま)。live-trial-status.sh の分類要修正
- 複数質問フォーム (questions 配列2件) は send-keys の数字キーでは選択登録されず、Down/Enter+Tab 操作が必要だった
