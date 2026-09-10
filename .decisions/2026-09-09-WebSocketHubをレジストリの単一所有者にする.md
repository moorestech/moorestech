# WebSocketHubをレジストリの単一所有者にする

- **決定**: `WebSocketHub` が topic/action/revision のレジストリを単独で所有し、`WebSocketMessageDispatcher` は hub の解決メソッド（`ResolveAction` 等）経由で引く。テスト（`moorestech-lnsf.6` の移植テスト3本）も同じ `ResolveAction` を通す。
- **棄却案**: `WebUiActionRegistry` を新設して action 辞書だけ切り出す。topic 側は現状維持で変更は小さいが、hub の中に「切り出された辞書」と「生の辞書」が混在して所有が二重になる。
- **理由**: `ResolveAction` に dispatcher という本番呼び出し元ができるため「デバッグ/テスト専用publicをプロダクションに残さない」（AGENTS.md）に抵触しない。あわせて hub が private 辞書3本を dispatcher へそのまま手渡している共有可変状態の臭いも消える。
- **文脈**: 移植テストが `new ElectricToGearSetOutputModeActionHandler(subInventoryState)` のように `WebUiGameBinder` の構築を書き写しており、本番配線で依存が増えてもテストが緑を保つ（`moorestech-lnsf.6`）。
- **リンク**: `moorestech-lnsf.6` / PR #1329 / [[2026-09-05-uGUIはパッケージごと完全撤去する]]
