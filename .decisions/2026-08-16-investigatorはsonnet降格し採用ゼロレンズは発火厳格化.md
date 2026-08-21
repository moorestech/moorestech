# investigatorはsonnet降格し採用ゼロレンズは発火厳格化

決定: 分割深掘り調査（investigator 3観点）は3本ともopus→sonnetへ降格する。採用ゼロだが既にsonnetの4レンズ（datastore-access-separation・master-data-defense・redundant-member-duplication・implicit-cardinality-assumption）は削除せず、発火条件を厳格化して残す（セレクタに`keywords_all`（AND条件）を新設し、パス条件＋実際の違反イディオムの追加行を要求）。

棄却案:
- investigatorのLuna max化（codex execバックグラウンド配管の新設）→ 工事コストと品質未検証リスクの先払いになるため棄却。sonnet降格を先に実測
- investigatorの発火閾値引き上げ・チャンク数制限 → 発火時の採用実績（適用3・設計判断3・「実害の大きい指摘の多くがここ由来」）が高く、発火自体を減らすのは非推奨として棄却
- 採用ゼロ4レンズの廃止 → 保険価値を残しつつ発火頻度だけ下げる方を選択

理由: investigator 3観点は全系統最大のコストブロック（全体の約37%・2026-08-16再監査）。sonnet降格の品質は次の大規模PRレビューのrecordsで検証する。

リンク: [[2026-08-16-採用ゼロreviewer削除とレンズ降格]] / [[2026-08-16-dead-code検知は統合でなく全面機械化する]]
