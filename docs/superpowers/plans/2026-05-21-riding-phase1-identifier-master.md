# 乗車システム Phase 1: 識別子・マスタ基盤・TrainCarInstanceId 永続化 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 乗車システムの土台となる「乗り物を指す識別子」型一式・座席マスタデータ・`TrainCarInstanceId` のセーブ永続化を実装する。

**Architecture:** moorestech 既存の `ISubInventoryIdentifier` / `InventoryIdentifierMessagePack` パターンを踏襲し、`IRidableIdentifier`（サーバー側インターフェース）と `RidableIdentifierMessagePack`（ネットワーク／永続用、enum discriminator＋ペイロード方式）を新設する。座席数は YAML スキーマで定義し SourceGenerator で自動生成。`TrainCar` のインスタンス ID をセーブデータに含め、ロード後も同一 ID を保つ。

**Tech Stack:** C# / Unity / MessagePack / moorestech SourceGenerator（VanillaSchema YAML）/ NUnit。

**設計仕様:** `docs/superpowers/specs/2026-05-20-riding-system-design.md`（セクション3・6・10）

**前提:** 作業ディレクトリ `/Users/katsumi/moorestech`。`.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`。

---

## ファイル構成

**新規作成:**
- `moorestech_server/Assets/Scripts/Server.Util/MessagePack/RidableIdentifierMessagePack.cs` — `RidableType` enum と `RidableIdentifierMessagePack`（ネットワーク／永続用の識別子）
- `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/Game.PlayerRiding.Interface.asmdef` — 乗車識別子アセンブリ定義
- `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/IRidableIdentifier.cs` — 識別子インターフェース
- `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/TrainCarRidableIdentifier.cs` — 列車車両用の識別子実装
- `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/RidableIdentifierConverter.cs` — `IRidableIdentifier` ⇔ `RidableIdentifierMessagePack` 相互変換
- `VanillaSchema/ref/ridableSeat.yml` — 座席1つのスキーマ（オフセット）
- `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RidableIdentifierTest.cs` — 識別子のユニットテスト

**変更:**
- `VanillaSchema/train.yml` — `trainCars` の items に `ridableSeats` 配列を追加
- `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainSnapshots.cs` — `TrainCarSaveData` に `TrainCarInstanceId` フィールド追加
- `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainCar.cs` — インスタンス ID 外部指定経路・セーブ／復元の更新
- `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainCarInstanceId.cs` — 既存 ID と衝突しない採番

**注意:**
- `Game.PlayerRiding.Interface` は `long` を識別子の値型に使い、`Game.Train` への参照を持たない（`TrainInventorySubInventoryIdentifier` と同じくアセンブリ循環回避のため）。
- `.asmdef` は手で作成してよい（`.meta` は Unity 起動時に自動生成。手動作成禁止）。新規 `.asmdef` 追加後は Unity を一度起動／リフレッシュして `.meta` を生成させる。

---

## Task 1: RidableType enum と RidableIdentifierMessagePack

`InventoryIdentifierMessagePack`（`Server.Util/MessagePack/InventoryIdentifierMessagePack.cs`）と同じ構造の、乗り物識別子のネットワーク／永続用クラスを作る。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/RidableIdentifierMessagePack.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RidableIdentifierTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RidableIdentifierTest.cs` を新規作成:

```csharp
using MessagePack;
using NUnit.Framework;
using Server.Util.MessagePack;

namespace Tests.UnitTest.PlayerRiding
{
    public class RidableIdentifierTest
    {
        [Test]
        public void RidableIdentifierMessagePack_CreateTrainCar_RoundTripsThroughMessagePack()
        {
            // 列車車両識別子を生成し、MessagePackシリアライズで往復しても値が保たれることを確認
            // A train-car identifier survives a MessagePack serialize/deserialize round trip.
            var original = RidableIdentifierMessagePack.CreateTrainCarMessage(123456789012345L);

            var bytes = MessagePackSerializer.Serialize(original);
            var restored = MessagePackSerializer.Deserialize<RidableIdentifierMessagePack>(bytes);

            Assert.AreEqual(RidableType.TrainCar, restored.RidableType);
            Assert.AreEqual("123456789012345", restored.TrainCarInstanceId);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RidableIdentifierTest"`
Expected: コンパイルエラー（`RidableIdentifierMessagePack` / `RidableType` 未定義）で FAIL。

- [ ] **Step 3: RidableIdentifierMessagePack を実装**

`moorestech_server/Assets/Scripts/Server.Util/MessagePack/RidableIdentifierMessagePack.cs` を新規作成:

```csharp
using MessagePack;

namespace Server.Util.MessagePack
{
    // 乗り物の種類。InventoryType に倣う enum discriminator。
    // Kind of ridable. An enum discriminator, mirroring InventoryType.
    public enum RidableType : byte
    {
        TrainCar,
    }

    /// <summary>
    /// 乗り物識別子を保持するMessagePackクラス。enum discriminator＋型別ペイロード方式。
    /// MessagePack class that holds a ridable identifier. Enum discriminator plus per-type payload.
    /// </summary>
    [MessagePackObject]
    public class RidableIdentifierMessagePack
    {
        [Key(0)] public RidableType RidableType { get; set; }

        /// <summary>
        /// 列車車両の場合の TrainCarInstanceId（long を文字列化）。
        /// TrainCarInstanceId for the train-car case (long stored as string).
        /// </summary>
        [Key(1)] public string TrainCarInstanceId { get; set; }

        public RidableIdentifierMessagePack() { }

        public static RidableIdentifierMessagePack CreateTrainCarMessage(long trainCarInstanceId)
        {
            return new RidableIdentifierMessagePack
            {
                RidableType = RidableType.TrainCar,
                TrainCarInstanceId = trainCarInstanceId.ToString(),
            };
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RidableIdentifierTest"`
Expected: PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Util/MessagePack/RidableIdentifierMessagePack.cs moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RidableIdentifierTest.cs
git commit -m "乗車Phase1: RidableType と RidableIdentifierMessagePack を追加"
```

---

## Task 2: Game.PlayerRiding.Interface アセンブリと IRidableIdentifier

`ISubInventoryIdentifier`（`Game.PlayerInventory.Interface`）に倣い、サーバー側の識別子インターフェースと列車車両実装を作る。`Game.Train` を参照しないため値型は `long`。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/Game.PlayerRiding.Interface.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/IRidableIdentifier.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/TrainCarRidableIdentifier.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RidableIdentifierTest.cs`

- [ ] **Step 1: アセンブリ定義を作成**

`moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/Game.PlayerRiding.Interface.asmdef` を新規作成:

```json
{
  "name": "Game.PlayerRiding.Interface",
  "rootNamespace": "",
  "references": [
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

その後 Unity を起動して `.meta` を生成させる（`uloop` 利用中なら一度コンパイルさせる）。

- [ ] **Step 2: 失敗するテストを書く**

`RidableIdentifierTest.cs` に以下のテストを追加（クラス内に追記）:

```csharp
        [Test]
        public void TrainCarRidableIdentifier_EqualityAndHashCode_AreBasedOnInstanceId()
        {
            // 同じ TrainCarInstanceId を持つ識別子は等価で、HashCode も一致する
            // Identifiers with the same TrainCarInstanceId are equal and share a hash code.
            var a = new TrainCarRidableIdentifier(777L);
            var b = new TrainCarRidableIdentifier(777L);
            var c = new TrainCarRidableIdentifier(778L);

            Assert.AreEqual(RidableType.TrainCar, a.Type);
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(c));
        }
```

ファイル先頭の using に `using Game.PlayerRiding.Interface;` を追加する。

- [ ] **Step 3: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCarRidableIdentifier_EqualityAndHashCode"`
Expected: コンパイルエラー（`TrainCarRidableIdentifier` 未定義）で FAIL。

- [ ] **Step 4: IRidableIdentifier を実装**

`moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/IRidableIdentifier.cs`:

```csharp
using Server.Util.MessagePack;

namespace Game.PlayerRiding.Interface
{
    // 乗り物を指す識別子の共通インターフェース。ISubInventoryIdentifier に倣う。
    // Common interface for identifiers that point at a ridable. Mirrors ISubInventoryIdentifier.
    public interface IRidableIdentifier
    {
        RidableType Type { get; }

        // Dictionary / HashSet のキーに使うので Equals と GetHashCode をオーバーライドする
        // Used as Dictionary / HashSet keys, so Equals and GetHashCode must be overridden.
        bool Equals(object obj);
        int GetHashCode();
    }
}
```

- [ ] **Step 5: TrainCarRidableIdentifier を実装**

`moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/TrainCarRidableIdentifier.cs`:

```csharp
using Server.Util.MessagePack;

namespace Game.PlayerRiding.Interface
{
    // 列車車両を指す識別子。TrainInventorySubInventoryIdentifier に倣う。
    // Identifier that points at a train car. Mirrors TrainInventorySubInventoryIdentifier.
    public class TrainCarRidableIdentifier : IRidableIdentifier
    {
        public RidableType Type => RidableType.TrainCar;

        // アセンブリ循環を避けるため TrainCarInstanceId 構造体ではなく long を保持する
        // Holds a long instead of the TrainCarInstanceId struct to avoid an assembly cycle.
        public long TrainCarInstanceId { get; }

        public TrainCarRidableIdentifier(long trainCarInstanceId)
        {
            TrainCarInstanceId = trainCarInstanceId;
        }

        public override bool Equals(object obj)
        {
            if (obj is TrainCarRidableIdentifier other)
            {
                return TrainCarInstanceId.Equals(other.TrainCarInstanceId);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return TrainCarInstanceId.GetHashCode();
        }
    }
}
```

- [ ] **Step 6: テスト用 asmdef 参照を追加**

`Tests.UnitTest` の `.asmdef`（`moorestech_server/Assets/Scripts/Tests.UnitTest/` 配下）の `references` 配列に `"Game.PlayerRiding.Interface"` を追加する。`Server.Util` が未参照なら併せて追加。

- [ ] **Step 7: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RidableIdentifierTest"`
Expected: 2件 PASS。

- [ ] **Step 8: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface moorestech_server/Assets/Scripts/Tests.UnitTest
git commit -m "乗車Phase1: IRidableIdentifier と TrainCarRidableIdentifier を追加"
```

---

## Task 3: 識別子 ⇔ MessagePack 相互変換

`ISubInventoryIdentifierExtension.ToMessagePack` と `SubscribeInventoryProtocol.ConvertIdentifier` に倣い、双方向の変換を1ファイルにまとめる。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/RidableIdentifierConverter.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RidableIdentifierTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`RidableIdentifierTest.cs` に追記:

```csharp
        [Test]
        public void RidableIdentifierConverter_RoundTripsBetweenInterfaceAndMessagePack()
        {
            // IRidableIdentifier → MessagePack → IRidableIdentifier の往復で等価性が保たれる
            // Round trip IRidableIdentifier -> MessagePack -> IRidableIdentifier preserves equality.
            IRidableIdentifier original = new TrainCarRidableIdentifier(999L);

            var messagePack = original.ToMessagePack();
            var restored = RidableIdentifierConverter.FromMessagePack(messagePack);

            Assert.IsTrue(original.Equals(restored));
        }
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RidableIdentifierConverter_RoundTrips"`
Expected: コンパイルエラー（`ToMessagePack` / `RidableIdentifierConverter` 未定義）で FAIL。

- [ ] **Step 3: RidableIdentifierConverter を実装**

`moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/RidableIdentifierConverter.cs`:

```csharp
using System;
using Server.Util.MessagePack;

namespace Game.PlayerRiding.Interface
{
    // IRidableIdentifier と RidableIdentifierMessagePack の相互変換。
    // ISubInventoryIdentifierExtension と SubscribeInventoryProtocol.ConvertIdentifier に倣う。
    // Two-way conversion between IRidableIdentifier and RidableIdentifierMessagePack.
    public static class RidableIdentifierConverter
    {
        public static RidableIdentifierMessagePack ToMessagePack(this IRidableIdentifier identifier)
        {
            return identifier switch
            {
                TrainCarRidableIdentifier trainCar => RidableIdentifierMessagePack.CreateTrainCarMessage(trainCar.TrainCarInstanceId),
                _ => throw new ArgumentException($"Unknown IRidableIdentifier type: {identifier.GetType()}")
            };
        }

        public static IRidableIdentifier FromMessagePack(RidableIdentifierMessagePack messagePack)
        {
            return messagePack.RidableType switch
            {
                RidableType.TrainCar => new TrainCarRidableIdentifier(long.Parse(messagePack.TrainCarInstanceId)),
                _ => throw new ArgumentException($"Unknown RidableType: {messagePack.RidableType}")
            };
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RidableIdentifierTest"`
Expected: 3件 PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding.Interface/RidableIdentifierConverter.cs moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RidableIdentifierTest.cs
git commit -m "乗車Phase1: 識別子とMessagePackの相互変換を追加"
```

---

## Task 4: 座席マスタデータ（YAML スキーマ）

座席を専用スキーマ `ref/ridableSeat.yml` に定義し、`train.yml` の `trainCars` から配列で参照する。SourceGenerator が `Mooresmaster.Model.TrainModule` を再生成する。

**Files:**
- Create: `VanillaSchema/ref/ridableSeat.yml`
- Modify: `VanillaSchema/train.yml`

**注意:** YAML スキーマ編集は moorestech の `edit-schema` skill に従うこと（SourceGenerator のトリガー方法を含む）。`Mooresmaster.Model.*Module` は自動生成物のため手動編集禁止。

- [ ] **Step 1: 座席スキーマを作成**

`VanillaSchema/ref/ridableSeat.yml` を新規作成:

```yaml
# NOTE このyamlに記述されているスキーマのコード、JSONローダーはSourceGeneratorによって自動生成されます。
# NOTE The schema code and JSON loader described in this YAML are automatically generated by the SourceGenerator.

id: ridableSeat
type: object
properties:
- key: offsetX
  type: number
  default: 0
- key: offsetY
  type: number
  default: 0
- key: offsetZ
  type: number
  default: 0
```

- [ ] **Step 2: train.yml の trainCars に ridableSeats 配列を追加**

`VanillaSchema/train.yml` の `trainCars` → `items` → `properties` 配列の末尾（`trainFuelFluids` の後）に以下を追加:

```yaml
    - key: ridableSeats
      type: array
      optional: true
      items:
        ref: ridableSeat
```

- [ ] **Step 3: SourceGenerator を再生成しコンパイル確認**

`edit-schema` skill の手順で SourceGenerator を再生成する。その後:

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。`Mooresmaster.Model.TrainModule` の `TrainCarMasterElement` に `RidableSeats`（座席要素配列）プロパティが生成されていること。

- [ ] **Step 4: 生成物の確認**

`grep -rn "RidableSeat" moorestech_client/Assets/` 等で、生成された座席要素型と `TrainCarMasterElement.RidableSeats` プロパティが存在することを目視確認する。生成プロパティ名が異なる場合は Phase 2 以降のタスクで実際の名前を使うこと。

- [ ] **Step 5: コミット**

```bash
git add VanillaSchema/ref/ridableSeat.yml VanillaSchema/train.yml
git add -A moorestech_client/Assets/Scripts/Mooresmaster moorestech_server/Assets/Scripts/Mooresmaster 2>/dev/null || true
git commit -m "乗車Phase1: 座席マスタスキーマ ridableSeat を追加"
```

（自動生成物のパスはプロジェクト構成に従う。`edit-schema` skill の指示に従い生成物もコミットする。）

---

## Task 5: TrainCarSaveData に TrainCarInstanceId を追加

セーブ→ロードで車両インスタンス ID を保つため、`TrainCarSaveData` に ID フィールドを追加する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainSnapshots.cs:21-29`

- [ ] **Step 1: TrainCarSaveData にフィールド追加**

`TrainSnapshots.cs` の `TrainCarSaveData` クラスを以下に変更:

```csharp
    [Serializable]
    public class TrainCarSaveData
    {
        // 車両インスタンスID。セーブ/ロードを跨いで安定させる（乗車状態の参照先に使う）。
        // Train car instance id. Kept stable across save/load (used as the riding-state reference).
        public long TrainCarInstanceId { get; set; }
        public Guid TrainCarMasterId { get; set; }
        public bool IsFacingForward { get; set; }
        public SerializableVector3Int? DockingBlockPosition { get; set; }
        public string ContainerSaveData { get; set; }
        public double RemainFuelTime { get; set; }
    }
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0（フィールド追加のみのため既存コードは壊れない）。

- [ ] **Step 3: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Train/Unit/TrainSnapshots.cs
git commit -m "乗車Phase1: TrainCarSaveData に TrainCarInstanceId を追加"
```

---

## Task 6: TrainCar にインスタンス ID 外部指定経路を追加

現状 `TrainCar` は `_trainCarInstanceId = TrainCarInstanceId.Create()` のフィールド初期化で常に新規採番する。復元時に保存済み ID を使えるよう、ID を引数で受けるコンストラクタを追加する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainCar.cs:24,45-77`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/TrainCarInstanceIdPersistenceTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/TrainCarInstanceIdPersistenceTest.cs` を新規作成。`creating-server-tests` skill のテスト雛形に従い、`MasterHolder` 等の初期化が必要なら同 skill の初期化パターンを使うこと。

```csharp
using Game.Train.Unit;
using NUnit.Framework;

namespace Tests.UnitTest.PlayerRiding
{
    public class TrainCarInstanceIdPersistenceTest
    {
        [Test]
        public void TrainCar_CreateSaveData_ThenRestore_KeepsSameInstanceId()
        {
            // セーブデータ作成→復元で TrainCarInstanceId が一致することを確認
            // CreateTrainCarSaveData -> RestoreTrainCar keeps the same TrainCarInstanceId.
            var car = TrainTestHelper.CreateAnyTrainCar(); // 既存テストの車両生成ヘルパに合わせて置換すること
            var originalId = car.TrainCarInstanceId;

            var saveData = car.CreateTrainCarSaveData();
            var restored = TrainCar.RestoreTrainCar(saveData);

            Assert.AreEqual(originalId, restored.TrainCarInstanceId);
        }
    }
}
```

注: `TrainTestHelper.CreateAnyTrainCar()` は仮。既存の列車テスト（`grep -rl "new TrainCar(" moorestech_server/Assets/Scripts` で検索）の車両生成方法に合わせて差し替える。`TrainCar` コンストラクタは `TrainCar(TrainCarMasterElement, bool)`。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCar_CreateSaveData_ThenRestore"`
Expected: FAIL（復元時に新規 ID が採番され ID 不一致）。

- [ ] **Step 3: TrainCar に ID 指定コンストラクタを追加**

`TrainCar.cs` の `_trainCarInstanceId` フィールド宣言（24行目）を、初期化なしに変更:

```csharp
        private readonly TrainCarInstanceId _trainCarInstanceId;
```

既存コンストラクタ（45行目）の冒頭に ID 採番を追加し、ID 指定コンストラクタを併設する。45行目の `public TrainCar(TrainCarMasterElement trainCarMaster, bool isFacingForward)` を以下の2コンストラクタに置き換える:

```csharp
        // 新規車両用: インスタンスIDを新規採番する
        // For new cars: generates a fresh instance id.
        public TrainCar(TrainCarMasterElement trainCarMaster, bool isFacingForward)
            : this(trainCarMaster, isFacingForward, TrainCarInstanceId.Create())
        {
        }

        // セーブ復元用: 保存済みインスタンスIDを引き継ぐ
        // For save restore: carries over the persisted instance id.
        public TrainCar(TrainCarMasterElement trainCarMaster, bool isFacingForward, TrainCarInstanceId trainCarInstanceId)
        {
            _trainCarInstanceId = trainCarInstanceId;
            TrainCarMasterElement = trainCarMaster;
            TractionForce = trainCarMaster.TractionForce;
            Length = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
            IsFacingForward = isFacingForward;
            dockingblock = null;

            _trainUpdateEvent = (TrainUpdateEvent)ServerContext.GetService<ITrainUpdateEvent>();

            // マスタ指定のデフォルトコンテナを装着する(セーブ復元時はRestoreTrainCar内のSetContainerで上書きされる)
            // Attach the default container per master spec; RestoreTrainCar's SetContainer overrides it on load.
            AttachDefaultContainerFromMaster();

            #region Internal

            void AttachDefaultContainerFromMaster()
            {
                switch (trainCarMaster.DefaultContainerType)
                {
                    case "Item":
                        SetContainer(ItemTrainCarContainer.CreateWithEmptySlots(trainCarMaster.InventorySlots));
                        break;
                    case "Fluid":
                        SetContainer(new FluidTrainCarContainer(new FluidContainer(trainCarMaster.FluidCapacity)));
                        break;
                    // None または未指定はコンテナ無し
                    // None or unspecified leaves the car without a container.
                }
            }

            #endregion
        }
```

- [ ] **Step 4: テストが失敗のままであることを確認（RestoreTrainCar 未更新）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCar_CreateSaveData_ThenRestore"`
Expected: まだ FAIL（`RestoreTrainCar` が ID 指定コンストラクタを使っていない）。Task 7 で解消する。

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Train/Unit/TrainCar.cs moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/TrainCarInstanceIdPersistenceTest.cs
git commit -m "乗車Phase1: TrainCar にインスタンスID指定コンストラクタを追加"
```

---

## Task 7: CreateTrainCarSaveData / RestoreTrainCar を ID 込みに更新

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainCar.cs:127-173`

- [ ] **Step 1: CreateTrainCarSaveData に ID を含める**

`TrainCar.cs` の `CreateTrainCarSaveData()` の `return new TrainCarSaveData { ... }` に `TrainCarInstanceId` を追加:

```csharp
            return new TrainCarSaveData
            {
                TrainCarInstanceId = this._trainCarInstanceId.AsPrimitive(),
                TrainCarMasterId = this.TrainCarMasterElement.TrainCarGuid,
                IsFacingForward = this.IsFacingForward,
                DockingBlockPosition = dockingPosition,
                ContainerSaveData = MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(Container)),
                RemainFuelTime = this.RemainFuelTime
            };
```

注: `TrainCarInstanceId` は `[UnitOf(typeof(long), ...)]` 生成型。`long` 値の取り出しは `AsPrimitive()`（`TrainCarEntityObject` で `TrainCarInstanceId.AsPrimitive()` の用例あり）。コンパイルエラーになる場合は生成された取り出しプロパティ名（`AsPrimitive()` 等）に合わせること。

- [ ] **Step 2: RestoreTrainCar で ID 指定コンストラクタを使う**

`TrainCar.cs` の `RestoreTrainCar(TrainCarSaveData data)` の車両生成行（154行目 `var car = new TrainCar(trainCarMaster, isFacingForward);`）を以下に変更:

```csharp
            var car = new TrainCar(trainCarMaster, isFacingForward, new TrainCarInstanceId(data.TrainCarInstanceId));
```

- [ ] **Step 3: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCar_CreateSaveData_ThenRestore"`
Expected: PASS。

- [ ] **Step 4: 既存の列車セーブ／ロードテストの回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Train.*SaveLoad|TrainSaveLoad"`
Expected: 既存の列車セーブ／ロード関連テストが PASS のまま。`train-rail-save-load` skill が関連テストの一覧を持つので、必要に応じ参照。

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Train/Unit/TrainCar.cs
git commit -m "乗車Phase1: TrainCarのセーブ/復元でインスタンスIDを永続化"
```

---

## Task 8: TrainCarInstanceId の一意性担保

永続 ID 導入により、ロードした ID と新規採番 ID の衝突、または壊れたセーブ内の重複 ID が問題になる。登録時の重複検出と、新規採番の衝突回避を入れる。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUnitDatastore.cs`（車両登録箇所）
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/TrainCarInstanceIdPersistenceTest.cs`

- [ ] **Step 1: 登録経路を調査**

`TrainUnitDatastore.cs` と `ITrainUnitLookupDatastore.cs` を読み、`TrainCarInstanceId` → `TrainCar` の逆引き（`TryGetTrainCar` 相当）がどこで構築・更新されるかを特定する。`grep -rn "TryGetTrainCar\|RegisterTrain" moorestech_server/Assets/Scripts/Game.Train` も併用。

- [ ] **Step 2: 失敗するテストを書く**

`TrainCarInstanceIdPersistenceTest.cs` に追記:

```csharp
        [Test]
        public void TrainUnitDatastore_RegisteringDuplicateTrainCarInstanceId_Throws()
        {
            // 同一 TrainCarInstanceId を持つ車両を二重登録したら例外になることを確認
            // Registering two cars with the same TrainCarInstanceId throws.
            // 具体的なセットアップは Step 1 で特定した登録APIに合わせて記述する。
            Assert.Throws<System.Exception>(() =>
            {
                TrainTestHelper.RegisterTwoTrainCarsWithSameInstanceId(); // Step 1 の調査結果に合わせて実装
            });
        }
```

`TrainTestHelper.RegisterTwoTrainCarsWithSameInstanceId()` は Step 1 で判明した登録 API を使って実際のセットアップに置き換える。

- [ ] **Step 3: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RegisteringDuplicateTrainCarInstanceId"`
Expected: FAIL（重複が素通りする）。

- [ ] **Step 4: 重複検出を実装**

Step 1 で特定した「`TrainCarInstanceId` → `TrainCar` 逆引きへの登録箇所」で、登録前に同一 ID が既に存在するかを確認し、存在したら `throw new Exception($"Duplicate TrainCarInstanceId: {id}")` する。try-catch は使わず条件分岐で（AGENTS.md 準拠）。

- [ ] **Step 5: 新規採番の衝突回避を実装**

`TrainCarInstanceId.cs` の `Create()` は乱数のみで衝突チェックが無い。Step 1 で特定した逆引き（登録済み ID 集合）を参照できる形にし、新規 `TrainCar` 生成時（Task 6 の引数なしコンストラクタ経由）に採番した ID が既存と衝突する場合は再採番する。実装方針:
- `TrainCarInstanceId.Create()` 自体は変更せず、車両を datastore に登録する箇所で「登録しようとした ID が既存と衝突するなら新しい ID を採番し直して再登録」とするか、
- `Create()` に既存 ID 集合を渡すオーバーロードを設け、新規車両生成経路から使う。
どちらにするかは Step 1 の登録経路の形に合わせて決め、選んだ理由をコミットメッセージに残す。

- [ ] **Step 6: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCarInstanceIdPersistenceTest"`
Expected: 全件 PASS。

- [ ] **Step 7: 列車関連テストの回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Train"`
Expected: 既存の列車テストが PASS のまま。

- [ ] **Step 8: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Train moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/TrainCarInstanceIdPersistenceTest.cs
git commit -m "乗車Phase1: TrainCarInstanceId の一意性を担保"
```

---

## Phase 1 完了確認

- [ ] `uloop compile --project-path ./moorestech_client` がエラー 0。
- [ ] `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RidableIdentifierTest|TrainCarInstanceIdPersistenceTest"` が全件 PASS。
- [ ] `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Train"` で列車既存テストが回帰なし。
- [ ] 仕様書セクション3（識別子）・6（座席マスタ）・10（`TrainCarInstanceId` 永続化・一意性）が実装されている。

Phase 1 で作った `IRidableIdentifier` / `RidableIdentifierMessagePack` / `RidableIdentifierConverter` / 座席マスタ / 安定した `TrainCarInstanceId` は、Phase 2（サーバー乗車コア: `PlayerRidingDatastore` / `RidableResolver` / `IRidable`）の前提となる。
