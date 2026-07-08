# iter 0: GOAL DECLARATION（圧縮版リグレッション）

## 対象
- target skill: brainstorming（+ 連鎖する writing-plans）
- task args: 「建設メニュー中のスポイト機能を実装したい。ミドルクリック」+ 既知回答（両ステート有効・向きコピー・全承認）
- 検証対象コミット: 6fb0344a7（データフロー地図検査を圧縮、-4行）
- 比較基準: 圧縮前 6eb525bfb は Opus 実走で 92/100 PASS 済み

## --goal（1文）
スポイト設計依頼に対し、brainstorming＋writing-plans が「共有選択モデル(PlacementSelection)への書き手が1人増えるだけ」のデータフロー一貫設計——(A)UIステートから毎フレームManualUpdate駆動 / (B)ミドルクリック検知も対象検知もサービス内部 / (C)反映はPlacementSelection一本(各PlaceSystemへの直接セッター等の第2経路なし)——を、ユーザーの修正指示なしで出せること。

## proxy 妥当性審問
- evaluator の score（3要件A/B/C適合の0-100）は goal の良い代理か? → **Yes**。A/B/C は ground truth（実ユーザー修正指示文）を直接エンコードしており、score が高い＝修正指示不要、が成立する。
- ただし Partial 面: score が高くてもエッジケース網羅・実装可能性は別。→ Step F の goal verification（PASS|FAIL + blocker）を必ず有効化。

## 緩め禁止リスト（この regression で PASS 詐欺になる操作）
1. 評価者に「圧縮で核が残っている」等の期待や改善履歴を渡す（context汚染）
2. 実走(live-trial)を省き SKILL.md 静的レビューで「核が残っているから大丈夫」と収束判定する（INVARIANT 8 違反）
3. 評価の3要件A/B/Cを緩める / 閾値を下げる / GameScreen遷移のグレーを甘く見る
4. N=1 の単発PASSで「リグレッション無し」と断定（対照・複数fresh contextを取らない）
5. Fable枯渇を理由に評価者を省略しorchestrator自己判定に置換

## 実行機構の正当性（なぜ live-trial か）
brainstorming は AskUserQuestion / plan承認 の対話ゲートを持つ。Agent直起動(Step A原型)はゲートで停止し静的レビューに退化する（INVARIANT 8が禁じる proxy すり替え）。本物tmuxセッション(run-skill-live-trial)での実走＝INVARIANT 8 が要求する「実挙動の行動ログ/成果物」評価そのもの。

## パラメータ
- N = 2（fresh 独立 live-trial 2本、圧縮版で実走）
- trial セッション model: claude-opus-4-8（Fable 5 上限枯渇のため。圧縮前も Opus で 92 のため同一条件で比較可能）
- evaluator model: sonnet（Fable枯渇の代替。fresh context 独立）
- goal-verification agent: sonnet、eval評価者とは別個体（Step F-2 / INVARIANT 8 独立性）
- 収束: 2本とも goal PASS かつ score が圧縮前(92)から有意低下なし(±5pt noise内)
