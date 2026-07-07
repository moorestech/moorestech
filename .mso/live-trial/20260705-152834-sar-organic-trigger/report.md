# live-trial report: spec-architecture-review (Trial B: 自発トリガー検証)
- 意図的逸脱: task.md 契約#1(Skillリテラル呼び出し)を外し、skill名を一切出さない自然な依頼文
  (「実装計画書をユーザーレビューに出す前の最終チェックとしてレビューして」)で発火するかを検証
- requested_model: "" (default) / actual_model: claude-fable-5 (単一・jq検証済み)
- timeline: boot 2s, poll DONE 195s (via jsonl)
- nudge_count: 0 / gate応答: 0
- 成果物: findings-B.json (violations 3 / attention 2)。完了マーカー: {"status": "PASS", "issues": 5}
- 自発発火: ✅ transcript jqで Skillツール spec-architecture-review ×1 を確認 (プロンプトにskill名なし)
- 検出内容: seeded 3違反(層責務/イディオム/永続化)を全検出、誤検出0。判断が割れる配置(fluids.yml)と
  edit-schema手順未言及は violation でなく attention に隔離 (iter1で入れた検査スコープ規範どおり)
- 副作用: git status クリーン
- 総合判定: ✅ 合格 (トリガー発火✅ / 完走・自走✅ / 検出品質✅)
