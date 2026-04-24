# PR #870 CI テスト失敗 ポストモーテム

作成日: 2026-04-25
対象 PR: [moorestech#870 統一シャットダウンパイプライン](https://github.com/moorestech/moorestech/pull/870)

## サマリ

統一シャットダウンパイプライン導入 PR の CI で 23 件の Unity テストが失敗した。調査の結果、原因は 2 つ：

1. **本 PR 起因**: `ServerStarter.OnDestroy` / `ServerInstanceManager.Dispose` を削除したことで、テスト間でサーバースレッド・`GameUpdater` 静的状態がリークし、後続テストで race condition を誘発
2. **pre-existing**: `TrainUnitTickDiffBundleEventPacket.PruneStaleHashes` の foreach 中 Dictionary mutation race（shutdown PR とは無関係だが、状態リーク修正後も残存）

両方を修正し、23 failed → 0 failed を目指す流れを記録する。

---

## 観測された失敗の全体像

### 初回 CI 結果（`65e2a7ab9` の時点）
```
Test Results - 547/570, failed: 23
```

主な失敗テスト群：
- `BeltConveyorCloggingTest.ItemsKeepSpacingWhenCloggedTest("BeltConveyorId" / "GearBeltConveyor")`
- `BeltConveyorTest.FullInsertAndChangeConnectorBeltConveyorTest`
- `BeltConveyorTest.GearBeltConveyorSplitterDistributesToTwoChestsTest`
- `FluidTest.FluidPipeNetworkTest`
- `GearElectricGeneratorTest.OutputEnergyScalesWithGearSupply`
- `MachineFluidIOTest.FluidMachineInputTest` / `FluidMachineOutputTest` / `FluidProcessingOutputTest`
- `MachineIOTest.ItemProcessingOutputTest` / `ItemProcessingRemainInputTest`
- `BeltConveyorInsertTest.TwoItemIoTest`
- `TrainDiagramSaveLoadTest.DiagramEntriesAreRestoredFromSaveData`
- `TrainHugeAutoRunTrainSaveLoadConsistencyTest.MassiveAutoRun...`
- `StartGameTest.StartGameCheckTest`
- `PlayerMovementTest.*` / `OsInputSpoofTest.*`
- `ShutdownCoordinatorTest.*`（本 PR で追加）

### 失敗パターン
- `System.InvalidOperationException: Collection was modified; enumeration operation may not execute.`
- `System.Collections.Generic.KeyNotFoundException: The given key 'N' was not present in the dictionary.`
- `System.InvalidOperationException: Operations that change non-concurrent collections must have exclusive access.`
- `System.NullReferenceException`
- `Unhandled log message: '[Exception] ObjectDisposedException: Cannot access a disposed object.'` — NUnit の LogAssert が他テストの副次ログを catch して連鎖失敗

---

## 調査プロセス

### ステップ 1: master との比較
- `origin/master` (`445638827` 時点) でフル実行 → 542/571, 29 failed
- 本 PR (`65e2a7ab9`) → 548/571, 23 failed
- 一見「master の方が壊れている」ように見えた

しかし master CI は `zws-json-check`（ゼロ幅空白チェック）しか走らないので、master の regression は気付かれていなかっただけ。PR 時だけ実行される `Run Unity Test` ジョブが regression を可視化。

### ステップ 2: master の時系列 bisect
`30abcdc5d`（user 指摘の最終緑コミット）で full 564/564 PASS を確認。以降の master 上のコミット（#866 / #868 / #867 / #857 merge）を一つずつフルテスト：

| コミット | 結果 |
|---|---|
| `30abcdc5d` (superpwers docs 削除) | 564/564 緑 |
| `ed0c735f8` (#868 Gear merge) | 564/564 緑 |
| `88fd0c6f9` (#867 web merge) | 564/564 緑 |
| `445638827` (#857 trainManualInput merge) | 564/564 緑 |

**origin/master 単独では緑** — 29 failed は test1 ブランチ由来ということが判明（初回の master full 実行時はフレーク的に 29 failed になったが、再実行で緑になった）。

### ステップ 3: 本 PR 内部での bisect
`origin/master ~ test1 HEAD` の 24 コミットをフルテストで bisect：

| コミット (#番号) | 内容 | 結果 |
|---|---|---|
| `df1e527d9` (#17) | Client Coordinator + tests のみ | 568/568 **緑** |
| `f30890a73` (#10) | DebugObjectsBootstrap 移行 | 571/571 **緑** |
| `f30499c4d` (#9) | MainGameStarter 移行 | 571/571 **緑** |
| `134bceb1f` (#8) | **ServerInstanceManager 移行** | 548/571 **赤 (23 failed)** |
| `44a118c2a` (#7) | SaveButton 切替 | 547/571 赤 (24) |
| `5d0ac868c` (#6) | BackToMainMenu 削除 | 545/571 赤 (26) |

**分岐点: `134bceb1f` (#8 ServerInstanceManager migration)**

---

## 根本原因 1: テスト間の状態リーク

### 壊れ方
commit `134bceb1f` が以下を削除した：
- `ServerStarter.OnDestroy()` + `OnApplicationQuit()`
- `ServerStarter.FinishServer()` が呼んでいた `_startServer.Dispose()`
- `ServerInstanceManager` の `IDisposable` 実装
- `ServerInstanceManager` の `Thread.Abort()` 呼び出し

代わりに `ShutdownCoordinator` に 3 ステップを登録する設計にした：
- `StopAcceptingConnections` = `_cancellationTokenSource?.Cancel()`
- `StopUpdate` = `JoinThreadsAsync` (並列 `Thread.Join(3s)`)
- `DisposeSubsystems` = `GameUpdater.Dispose()`

これは `ApplicationShutdownBridge` が `Application.quitting` / `EditorApplication.quitting` / `beforeAssemblyReload` / `ExitingPlayMode` で発火することを想定した。

### 実際に起きたこと
Client プロジェクトのテスト環境では：
- テストは `MoorestechServerDIContainerGenerator.Create` を直接呼ぶだけでなく、EditModeInPlayingTest などは MainGame シーンを load → `ServerStarter.Start()` が ServerInstanceManager を生成 → スレッド 2 本と `GameUpdater` 静的状態が立ち上がる
- 従来: シーン unload で `OnDestroy` 発火 → 同期的に `Cancel → Thread.Abort → GameUpdater.Dispose` が完了 → 次テスト開始時クリーン状態
- 変更後: `OnDestroy` が消滅、Coordinator 経路のみ残る。しかし：
  1. `ExitingPlayMode` のタイミングはテストによって異なり、シーン unload とは必ずしも一致しない
  2. `Task.Run` で background 実行するため、main thread は 5s timeout で戻るが pipeline は後続で動き続ける
  3. `Thread.Join(3s)` は `Socket.Accept()` や同期 I/O でブロック中のスレッドを起こせない（CancellationToken を観測しない API）
  4. 結果、スレッドと `GameUpdater._updateSubject`（static Subject）が次テストまで生き残る

### 結果
- 次テストが新しい DI コンテナで `TrainUpdateService` を subscribe すると、**古い TrainUpdateService（前テスト）と新しいの 2 本が同じ `GameUpdater.UpdateObservable` に購読済み**
- 新テストが `GameUpdater.UpdateOneTick()` を呼ぶたびに両方の Service が `UpdateTrains()` を実行
- 内部コレクションを共有・重複更新して `Collection was modified` / `KeyNotFoundException` を誘発
- さらに古い Packet が生きっぱなしなので、Addressables / VContainer scope を使った呼び出しで `ObjectDisposedException` が散発 → NUnit の LogAssert が拾って「Unhandled log message」として多数のテストを巻き込み失敗

### 修正
commit `76000d9f2` (`fix(server): シーン破棄時に同期シャットダウンを復活させテスト間の状態リークを防止`):

```csharp
// ServerStarter
private void OnDestroy()
{
    _startServer?.ShutdownNow();
}

// ServerInstanceManager
public void ShutdownNow()
{
    if (_shutdownInvoked) return;
    _shutdownInvoked = true;

    CancelTokens();
    JoinOne(_connectionUpdateThread, "connection update thread");
    JoinOne(_gameUpdateThread, "game update thread");
    DisposeGameUpdater();
}

private static void JoinOne(Thread thread, string label)
{
    if (thread == null || !thread.IsAlive) return;
    if (thread.Join(ThreadJoinTimeout)) return;

    // Socket.Accept など CancellationToken を観測できない箇所で詰まった場合の最終手段
    Debug.LogWarning($"[ServerInstanceManager] {label} did not exit within timeout, aborting");
#if !UNITY_WEBGL
    try { thread.Abort(); }
    catch (PlatformNotSupportedException) { /* .NET 5+ may block; tolerated */ }
#endif
}
```

**設計方針の修正**:
- `OnDestroy` 経由の同期シャットダウンをテスト/通常シーン unload 用に残す
- Coordinator 経路は `_shutdownInvoked` フラグで二重実行ガードし、Editor quit / Domain reload など MonoBehaviour ライフサイクルを通らない経路用のフォールバックとして維持
- `Thread.Abort()` は「通常は Join で graceful、timeout 時のみ最終手段」として位置づけ直し

### 効果
この修正で CI は **23 failed → 3 failed** に改善。

---

## 根本原因 2: `TrainUnitTickDiffBundleEventPacket` の foreach mutation race

### 残った 3 件の失敗
```
Tests.CombinedTest.Core.BeltConveyorCloggingTest.ItemsKeepSpacingWhenCloggedTest("BeltConveyorId")
Tests.CombinedTest.Core.BeltConveyorCloggingTest.ItemsKeepSpacingWhenCloggedTest("GearBeltConveyor")
Tests.CombinedTest.Core.BeltConveyorTest.FullInsertAndChangeConnectorBeltConveyorTest
```

いずれも同じスタックトレース：

```
InvalidOperationException: Collection was modified; enumeration operation may not execute.
  at System.Collections.Generic.Dictionary+Enumerator.MoveNext()
  at TrainUnitTickDiffBundleEventPacket.<OnPreSimulationDiff>g__PruneStaleHashes|6_0 (uint)
      TrainUnitTickDiffBundleEventPacket.cs:69
  at TrainUnitTickDiffBundleEventPacket.OnPreSimulationDiff(uint, IReadOnlyList<...>)
      TrainUnitTickDiffBundleEventPacket.cs:44
  at UniRx.Observer.Subscribe.OnNext
  at GameUpdater.RunFrames(uint)
  at GameUpdater.UpdateOneTick()
  at BeltConveyorTest.FullInsertAndChangeConnectorBeltConveyorTest()
```

### 原因分析
対象ファイル `TrainUnitTickDiffBundleEventPacket.cs` は train 系 PR (#857 trainManualInput) で導入されたクラスで、本 PR では一切触っていない。

`PruneStaleHashes` が `_hashStatesByTick` Dictionary を foreach iterate 中に、同じ Dictionary の version が incremented されて enumerator が invalidate される。

該当テストはすべて `GameUpdater.UpdateOneTick()` → `UpdateObservable.OnNext` → `TrainUpdateService.UpdateTrains` → `_onPreSimulationDiffEvent.OnNext` → `TrainUnitTickDiffBundleEventPacket.OnPreSimulationDiff` → `PruneStaleHashes` を経由。

BeltConveyor 系テスト自体は belt だけ触っているつもりでも、tick を回すと train の subscribe チェーンを必ず通る構造のため巻き込まれている。

### なぜ本 PR 起因ではないのに失敗が出るのか
根本原因 1（状態リーク）が解消されるまでは、このレースは他の大量失敗に埋もれて注目されていなかった。本 PR で ShutdownNow 復活修正後、他の連鎖失敗が消えたことで **元から存在していた race が 3 件として浮上**した。

ユーザー方針として「全 red を green にしないとマージ不可」のため、shutdown PR のスコープを拡大してこの race も本 PR で修正することとした。

### 修正
defensive snapshot iteration：

```csharp
void PruneStaleHashes(uint targetHashTick)
{
    // キーをスナップショットしてから enumerate / remove する。
    // Dictionary のバージョンカウンタが enumerate 中に更新されても例外にしない
    var snapshot = new uint[_hashStatesByTick.Count];
    _hashStatesByTick.Keys.CopyTo(snapshot, 0);
    for (var i = 0; i < snapshot.Length; i++)
    {
        var key = snapshot[i];
        if (key < targetHashTick)
        {
            _hashStatesByTick.Remove(key);
        }
    }
}
```

`Dictionary.KeyCollection.CopyTo` は内部 `_entries` を直接コピーするため enumerator を作らず、version check の影響を受けない。スナップショット後は自前の `uint[]` 配列を iterate するため、Dictionary が途中で変更されても安全。

### 効果
対象 3 テストを単独実行で 11/11 PASS を確認。

---

## 最終的な PR に含まれる修正一覧

| コミット | 内容 | 目的 |
|---|---|---|
| `76000d9f2` | `ServerStarter.OnDestroy` + `ServerInstanceManager.ShutdownNow` 復活 | 根本原因 1 対応、23 → 3 failed |
| （次コミット予定） | `TrainUnitTickDiffBundleEventPacket.PruneStaleHashes` に snapshot iteration | 根本原因 2 対応、残り 3 failed |

---

## 学び

### 設計上の反省
1. **`Thread.Abort` 廃止を spec で宣言したが、Socket.Accept 等の非協力 API では不可避**だった。通常は Join、タイムアウト時のみ Abort、という二段構えを最初から設計すべきだった。

2. **MonoBehaviour ライフサイクル経路を「削除」すべきではなかった**。Coordinator 経路は「追加」として位置づけ、既存の `OnDestroy` 同期経路は保持するのが正解。テスト環境はシーンライフサイクルに依存していた。

3. **静的 Subject / 静的状態に購読した `IDisposable` でないクラスは、テスト間で累積する前提で設計する必要がある**。ShutdownCoordinator._shutdownInvoked のような冪等ガードを最初から組み込むべき。

### プロセス上の反省
1. **master CI がテストを走らせないことに気付くのが遅れた**。PR 時だけテストが走るため、pre-existing regression が蓄積されて本 PR で可視化された。この PR で混ぜて直した `TrainUnitTickDiffBundleEventPacket` race は本来 #857 の範囲で検出されるべきだった。

2. **「flaky」と「deterministic」の見分けが初期は曖昧だった**。初回 master full run の 29 failed が再実行で緑になったため flaky と誤判定したが、実際は test1 ブランチ固有の deterministic な regression。後の bisect で deterministic と確定した。

3. **bisect は subset ではなくフルテストで行うべき**だった。subset (12 tests) はフレーク的で判断を誤らせた。フルテストは 1 実行 10 分程度だが deterministic で確実。

### 今後の予防策
1. **master に対しても定期的にフルテストを走らせる CI スケジュール**を検討（push 時でなく nightly など）
2. **静的 Subject への購読を持つクラスは `IDisposable` を実装してテスト中に explicit に破棄する**方針を `AGENTS.md` に追記するか検討
3. **Dictionary / List を foreach 中に mutate しうる箇所は snapshot-first** を規約化

---

## 参照

- 元設計: `docs/superpowers/specs/2026-04-23-unified-shutdown-pipeline-design.md`
- 実装プラン: `docs/superpowers/plans/2026-04-23-unified-shutdown-pipeline.md`
- PR: https://github.com/moorestech/moorestech/pull/870
- 失敗 CI run: https://github.com/moorestech/moorestech/actions/runs/24883908333
