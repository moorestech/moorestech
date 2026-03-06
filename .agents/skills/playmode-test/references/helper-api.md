# PlayModeTestUtil ヘルパーAPI

ソースファイル: `moorestech_client/Assets/Scripts/Client.Tests/PlayModeTest/Util/PlayModeTestUtil.cs`

## EnterPlayModeUtil

```csharp
public static IEnumerator EnterPlayModeUtil()
```

PlayMode遷移の準備と実行を行う。以下を内部で実行:
1. `SessionState.SetBool("DebugObjectsBootstrap_Disabled", true)` - デバッグオブジェクト無効化
2. `AssetBundle.UnloadAllAssetBundles(true)` - 前回の残留AssetBundleクリーンアップ
3. `new EnterPlayMode(expectDomainReload: true)` をyield return

## LoadMainGame

```csharp
public static async UniTask LoadMainGame(string serverDirectory = null, string saveFilePath = null)
```

ゲームを起動してメインシーンをロードする。

- `serverDirectory`: サーバーデータディレクトリ。省略時は`PlayModeTestServerDirectoryPath`（`PlayModeTest/ServerData`）
- `saveFilePath`: セーブファイルパス。省略時は`dummy_play_mode_test_{GUID}.json`（既存セーブを読まない）

内部処理:
1. `GameInitializerScene`をロード
2. `InitializeScenePipeline`に`InitializeProprieties`を設定（ローカルサーバー起動、オートセーブ無効）
3. `GameInitializerSceneLoader`が表示されるまで最大60秒待機

## GiveItem

```csharp
public static async UniTask GiveItem(string itemName, int count)
```

プレイヤーにアイテムを付与する。名前で検索してコマンド送信。1秒待機後に返る。

- `itemName`: アイテム名（マスターデータの`Name`と一致させる）
- `count`: 付与数

## PlaceBlock

```csharp
public static IBlock PlaceBlock(string blockName, Vector3Int position, BlockDirection direction)
```

サーバー側にブロックを設置する。名前で検索して`WorldBlockDatastore.TryAddBlock`を呼ぶ。

- `blockName`: ブロック名（マスターデータの`Name`と一致させる）
- `position`: 設置座標
- `direction`: 向き（`BlockDirection.North/South/East/West`）
- 戻り値: `IBlock` - 設置されたブロック

## RemoveBlock

```csharp
public static bool RemoveBlock(Vector3Int position)
```

指定座標のブロックを除去する。

## InsertItemToBlock

```csharp
public static IItemStack InsertItemToBlock(IBlock block, ItemId itemId, int count)
```

ブロックのインベントリにアイテムを挿入する。

- `block`: `PlaceBlock`の戻り値など
- `itemId`: アイテムID
- `count`: 挿入数
- 戻り値: `IItemStack` - 挿入できなかった残り

## PlayModeTestServerDirectoryPath

```csharp
public static string PlayModeTestServerDirectoryPath
```

PlayModeテスト用サーバーデータのパス: `moorestech_client/Assets/Scripts/Client.Tests/PlayModeTest/ServerData`

テスト用マスターデータ（blocks.json, items.json等）がここに配置されている。
