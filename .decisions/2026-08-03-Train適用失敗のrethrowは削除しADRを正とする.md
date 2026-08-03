# Train適用失敗のrethrowは削除しADRを正とする

決定: `TrainFullSnapshotEventNetworkHandler` の `throw;`（rail :76 / train :115）を削除し、`TrySetException` で畳み切る。ADR#19「再送出はしない」と計画書 Task 6 Step 3 を正とする。`TrainFullSnapshotFailurePropagationTest` の再送出期待（4テスト）を反転する。

棄却案:
- ADR#19と計画書の方を実装に合わせて書き換える（rethrow維持）— bug-fix-intent の提案
- `VanillaApiEvent.InitializeDispatch()` のreplayループを隔離して両立させる

理由: 初期full snapshotは接続登録直後にpushされ購読より前に届くため、必ず `InitializeDispatch()` の同期replayを通る。この経路は `PacketExchangeManager` の隔離外なので、rethrowは地形構築前に起動を中断し、残りのbuffered eventが永久に配信されず（`_bufferedEvents.Clear()` 未到達のまま `_isDispatchStarted=true`）、畳んだFaultedも未観測のままGC時に `Debug.LogException` として湧く。4系統（try-catch境界verifier / Codex High① / server-state-sync / async-cancellation）が一致。

リンク: [[docs/plans/map-autogen-world-design.md ADR#19]] / PR #1104 最終レビュー 2026-08-03
