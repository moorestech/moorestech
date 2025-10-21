# Technology Stack - moorestech

## Architecture

### 全体構成
moorestechはUnityベースのクライアント・サーバー分離型アーキテクチャを採用しています。

```
┌─────────────────────────────────────────────────────────────┐
│                    moorestech_client                         │
│                   (Unity Client Project)                     │
│                                                               │
│  Client.Starter → Client.Network → Client.Game               │
│                                                               │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      │ MessagePack Protocol
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                    moorestech_server                         │
│                   (Unity Server Project)                     │
│                                                               │
│  Server.Boot → Server.Protocol → Game.World → Core           │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### サーバーアーキテクチャ

#### レイヤー構成
```
Server.Boot (エントリーポイント、DI構築)
    ↓
Server.Protocol (通信管理、MessagePack)
    ↓
Game.World (ワールド管理、ブロック・エンティティ配置)
    ↓
Game.* (各種ゲームシステム)
    ├── Game.Block (ブロックシステム)
    ├── Game.Entity (エンティティシステム)
    ├── Game.PlayerInventory (プレイヤーインベントリ)
    ├── Game.Train (列車システム)
    ├── Game.EnergySystem (電力システム)
    ├── Game.Fluid (液体システム)
    ├── Game.Challenge (チャレンジシステム)
    ├── Game.Research (研究システム)
    └── Game.SaveLoad (セーブ・ロード)
    ↓
Core.* (基盤機能)
    ├── Core.Item (アイテム基盤)
    ├── Core.Inventory (インベントリ基盤)
    ├── Core.Master (マスターデータ管理)
    └── Core.Update (更新システム)
```

#### 主要コンポーネント
- **ServerContext**: サーバー全体の静的コンテキスト（`Game.Context/ServerContext.cs`）
  - ItemStackFactory, BlockFactory
  - WorldBlockDatastore, MapVeinDatastore
  - イベントハンドラー (WorldBlockUpdateEvent, BlockOpenableInventoryUpdateEvent)
- **依存性注入**: Microsoft.Extensions.DependencyInjection
- **プロトコル**: MessagePackベースのバイナリシリアライゼーション

### クライアントアーキテクチャ

#### レイヤー構成
```
Client.Starter (エントリーポイント、DI構築)
    ↓
Client.Network (サーバー通信)
    ↓
Client.Game (ゲーム状態管理、シーケンス制御)
    ↓
Client.Game.InGame.* (ゲーム内機能)
    ├── Client.Game.InGame.Block (ブロック表示・操作)
    ├── Client.Game.InGame.UI (UI表示)
    └── その他
```

#### 主要コンポーネント
- **VContainer**: クライアント側のDIコンテナ
- **Addressables**: アセット動的ロード
- **UI Framework**: Unity UI (uGUI)

## Backend (Unity Server)

### 言語とフレームワーク
- **言語**: C# (.NET Standard 2.1)
- **ゲームエンジン**: Unity 2022.3 LTS
- **アセンブリ定義**: 44個のasmdefファイルによるモジュール分離

### 主要ライブラリ
- **MessagePack**: バイナリシリアライゼーション (サーバー・クライアント通信)
- **Microsoft.Extensions.DependencyInjection**: 依存性注入
- **CsvHelper**: CSVファイル処理
- **YamlDotNet**: YAMLパース (SourceGenerator用)

### マスターデータシステム

#### データフロー
```
VanillaSchema/*.yml (YAMLスキーマ定義)
    ↓
SourceGenerator (ビルド時に自動生成)
    ↓
Mooresmaster.Model.*Module (C#クラス自動生成)
    ↓
mods/*/master/*.json (実際のゲームデータ)
    ↓
MasterHolder.Load() (実行時ロード)
    ↓
MasterHolder.ItemMaster, BlockMaster等でアクセス
```

#### スキーマファイル
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

### ゲームシステムモジュール
- **Game.Block**: ブロック配置・削除・更新ロジック
- **Game.Entity**: エンティティ（プレイヤー、NPC）管理
- **Game.Train**: 列車の移動・積載管理
- **Game.EnergySystem**: 電力生成・消費・伝達
- **Game.Fluid**: 液体の生成・移動・消費
- **Game.Crafting**: クラフトレシピ実行
- **Game.Challenge**: チャレンジ進行管理
- **Game.Research**: 技術研究のアンロック
- **Game.UnlockState**: ゲーム進行状態管理
- **Game.SaveLoad**: ワールドデータの保存・読み込み

## Frontend (Unity Client)

### 言語とフレームワーク
- **言語**: C# (.NET Standard 2.1)
- **ゲームエンジン**: Unity 2022.3 LTS
- **UI**: Unity UI (uGUI)

### 主要ライブラリ
- **VContainer**: 依存性注入フレームワーク
- **Addressables**: アセット動的ロード
- **MessagePack**: サーバー通信用シリアライゼーション
- **NaughtyCharacter**: キャラクター制御 (サードパーティ)
- **InGameDebugConsole**: デバッグコンソール

### クライアントモジュール
- **Client.Network**: サーバー通信プロトコル実装
- **Client.Game**: ゲームシーン管理、状態制御
- **Client.Input**: プレイヤー入力処理
- **Client.Mod**: MODコンテンツのロード
- **Client.Localization**: 多言語対応
- **Client.Skit**: カットシーン・会話システム
- **Client.CutScene**: カットシーン再生

## Development Environment

### 必須ツール
- **Unity Hub**: Unity 2022.3 LTS
- **IDE**:
  - JetBrains Rider (推奨)
  - Visual Studio
  - Visual Studio Code
- **Git**: バージョン管理

### プロジェクト構成
```
moorestech/
├── moorestech_server/      # サーバー Unity プロジェクト
├── moorestech_client/      # クライアント Unity プロジェクト
├── VanillaSchema/          # YAMLスキーマ定義
├── mods/                   # MODデータ（JSON）
├── tools/                  # ビルド・テストスクリプト
└── docs/                   # ドキュメント
```

## Common Commands

### テスト実行

#### MCPツール使用（推奨）
```bash
# サーバー側テスト（MCPツール）
mcp__moorestech_server__RunEditModeTests
  groupNames: ["^Tests\\.CombinedTest\\.Core\\.ElectricPumpTest$"]

# クライアント側テスト（MCPツール）
mcp__moorestech_client__RunEditModeTests
  groupNames: ["^ClientTests\\.Feature\\.InventoryTest$"]
```

#### シェルスクリプト使用（フォールバック）
```bash
# サーバー側テスト
./tools/unity-test.sh moorestech_server "^Tests\\.CombinedTest\\.Core\\.ElectricPumpTest$"

# クライアント側テスト（GUIモード）
./tools/unity-test.sh moorestech_client "^ClientTests\\.Feature\\.InventoryTest$" isGui
```

### コンパイル確認

#### MCPツール使用（推奨）
```bash
# サーバー側コンパイル
mcp__moorestech_server__RefreshAssets  # コンパイル実行
mcp__moorestech_server__GetCompileLogs # エラー確認

# クライアント側コンパイル
mcp__moorestech_client__RefreshAssets  # コンパイル実行
mcp__moorestech_client__GetCompileLogs # エラー確認
```

### ビルド実行
```bash
# クライアントビルド（デフォルト出力先）
./tools/unity-build-test.sh moorestech_client

# クライアントビルド（出力先指定）
./tools/unity-build-test.sh moorestech_client /path/to/output
```

### ゲーム起動
```bash
# Unity Editorでの起動
# 1. moorestech_server を開き、Boot シーンを再生
# 2. moorestech_client を開き、MainGame シーンを再生
```

## Environment Variables

### Unity Editor設定
```bash
# Unity Hub経由で 2022.3 LTS をインストール
# プロジェクトごとに以下を設定：
# - Scripting Backend: Mono (Editor) / IL2CPP (Build)
# - API Compatibility Level: .NET Standard 2.1
```

### テスト用マスターデータ
```
moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/
├── items.json
├── blocks.json
├── fluids.json
├── craftRecipes.json
└── ...
```

## Port Configuration

### デフォルトポート
- **サーバー**: (Unity Editor内で動作、外部ポート不使用)
- **クライアント**: (サーバーに接続、外部ポート不使用)

## Development Workflow

### 1. 新機能開発
```bash
# 1. 既存システムの調査（Glob, Grep, Read）
# 2. 類似実装パターンの確認
# 3. 必要に応じてYAMLスキーマ更新
# 4. テストコード作成
# 5. 実装
# 6. コンパイル確認
# 7. テスト実行
```

### 2. マスターデータ追加
```bash
# 1. VanillaSchema/*.yml にスキーマ追加
# 2. Unityでリビルド（SourceGeneratorが自動生成）
# 3. mods/*/master/*.json に実データ追加
# 4. MasterHolder にプロパティ追加
# 5. テストデータ更新
```

### 3. プロトコル実装
```bash
# docs/ProtocolImplementationGuide.md を参照
# 1. サーバー側プロトコル実装
# 2. クライアント側プロトコル実装
# 3. MessagePack属性追加
# 4. テストコード作成
```

## Code Quality Tools

### 静的解析
- **Unity Analyzer**: Unity特有のアンチパターン検出
- **Roslyn Analyzers**: C#コード品質チェック

### テストフレームワーク
- **Unity Test Runner**: Edit Mode / Play Mode テスト
- **NUnit**: 単体テストフレームワーク

## Important Notes

### 禁止事項
- **.metaファイルの手動作成禁止**: Unityが自動生成
- **Libraryディレクトリの削除禁止**: キャッシュ破壊により再インポートが発生
- **Mooresmaster.Model.*の手動作成禁止**: SourceGeneratorが自動生成
- **try-catch使用禁止**: 条件分岐とnullチェックで対応

### 必須事項
- **コード作成後は必ずコンパイル実行**
- **サーバー実装時はdocs/ServerGuide.md参照**
- **クライアント実装時はdocs/ClientGuide.md参照**
- **プロトコル実装時はdocs/ProtocolImplementationGuide.md参照**
- **テストはgroupNamesで実行対象を限定**

### ベストプラクティス
- **既存システムの徹底調査**: 新機能前に必ず既存実装を確認
- **XY問題の回避**: 根本的な解決を優先
- **#regionとローカル関数**: 複雑なロジックの可読性向上
- **null前提の最小化**: 基本的な部分はnullではない前提
