# blockConnectInfo.yml廃止とインターフェースベーススキーマへの移行計画

## 概要

現在の`blockConnectInfo.yml`は、`connectType`（Inventory/Gear/Fluid）を手動選択する必要がありミスが起こりやすい。これを廃止し、型安全な別々のスキーマとして分離する。

## スコープ

- [x] スキーマ変更（YAMLファイル）
- [x] C#コード修正
- [x] JSONマスターデータ移行

---

## Phase 1: スキーマ変更

### 1.1 blocks.ymlにインターフェース追加

**ファイル**: `VanillaSchema/blocks.yml`

`defineInterface`セクションに以下を追加:

```yaml
- interfaceName: IBlockConnectOption
  properties: []

- interfaceName: IBlockConnector
  properties:
  - key: offset
    type: vector3Int
    default: [0, 0, 0]
  - key: directions
    type: array
    optional: true
    items:
      type: vector3Int
  - key: option
    type: object
    properties: []
    implementationInterface:
      - IBlockConnectOption
```

### 1.2 inventoryConnects.yml書き換え

**ファイル**: `VanillaSchema/ref/inventoryConnects.yml`

変更前: `ref: blockConnectInfo` + `fixedParameter`
変更後: 直接定義 + `implementationInterface: [IBlockConnector]`

```yaml
id: inventoryConnects
type: object
properties:
- key: inputConnects
  type: array
  optional: true
  items:
    type: object
    implementationInterface:
    - IBlockConnector
    properties:
    - key: offset
      type: vector3Int
      default: [0, 0, 0]
    - key: directions
      type: array
      optional: true
      items:
        type: vector3Int
    - key: option
      type: object
      properties: []
- key: outputConnects
  type: array
  optional: true
  items:
    type: object
    implementationInterface:
    - IBlockConnector
    properties:
    - key: offset
      type: vector3Int
      default: [0, 0, 0]
    - key: directions
      type: array
      optional: true
      items:
        type: vector3Int
    - key: option
      type: object
      implementationInterface:
      - IBlockConnectOption
      properties: []
```

### 1.3 gearConnects.yml書き換え

**ファイル**: `VanillaSchema/ref/gearConnects.yml`

```yaml
id: gear
type: object
properties:
- key: gearConnects
  type: array
  items:
    type: object
    implementationInterface:
    - IBlockConnector
    properties:
    - key: offset
      type: vector3Int
      default: [0, 0, 0]
    - key: directions
      type: array
      optional: true
      items:
        type: vector3Int
    - key: option
      type: object
      implementationInterface:
      - IBlockConnectOption
      properties:
      - key: isReverse
        type: boolean
        default: true
```

### 1.4 fluidInventoryConnects.yml書き換え

**ファイル**: `VanillaSchema/ref/fluidInventoryConnects.yml`

```yaml
id: fluidInventoryConnects
type: object
properties:
- key: inflowConnects
  type: array
  optional: true
  items:
    type: object
    implementationInterface:
    - IBlockConnector
    properties:
    - key: offset
      type: vector3Int
      default: [0, 0, 0]
    - key: directions
      type: array
      optional: true
      items:
        type: vector3Int
    - key: option
      type: object
      implementationInterface:
      - IBlockConnectOption
      properties:
      - key: flowCapacity
        type: number
        default: 10
      - key: connectTankIndex
        type: integer
        default: 0
- key: outflowConnects
  type: array
  optional: true
  items:
    type: object
    implementationInterface:
    - IBlockConnector
    properties:
    - key: offset
      type: vector3Int
      default: [0, 0, 0]
    - key: directions
      type: array
      optional: true
      items:
        type: vector3Int
    - key: option
      type: object
      implementationInterface:
      - IBlockConnectOption
      properties:
      - key: flowCapacity
        type: number
        default: 10
      - key: connectTankIndex
        type: integer
        default: 0
```

### 1.5 blockConnectInfo.yml削除

**ファイル**: `VanillaSchema/ref/blockConnectInfo.yml` → 削除

### 1.6 SourceGenerator再生成トリガー

**ファイル**: `moorestech_server/Assets/Scripts/Core.Master/Dummy.cs`

`dummyText`の値を変更してSourceGeneratorを再実行

---

## Phase 2: C#コード修正

コンパイル後、生成された型に合わせてC#コードを修正。

### 主な修正対象ファイル

| ファイル | 修正内容 |
|---------|---------|
| `Game.Block/Component/BlockConnectorConnectPositionCalculator.cs` | 新しい型・プロパティ名に対応 |
| `Game.Block.Interface/Component/IBlockConnectorComponent.cs` | IConnectOption → IBlockConnectOption（必要に応じて） |
| `Server.Boot/BlockFactory/BlockTemplate/BlockTemplateUtil.cs` | InventoryConnects型の変更対応 |
| `Server.Boot/BlockFactory/BlockTemplate/VanillaGearTemplate.cs` | Gear型の変更対応 |
| `Server.Boot/BlockFactory/BlockTemplate/VanillaFluidBlockTemplate.cs` | FluidInventoryConnects型の変更対応 |
| `Game.Block/Blocks/Power/GearEnergyTransformerComponent.cs` | GearConnectOption → 新しいOption型 |
| `Game.Block/Blocks/Pipe/FluidPipeComponent.cs` | FluidConnectOption → 新しいOption型 |
| `Game.Block/Blocks/Pipe/PumpFluidOutputComponent.cs` | FluidConnectOption → 新しいOption型 |

### 主な変更パターン

1. **プロパティ名変更**: `ConnectOption` → `Option`
2. **型名変更**: `BlockConnectInfoElement` → 各コネクタ固有の型
3. **キャスト修正**: `as GearConnectOption` → 新しいOption型へのキャスト

---

## Phase 3: JSONマスターデータ移行

### 変更内容

| 変更前 | 変更後 |
|--------|--------|
| `connectType: "Inventory"` | 削除 |
| `connectType: "Gear"` | 削除 |
| `connectType: "Fluid"` | 削除 |
| `connectOption: {...}` | `option: {...}` |

### 対象ファイル

1. `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
2. `moorestech_client/Assets/Scripts/Client.Tests/PlayModeTest/ServerData/mods/PlayModeTestMod/master/blocks.json`
3. 本番用マスターデータ（mooreseditor管理、必要に応じて）

### JSON変更例

**変更前:**
```json
{
  "offset": [0, 0, 0],
  "connectType": "Gear",
  "directions": [[1, 0, 0]],
  "connectOption": {"isReverse": true}
}
```

**変更後:**
```json
{
  "offset": [0, 0, 0],
  "directions": [[1, 0, 0]],
  "option": {"isReverse": true}
}
```

---

## Phase 4: テスト

1. コンパイル確認（moorestech_server + moorestech_client）
2. 関連ユニットテスト実行
3. 接続系の統合テスト確認

---

## 実装順序

1. スキーマYAML変更（blocks.yml → inventoryConnects.yml → gearConnects.yml → fluidInventoryConnects.yml）
2. blockConnectInfo.yml削除
3. Dummy.cs更新 → コンパイル
4. コンパイルエラー確認・C#コード修正
5. JSONマスターデータ移行
6. テスト実行

---

## クリティカルファイル一覧

### スキーマ
- `VanillaSchema/blocks.yml`
- `VanillaSchema/ref/inventoryConnects.yml`
- `VanillaSchema/ref/gearConnects.yml`
- `VanillaSchema/ref/fluidInventoryConnects.yml`
- `VanillaSchema/ref/blockConnectInfo.yml`（削除対象）

### C#
- `moorestech_server/Assets/Scripts/Game.Block/Component/BlockConnectorConnectPositionCalculator.cs`
- `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/IBlockConnectorComponent.cs`
- `moorestech_server/Assets/Scripts/Server.Boot/BlockFactory/BlockTemplate/BlockTemplateUtil.cs`
- `moorestech_server/Assets/Scripts/Server.Boot/BlockFactory/BlockTemplate/VanillaGearTemplate.cs`
- `moorestech_server/Assets/Scripts/Server.Boot/BlockFactory/BlockTemplate/VanillaFluidBlockTemplate.cs`
- `moorestech_server/Assets/Scripts/Core.Master/Dummy.cs`

### JSON
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- `moorestech_client/Assets/Scripts/Client.Tests/PlayModeTest/ServerData/mods/PlayModeTestMod/master/blocks.json`
