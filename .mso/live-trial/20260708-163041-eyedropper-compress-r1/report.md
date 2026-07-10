# 圧縮版スキル リグレッションテスト報告（run-skill-iter-improve 規律準拠）

## 判定: ✅ リグレッションなし（圧縮は品質を損なわなかった）

## 対象と目的
- 検証コミット: 6fb0344a7「データフロー地図検査を圧縮（-4行）」
- 比較基準: 圧縮前 6eb525bfb が Opus 実走で 92/100 PASS 済み
- 問い: 圧縮で核（矢印列・書き手/読み手/交差点・畳み直し規則）が保たれ、同じ正解形状を出せるか

## 実行機構の正当性（INVARIANT 8 準拠）
brainstorming は対話ゲートを持つため Agent 直起動は静的レビューに退化する（proxy すり替え=PASS詐欺）。本物 tmux セッションでの実走（run-skill-live-trial）＝INVARIANT 8 が要求する実挙動の成果物評価。静的 SKILL.md レビューは収束判定に一切使っていない（寄与重み0%）。

## 方法（N=2 独立 fresh live-trial）
- trial model: claude-opus-4-8（jq 実証、両本一致）。Fable 5 上限枯渇のため。圧縮前も Opus のため同条件比較。
- 2本を並列 fresh worktree（同一 HEAD 6fb0344a7）で実走。nudge 0・gate 0・完全自走。
- score 評価者: sonnet 2体（各本1体、改善履歴非共有の fresh context）
- goal 検証者: sonnet 1体、score 評価者とは別個体（Step F-2 / INVARIANT 8 判定者独立性）

## 結果
| 項目 | r1 | r2 | 圧縮前(Opus) |
|---|---|---|---|
| status | PASS | PASS | PASS |
| score(fresh sonnet) | 90 | 90 | 92 |
| A 毎フレームManualUpdate駆動 | 適合 | 適合 | 適合 |
| B 検知はサービス内部 | 適合 | 適合 | 適合 |
| C PlacementSelection一本 | 適合 | 適合 | 適合 |
| 直接セッター(禁止第2経路) | 0件 | 0件 | 0件 |
| GameScreen遷移 | OnPicked(UniRx)購読 | OnPicked(UniRx)購読 | OnPicked(UniRx)購読 |
| データフロー参照(transcript) | 10回 | 7回 | 9回 |

- score 90/90 は圧縮前 92 から ±2pt（LLM主観±3-5ptのnoise帯内）。有意な後退なし。
- 両本とも spec 冒頭に「データフロー」節を自発生成し矢印列を記述、圧縮版の文言「共有モデルへの書き手が1人増えるだけ」をそのまま適用。核が生きている証拠。
- **GOAL VERIFICATION（独立 fresh agent、PASS|FAIL のみ）: 2本とも PASS・blocker なし。**

## PASS詐欺自己審問（INVARIANT 1/8）
- 改善内容（散文圧縮）は評価経路・mode・閾値・採点対象・評価手順・収束条件に一切触れない → Step C-5(b) 非該当
- score は 92→90 の微減で +10pt 急上昇なし → (a) 非該当。外部 diff 独立判定の起動条件を満たさない
- 評価は圧縮前と同一 A/B/C ルーブリック・同一 fresh プロトコル・実走ベース。緩め操作ゼロ。
- 破棄した改善: なし（本タスクは改善投入でなくリグレッション確認のため Step D 編集なし）

## 収束判定（Step F）
- score gate: avg 90 ≥ 閾値、圧縮前から後退なし → 通過
- goal gate: 独立 fresh agent が 2本とも PASS・blocker なし → 通過
- **両gate通過 → 真の収束。圧縮版 6fb0344a7 を確定維持。**

## 評価の前提明示
最終 score は「fresh sonnet 評価者が ground truth 3要件 A/B/C に対して 0-100 採点、実ユーザーが修正指示を出すかを基準」で出したもの。trial 本体は Opus 4.8 実走。同一成果物でもモデル/ルーブリックが変われば点数は動くが、A/B/C 適合という質的判定は3fresh評価者すべてで一致。

## 残課題（次の structural change 候補、今回は投入せず）
- 両本の goal-verify で共通指摘: BlockEyedropperService の自動テストが薄く PlayMode 依存（プロジェクト事情としては許容だが設計リスク）。スキルの弱点ではなくドメイン事情。
- GameScreen 遷移の push→flag→pull 三段が僅かに複雑（3評価者とも「A/B/Cは侵さない妥当な選択」と一致）。スキル改善対象ではない。
