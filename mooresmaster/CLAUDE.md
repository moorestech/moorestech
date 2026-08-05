# CLAUDE.md

## Schema Format

Schemas are YAML files defining data structures. Key features:

- **Types**: string, integer, number, boolean, uuid, vector2/3/4, vector2Int/3Int, object, array
- **`defineInterface`**: Creates polymorphic interfaces for type switching
- **`switch`**: Path-based conditional type selection
- **`index`**: Array indexing attribute for generated lookup methods

## Git Subtree

The `mooresmaster.SandBox/schema/` directory is a git subtree from VanillaSchema:

```bash
# Pull updates from VanillaSchema
git subtree pull --prefix=mooresmaster.SandBox/schema schema main

# Push changes to VanillaSchema
git subtree push --prefix=mooresmaster.SandBox/schema schema main
```

## Development Workflow

After making any code changes, always run the build to verify the changes compile successfully:

```bash
dotnet build mooresmaster.Generator/ -c release && dotnet test
```

## Technical Notes

- YamlDotNet is embedded in the source to avoid dependency conflicts with consuming projects
- Generator targets netstandard2.0 for broad compatibility
- Errors are reported as compiler diagnostics via Roslyn's diagnostic API

## Design Patterns

### Diagnostics Design

Diagnosticsは文字列でreasonを保持せず、理由ごとに型を分けて構造化データを保持する:

```csharp
// Bad: 文字列でreason保持
public class SomeDiagnostics : IDiagnostics
{
    private readonly string _reason;  // "Property 'xxx' not found..."
}

// Good: 理由ごとに型を分け、構造化データを保持
public class PropertyNotFoundDiagnostics : IDiagnostics
{
    public string PropertyName { get; }
    public string[] AvailableProperties { get; }
}
```

理由:
- テスト時に型でマッチングでき、プロパティで検証できる
- エラーメッセージの変更が検証ロジックに影響しない

#### Diagnosticsには実際の型を保持する

Diagnosticsには文字列リテラルだけでなく、元のオブジェクト（JsonNode、SchemaIdなど）も保持する:

```csharp
// Bad: 文字列のみ保持
public class ArrayItemsNotFoundDiagnostics : IDiagnostics
{
    public string? ArrayPropertyName { get; }
    public Location Location { get; }
}

// Good: 元のオブジェクトも保持
public class ArrayItemsNotFoundDiagnostics : IDiagnostics
{
    public JsonObject ArrayJson { get; }        // パース対象の生データ
    public SchemaId ArraySchemaId { get; }      // 生成されたSchemaのID
    public string? PropertyName { get; }        // 便利用に文字列も保持
    public Location Location { get; }
}
```

理由:
- 後続の処理で元データにアクセスできる
- より詳細なエラー情報を動的に生成できる
- テスト時に元データの状態も検証できる

### Analyzer Test Pattern

テストでは`Assert.IsType<T>`で型チェック＋キャストを行い、プロパティで検証する:

```csharp
// Bad: 文字列マッチング
Assert.Equal(typeof(SomeDiagnostics), diagnosticsArray[0].GetType());
Assert.Contains("propertyName", diagnosticsArray[0].Message);

// Good: 型チェック＋プロパティ検証
var diagnostics = Assert.IsType<PropertyNotFoundDiagnostics>(diagnosticsArray[0]);
Assert.Equal("propertyName", diagnostics.PropertyName);
Assert.Contains("availableProp", diagnostics.AvailableProperties);
```

### Location Information

エラー報告にはLocation（行・列）が必要。パース時にLocation情報を保持するよう設計する:

```csharp
// Schema定義にLocationを追加
public record SwitchSchema(..., Location SwitchPathLocation) : ISchema

// パース時にJsonStringからLocationを取得
var jsonString = (json[key] as JsonString)!;
new SomeSchema(..., jsonString.Location)
```

### Shared SchemaTable Validation

複数スキーマファイルが同一のSchemaTableを共有する場合、各スキーマがどのルートに属するかを追跡する:

```csharp
// ルートSchemaIdのセットを作成
var rootSchemaIds = new HashSet<SchemaId>();
foreach (var schemaFile in schemaFiles)
    rootSchemaIds.Add(schemaFile.Schema.InnerSchema);

// 各Schemaのルートを親を辿って特定
```

### .NET Standard 2.0 Compatibility

Source Generatorはnetstandard2.0をターゲットにするため、新しいC#機能に注意:

```csharp
// NG: KeyValuePairのDeconstruct
foreach (var (key, value) in dictionary)

// OK
foreach (var kvp in dictionary)
{
    var key = kvp.Key;
    var value = kvp.Value;
}
```
