# moorestechプロジェクト MOD開発ガイド

このドキュメントでは、moorestechプロジェクトのMOD開発方法について説明します。MODの基本構造、APIの使用方法、デプロイ方法などを含みます。

## 目次

1. [MODの概要](#modの概要)
2. [MODの基本構造](#modの基本構造)
3. [MOD APIの使用方法](#mod-apiの使用方法)
4. [アイテムの追加](#アイテムの追加)
5. [ブロックの追加](#ブロックの追加)
6. [レシピの追加](#レシピの追加)
7. [チャレンジの追加](#チャレンジの追加)
8. [MODのデプロイ](#modのデプロイ)
9. [MODのテスト](#modのテスト)
10. [MODの配布](#modの配布)

## MODの概要

moorestechでは、MODを使用してゲームに新しいコンテンツや機能を追加することができます。MODは、以下のような要素を追加または変更することができます：

- アイテム
- ブロック
- レシピ
- チャレンジ
- ゲームメカニクス

MODは、C#で記述され、DLLとしてコンパイルされます。MODは、ゲームの起動時にロードされ、ゲームに統合されます。

## MODの基本構造

MODは、以下のような基本構造を持ちます：

```
MyMod/
  ├── mod.json           # MODのメタデータ
  ├── MyMod.dll          # MODのコード
  ├── assets/            # MODのアセット
  │   ├── textures/      # テクスチャ
  │   ├── models/        # モデル
  │   └── sounds/        # サウンド
  └── data/              # MODのデータ
      ├── items.yml      # アイテム定義
      ├── blocks.yml     # ブロック定義
      ├── recipes.yml    # レシピ定義
      └── challenges.yml # チャレンジ定義
```

### mod.json

`mod.json`ファイルは、MODのメタデータを定義します。以下のような情報を含みます：

```json
{
  "id": "my_mod",
  "name": "My Mod",
  "version": "1.0.0",
  "description": "This is my first mod for moorestech.",
  "author": "Your Name",
  "dependencies": [
    {
      "id": "base",
      "version": ">=1.0.0"
    }
  ],
  "entryPoints": [
    "MyMod.MyModEntryPoint"
  ]
}
```

- `id`: MODの一意の識別子
- `name`: MODの表示名
- `version`: MODのバージョン
- `description`: MODの説明
- `author`: MODの作者
- `dependencies`: MODの依存関係
- `entryPoints`: MODのエントリーポイントクラス

### エントリーポイント

エントリーポイントは、MODがロードされたときに実行されるクラスです。以下のようなインターフェースを実装します：

```csharp
using Mod.Base;

namespace MyMod
{
    public class MyModEntryPoint : IModEntryPoint
    {
        public void OnLoad(IModEntryInterface modEntryInterface)
        {
            // MODの初期化コード
        }
    }
}
```

`OnLoad`メソッドは、MODがロードされたときに呼び出されます。このメソッドでは、MODの初期化コードを記述します。

## MOD APIの使用方法

moorestechでは、MOD APIを使用してゲームに新しいコンテンツや機能を追加することができます。MOD APIは、以下のような機能を提供します：

- アイテムの追加
- ブロックの追加
- レシピの追加
- チャレンジの追加
- イベントの購読
- ゲームメカニクスの変更

### IModEntryInterface

`IModEntryInterface`は、MODがゲームと対話するためのインターフェースです。以下のようなメソッドやプロパティを提供します：

```csharp
public interface IModEntryInterface
{
    // サービスの取得
    T GetService<T>();
    
    // イベントの購読
    IDisposable Subscribe<T>(IObservable<T> observable, Action<T> onNext);
    
    // パケットの送受信
    void SendPacket(ProtocolMessagePackBase packet);
    IObservable<T> ReceivePacket<T>() where T : ProtocolMessagePackBase;
}
```

### サービスの取得

`GetService<T>()`メソッドを使用して、ゲームのサービスを取得することができます。以下のようなサービスが利用可能です：

```csharp
// ブロックファクトリの取得
var blockFactory = modEntryInterface.GetService<IBlockFactory>();

// アイテムスタックファクトリの取得
var itemStackFactory = modEntryInterface.GetService<IItemStackFactory>();

// ワールドデータストアの取得
var worldDataStore = modEntryInterface.GetService<IWorldDataStore>();

// プレイヤーインベントリデータストアの取得
var playerInventoryDataStore = modEntryInterface.GetService<IPlayerInventoryDataStore>();

// チャレンジデータストアの取得
var challengeDatastore = modEntryInterface.GetService<ChallengeDatastore>();

// ゲームアンロック状態データコントローラーの取得
var gameUnlockStateDataController = modEntryInterface.GetService<IGameUnlockStateDataController>();
```

### イベントの購読

`Subscribe<T>()`メソッドを使用して、ゲームのイベントを購読することができます。以下のようなイベントが利用可能です：

```csharp
// ブロック設置イベントの購読
var worldEvent = modEntryInterface.GetService<WorldBlockUpdateEvent>();
modEntryInterface.Subscribe(worldEvent.OnBlockPlaceEvent, OnBlockPlace);

// アイテム作成イベントの購読
var craftEvent = modEntryInterface.GetService<CraftEvent>();
modEntryInterface.Subscribe(craftEvent.OnCraftItem, OnCraftItem);

// チャレンジ完了イベントの購読
var challengeEvent = modEntryInterface.GetService<ChallengeEvent>();
modEntryInterface.Subscribe(challengeEvent.OnCompleteChallenge, OnCompleteChallenge);
```

## アイテムの追加

アイテムを追加するには、以下の手順を実行します：

1. `data/items.yml`ファイルを作成します。
2. アイテムの定義を記述します。
3. MODのエントリーポイントでアイテムを登録します。

### items.yml

```yaml
items:
  - id: my_item
    name: My Item
    description: This is my custom item.
    stackSize: 64
    texture: my_mod:textures/items/my_item.png
```

### アイテムの登録

```csharp
public void OnLoad(IModEntryInterface modEntryInterface)
{
    // アイテムマスターの取得
    var itemMaster = MasterHolder.ItemMaster;
    
    // アイテムの登録
    var itemId = itemMaster.RegisterItem(new Guid("00000000-0000-0000-0000-000000000001"), "my_item", 64);
    
    // アイテムのアンロック
    var gameUnlockStateDataController = modEntryInterface.GetService<IGameUnlockStateDataController>();
    gameUnlockStateDataController.UnlockItem(itemId);
}
```

## ブロックの追加

ブロックを追加するには、以下の手順を実行します：

1. `data/blocks.yml`ファイルを作成します。
2. ブロックの定義を記述します。
3. MODのエントリーポイントでブロックを登録します。

### blocks.yml

```yaml
blocks:
  - id: my_block
    name: My Block
    description: This is my custom block.
    texture: my_mod:textures/blocks/my_block.png
    components:
      - type: inventory
        slots: 9
      - type: energy
        capacity: 1000
        consumption: 10
```

### ブロックの登録

```csharp
public void OnLoad(IModEntryInterface modEntryInterface)
{
    // ブロックファクトリの取得
    var blockFactory = modEntryInterface.GetService<IBlockFactory>();
    
    // ブロックの登録
    var blockId = blockFactory.RegisterBlock(new Guid("00000000-0000-0000-0000-000000000002"), "my_block");
    
    // ブロックのコンポーネントの設定
    blockFactory.RegisterBlockComponentFactory(blockId, (block) => {
        var componentManager = block.ComponentManager;
        componentManager.AddComponent(new BlockInventoryComponent(9));
        componentManager.AddComponent(new BlockEnergyComponent(1000, 10));
        return block;
    });
}
```

## レシピの追加

レシピを追加するには、以下の手順を実行します：

1. `data/recipes.yml`ファイルを作成します。
2. レシピの定義を記述します。
3. MODのエントリーポイントでレシピを登録します。

### recipes.yml

```yaml
recipes:
  - id: my_recipe
    type: crafting
    result:
      item: my_item
      count: 1
    ingredients:
      - item: iron_ingot
        count: 2
      - item: copper_ingot
        count: 1
```

### レシピの登録

```csharp
public void OnLoad(IModEntryInterface modEntryInterface)
{
    // クラフトレシピマスターの取得
    var craftRecipeMaster = MasterHolder.CraftRecipeMaster;
    
    // レシピの登録
    var recipeGuid = new Guid("00000000-0000-0000-0000-000000000003");
    var resultItemGuid = new Guid("00000000-0000-0000-0000-000000000001"); // my_item
    var ingredients = new List<CraftIngredient>
    {
        new CraftIngredient(MasterHolder.ItemMaster.GetItemId(new Guid("00000000-0000-0000-0000-000000000004")), 2), // iron_ingot
        new CraftIngredient(MasterHolder.ItemMaster.GetItemId(new Guid("00000000-0000-0000-0000-000000000005")), 1)  // copper_ingot
    };
    
    craftRecipeMaster.RegisterCraftRecipe(recipeGuid, resultItemGuid, 1, ingredients);
    
    // レシピのアンロック
    var gameUnlockStateDataController = modEntryInterface.GetService<IGameUnlockStateDataController>();
    gameUnlockStateDataController.UnlockCraftRecipe(recipeGuid);
}
```

## チャレンジの追加

チャレンジを追加するには、以下の手順を実行します：

1. `data/challenges.yml`ファイルを作成します。
2. チャレンジの定義を記述します。
3. MODのエントリーポイントでチャレンジを登録します。

### challenges.yml

```yaml
challenges:
  - id: my_challenge
    name: My Challenge
    description: This is my custom challenge.
    type: create_item
    params:
      item: my_item
    next_challenges:
      - another_challenge
    cleared_actions:
      - type: unlock_craft_recipe
        recipe: my_recipe
```

### チャレンジの登録

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

## MODのデプロイ

MODをデプロイするには、以下の手順を実行します：

1. MODのコードをコンパイルしてDLLを生成します。
2. MODのアセットとデータファイルを準備します。
3. MODのファイルをゲームのMODディレクトリに配置します。

### MODのコンパイル

```bash
# .NET CLIを使用してMODをコンパイル
dotnet build MyMod.csproj -c Release
```

### MODのインストール

MODのファイルを以下のディレクトリに配置します：

```
<game_directory>/mods/MyMod/
```

## MODのテスト

MODをテストするには、以下の手順を実行します：

1. MODをデプロイします。
2. ゲームを起動します。
3. MODが正しくロードされていることを確認します。
4. MODの機能をテストします。

### MODのデバッグ

MODのデバッグには、以下の方法が利用できます：

- Unityのコンソールでログを確認する
- デバッグビルドを使用してブレークポイントを設定する
- MODのコードにログ出力を追加する

```csharp
// ログの出力
Debug.Log("My Mod: Initialized");
Debug.LogWarning("My Mod: Warning message");
Debug.LogError("My Mod: Error message");
```

## MODの配布

MODを配布するには、以下の手順を実行します：

1. MODのファイルをZIPアーカイブにパッケージ化します。
2. MODの説明、スクリーンショット、インストール手順などを含むREADMEファイルを作成します。
3. MODをMOD配布プラットフォームにアップロードします。

### MODのパッケージ化

```bash
# MODのファイルをZIPアーカイブにパッケージ化
zip -r MyMod.zip MyMod/
```

### READMEの作成

```markdown
# My Mod

This is my custom mod for moorestech.

## Description

This mod adds new items, blocks, recipes, and challenges to the game.

## Installation

1. Download the mod ZIP file.
2. Extract the contents to the `mods` directory in your game installation.
3. Start the game.

## Features

- New item: My Item
- New block: My Block
- New recipe: My Recipe
- New challenge: My Challenge

## Screenshots

![Screenshot 1](screenshots/screenshot1.png)
![Screenshot 2](screenshots/screenshot2.png)

## License

This mod is licensed under the MIT License.
```

以上がmoorestechプロジェクトのMOD開発ガイドです。このガイドに従うことで、moorestechに新しいコンテンツや機能を追加するMODを開発することができます。