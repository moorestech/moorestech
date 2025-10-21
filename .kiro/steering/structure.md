# Project Structure - moorestech

## Root Directory Organization

```
moorestech/
├── moorestech_server/          # サーバー Unity プロジェクト (ゲームロジック)
├── moorestech_client/          # クライアント Unity プロジェクト (描画・UI)
├── VanillaSchema/              # YAMLスキーマ定義（マスターデータ）
├── mods/                       # MODデータ（JSON形式のゲームデータ）
├── tools/                      # ビルド・テスト用シェルスクリプト
├── docs/                       # 技術ドキュメント
├── memory-bank/                # プロジェクトメモリ（仕様・進捗）
├── .kiro/                      # Kiroステアリングドキュメント
├── .specify/                   # 仕様駆動開発テンプレート
├── .serena/                    # Serenaメモリ（AI支援）
├── .claude/                    # Claude Code設定・コマンド
├── .github/                    # GitHub Actions, Notion同期
├── AGENTS.md                   # AI開発ガイドライン
├── CLAUDE.md                   # Claude固有の指示
└── README.md                   # プロジェクト概要
```

### ディレクトリの役割

#### moorestech_server/
サーバー側のゲームロジックを担当するUnityプロジェクト。ブロック配置、アイテム管理、レシピ処理、セーブ・ロードなどの全ゲームシステムを実装。

**主要ディレクトリ**:
- `Assets/Scripts/`: サーバーコードベース
  - `Server.Boot/`: エントリーポイント、DI構築
  - `Server.Protocol/`: クライアント通信プロトコル
  - `Game.*/`: 各種ゲームシステム（Block, Entity, Train等）
  - `Core.*/`: 基盤機能（Item, Inventory, Master）
  - `Tests/`, `Tests.Module/`: テストコード

#### moorestech_client/
クライアント側のUI・描画を担当するUnityプロジェクト。プレイヤー操作、ブロック表示、インベントリUIなどを実装。

**主要ディレクトリ**:
- `Assets/Scripts/`: クライアントコードベース
  - `Client.Starter/`: エントリーポイント、DI構築
  - `Client.Network/`: サーバー通信
  - `Client.Game/`: ゲーム状態管理
  - `Client.Tests/`: テストコード
- `Assets/AddressableResources/`: 動的ロードアセット（モデル、UI素材）

#### VanillaSchema/
マスターデータのYAMLスキーマ定義。SourceGeneratorがこれを読み込み、C#クラスを自動生成。

**ファイル一覧**:
- `blocks.yml`: ブロック定義
- `items.yml`: アイテム定義
- `fluids.yml`: 液体定義
- `machineRecipes.yml`: 機械レシピ定義
- `craftRecipes.yml`: クラフトレシピ定義
- `challenges.yml`: チャレンジ定義
- `research.yml`: 研究定義
- `mapObjects.yml`: マップオブジェクト定義
- `characters.yml`: キャラクター定義
- `modMeta.yml`: MODメタデータ

#### mods/
実際のゲームデータをJSON形式で格納。MODシステムにより動的にロード。

#### tools/
開発用シェルスクリプト集。
- `unity-test.sh`: Unity Test Runner実行
- `unity-build-test.sh`: Unityビルド実行

#### docs/
技術ドキュメント。
- `ServerGuide.md`: サーバー実装ガイド
- `ClientGuide.md`: クライアント実装ガイド
- `ProtocolImplementationGuide.md`: プロトコル実装ガイド
- `train/`: 列車システムドキュメント

## Subdirectory Structures

### moorestech_server/Assets/Scripts/

```
Scripts/
├── Server.Boot/                    # サーバー起動・初期化
├── Server.Protocol/                # クライアント通信プロトコル
├── Server.Event/                   # サーバーイベントシステム
├── Server.Util/                    # サーバーユーティリティ
├── Game.Context/                   # ServerContext（静的コンテキスト）
├── Game.World/                     # ワールド管理（ブロック・エンティティ配置）
├── Game.World.Interface/           # ワールドインターフェース
├── Game.World.EventHandler/        # ワールドイベントハンドラー
├── Game.Block/                     # ブロックシステム実装
├── Game.Block.Interface/           # ブロックインターフェース
├── Game.Entity/                    # エンティティシステム実装
├── Game.Entity.Interface/          # エンティティインターフェース
├── Game.PlayerInventory/           # プレイヤーインベントリ
├── Game.PlayerInventory.Interface/ # プレイヤーインベントリIF
├── Game.Train/                     # 列車システム
├── Game.EnergySystem/              # 電力システム
├── Game.Fluid/                     # 液体システム
├── Game.Crafting.Interface/        # クラフトインターフェース
├── Game.CraftChainer/              # クラフトチェーン管理
├── Game.CraftTree/                 # クラフトツリー
├── Game.Gear/                      # ギアシステム
├── Game.Map/                       # マップ管理
├── Game.Map.Interface/             # マップインターフェース
├── Game.Challenge/                 # チャレンジシステム
├── Game.Research/                  # 研究システム
├── Game.UnlockState/               # アンロック状態管理
├── Game.SaveLoad/                  # セーブ・ロード
├── Game.SaveLoad.Interface/        # セーブ・ロードIF
├── Game.Action/                    # ゲームアクション
├── Game.Paths/                     # パス管理
├── Core.Item/                      # アイテム基盤
├── Core.Item.Interface/            # アイテムインターフェース
├── Core.Inventory/                 # インベントリ基盤
├── Core.Master/                    # マスターデータ管理
├── Core.Update/                    # 更新システム
├── Mod.Base/                       # MODベース機能
├── Mod.Config/                     # MOD設定
├── Mod.Loader/                     # MODローダー
├── Common.Debug/                   # デバッグ機能
├── ClassLibrary/                   # 共通ライブラリ
├── Nuget/                          # NuGetパッケージ
├── Tests/                          # テストコード（統合テスト）
├── Tests.Module/                   # テストモジュール（テスト用データ）
└── Editor/                         # Unityエディタ拡張
```

### moorestech_client/Assets/Scripts/

```
Scripts/
├── Client.Starter/         # クライアント起動・初期化
├── Client.Network/         # サーバー通信
├── Client.Game/            # ゲーム状態管理・シーケンス制御
├── Client.Input/           # プレイヤー入力処理
├── Client.Mod/             # MODコンテンツロード
├── Client.Localization/    # 多言語対応
├── Client.Skit/            # 会話システム
├── Client.CutScene/        # カットシーン再生
├── Client.Common/          # クライアント共通機能
└── Client.Tests/           # クライアントテストコード
```

## Code Organization Patterns

### アセンブリ分離原則
各機能モジュールは独立した`.asmdef`ファイルで定義され、依存関係が明確に管理されています。

**依存関係の方向**:
```
Server.Boot → Server.Protocol → Game.World → Game.* → Core.*
```

**インターフェース分離**:
多くのモジュールは実装とインターフェースを分離しています。
- `Game.Block` (実装) ← `Game.Block.Interface` (IF)
- `Game.Entity` (実装) ← `Game.Entity.Interface` (IF)
- `Core.Item` (実装) ← `Core.Item.Interface` (IF)

### 名前空間規則
名前空間はアセンブリ名と一致させます。

**例**:
- アセンブリ: `Game.Block.asmdef`
- 名前空間: `Game.Block`

### レイヤー分離

#### サーバー側レイヤー
1. **Server Layer**: 通信、プロトコル、起動処理
2. **Game Layer**: ゲームロジック（Block, Entity, World等）
3. **Core Layer**: 基盤機能（Item, Inventory, Master）

#### クライアント側レイヤー
1. **Client.Starter**: 起動・初期化
2. **Client.Network**: 通信
3. **Client.Game**: ゲーム状態管理
4. **Client.Game.InGame.***: ゲーム内機能（Block, UI等）

## File Naming Conventions

### C#ファイル
- **クラス名と一致**: `ServerContext.cs` → `class ServerContext`
- **インターフェース**: `IBlockFactory.cs` → `interface IBlockFactory`
- **抽象クラス**: `BaseBlock.cs` → `abstract class BaseBlock`

### アセンブリ定義
- **形式**: `<Namespace>.asmdef`
- **例**: `Game.Block.asmdef`, `Core.Item.Interface.asmdef`

### YAMLスキーマ
- **複数形**: `blocks.yml`, `items.yml`, `fluids.yml`
- **キャメルケース**: `machineRecipes.yml`, `craftRecipes.yml`

### JSONマスターデータ
- **YAMLと同名**: `blocks.json`, `items.json`, `fluids.json`
- **配置場所**: `mods/<modName>/master/<fileName>.json`

### テストファイル
- **テストクラス名**: `<対象クラス名>Test.cs`
- **例**: `ElectricPumpTest.cs` → `class ElectricPumpTest`

## Import Organization

### using文の順序
```csharp
// 1. System名前空間
using System;
using System.Collections.Generic;

// 2. Unity名前空間
using UnityEngine;

// 3. サードパーティライブラリ
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

// 4. プロジェクト内名前空間（アルファベット順）
using Core.Item.Interface;
using Game.Block.Interface;
using Game.Map.Interface.MapObject;
```

### 依存関係の方向
- **上位レイヤーは下位レイヤーに依存可能**
- **下位レイヤーは上位レイヤーに依存禁止**
- **インターフェースを介して疎結合を実現**

**例**:
```csharp
// OK: Game.Block は Core.Item.Interface に依存可能
using Core.Item.Interface;

// NG: Core.Item は Game.Block に依存不可
// using Game.Block; // コンパイルエラー
```

## Key Architectural Principles

### 1. 依存性注入 (DI)
すべての依存関係はDIコンテナを通じて解決します。

**サーバー側**:
```csharp
// Microsoft.Extensions.DependencyInjection
services.AddSingleton<IItemStackFactory, ItemStackFactory>();
```

**クライアント側**:
```csharp
// VContainer
builder.Register<INetworkClient, NetworkClient>(Lifetime.Singleton);
```

### 2. イベント駆動設計
ゲームシステム間の通知はイベントシステムで実装。

**例**:
```csharp
ServerContext.WorldBlockUpdateEvent.Subscribe(OnBlockUpdate);
ServerContext.BlockOpenableInventoryUpdateEvent.Subscribe(OnInventoryUpdate);
```

### 3. マスターデータ駆動
すべてのゲームデータはYAMLスキーマ + JSON実データで定義。

**アクセス例**:
```csharp
var itemData = MasterHolder.ItemMaster.GetItemData(itemId);
var blockData = MasterHolder.BlockMaster.GetBlockData(blockId);
```

### 4. インターフェース分離原則
実装とインターフェースを分離し、依存関係を最小化。

**例**:
- `IItemStackFactory` (IF) ← `ItemStackFactory` (実装)
- `IBlockFactory` (IF) ← `BlockFactory` (実装)

### 5. テスト駆動開発 (TDD)
新機能は必ずテストコードとセットで実装。

**テスト用マスターデータ**:
```
moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/
└── mods/forUnitTest/master/
    ├── items.json
    ├── blocks.json
    └── ...
```

**テスト用ブロックID**:
```csharp
// ForUnitTestModBlockId.cs
public static class ForUnitTestModBlockId
{
    public static int ElectricPump = 1;
    public static int Generator = 2;
    // ...
}
```

### 6. #regionとローカル関数
複雑なメソッドは主要フローと実装詳細を分離。

```csharp
public void ComplexMethod()
{
    // メインの処理フロー（一目で理解可能）
    var data = ProcessData();
    var result = CalculateResult(data);

    #region Internal

    Data ProcessData()
    {
        // 詳細な実装（#regionで隠蔽）
    }

    Result CalculateResult(Data data)
    {
        // 詳細な実装
    }

    #endregion
}
```

**重要**: `#endregion`の下にはコードを書かない。

### 7. 既存システムの再利用
新機能追加時は既存システムを徹底調査し、拡張ポイントを活用。

**調査手順**:
1. Glob, Grepで関連ファイルを検索
2. 類似機能の実装パターンを確認
3. 既存インターフェースやベースクラスを特定
4. 既存システムを拡張する形で実装

### 8. Null安全設計
基本的な部分はnullではない前提でコードを書く。

**nullチェックが必要な場面**:
- 外部から受け取るデータ（API、ユーザー入力）
- Addressables等の非同期ロード結果

**nullチェック不要な場面**:
- MasterHolderなどのシステムコアコンポーネント
- Awake/Startで初期化される基本的なコンポーネント
- 設計上必ず存在することが保証されているオブジェクト

### 9. 一貫性の保持
ファイル全体、ネームスペース全体、アセンブリ全体、プロジェクト全体で一貫性のあるコードを書く。

**チェックポイント**:
- 命名規約の統一
- API構成の整合性
- エラー伝播方針の一致
- コメントスタイルの統一

### 10. XY問題の回避
目先の問題にとらわれず、根本的な解決を常に行う。

**思考プロセス**:
1. 「本当に解決すべき問題は何か？」を問う
2. 「既存のどの原則に則るべきか？」を確認
3. 「既存資産をどう再利用できるか？」を検討
4. 「この変更が全体最適にどう寄与するか？」を評価
