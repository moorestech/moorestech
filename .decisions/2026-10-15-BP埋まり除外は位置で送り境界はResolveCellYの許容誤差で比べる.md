決定: (1) BP貼り付けの埋まり除外は、ブロック番号ではなく貼付先のワールド位置の列でリクエストに載せる。(2) 地形めり込みの境界「底+1を超えたら不可」は、設置Yの決定と同じ許容誤差（ResolveCellY）で比べる。(3) 「プレビュー表示→埋まり判定→理由反映」の3系統での繰り返しと、電柱・歯車ポールの1セル判定の繰り返しはまとめない。(4) 届け先は master へのPRだけ（次回のプレイテスト配布で入る）。
棄却案: (1)(2) planをADRの字面（番号で送る・許容誤差なしの厳密比較）に合わせる。(3) 繰り返しを1メソッドへまとめる。(4) マージ後にプレイテスト版へも反映する。
理由: ユーザー裁定 2026-10-15。plan検査（Phase 2.6）の質問に選択「ADRをplanに合わせる (推奨)」「このままでよい (推奨)」「masterへのPRだけ (推奨)」。
リンク: docs/adr/0081-preview-has-no-collider-and-terrain-sink-gate-for-all-placement.md / docs/superpowers/plans/2026-10-15-preview-no-collider-terrain-sink.md
