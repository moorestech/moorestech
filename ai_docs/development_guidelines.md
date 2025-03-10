# moorestechプロジェクト 開発ガイドライン

このドキュメントでは、moorestechプロジェクトの開発ガイドラインについて説明します。コーディング規約、ベストプラクティス、開発フローなどを含みます。

## 目次

1. [コーディング規約](#コーディング規約)
2. [アーキテクチャガイドライン](#アーキテクチャガイドライン)
3. [テストガイドライン](#テストガイドライン)
4. [ドキュメントガイドライン](#ドキュメントガイドライン)
5. [Git運用ガイドライン](#git運用ガイドライン)
6. [リリースガイドライン](#リリースガイドライン)

## コーディング規約

### 命名規則

- **クラス名**: PascalCase（例: `BlockInventory`）
- **メソッド名**: PascalCase（例: `GetItem`）
- **プロパティ名**: PascalCase（例: `ItemId`）
- **変数名**: camelCase（例: `itemStack`）
- **プライベートフィールド**: _camelCase（例: `_blockComponents`）
- **インターフェース名**: IPascalCase（例: `IBlock`）
- **定数**: UPPER_CASE（例: `MAX_STACK_SIZE`）
- **列挙型**: PascalCase（例: `BlockDirection`）
- **列挙型の値**: PascalCase（例: `North`）

### ファイル構成

- 1ファイルにつき1クラスを原則とします。
- ファイル名はクラス名と一致させます。
- 名前空間はディレクトリ構造と一致させます。

### コードスタイル

- インデントにはスペースを使用します。
- 中括弧は新しい行に配置します。
- メソッドの間には空行を入れます。
- コメントは日本語と英語の両方で記述します。
- 複雑なロジックには説明コメントを追加します。
- メソッドの長さは30行以内を目指します。
- クラスの長さは300行以内を目指します。

### コメント

- クラスやメソッドには、その目的や機能を説明するコメントを追加します。
- パブリックAPIには、パラメータや戻り値の説明を含むコメントを追加します。
- 複雑なロジックには、その意図や動作を説明するコメントを追加します。
- コメントは日本語と英語の両方で記述します。

```csharp
/// <summary>
///     ブロックで何らかのステートが変化したときに呼び出されます
///     例えば、動いている機械が止まったなど
///     クライアント側で稼働アニメーションや稼働音を実行するときに使用します
/// </summary>
public IObservable<BlockState> BlockStateChange { get; }
```

### 例外処理

- 例外は適切に処理します。
- 例外をキャッチする場合は、具体的な例外型を指定します。
- 例外をスローする場合は、適切な例外型を使用します。
- 例外メッセージは明確で具体的なものにします。

```csharp
try
{
    // 処理
}
catch (IOException e)
{
    Debug.LogError($"ファイルの読み込みに失敗しました: {e.Message}");
    throw;
}
```

## アーキテクチャガイドライン

### 依存性注入

- 依存性注入（DI）を使用して、コンポーネント間の結合度を低くします。
- サーバー側では`Microsoft.Extensions.DependencyInjection`を使用します。
- クライアント側では`VContainer`を使用します。
- インターフェースを使用して、実装の詳細を隠蔽します。

```csharp
// サーバー側
services.AddSingleton<IBlockFactory, BlockFactory>();

// クライアント側
builder.Register<IBlockFactory, BlockFactory>(Lifetime.Singleton);
```

### SOLID原則

- **単一責任の原則（SRP）**: クラスは1つの責任のみを持つようにします。
- **オープン・クローズドの原則（OCP）**: クラスは拡張に対してオープンで、修正に対してクローズドであるべきです。
- **リスコフの置換原則（LSP）**: サブクラスはスーパークラスの代わりに使用できるべきです。
- **インターフェース分離の原則（ISP）**: クライアントは使用しないインターフェースに依存すべきではありません。
- **依存性逆転の原則（DIP）**: 高レベルのモジュールは低レベルのモジュールに依存すべきではありません。両方とも抽象に依存すべきです。

### コンポーネント指向

- ブロックやエンティティなどのゲームオブジェクトは、コンポーネントの集合として実装します。
- コンポーネントは単一の機能を提供し、他のコンポーネントと協調して動作します。
- コンポーネントは`IBlockComponent`などのインターフェースを実装します。

```csharp
public class Block : IBlock
{
    public BlockComponentManager ComponentManager { get; }
    
    public Block()
    {
        ComponentManager = new BlockComponentManager();
        ComponentManager.AddComponent(new BlockInventoryComponent());
        ComponentManager.AddComponent(new BlockEnergyComponent());
    }
}
```

### イベント駆動型アーキテクチャ

- コンポーネント間の通信には、イベント駆動型のアプローチを使用します。
- イベントの発行と購読には、UniRxの`IObservable`と`IObserver`を使用します。
- イベントハンドラは、イベントの発生源から分離します。

```csharp
// イベントの発行
private readonly Subject<BlockState> _blockStateChange = new();
public IObservable<BlockState> BlockStateChange => _blockStateChange;

// イベントの発火
_blockStateChange.OnNext(new BlockState(stateDetails));

// イベントの購読
blockStateChange.Subscribe(OnBlockStateChange);
```

## テストガイドライン

### ユニットテスト

- ユニットテストは、個々のクラスやメソッドの機能をテストします。
- テストは独立していて、他のテストに依存しないようにします。
- テストは自動化され、CI/CDパイプラインの一部として実行されます。
- テストは、コードの変更が既存の機能を壊していないことを確認するために使用されます。

```csharp
[Test]
public void InsertItem_WhenInventoryIsEmpty_ShouldInsertItem()
{
    // Arrange
    var inventory = new BlockInventory(10);
    var item = new ItemStack(new ItemId(1), 5);
    
    // Act
    var result = inventory.InsertItem(item);
    
    // Assert
    Assert.AreEqual(0, result.Count);
    Assert.AreEqual(5, inventory.GetItem(0).Count);
}
```

### 統合テスト

- 統合テストは、複数のコンポーネントが一緒に動作することをテストします。
- テストは、実際のユースケースをシミュレートします。
- テストは、コンポーネント間の相互作用が期待通りに動作することを確認します。

```csharp
[Test]
public void CraftItem_WhenIngredientsAreAvailable_ShouldCreateItem()
{
    // Arrange
    var playerInventory = new PlayerInventory();
    var craftingInventory = new CraftingInventory(playerInventory);
    var craftRecipe = new CraftRecipe(/* ... */);
    
    // Act
    craftingInventory.SetItem(0, new ItemStack(new ItemId(1), 1));
    craftingInventory.SetItem(1, new ItemStack(new ItemId(2), 1));
    craftingInventory.NormalCraft();
    
    // Assert
    Assert.AreEqual(1, playerInventory.GetItem(0).Count);
}
```

## ドキュメントガイドライン

### コードドキュメント

- パブリックAPIには、XMLドキュメントコメントを追加します。
- コメントには、メソッドの目的、パラメータ、戻り値、例外などの情報を含めます。
- コメントは日本語と英語の両方で記述します。

```csharp
/// <summary>
///     アイテムをインベントリに挿入します
///     Insert an item into the inventory
/// </summary>
/// <param name="itemStack">挿入するアイテム / The item to insert</param>
/// <returns>挿入できなかったアイテム / The item that could not be inserted</returns>
/// <exception cref="ArgumentNullException">itemStackがnullの場合 / If itemStack is null</exception>
public IItemStack InsertItem(IItemStack itemStack)
{
    // ...
}
```

### プロジェクトドキュメント

- プロジェクトの概要、アーキテクチャ、主要コンポーネントなどを説明するドキュメントを作成します。
- 開発環境のセットアップ、ビルド方法、テスト方法などの手順を記述します。
- ドキュメントはMarkdown形式で作成し、リポジトリに保存します。

## Git運用ガイドライン

### ブランチ戦略

- `main`: リリース可能な状態を維持するブランチ
- `develop`: 開発中の機能を統合するブランチ
- `feature/*`: 新機能の開発を行うブランチ
- `bugfix/*`: バグ修正を行うブランチ
- `release/*`: リリース準備を行うブランチ
- `hotfix/*`: 緊急のバグ修正を行うブランチ

### コミットメッセージ

- コミットメッセージは、変更内容を明確に説明します。
- コミットメッセージは、以下の形式に従います：

```
[種類] 変更内容の要約

変更内容の詳細な説明
```

- 種類は以下のいずれかを使用します：
  - `feat`: 新機能
  - `fix`: バグ修正
  - `docs`: ドキュメントのみの変更
  - `style`: コードの意味に影響を与えない変更（空白、フォーマット、セミコロンの欠落など）
  - `refactor`: バグ修正や機能追加ではないコードの変更
  - `perf`: パフォーマンスを向上させるコードの変更
  - `test`: テストの追加や修正
  - `chore`: ビルドプロセスやツールの変更

### プルリクエスト

- プルリクエストは、機能やバグ修正ごとに作成します。
- プルリクエストには、変更内容の説明、関連するIssue、テスト方法などを記述します。
- プルリクエストは、レビューとCIテストに合格した後にマージされます。

## リリースガイドライン

### バージョニング

- セマンティックバージョニング（SemVer）を使用します：`MAJOR.MINOR.PATCH`
  - `MAJOR`: 互換性のない変更
  - `MINOR`: 後方互換性のある機能追加
  - `PATCH`: 後方互換性のあるバグ修正

### リリースプロセス

1. `release/vX.Y.Z`ブランチを`develop`から作成します。
2. リリース準備（バージョン番号の更新、リリースノートの作成など）を行います。
3. テストを実行し、問題がないことを確認します。
4. `release/vX.Y.Z`を`main`にマージします。
5. `main`にタグ`vX.Y.Z`を付けます。
6. `main`を`develop`にマージします。

### リリースノート

- リリースノートには、新機能、バグ修正、既知の問題などを記述します。
- リリースノートは、ユーザーにとって理解しやすい言葉で記述します。
- リリースノートは、GitHubのリリースページに公開します。

以上がmoorestechプロジェクトの開発ガイドラインです。これらのガイドラインに従うことで、コードの品質を維持し、開発プロセスをスムーズに進めることができます。