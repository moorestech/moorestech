# 乗車システム Phase 2: サーバー乗車コア 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 乗車状態をサーバー権威で管理する中核（`PlayerRidingDatastore` / `RidableResolver` / `IRidable`）と、その永続化を実装する。

**Architecture:** 乗車状態の決定ロジックを `PlayerRidingDatastore` に集約する（仕様書セクション4.0）。`RidableResolver` だけが `IRidableIdentifier` → 乗り物実体（`IRidable`）を解決し、`PlayerRidingDatastore` のみがそれを使う。`TrainCar` を `IRidable` に適合させる。乗車状態は `WorldSaveAllInfoV1` に追加して永続化する。

**Tech Stack:** C# / Unity / Microsoft.Extensions.DependencyInjection / Newtonsoft.Json / NUnit。

**前提:** Phase 1 完了済み（`IRidableIdentifier` / `TrainCarRidableIdentifier` / `RidableIdentifierConverter` / `RidableIdentifierMessagePack` / 座席マスタ `ridableSeat` / `TrainCarSaveData.TrainCarInstanceId`）。設計仕様: `docs/superpowers/specs/2026-05-20-riding-system-design.md`（セクション4・8・10）。作業ディレクトリ `/Users/katsumi/moorestech`。

**重要な既存事実:**
- `TrainCar`（`Game.Train/Unit/TrainCar.cs`）は `TrainCarInstanceId TrainCarInstanceId` プロパティを持つ。
- `ITrainUnitLookupDatastore.TryGetTrainCar(TrainCarInstanceId, out TrainCar)` で車両を引ける（`Game.Train/Unit/`）。
- DI 登録は `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` の本体コレクション `services`。`TrainCarRidingInputBuffer` が `services.AddSingleton<TrainCarRidingInputBuffer>();`（約169行目）で登録済み。`ServerContext.GetService<T>()` は本体コレクションのサービスを返す。
- セーブ集約は `Game.SaveLoad/Json/AssembleSaveJsonText.cs`、ロードは `Game.SaveLoad/Json/WorldLoaderFromJson.cs`、DTO は `Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`。
- `WorldLoaderFromJson.Load()` は列車を `_trainSaveLoadService.RestoreTrainStates(load.TrainUnits)` で復元する（約128行目）。

---

## ファイル構成

**新規作成:**
- `Game.PlayerRiding.Interface/IRidable.cs` — 乗り物実体のサーバー側インターフェース
- `Game.PlayerRiding.Interface/RidingState.cs` — 1プレイヤーの乗車状態
- `Game.PlayerRiding.Interface/RideActionResult.cs` — 乗車/降車の結果 enum
- `Game.PlayerRiding.Interface/IPlayerConnectionChecker.cs` — 接続中判定の抽象（Phase 3 が実装を差し替える）
- `Game.PlayerRiding/Game.PlayerRiding.asmdef` — 乗車コアアセンブリ
- `Game.PlayerRiding/RidableResolver.cs` — 識別子 → `IRidable` 解決
- `Game.PlayerRiding/PlayerRidingDatastore.cs` — 乗車状態の中核データストア
- `Game.PlayerRiding/AlwaysConnectedChecker.cs` — `IPlayerConnectionChecker` の暫定実装
- `Game.PlayerRiding/PlayerRidingSaveData.cs` — 乗車状態のセーブ DTO
- `Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs` — 乗車コアのテスト

**変更:**
- `Game.Train/Game.Train.asmdef` — `Game.PlayerRiding.Interface` を参照に追加
- `Game.Train/Unit/TrainCar.cs` — `IRidable` を実装
- `Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs` — `playerRidingStates` 追加
- `Game.SaveLoad/Json/AssembleSaveJsonText.cs` — セーブに `PlayerRidingDatastore` を組み込む
- `Game.SaveLoad/Json/WorldLoaderFromJson.cs` — ロードに乗車状態復元を追加
- `Server.Boot/MoorestechServerDIContainerGenerator.cs` — `RidableResolver` / `PlayerRidingDatastore` / `AlwaysConnectedChecker` を DI 登録

**注意:** `.asmdef` は手作成可、`.meta` は Unity 自動生成。新規 asmdef 追加後は Unity を起動／コンパイルさせて `.meta` を生成する。

---

## Task 1: IRidable / RidingState / RideActionResult / IPlayerConnectionChecker

`Game.PlayerRiding.Interface`（Phase 1 で作成済みアセンブリ）に、`Game.Train` 非依存の型を追加する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/IRidable.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/RidingState.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/RideActionResult.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/IPlayerConnectionChecker.cs`

- [ ] **Step 1: RideActionResult を実装**

`RideActionResult.cs`:

```csharp
namespace Game.PlayerRiding.Interface
{
    // 乗車/降車要求の結果。RideActionProtocol のレスポンスにも使う（Phase 3）。
    // Result of a ride/dismount request. Also used in the RideActionProtocol response (Phase 3).
    public enum RideActionResult : byte
    {
        Success,
        NoSeatAvailable,
        RidableNotFound,
        AlreadyRiding,
        NotRiding,
    }
}
```

- [ ] **Step 2: IRidable を実装**

`IRidable.cs`:

```csharp
namespace Game.PlayerRiding.Interface
{
    // 乗り物実体が実装するサーバー側インターフェース。
    // サーバーは座席数のみを使う（座席のワールド座標は計算しない。仕様書セクション3・4.5）。
    // Server-side interface implemented by a ridable. The server only needs seat count.
    public interface IRidable
    {
        IRidableIdentifier Identifier { get; }
        int SeatCount { get; }
    }
}
```

- [ ] **Step 3: RidingState を実装**

`RidingState.cs`:

```csharp
namespace Game.PlayerRiding.Interface
{
    // プレイヤー1人の乗車状態（プレイヤー ⇄ 乗り物 ⇄ 座席）。
    // One player's riding state (player <-> ridable <-> seat).
    public class RidingState
    {
        public IRidableIdentifier Identifier { get; }
        public int SeatIndex { get; }

        public RidingState(IRidableIdentifier identifier, int seatIndex)
        {
            Identifier = identifier;
            SeatIndex = seatIndex;
        }
    }
}
```

- [ ] **Step 4: IPlayerConnectionChecker を実装**

`IPlayerConnectionChecker.cs`:

```csharp
namespace Game.PlayerRiding.Interface
{
    // プレイヤーが接続中かを判定する抽象。座席占有判定が「接続中プレイヤーのみ」を対象にするため必要。
    // Phase 2 では常に true を返す暫定実装を使い、Phase 3 で実接続レジストリに差し替える。
    // Abstraction for "is this player connected". Phase 3 replaces the stub implementation.
    public interface IPlayerConnectionChecker
    {
        bool IsConnected(int playerId);
    }
}
```

- [ ] **Step 5: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface
git commit -m "乗車Phase2: IRidable/RidingState/RideActionResult/IPlayerConnectionChecker を追加"
```

---

## Task 2: TrainCar を IRidable に適合

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Game.Train.asmdef`
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainCar.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs`

- [ ] **Step 1: Game.Train asmdef に参照を追加**

`Game.Train/Game.Train.asmdef` の `references` 配列に `"Game.PlayerRiding.Interface"` を追加する。

- [ ] **Step 2: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs` を新規作成。`creating-server-tests` skill の初期化パターン（`MasterHolder` 初期化等）に従うこと。

```csharp
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
using NUnit.Framework;

namespace Tests.UnitTest.PlayerRiding
{
    public class PlayerRidingDatastoreTest
    {
        [Test]
        public void TrainCar_ImplementsIRidable_WithSeatCountFromMaster()
        {
            // TrainCar が IRidable であり、SeatCount をマスタの座席数から返すことを確認
            // TrainCar implements IRidable and exposes SeatCount from master data.
            var car = TrainTestHelper.CreateTrainCarWithSeats(2); // 座席2席のマスタで車両を生成（ヘルパは既存テストに合わせ実装）
            IRidable ridable = car;

            Assert.IsInstanceOf<TrainCarRidableIdentifier>(ridable.Identifier);
            Assert.AreEqual(2, ridable.SeatCount);
            Assert.AreEqual(car.TrainCarInstanceId.AsPrimitive(),
                ((TrainCarRidableIdentifier)ridable.Identifier).TrainCarInstanceId);
        }
    }
}
```

注: `TrainTestHelper.CreateTrainCarWithSeats(int)` は仮。既存の列車テストの車両生成方法（`grep -rn "new TrainCar(" moorestech_server/Assets/Scripts/Tests*`）に合わせて実装する。座席数はテスト用マスタの `ridableSeats` 件数で決まる。テスト用マスタの座席設定方法は `creating-server-tests` skill / 既存の列車マスタテストを参照。

- [ ] **Step 3: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCar_ImplementsIRidable"`
Expected: コンパイルエラー（`TrainCar` が `IRidable` 非実装）で FAIL。

- [ ] **Step 4: TrainCar に IRidable を実装**

`TrainCar.cs` のクラス宣言（22行目 `public class TrainCar : ITrainDiagramCar`）を変更:

```csharp
    public class TrainCar : ITrainDiagramCar, IRidable
```

ファイル先頭の using に `using Game.PlayerRiding.Interface;` を追加。クラス内（プロパティ群の適切な位置、例えば `TrainCarInstanceId` プロパティの直後）に以下を追加:

```csharp
        // IRidable 実装: 乗り物としての識別子と座席数を公開する
        // IRidable implementation: exposes the ridable identifier and seat count.
        IRidableIdentifier IRidable.Identifier => new TrainCarRidableIdentifier(_trainCarInstanceId.AsPrimitive());
        int IRidable.SeatCount => TrainCarMasterElement.RidableSeats?.Length ?? 0;
```

注: `RidableSeats` は Phase 1 Task 4 で生成された `TrainCarMasterElement` のプロパティ名。Phase 1 Task 4 Step 4 で確認した実際の名前・型に合わせること（配列でなく `IReadOnlyList` 等なら `.Count` に変える）。`AsPrimitive()` は `TrainCarInstanceId`（`[UnitOf(typeof(long))]` 生成型）の long 取り出し。

- [ ] **Step 5: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCar_ImplementsIRidable"`
Expected: PASS。

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Train moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs
git commit -m "乗車Phase2: TrainCar を IRidable に適合"
```

---

## Task 3: Game.PlayerRiding アセンブリと RidableResolver

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding/Game.PlayerRiding.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding/RidableResolver.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding/AlwaysConnectedChecker.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs`

- [ ] **Step 1: アセンブリ定義を作成**

`Game.PlayerRiding/Game.PlayerRiding.asmdef`:

```json
{
  "name": "Game.PlayerRiding",
  "rootNamespace": "",
  "references": [
    "Game.PlayerRiding.Interface",
    "Game.Train",
    "Game.Context",
    "Server.Util"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

Unity を起動／コンパイルさせ `.meta` を生成する。

- [ ] **Step 2: 失敗するテストを書く**

`PlayerRidingDatastoreTest.cs` に追記:

```csharp
        [Test]
        public void RidableResolver_ResolvesExistingTrainCar_AndReturnsNullForMissing()
        {
            // 登録済み車両は解決でき、未知のIDは null を返す
            // A registered car resolves; an unknown id returns null.
            var (resolver, lookupDatastore, car) = TrainTestHelper.CreateResolverWithOneTrainCar();

            var existing = resolver.Resolve(new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive()));
            var missing = resolver.Resolve(new TrainCarRidableIdentifier(-1L));

            Assert.IsNotNull(existing);
            Assert.AreEqual(car.TrainCarInstanceId.AsPrimitive(),
                ((TrainCarRidableIdentifier)existing.Identifier).TrainCarInstanceId);
            Assert.IsNull(missing);
        }
```

`TrainTestHelper.CreateResolverWithOneTrainCar()` は、`TrainUnitDatastore` を作って車両を1両登録し、`new RidableResolver(trainUnitLookupDatastore)` を返すヘルパ。`TrainUnitDatastore` への列車登録は `RegisterTrain(TrainUnit)`（既存）を使う。既存の列車テストの登録方法に合わせて実装する。

- [ ] **Step 3: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RidableResolver_Resolves"`
Expected: コンパイルエラー（`RidableResolver` 未定義）で FAIL。

- [ ] **Step 4: RidableResolver を実装**

`Game.PlayerRiding/RidableResolver.cs`:

```csharp
using Game.PlayerRiding.Interface;
using Game.Train.Unit;
using Server.Util.MessagePack;

namespace Game.PlayerRiding
{
    // IRidableIdentifier から乗り物実体 IRidable を解決する。
    // PlayerRidingDatastore からのみ使われる（仕様書セクション4.0・4.2）。
    // Resolves an IRidable from an IRidableIdentifier. Used only by PlayerRidingDatastore.
    public class RidableResolver
    {
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;

        public RidableResolver(ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
        }

        // 解決できない（乗り物が存在しない）場合は null を返す。
        // Returns null when the ridable does not exist.
        public IRidable Resolve(IRidableIdentifier identifier)
        {
            switch (identifier.Type)
            {
                case RidableType.TrainCar:
                    var trainCarId = new TrainCarInstanceId(((TrainCarRidableIdentifier)identifier).TrainCarInstanceId);
                    if (_trainUnitLookupDatastore.TryGetTrainCar(trainCarId, out var trainCar))
                    {
                        return trainCar;
                    }
                    return null;
                default:
                    return null;
            }
        }
    }
}
```

- [ ] **Step 5: AlwaysConnectedChecker を実装**

`Game.PlayerRiding/AlwaysConnectedChecker.cs`:

```csharp
using Game.PlayerRiding.Interface;

namespace Game.PlayerRiding
{
    // IPlayerConnectionChecker の暫定実装。Phase 3 で実接続レジストリに差し替える。
    // Stub IPlayerConnectionChecker. Phase 3 replaces it with the real connection registry.
    public class AlwaysConnectedChecker : IPlayerConnectionChecker
    {
        public bool IsConnected(int playerId) => true;
    }
}
```

- [ ] **Step 6: Tests.UnitTest の asmdef に参照を追加**

`Tests.UnitTest` の `.asmdef` の `references` に `"Game.PlayerRiding"` を追加。

- [ ] **Step 7: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RidableResolver_Resolves"`
Expected: PASS。

- [ ] **Step 8: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding moorestech_server/Assets/Scripts/Tests.UnitTest
git commit -m "乗車Phase2: Game.PlayerRiding アセンブリと RidableResolver を追加"
```

---

## Task 4: PlayerRidingDatastore — TryRide / TryDismount

乗車状態の中核。`playerId ⇄ RidingState` の双方向管理と、乗車・降車ロジック。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerRidingDatastore.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`PlayerRidingDatastoreTest.cs` に追記:

```csharp
        [Test]
        public void PlayerRidingDatastore_TryRide_AssignsFreeSeat_AndRejectsWhenFull()
        {
            // 座席2席の車両: 1人目・2人目は乗車成功、3人目は NoSeatAvailable
            // A 2-seat car: first and second riders succeed, third gets NoSeatAvailable.
            var (datastore, car) = TrainTestHelper.CreateDatastoreWithOneTrainCar(seatCount: 2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            Assert.AreEqual(RideActionResult.Success, datastore.TryRide(playerId: 1, id, out _));
            Assert.AreEqual(RideActionResult.Success, datastore.TryRide(playerId: 2, id, out _));
            Assert.AreEqual(RideActionResult.NoSeatAvailable, datastore.TryRide(playerId: 3, id, out _));
        }

        [Test]
        public void PlayerRidingDatastore_TryRide_RejectsWhenAlreadyRiding_AndUnknownRidable()
        {
            var (datastore, car) = TrainTestHelper.CreateDatastoreWithOneTrainCar(seatCount: 2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            Assert.AreEqual(RideActionResult.Success, datastore.TryRide(1, id, out _));
            Assert.AreEqual(RideActionResult.AlreadyRiding, datastore.TryRide(1, id, out _));
            Assert.AreEqual(RideActionResult.RidableNotFound, datastore.TryRide(2, new TrainCarRidableIdentifier(-1L), out _));
        }

        [Test]
        public void PlayerRidingDatastore_TryDismount_ClearsState()
        {
            var (datastore, car) = TrainTestHelper.CreateDatastoreWithOneTrainCar(seatCount: 2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            Assert.AreEqual(RideActionResult.NotRiding, datastore.TryDismount(1));
            datastore.TryRide(1, id, out _);
            Assert.AreEqual(RideActionResult.Success, datastore.TryDismount(1));
            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
        }
```

`TrainTestHelper.CreateDatastoreWithOneTrainCar(int seatCount)` は、`RidableResolver` と `AlwaysConnectedChecker` を使って `new PlayerRidingDatastore(resolver, checker)` を構築し、座席数 `seatCount` の車両を1両登録した状態を返すヘルパ。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRidingDatastore_TryRide|PlayerRidingDatastore_TryDismount"`
Expected: コンパイルエラー（`PlayerRidingDatastore` 未定義）で FAIL。

- [ ] **Step 3: PlayerRidingDatastore を実装**

`Game.PlayerRiding/PlayerRidingDatastore.cs`:

```csharp
using System.Collections.Generic;
using Game.PlayerRiding.Interface;

namespace Game.PlayerRiding
{
    // 乗車状態の中核データストア。乗車可否・空席割当・降車の決定ロジックを集約する（仕様書セクション4.0・4.1）。
    // Core datastore for riding state. Owns ride/dismount decision logic.
    public class PlayerRidingDatastore
    {
        private readonly RidableResolver _ridableResolver;
        private readonly IPlayerConnectionChecker _connectionChecker;

        // playerId -> RidingState
        private readonly Dictionary<int, RidingState> _ridingStateByPlayerId = new();

        public PlayerRidingDatastore(RidableResolver ridableResolver, IPlayerConnectionChecker connectionChecker)
        {
            _ridableResolver = ridableResolver;
            _connectionChecker = connectionChecker;
        }

        public bool TryGetRidingState(int playerId, out RidingState ridingState)
        {
            return _ridingStateByPlayerId.TryGetValue(playerId, out ridingState);
        }

        // 乗車要求。空席を割り当てて RidingState を設定する。
        // Ride request. Assigns a free seat and sets the RidingState.
        public RideActionResult TryRide(int playerId, IRidableIdentifier identifier, out int assignedSeatIndex)
        {
            assignedSeatIndex = -1;

            // 既に乗車中なら拒否（移乗は不可、先に降車が必要）
            // Reject if already riding (no transfer; must dismount first).
            if (_ridingStateByPlayerId.ContainsKey(playerId))
            {
                return RideActionResult.AlreadyRiding;
            }

            var ridable = _ridableResolver.Resolve(identifier);
            if (ridable == null)
            {
                return RideActionResult.RidableNotFound;
            }

            var freeSeat = FindFreeSeat(identifier, ridable.SeatCount);
            if (freeSeat < 0)
            {
                return RideActionResult.NoSeatAvailable;
            }

            _ridingStateByPlayerId[playerId] = new RidingState(identifier, freeSeat);
            assignedSeatIndex = freeSeat;
            return RideActionResult.Success;

            #region Internal

            int FindFreeSeat(IRidableIdentifier target, int seatCount)
            {
                // 接続中プレイヤーが占有していない最小の座席indexを返す（仕様書セクション7）
                // Returns the smallest seat index not occupied by a connected player.
                for (var seat = 0; seat < seatCount; seat++)
                {
                    if (!IsSeatOccupiedByConnectedPlayer(target, seat))
                    {
                        return seat;
                    }
                }
                return -1;
            }

            #endregion
        }

        // 降車要求。RidingState をクリアする。
        // Dismount request. Clears the RidingState.
        public RideActionResult TryDismount(int playerId)
        {
            if (!_ridingStateByPlayerId.ContainsKey(playerId))
            {
                return RideActionResult.NotRiding;
            }

            _ridingStateByPlayerId.Remove(playerId);
            return RideActionResult.Success;
        }

        // 同じ (identifier, seatIndex) を持つ接続中の別プレイヤーがいるか
        // Whether a connected other player occupies the same (identifier, seatIndex).
        private bool IsSeatOccupiedByConnectedPlayer(IRidableIdentifier identifier, int seatIndex, int excludePlayerId = -1)
        {
            foreach (var pair in _ridingStateByPlayerId)
            {
                if (pair.Key == excludePlayerId) continue;
                var state = pair.Value;
                if (state.SeatIndex == seatIndex
                    && state.Identifier.Equals(identifier)
                    && _connectionChecker.IsConnected(pair.Key))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRidingDatastore_TryRide|PlayerRidingDatastore_TryDismount"`
Expected: 3件 PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerRidingDatastore.cs moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs
git commit -m "乗車Phase2: PlayerRidingDatastore の TryRide/TryDismount を実装"
```

---

## Task 5: PlayerRidingDatastore — OnRidableRemoved（乗り物破棄時の降車）

仕様書セクション4.4。車両削除時に、その乗り物を指す乗員の `RidingState` を処理する。本タスクでは `PlayerRidingDatastore` 側のメソッドのみ実装する（イベント購読の配線は Phase 3）。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerRidingDatastore.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`PlayerRidingDatastoreTest.cs` に追記:

```csharp
        [Test]
        public void PlayerRidingDatastore_OnRidableRemoved_DismountsRidersOfThatRidable()
        {
            // 乗り物Aに乗っているプレイヤーは OnRidableRemoved(A) でクリアされ、他乗り物の乗員は残る
            // Riders of removed ridable A are cleared; riders of other ridables remain.
            var (datastore, carA, carB) = TrainTestHelper.CreateDatastoreWithTwoTrainCars(seatCount: 2);
            var idA = new TrainCarRidableIdentifier(carA.TrainCarInstanceId.AsPrimitive());
            var idB = new TrainCarRidableIdentifier(carB.TrainCarInstanceId.AsPrimitive());
            datastore.TryRide(1, idA, out _);
            datastore.TryRide(2, idB, out _);

            var dismounted = datastore.OnRidableRemoved(idA);

            Assert.Contains(1, dismounted);
            Assert.AreEqual(1, dismounted.Count);
            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
            Assert.IsTrue(datastore.TryGetRidingState(2, out _));
        }

        [Test]
        public void PlayerRidingDatastore_OnRidableRemoved_IsIdempotent()
        {
            // 既に降車済みに対する OnRidableRemoved は no-op（冪等。仕様書セクション4.4）
            // OnRidableRemoved on an already-cleared ridable is a no-op (idempotent).
            var (datastore, carA, _) = TrainTestHelper.CreateDatastoreWithTwoTrainCars(seatCount: 2);
            var idA = new TrainCarRidableIdentifier(carA.TrainCarInstanceId.AsPrimitive());
            datastore.TryRide(1, idA, out _);

            datastore.OnRidableRemoved(idA);
            var second = datastore.OnRidableRemoved(idA);

            Assert.AreEqual(0, second.Count);
        }
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRidingDatastore_OnRidableRemoved"`
Expected: コンパイルエラー（`OnRidableRemoved` 未定義）で FAIL。

- [ ] **Step 3: OnRidableRemoved を実装**

`PlayerRidingDatastore.cs` の `TryDismount` の後ろに追加:

```csharp
        // 乗り物が破棄されたとき、その乗り物に乗っていた全プレイヤーの RidingState をクリアする。
        // 戻り値は降車させた playerId 一覧（接続中の乗員へ降車イベントを broadcast するために使う。Phase 3）。
        // Clears riding states of all players on a removed ridable. Returns the dismounted player ids.
        public IReadOnlyList<int> OnRidableRemoved(IRidableIdentifier identifier)
        {
            var dismounted = new List<int>();
            foreach (var pair in _ridingStateByPlayerId)
            {
                if (pair.Value.Identifier.Equals(identifier))
                {
                    dismounted.Add(pair.Key);
                }
            }
            // 降車処理は冪等（既に消えていれば dismounted は空。仕様書セクション4.4）
            // Idempotent: if nothing matched, dismounted is empty.
            foreach (var playerId in dismounted)
            {
                _ridingStateByPlayerId.Remove(playerId);
            }
            return dismounted;
        }
```

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRidingDatastore_OnRidableRemoved"`
Expected: 2件 PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerRidingDatastore.cs moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs
git commit -m "乗車Phase2: PlayerRidingDatastore.OnRidableRemoved を実装"
```

---

## Task 6: PlayerRidingDatastore — EvaluateOnLogin（ログイン復帰判定）

仕様書セクション8。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerRidingDatastore.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`PlayerRidingDatastoreTest.cs` に追記:

```csharp
        [Test]
        public void PlayerRidingDatastore_EvaluateOnLogin_RestoresWhenSeatValidAndFree()
        {
            // 乗り物が存在し記録席が有効・空き → 復帰（RidingState 維持）
            var (datastore, car) = TrainTestHelper.CreateDatastoreWithOneTrainCar(seatCount: 2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());
            datastore.InjectRidingStateForTest(playerId: 1, new RidingState(id, 0));

            var restored = datastore.EvaluateOnLogin(1);

            Assert.IsTrue(restored);
            Assert.IsTrue(datastore.TryGetRidingState(1, out _));
        }

        [Test]
        public void PlayerRidingDatastore_EvaluateOnLogin_DismountsWhenRidableMissingOrSeatOutOfRange()
        {
            var (datastore, car) = TrainTestHelper.CreateDatastoreWithOneTrainCar(seatCount: 2);
            var missingId = new TrainCarRidableIdentifier(-1L);
            var validId = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());
            datastore.InjectRidingStateForTest(1, new RidingState(missingId, 0));    // 乗り物消失
            datastore.InjectRidingStateForTest(2, new RidingState(validId, 99));     // 席が範囲外

            Assert.IsFalse(datastore.EvaluateOnLogin(1));
            Assert.IsFalse(datastore.EvaluateOnLogin(2));
            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
            Assert.IsFalse(datastore.TryGetRidingState(2, out _));
        }
```

`InjectRidingStateForTest` は復元/テスト用に `RidingState` を直接入れるメソッド（Step 3 で実装。Task 7 のロードでも使う）。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRidingDatastore_EvaluateOnLogin"`
Expected: コンパイルエラー（`EvaluateOnLogin` / `InjectRidingStateForTest` 未定義）で FAIL。

- [ ] **Step 3: EvaluateOnLogin と InjectRidingStateForTest を実装**

`PlayerRidingDatastore.cs` の `OnRidableRemoved` の後ろに追加:

```csharp
        // ロード/復元用に RidingState を直接登録する。整合検証はしない（仕様書セクション10）。
        // Directly registers a RidingState for load/restore. No consistency check here.
        public void InjectRidingStateForTest(int playerId, RidingState ridingState)
        {
            _ridingStateByPlayerId[playerId] = ridingState;
        }

        // ログイン時の復帰判定（仕様書セクション8）。
        // 復帰可なら RidingState を維持して true、不可ならクリアして false を返す。
        // Login-time evaluation. Keeps the RidingState and returns true if restorable, else clears and returns false.
        public bool EvaluateOnLogin(int playerId)
        {
            if (!_ridingStateByPlayerId.TryGetValue(playerId, out var state))
            {
                return false;
            }

            var ridable = _ridableResolver.Resolve(state.Identifier);
            // 乗り物が消失している
            if (ridable == null)
            {
                _ridingStateByPlayerId.Remove(playerId);
                return false;
            }
            // 記録席が範囲外（マスタ変更・セーブ手編集対策。仕様書セクション8）
            if (state.SeatIndex < 0 || state.SeatIndex >= ridable.SeatCount)
            {
                _ridingStateByPlayerId.Remove(playerId);
                return false;
            }
            // 記録席を接続中の別プレイヤーが使用中
            if (IsSeatOccupiedByConnectedPlayer(state.Identifier, state.SeatIndex, excludePlayerId: playerId))
            {
                _ridingStateByPlayerId.Remove(playerId);
                return false;
            }
            return true;
        }
```

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRidingDatastore_EvaluateOnLogin"`
Expected: 2件 PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerRidingDatastore.cs moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs
git commit -m "乗車Phase2: PlayerRidingDatastore.EvaluateOnLogin を実装"
```

---

## Task 7: 乗車状態のセーブ／ロード

仕様書セクション10。`PlayerRidingDatastore` にセーブ／ロードのメソッドを足し、`WorldSaveAllInfoV1` / `AssembleSaveJsonText` / `WorldLoaderFromJson` に組み込む。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerRidingSaveData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerRidingDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs`

- [ ] **Step 1: PlayerRidingSaveData を実装**

`Game.PlayerRiding/PlayerRidingSaveData.cs`:

```csharp
using System;

namespace Game.PlayerRiding
{
    // 乗車状態1件のセーブDTO。識別子は RidableType + 型別ペイロードで保存する（仕様書セクション10）。
    // Save DTO for one riding state. The identifier is stored as RidableType + per-type payload.
    [Serializable]
    public class PlayerRidingSaveData
    {
        public int PlayerId { get; set; }
        public byte RidableType { get; set; }      // Server.Util.MessagePack.RidableType を byte で保存
        public long TrainCarInstanceId { get; set; } // RidableType==TrainCar のときの車両ID
        public int SeatIndex { get; set; }
    }
}
```

- [ ] **Step 2: 失敗するテストを書く**

`PlayerRidingDatastoreTest.cs` に追記:

```csharp
        [Test]
        public void PlayerRidingDatastore_SaveData_RoundTrips()
        {
            // GetSaveData → LoadSaveData で乗車状態が往復することを確認
            var (datastore, car) = TrainTestHelper.CreateDatastoreWithOneTrainCar(seatCount: 2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());
            datastore.TryRide(7, id, out _);

            var saveData = datastore.GetSaveData();

            var (datastore2, _) = TrainTestHelper.CreateDatastoreWithOneTrainCar(seatCount: 2);
            datastore2.LoadSaveData(saveData);

            Assert.IsTrue(datastore2.TryGetRidingState(7, out var state));
            Assert.AreEqual(0, state.SeatIndex);
            Assert.IsTrue(state.Identifier.Equals(id));
        }
```

- [ ] **Step 3: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRidingDatastore_SaveData_RoundTrips"`
Expected: コンパイルエラー（`GetSaveData` / `LoadSaveData` 未定義）で FAIL。

- [ ] **Step 4: GetSaveData / LoadSaveData を実装**

`PlayerRidingDatastore.cs` に追加（using に `using System.Linq;` と `using Server.Util.MessagePack;` を追加）:

```csharp
        // 乗車状態を全件セーブDTOに変換する（仕様書セクション10）。
        // Converts all riding states into save DTOs.
        public List<PlayerRidingSaveData> GetSaveData()
        {
            var list = new List<PlayerRidingSaveData>();
            foreach (var pair in _ridingStateByPlayerId)
            {
                var identifier = pair.Value.Identifier;
                // 現状 TrainCar のみ。新 RidableType 追加時はここに分岐を足す。
                var trainCar = (TrainCarRidableIdentifier)identifier;
                list.Add(new PlayerRidingSaveData
                {
                    PlayerId = pair.Key,
                    RidableType = (byte)identifier.Type,
                    TrainCarInstanceId = trainCar.TrainCarInstanceId,
                    SeatIndex = pair.Value.SeatIndex,
                });
            }
            return list;
        }

        // セーブDTOから乗車状態を復元する。参照先の存在検証はしない（ログイン時まで遅延。仕様書セクション10）。
        // Restores riding states from save DTOs. No reference validation here (deferred to login).
        public void LoadSaveData(IReadOnlyList<PlayerRidingSaveData> saveData)
        {
            _ridingStateByPlayerId.Clear();
            if (saveData == null) return;
            foreach (var data in saveData)
            {
                IRidableIdentifier identifier = (RidableType)data.RidableType switch
                {
                    RidableType.TrainCar => new TrainCarRidableIdentifier(data.TrainCarInstanceId),
                    _ => null,
                };
                if (identifier == null) continue;
                _ridingStateByPlayerId[data.PlayerId] = new RidingState(identifier, data.SeatIndex);
            }
        }
```

- [ ] **Step 5: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRidingDatastore_SaveData_RoundTrips"`
Expected: PASS。

- [ ] **Step 6: WorldSaveAllInfoV1 に playerRidingStates を追加**

`WorldSaveAllInfoV1.cs` を変更:
- using に `using Game.PlayerRiding;` を追加。
- コンストラクタ引数の末尾に `List<PlayerRidingSaveData> playerRidingStates` を追加。
- コンストラクタ本体末尾に `PlayerRidingStates = playerRidingStates ?? new List<PlayerRidingSaveData>();` を追加。
- プロパティを追加: `[JsonProperty("playerRidingStates")] public List<PlayerRidingSaveData> PlayerRidingStates { get; }`

- [ ] **Step 7: AssembleSaveJsonText にセーブを組み込む**

`AssembleSaveJsonText.cs` を変更:
- using に `using Game.PlayerRiding;` を追加。
- フィールド `private readonly PlayerRidingDatastore _playerRidingDatastore;` を追加。
- コンストラクタ引数末尾に `PlayerRidingDatastore playerRidingDatastore` を追加し、本体で `_playerRidingDatastore = playerRidingDatastore;` を代入。
- `AssembleSaveJson()` の `new WorldSaveAllInfoV1(...)` 引数末尾（`_railGraphSaveLoadService.GetSaveData()` の後）に `, _playerRidingDatastore.GetSaveData()` を追加。

- [ ] **Step 8: WorldLoaderFromJson にロードを組み込む**

`WorldLoaderFromJson.cs` を変更:
- using に `using Game.PlayerRiding;` を追加。
- フィールド `private readonly PlayerRidingDatastore _playerRidingDatastore;` を追加。
- コンストラクタ引数末尾に `PlayerRidingDatastore playerRidingDatastore` を追加し、本体で代入。
- `Load()` メソッドの `_trainSaveLoadService.RestoreTrainStates(load.TrainUnits);`（約128行目）の **直後**に以下を追加（仕様書セクション10: 列車復元後にロード）:

```csharp
            // 乗車状態は列車復元後にロードする。参照整合の解決はログイン時まで遅延する（仕様書セクション8・10）。
            // Load riding states after trains are restored; reference resolution is deferred to login.
            _playerRidingDatastore.LoadSaveData(load.PlayerRidingStates);
```

- [ ] **Step 9: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。`AssembleSaveJsonText` / `WorldLoaderFromJson` のコンストラクタは DI（Task 8 で `PlayerRidingDatastore` を登録）が解決する。

- [ ] **Step 10: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs
git commit -m "乗車Phase2: 乗車状態のセーブ/ロードを実装"
```

---

## Task 8: DI 登録

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`

- [ ] **Step 1: 本体コレクションに登録を追加**

`MoorestechServerDIContainerGenerator.cs` の本体コレクション `services` の列車関連登録ブロック（`services.AddSingleton<TrainCarRidingInputBuffer>();` 付近、約169行目）に以下を追加:

```csharp
            services.AddSingleton<Game.PlayerRiding.Interface.IPlayerConnectionChecker, Game.PlayerRiding.AlwaysConnectedChecker>();
            services.AddSingleton<Game.PlayerRiding.RidableResolver>();
            services.AddSingleton<Game.PlayerRiding.PlayerRidingDatastore>();
```

`RidableResolver` は `ITrainUnitLookupDatastore`（登録済み）を、`PlayerRidingDatastore` は `RidableResolver` と `IPlayerConnectionChecker` をコンストラクタ DI で受ける。すべて本体 `services` にあるため解決できる。

注: `IPlayerConnectionChecker` の実装は Phase 3 で `AlwaysConnectedChecker` から実接続レジストリへ差し替える。

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 3: サーバー起動系テストの回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRiding|StartGame|SaveLoad"`
Expected: 既存のセーブ／ロード・起動テストが PASS のまま（DI 解決が壊れていないこと）。

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs
git commit -m "乗車Phase2: 乗車コアサービスをDI登録"
```

---

## Phase 2 完了確認

- [ ] `uloop compile --project-path ./moorestech_client` がエラー 0。
- [ ] `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRidingDatastoreTest"` が全件 PASS。
- [ ] `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Train|SaveLoad"` で既存テストが回帰なし。
- [ ] 仕様書セクション4（`PlayerRidingDatastore` / `RidableResolver` / `IRidable` / `TrainCar` 適合）・8（`EvaluateOnLogin`）・10（セーブ／ロード）が実装されている。

Phase 2 で作った `PlayerRidingDatastore`（`TryRide` / `TryDismount` / `OnRidableRemoved` / `EvaluateOnLogin` / セーブ・ロード）は、Phase 3（プロトコル・接続検知・ハンドシェイク拡張）が呼び出す。`IPlayerConnectionChecker` は Phase 3 で実装が差し替わる。
