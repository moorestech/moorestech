# アンロックシステム

## 概要

moorestechのアンロックシステムは、ゲームの進行に応じて新しい機能やアイテムを解放する機能です。プレイヤーの進行度に合わせてコンテンツを段階的に解放することで、ゲーム体験を向上させ、学習曲線を適切に設計するために重要な役割を果たします。

## 主要コンポーネント

### IGameUnlockStateDataController

アンロック状態を管理するための主要インターフェースです。以下のような機能を提供します：

- アンロック状態の管理
- アンロックイベントの通知
- アンロック状態の保存・読み込み

```csharp
public interface IGameUnlockStateDataController
{
    // アイテムのアンロック状態を取得
    bool IsUnlockedItem(ItemId itemId);
    
    // アイテムをアンロック
    void UnlockItem(ItemId itemId);
    
    // クラフトレシピのアンロック状態を取得
    bool IsUnlockedCraftRecipe(Guid recipeGuid);
    
    // クラフトレシピをアンロック
    void UnlockCraftRecipe(Guid recipeGuid);
    
    // アンロック状態の保存
    void Save();
    
    // アンロック状態の読み込み
    void Load();
}
```

### アンロック状態情報

アンロック状態を保持するデータ構造です。主に以下のような種類があります：

- **CraftRecipeUnlockStateInfo**: クラフトレシピのアンロック状態情報
- **ItemUnlockStateInfo**: アイテムのアンロック状態情報

## アンロックの種類

### クラフトレシピのアンロック

クラフトレシピのアンロックにより、プレイヤーは新しいアイテムをクラフトできるようになります。

```csharp
// クラフトレシピのアンロック
var gameUnlockStateDataController = modEntryInterface.GetService<IGameUnlockStateDataController>();
gameUnlockStateDataController.UnlockCraftRecipe(recipeGuid);
```

### アイテムのアンロック

アイテムのアンロックにより、プレイヤーは新しいアイテムを使用できるようになります。

```csharp
// アイテムのアンロック
var gameUnlockStateDataController = modEntryInterface.GetService<IGameUnlockStateDataController>();
gameUnlockStateDataController.UnlockItem(itemId);
```

## アンロックのトリガー

アンロックは、以下のようなイベントによってトリガーされることがあります：

### チャレンジの完了

チャレンジを完了すると、報酬としてアイテムやレシピがアンロックされることがあります。

```csharp
var clearedActions = new List<ClearedActionsElement>
{
    new ClearedActionsElement(ClearedActionsElement.ClearedActionTypeConst.unlockCraftRecipe, new UnlockCraftRecipeClearedActionParam(recipeGuid))
};
```

### プログラムによるアンロック

MODやゲームコードから直接アンロックを行うこともできます。

```csharp
// MODのエントリーポイントでアイテムをアンロック
public void OnLoad(IModEntryInterface modEntryInterface)
{
    var itemId = itemMaster.RegisterItem(new Guid("00000000-0000-0000-0000-000000000001"), "my_item", 64);
    
    var gameUnlockStateDataController = modEntryInterface.GetService<IGameUnlockStateDataController>();
    gameUnlockStateDataController.UnlockItem(itemId);
}
```

## アンロック状態の永続化

アンロック状態は、ゲームのセーブデータの一部として保存されます。`IGameUnlockStateDataController`の`Save`メソッドと`Load`メソッドを使用して、アンロック状態の保存と読み込みを行います。

```csharp
// アンロック状態の保存
gameUnlockStateDataController.Save();

// アンロック状態の読み込み
gameUnlockStateDataController.Load();
```

## アンロックイベントの購読

アンロックイベントを購読して、アンロックが発生したときに特定の処理を実行することができます。

```csharp
// アンロックイベントの購読
var unlockEvent = modEntryInterface.GetService<UnlockEvent>();
modEntryInterface.Subscribe(unlockEvent.OnUnlockItem, OnUnlockItem);
modEntryInterface.Subscribe(unlockEvent.OnUnlockCraftRecipe, OnUnlockCraftRecipe);

// アンロックイベントのハンドラ
private void OnUnlockItem(ItemId itemId)
{
    Debug.Log($"Item unlocked: {itemId}");
    // アイテムがアンロックされたときの処理
}

private void OnUnlockCraftRecipe(Guid recipeGuid)
{
    Debug.Log($"Craft recipe unlocked: {recipeGuid}");
    // クラフトレシピがアンロックされたときの処理
}
```

## MODでのアンロックシステムの活用

MOD開発者は、アンロックシステムを活用して、MODのコンテンツを段階的に解放することができます。

```csharp
public void OnLoad(IModEntryInterface modEntryInterface)
{
    // アイテムの登録
    var itemId = itemMaster.RegisterItem(new Guid("00000000-0000-0000-0000-000000000001"), "my_item", 64);
    
    // レシピの登録
    var recipeGuid = new Guid("00000000-0000-0000-0000-000000000003");
    var resultItemGuid = new Guid("00000000-0000-0000-0000-000000000001"); // my_item
    var ingredients = new List<CraftIngredient>
    {
        new CraftIngredient(MasterHolder.ItemMaster.GetItemId(new Guid("00000000-0000-0000-0000-000000000004")), 2), // iron_ingot
        new CraftIngredient(MasterHolder.ItemMaster.GetItemId(new Guid("00000000-0000-0000-0000-000000000005")), 1)  // copper_ingot
    };
    
    craftRecipeMaster.RegisterCraftRecipe(recipeGuid, resultItemGuid, 1, ingredients);
    
    // チャレンジの登録
    var challengeGuid = new Guid("00000000-0000-0000-0000-000000000006");
    var taskParam = new CreateItemTaskParam(itemGuid);
    var clearedActions = new List<ClearedActionsElement>
    {
        new ClearedActionsElement(ClearedActionsElement.ClearedActionTypeConst.unlockCraftRecipe, new UnlockCraftRecipeClearedActionParam(recipeGuid))
    };
    
    challengeMaster.RegisterChallenge(challengeGuid, "my_challenge", "This is my custom challenge.", ChallengeMasterElement.TaskTypeConst.createItem, taskParam, null, clearedActions);
    
    // アイテムのアンロック（初期状態でアンロックする場合）
    var gameUnlockStateDataController = modEntryInterface.GetService<IGameUnlockStateDataController>();
    gameUnlockStateDataController.UnlockItem(itemId);
    
    // レシピはチャレンジ完了時にアンロックされるため、ここではアンロックしない
}
```

## まとめ

アンロックシステムは、ゲームの進行に応じて新しい機能やアイテムを解放する重要な機能です。このシステムにより、プレイヤーは段階的にゲームの内容を体験し、学習曲線に沿って進行することができます。また、MOD開発者は、このシステムを活用して、MODのコンテンツを段階的に解放することができます。