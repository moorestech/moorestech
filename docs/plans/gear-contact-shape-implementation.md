# 歯車コネクター接触形状（contactShape）追加 実装計画書

## 概要

歯車コネクターに「接触形状（contactShape）」の概念を追加し、同じ形状同士のみ接続可能にする。形状はマスターデータで定義し、コード変更なしで新形状を追加可能にする。

## 背景・課題

**現状の問題**：
- 歯車接続は座標の一致のみで判定
- 歯車とシャフトなど、異なる形状のコネクター同士でも接続されてしまう

**解決策**：
- コネクターに`contactShape`プロパティを追加
- `blocks.yml`内に形状マスターを定義し、foreignKey参照でタイプミスを防止
- `IBlockConnectorContext`による抽象化で接続判定を拡張可能に

---

## 変更対象ファイル

| ファイル | 変更内容 |
|---------|---------|
| `VanillaSchema/blocks.yml` | gearContactShapesマスター定義を追加 |
| `VanillaSchema/ref/blockConnectInfo.yml` | GearのconnectOptionにcontactShapeを追加 |
| `moorestech_server/.../BlockConnectorComponent.cs` | IBlockConnectorContextを使用した接続判定を追加 |
| `moorestech_server/.../DefaultBlockConnectorContext.cs` | **新規** Null Objectパターンのデフォルト実装 |
| `moorestech_server/.../GearBlockConnectorContext.cs` | **新規** Gear用のcontactShapeチェック実装 |
| `moorestech_server/.../Core.Master/_CompileRequester.cs` | SourceGenerator再生成トリガー |
| テスト用blocks.json | テストデータ更新 |
| 新規テストファイル | contactShape接続テスト |

---

## 実装ステップ

### Step 1: スキーマ変更

#### 1.1 blocks.yml に形状マスターを追加（一番下に配置）

```yaml
# blocks.yml の properties セクションの一番下に追加
properties:
- key: data
  # ... 既存のdata定義

- key: gearContactShapes    # 一番下に追加
  type: array
  items:
    type: object
    properties:
    - key: shapeId
      type: string
      primaryKey: true
    - key: displayName
      type: string
      optional: true
```

#### 1.2 blockConnectInfo.yml のGear部分を更新

```yaml
- when: Gear
  type: object
  properties:
  - key: isReverse
    type: boolean
    default: true
  - key: contactShape
    type: string
    default: "tooth"
    foreignKey:
      file: blocks.yml
      foreignKeyIdPath: /gearContactShapes/[*]/shapeId
      displayElementPath: /gearContactShapes/[*]/displayName
```

---

### Step 2: SourceGenerator再生成

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` を変更してリビルド。

これにより `GearConnectOption` クラスに `ContactShape` プロパティが自動生成される。

---

### Step 3: 接続判定ロジック修正（IBlockConnectorContext + Null Objectパターン）

#### 3.1 DefaultBlockConnectorContext.cs（新規作成）

**ファイル**: `moorestech_server/Assets/Scripts/Game.Block/Component/DefaultBlockConnectorContext.cs`

```csharp
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Component
{
    // Null Objectパターン: 常に接続を許可するデフォルト実装
    // Null Object pattern: Default implementation that always allows connection
    public class DefaultBlockConnectorContext : IBlockConnectorContext
    {
        public static readonly DefaultBlockConnectorContext Instance = new();

        public bool IsConnect(BlockConnectInfoElement self, BlockConnectInfoElement connect)
        {
            return true;
        }
    }
}
```

#### 3.2 GearBlockConnectorContext.cs（新規作成）

**ファイル**: `moorestech_server/Assets/Scripts/Game.Block/Component/GearBlockConnectorContext.cs`

```csharp
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Block.Component
{
    // Gear用の接続判定: contactShapeが一致する場合のみ接続可能
    // Gear connection context: Only allows connection when contactShape matches
    public class GearBlockConnectorContext : IBlockConnectorContext
    {
        public static readonly GearBlockConnectorContext Instance = new();

        public bool IsConnect(BlockConnectInfoElement self, BlockConnectInfoElement connect)
        {
            // どちらかがnullの場合は接続を許可（後方互換性）
            // Allow connection if either is null (backward compatibility)
            if (self?.ConnectOption is not GearConnectOption selfGear) return true;
            if (connect?.ConnectOption is not GearConnectOption targetGear) return true;

            // contactShapeが一致する場合のみ接続可能
            // Only connect when contactShape matches
            return selfGear.ContactShape == targetGear.ContactShape;
        }
    }
}
```

#### 3.3 BlockConnectorComponent.cs の修正

**ファイル**: `moorestech_server/Assets/Scripts/Game.Block/Component/BlockConnectorComponent.cs`

```csharp
[DisallowMultiple]
public class BlockConnectorComponent<TTarget> : IBlockConnectorComponent<TTarget> where TTarget : IBlockComponent
{
    // コンテキストによる追加接続判定
    // Additional connection check via context
    private readonly IBlockConnectorContext _context;

    // ... 既存のフィールド ...

    public BlockConnectorComponent(
        BlockConnectInfo inputConnectInfo,
        BlockConnectInfo outputConnectInfo,
        BlockPositionInfo blockPositionInfo,
        IBlockConnectorContext context)
    {
        _context = context;
        // ... 既存の初期化処理 ...
    }

    private void OnPlaceBlock(Vector3Int outputTargetPos)
    {
        // ... 既存の位置判定処理 ...

        if (!isConnect) return;

        // コンテキストによる追加判定
        // Additional check via context
        if (!_context.IsConnect(selfElement, targetElement)) return;

        // 接続元ブロックと接続先ブロックを接続
        // Connect source block to target block
        if (!_connectedTargets.ContainsKey(targetComponent))
        {
            var block = ServerContext.WorldBlockDatastore.GetBlock(outputTargetPos);
            var connectedInfo = new ConnectedInfo(selfElement, targetElement, block);
            _connectedTargets.Add(targetComponent, connectedInfo);
        }
    }

    // ... 既存のメソッド ...
}
```

#### 3.4 各ブロックファクトリでのコンテキスト使用

Gear系ブロックのファクトリでは `GearBlockConnectorContext.Instance` を渡す：

```csharp
// Gear系ブロック
new BlockConnectorComponent<IGearEnergyTransformer>(
    inputConnectInfo,
    outputConnectInfo,
    blockPositionInfo,
    GearBlockConnectorContext.Instance);

// Inventory系ブロック（既存動作を維持）
new BlockConnectorComponent<IInventoryComponent>(
    inputConnectInfo,
    outputConnectInfo,
    blockPositionInfo,
    DefaultBlockConnectorContext.Instance);
```

---

### Step 4: マスターデータ追加

#### 4.1 テスト用 blocks.json

`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` の先頭に追加：

```json
{
  "gearContactShapes": [
    { "shapeId": "tooth", "displayName": "歯車の歯" },
    { "shapeId": "shaft", "displayName": "シャフト" },
    { "shapeId": "chain", "displayName": "チェーン" }
  ],
  "data": [
    // 既存のブロックデータ
  ]
}
```

---

### Step 5: 既存ブロックのcontactShape設定

既存の歯車関連ブロックに適切なcontactShapeを設定：

| ブロックタイプ | contactShape |
|--------------|--------------|
| Gear | tooth |
| Shaft | shaft |
| GearChainPole | chain |
| GearMachine | tooth |
| SimpleGearGenerator | tooth |
| FuelGearGenerator | tooth |
| GearBeltConveyor | shaft |
| GearMiner | tooth |
| GearElectricGenerator | tooth |
| GearPump | tooth |

---

### Step 6: テスト作成

新規テストケース：
1. **同じcontactShape同士の接続テスト** - 接続成功を確認
2. **異なるcontactShape同士の接続テスト** - 接続失敗を確認
3. **既存テストの動作確認** - リグレッションなし

**テストファイル**: `GearNetworkTest.cs` に追加、または新規ファイル作成

---

## 検証項目

- [ ] SourceGenerator再生成後、GearConnectOptionにContactShapeプロパティが存在
- [ ] 同じcontactShape同士は接続可能
- [ ] 異なるcontactShape同士は接続不可
- [ ] 既存のGearNetworkTestが全てパス
- [ ] foreignKeyバリデーションが機能（存在しないshapeIdでエラー）

---

## リスク・注意点

1. **既存データの後方互換性**: `default: "tooth"` により、contactShapeが未指定の既存データは "tooth" として扱われる

2. **テストデータの更新**: テスト用blocks.jsonにもgearContactShapesを追加しないとテストが失敗する

3. **foreignKeyパスの正確性**: `foreignKeyIdPath` のパス形式がSourceGeneratorの期待と一致するか要確認

4. **既存ファクトリの更新**: `BlockConnectorComponent`を生成している全てのファクトリで`IBlockConnectorContext`引数を追加する必要がある

---

## 関連ファイルパス

```
VanillaSchema/blocks.yml
VanillaSchema/ref/blockConnectInfo.yml
moorestech_server/Assets/Scripts/Game.Block/Component/BlockConnectorComponent.cs
moorestech_server/Assets/Scripts/Game.Block/Component/IBlockConnectorContext.cs
moorestech_server/Assets/Scripts/Game.Block/Component/DefaultBlockConnectorContext.cs（新規）
moorestech_server/Assets/Scripts/Game.Block/Component/GearBlockConnectorContext.cs（新規）
moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json
moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/GearNetworkTest.cs
```
