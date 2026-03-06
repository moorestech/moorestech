# テスト用ID定義とヘルパークラス

## ブロックID (`ForUnitTestModBlockId`)

```csharp
ForUnitTestModBlockId.ChestId
ForUnitTestModBlockId.BeltConveyorId
ForUnitTestModBlockId.MachineId
ForUnitTestModBlockId.GeneratorId
ForUnitTestModBlockId.ElectricPoleId
ForUnitTestModBlockId.ElectricMinerId
ForUnitTestModBlockId.FluidPipe
// ... 他多数
```

## アイテムID (`ForUnitTestItemId`)

```csharp
ForUnitTestItemId.ItemId1  // ItemId(1)
ForUnitTestItemId.ItemId2  // ItemId(2)
ForUnitTestItemId.ItemId3  // ItemId(3)
ForUnitTestItemId.ItemId4  // ItemId(4)
```

## テスト用マスターデータ

パス: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/`

新しいテスト用ブロックが必要な場合:
1. `blocks.json` にブロック定義を追加
2. `ForUnitTestModBlockId.cs` にIDプロパティを追加

## ヘルパークラス

### DummyBlockInventory
アイテム挿入を自動受け入れるダミーインベントリ。
```csharp
var dummy = new DummyBlockInventory();
// dummy.InsertedItems でアイテム挿入記録を検証
```

### ConfigurableBlockInventory
挿入可否・最大数を制御可能なモックインベントリ。
```csharp
var inventory = new ConfigurableBlockInventory(maxSlot: 10, maxInsertCount: 5, allowInsertionCheck: true, rejectInsert: false);
inventory.SetRejectInsert(true);
```

### TestElectricGenerator
無限電力供給用のテストジェネレーター。
```csharp
var generator = new TestElectricGenerator(new ElectricPower(100), new BlockInstanceId(0));
segment.AddGenerator(generator);
```
