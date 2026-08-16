# DeepSeek v4 pro対Sonnet対照実験の設計

決定:
- 並行独立2worktree（各腕がAlt自由カーソルplan Task1〜5を独立実装）
- briefは実装コードだけ剥がす（テストコード・Files・Interfaces・手順は残す）
- Task1-4は両腕並列、Task5（プレイ録画）だけ直列排他
- 成果は比較だけして破棄はしない。採否は結果を見てユーザーが判断

棄却案:
- 交互割当1worktree（前回Cursor試験形式）
- planそのまま渡す
- テストも剥がす
- 腕ごと完全直列

理由: 同一タスクの直接比較性（並行独立）／コピペ勝負ではモデル差が出ない（コード剥がし）／テストまで剥がすとAPI形状が割れ比較不能／壁時計優先で並列、Unity競合時は直列フォールバック

リンク: docs/superpowers/plans/2026-08-05-gameplay-alt-free-cursor.md / [[2026-08-05-Alt自由カーソルはユニットとunityプレイ録画テストで検証する]]
