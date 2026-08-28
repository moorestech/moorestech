# generatedワールドの内容ベースID＋スナップショット復元 実装plan

## Requirements
- ADR: docs/adr/0037-generated-world-content-id-and-snapshot-restore.md
- worldId = SHA256(seed:生成マスタ指紋:generator版)[0..16]（generatedのみ）
- EnsureWorld: 生成前にIDを算出 → 旧キャッシュGC → 同梱源/共有キャッシュから復元 → ミス時は生成＋先焼き＋共有キャッシュへ書き戻し
- ビルド: `game/worldSnapshots/<id>/` へ共有キャッシュをコピー（無ければ一時ワールドで生成）

## Tasks
1. `Contract/Transfer/WorldIdentity` 新設、`TerrainTransferMetaReader` から利用（template は seed:createdAt 維持）
2. `Provisioning/WorldSnapshotStore`: `TryRestore(worldRoot, serverDataDirectory, worldId)` / `Store(worldRoot, worldId)`
3. `Provisioning/StaleWorldCacheCollector.Collect(currentWorldId)`
4. `WorldProvisioner.EnsureWorld` の分岐差し替え
5. Editor `WorldSnapshotBundler` を `BuildPipeline` の GameDataBundler 直後に追加
6. 単体テスト: WorldIdentity決定性、Store→TryRestore往復、GCが現在IDを残す
7. コンパイル → `Tests.UnitTest.Game.MapGeneration` 回帰 → Editor実測（復元起動）
