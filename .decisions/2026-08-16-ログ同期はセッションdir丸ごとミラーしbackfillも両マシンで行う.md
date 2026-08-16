# ログ同期はセッションdir丸ごとミラーしbackfillも両マシンで行う

決定: logs-syncの同期対象を `~/.claude/projects/<slug>/<セッションID>/` 配下（subagents/・tool-results/含む）丸ごとに拡張する。過去分のbackfillはMacBook・mac miniの両方で実行する。

棄却案:
- subagents/のみ同期（tool-resultsは捨てる）→ 生ログ全量保存の裁定に反し、考古学で巨大ツール出力のスピルが欠けるため棄却
- hook修正のみでbackfillしない → 過去分の監査のたびにSSH実機抽出が必要になるため棄却

理由: agent効率監査（2026-08-16）でミラーにsubagent transcriptが含まれず、mac mini分をSSH実機抽出する羽目になった。容量前倒し（pack約4GB化・soft limit 5GB接近）は許容し、接近時に古ログのアーカイブ分離で対応する。

リンク: [[2026-08-03-生ログはprivateログrepoへ全量保存する]]
