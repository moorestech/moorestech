# チャレンジシステム

## 概要

moorestechのチャレンジシステムは、プレイヤーに目標を提供し、ゲームの進行を導く重要な機能です。チャレンジを通じて、プレイヤーは新しい機能やアイテムの使い方を学び、ゲームの深い部分を探索するモチベーションを得ることができます。また、チャレンジの完了によって、新しいアイテムやレシピがアンロックされるなど、ゲームの進行に直接影響を与えます。

## 主要コンポーネント

### ChallengeDatastore

チャレンジデータを管理する中心的なコンポーネントです。以下のような機能を提供します：

- チャレンジの作成と登録
- チャレンジの完了状態の管理
- チャレンジの保存と読み込み
- チャレンジ完了時のイベント通知

```csharp
public class ChallengeDatastore
{
    // チャレンジの登録
    public void RegisterChallenge(IChallengeTask challengeTask);
    
    // チャレンジの完了確認
    public bool IsCompleted(Guid challengeGuid);
    
    // チャレンジの完了処理
    public void CompleteChallenge(Guid challengeGuid);
    
    // チャレンジの保存
    public void Save();
    
    // チャレンジの読み込み
    public void Load();
}
```

### IChallengeTask

チャレンジタスクのインターフェースです。各種チャレンジはこのインターフェースを実装します。

```csharp
public interface IChallengeTask
{
    // チャレンジのGUID
    Guid ChallengeGuid { get; }
    
    // チャレンジの名前
    string ChallengeName { get; }
    
    // チャレンジの説明
    string ChallengeDescription { get; }
    
    // チャレンジが完了したかどうかの確認
    bool IsCompleted();
    
    // チャレンジの更新処理
    void Update();
    
    // チャレンジ完了時の処理
    void OnCompleted();
}
```

### ChallengeMaster

チャレンジのマスターデータを管理するコンポーネントです。チャレンジの定義情報を保持します。

```csharp
public class ChallengeMaster
{
    // チャレンジの登録
    public void RegisterChallenge(Guid challengeGuid, string challengeName, string challengeDescription, 
                                 ChallengeMasterElement.TaskTypeConst taskType, object taskParam, 
                                 List<NextChallengeElement> nextChallenges, List<ClearedActionsElement> clearedActions);
    
    // チャレンジの取得
    public ChallengeMasterElement GetChallenge(Guid challengeGuid);
    
    // 全チャレンジの取得
    public IReadOnlyList<ChallengeMasterElement> GetAllChallenges();
}
```

## チャレンジの種類

moorestechでは、以下のような種類のチャレンジが実装されています：

### ブロック設置チャレンジ (BlockPlaceChallenge)

特定のブロックを設置することで完了するチャレンジです。

```csharp
public class BlockPlaceChallenge : IChallengeTask
{
    public BlockPlaceChallenge(Guid challengeGuid, string challengeName, string challengeDescription, 
                              BlockPlaceTaskParam taskParam, IWorldDataStore worldDataStore)
    {
        // ...
    }
    
    public bool IsCompleted()
    {
        // 特定のブロックが設置されているかを確認
        return _worldDataStore.IsBlockPlaced(_taskParam.BlockGuid);
    }
}
```

### アイテム作成チャレンジ (CreateItemChallenge)

特定のアイテムを作成することで完了するチャレンジです。

```csharp
public class CreateItemChallenge : IChallengeTask
{
    public CreateItemChallenge(Guid challengeGuid, string challengeName, string challengeDescription, 
                              CreateItemTaskParam taskParam, CraftEvent craftEvent)
    {
        // ...
        craftEvent.OnCraftItem.Subscribe(OnCraftItem);
    }
    
    private void OnCraftItem(CraftEventArgs args)
    {
        // 特定のアイテムが作成されたかを確認
        if (args.ResultItemId.Guid == _taskParam.ItemGuid)
        {
            _isCompleted = true;
        }
    }
}
```

### インベントリ内アイテムチャレンジ (InventoryItemChallenge)

特定のアイテムをインベントリに持っていることで完了するチャレンジです。

```csharp
public class InventoryItemChallenge : IChallengeTask
{
    public InventoryItemChallenge(Guid challengeGuid, string challengeName, string challengeDescription, 
                                 InventoryItemTaskParam taskParam, IPlayerInventoryDataStore playerInventoryDataStore)
    {
        // ...
    }
    
    public bool IsCompleted()
    {
        // インベントリに特定のアイテムがあるかを確認
        return _playerInventoryDataStore.HasItem(_taskParam.ItemGuid, _taskParam.Count);
    }
}
```

## チャレンジの完了処理

チャレンジが完了すると、以下のような処理が行われます：

1. チャレンジの完了状態が更新されます。
2. チャレンジ完了イベントが発火されます。
3. 次のチャレンジが解放されます（定義されている場合）。
4. クリア時アクションが実行されます（定義されている場合）。

```csharp
public void CompleteChallenge(Guid challengeGuid)
{
    if (IsCompleted(challengeGuid))
    {
        return;
    }
    
    // チャレンジの完了状態を更新
    _completedChallenges.Add(challengeGuid);
    
    // チャレンジ完了イベントを発火
    _onCompleteChallenge.OnNext(challengeGuid);
    
    // チャレンジマスターからチャレンジ情報を取得
    var challengeMasterElement = _challengeMaster.GetChallenge(challengeGuid);
    
    // 次のチャレンジを解放
    foreach (var nextChallenge in challengeMasterElement.NextChallenges)
    {
        // ...
    }
    
    // クリア時アクションを実行
    foreach (var clearedAction in challengeMasterElement.ClearedActions)
    {
        ExecuteClearedAction(clearedAction);
    }
    
    // チャレンジの保存
    Save();
}
```

## クリア時アクション

チャレンジが完了したときに実行されるアクションです。以下のような種類があります：

### アイテムアンロックアクション

特定のアイテムをアンロックするアクションです。

```csharp
private void ExecuteClearedAction(ClearedActionsElement clearedAction)
{
    switch (clearedAction.Type)
    {
        case ClearedActionsElement.ClearedActionTypeConst.unlockItem:
            var param = (UnlockItemClearedActionParam)clearedAction.Param;
            _gameUnlockStateDataController.UnlockItem(param.ItemGuid);
            break;
        // ...
    }
}
```

### クラフトレシピアンロックアクション

特定のクラフトレシピをアンロックするアクションです。

```csharp
private void ExecuteClearedAction(ClearedActionsElement clearedAction)
{
    switch (clearedAction.Type)
    {
        // ...
        case ClearedActionsElement.ClearedActionTypeConst.unlockCraftRecipe:
            var param = (UnlockCraftRecipeClearedActionParam)clearedAction.Param;
            _gameUnlockStateDataController.UnlockCraftRecipe(param.RecipeGuid);
            break;
        // ...
    }
}
```

## チャレンジイベント

チャレンジシステムは、以下のようなイベントを提供します：

### チャレンジ完了イベント

チャレンジが完了したときに発火されるイベントです。

```csharp
public class ChallengeEvent
{
    private readonly Subject<Guid> _onCompleteChallenge = new Subject<Guid>();
    public IObservable<Guid> OnCompleteChallenge => _onCompleteChallenge;
    
    public void CompleteChallenge(Guid challengeGuid)
    {
        _onCompleteChallenge.OnNext(challengeGuid);
    }
}
```

このイベントを購読することで、チャレンジが完了したときに特定の処理を実行することができます。

```csharp
// チャレンジ完了イベントの購読
var challengeEvent = modEntryInterface.GetService<ChallengeEvent>();
modEntryInterface.Subscribe(challengeEvent.OnCompleteChallenge, OnCompleteChallenge);

// チャレンジ完了イベントのハンドラ
private void OnCompleteChallenge(Guid challengeGuid)
{
    Debug.Log($"Challenge completed: {challengeGuid}");
    // チャレンジが完了したときの処理
}
```

## チャレンジの永続化

チャレンジの完了状態は、ゲームのセーブデータの一部として保存されます。`ChallengeDatastore`の`Save`メソッドと`Load`メソッドを使用して、チャレンジの保存と読み込みを行います。

```csharp
// チャレンジの保存
public void Save()
{
    var saveData = new ChallengeDatastoreSaveData
    {
        CompletedChallenges = _completedChallenges.ToList()
    };
    
    var json = JsonConvert.SerializeObject(saveData);
    File.WriteAllText("challenges.json", json);
}

// チャレンジの読み込み
public void Load()
{
    if (!File.Exists("challenges.json"))
    {
        return;
    }
    
    var json = File.ReadAllText("challenges.json");
    var saveData = JsonConvert.DeserializeObject<ChallengeDatastoreSaveData>(json);
    
    _completedChallenges.Clear();
    foreach (var challengeGuid in saveData.CompletedChallenges)
    {
        _completedChallenges.Add(challengeGuid);
    }
}
```

## MODでのチャレンジシステムの活用

MOD開発者は、チャレンジシステムを活用して、MODのコンテンツに関連するチャレンジを追加することができます。

```csharp
public void OnLoad(IModEntryInterface modEntryInterface)
{
    // チャレンジマスターの取得
    var challengeMaster = MasterHolder.ChallengeMaster;
    
    // チャレンジの登録
    var challengeGuid = new Guid("00000000-0000-0000-0000-000000000006");
    var itemGuid = new Guid("00000000-0000-0000-0000-000000000001"); // my_item
    var nextChallengeGuid = new Guid("00000000-0000-0000-0000-000000000007"); // another_challenge
    var recipeGuid = new Guid("00000000-0000-0000-0000-000000000003"); // my_recipe
    
    var taskParam = new CreateItemTaskParam(itemGuid);
    var nextChallenges = new List<NextChallengeElement>
    {
        new NextChallengeElement(nextChallengeGuid)
    };
    var clearedActions = new List<ClearedActionsElement>
    {
        new ClearedActionsElement(ClearedActionsElement.ClearedActionTypeConst.unlockCraftRecipe, new UnlockCraftRecipeClearedActionParam(recipeGuid))
    };
    
    challengeMaster.RegisterChallenge(challengeGuid, "my_challenge", "This is my custom challenge.", ChallengeMasterElement.TaskTypeConst.createItem, taskParam, nextChallenges, clearedActions);
}
```

## まとめ

チャレンジシステムは、プレイヤーに目標を提供し、ゲームの進行を導く重要な機能です。チャレンジの完了によって、新しいアイテムやレシピがアンロックされるなど、ゲームの進行に直接影響を与えます。また、MOD開発者は、チャレンジシステムを活用して、MODのコンテンツに関連するチャレンジを追加することができます。