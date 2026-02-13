# 初期化パターン

## ディレクトリ構造

```
moorestech_server/Assets/Scripts/
├── Tests/
│   ├── CombinedTest/          # 統合テスト（複数システムの連携）
│   │   ├── Core/              # コアシステム（電力、液体、歯車等）
│   │   ├── Server/
│   │   │   └── PacketTest/    # プロトコルテスト
│   │   │       └── Event/     # イベントプロトコルテスト
│   │   └── Game/              # ゲーム機能テスト
│   ├── UnitTest/              # 単体テスト
│   │   ├── Core/
│   │   │   ├── Block/
│   │   │   ├── Inventory/
│   │   │   └── Other/
│   │   ├── Server/
│   │   └── Game/
│   └── Util/                  # テストヘルパー
└── Tests.Module/              # テストインフラ
    └── TestMod/               # テスト用マスターデータ・ID定義
```

## 基本初期化（全テスト共通）

```csharp
using Server.Boot;
using Tests.Module.TestMod;
using Game.Context;

// DIコンテナを生成してServerContextを初期化
var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator()
    .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
```

- `packet`: `PacketResponseCreator` - パケットテストで使用
- `serviceProvider`: `IServiceProvider` - 個別サービスの取得に使用
- この呼び出しで `ServerContext` の静的プロパティが初期化される

## ServerContext経由のアクセス

```csharp
ServerContext.WorldBlockDatastore   // ワールドのブロックデータストア
ServerContext.ItemStackFactory      // アイテムスタック生成
ServerContext.BlockFactory          // ブロック生成
ServerContext.WorldBlockComponentDatastore<T>()  // コンポーネントデータストア
```

## ServiceProvider経由のアクセス

```csharp
var service = serviceProvider.GetService<TrainUpdateService>();
```
