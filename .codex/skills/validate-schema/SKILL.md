---
name: validate-schema
description: |
  スキーマ編集後のバリデーション実装チェックスキル。foreignKeyを持つプロパティを追加した際にC#バリデーションの追加漏れを防ぐ。
  Use when:
  1. VanillaSchemaのYAMLファイルにforeignKeyを持つプロパティを追加した後
  2. BlockParamや他のマスターデータにGuid参照を追加した後
  3. スキーマ編集の完了確認時
  4. 「バリデーションチェック」「validate-schema」と言われた時
context: fork
---

# Schema Validation Check Guide

## Why This Matters

SourceGeneratorは`foreignKey`定義からバリデーションコードを**自動生成しない**。
手動でC#バリデーションを追加しないと、存在しないGuidが実行時に`InvalidOperationException`を引き起こす。

## Schema to Validator Mapping

| スキーマファイル | Validatorファイル |
|-----------------|------------------|
| blocks.yml | `BlockMasterUtil.cs` |
| items.yml | `ItemMasterUtil.cs` |
| fluids.yml | `FluidMasterUtil.cs` |
| craftRecipes.yml | `CraftRecipeMasterUtil.cs` |
| machineRecipes.yml | `MachineRecipesMasterUtil.cs` |
| research.yml | `ResearchMasterUtil.cs` |
| challenges.yml | `ChallengeMasterUtil.cs` |
| mapObjects.yml | `MapObjectMasterUtil.cs` |
| placeSystem.yml | `PlaceSystemMasterUtil.cs` |
| train.yml | `TrainUnitMasterUtil.cs` |
| characters.yml | `CharacterMasterUtil.cs` |

**Validatorパス**: `moorestech_server/Assets/Scripts/Core.Master/Validator/`

## foreignKey Types and Validation Methods

### ItemGuid → items
```csharp
var id = MasterHolder.ItemMaster.GetItemIdOrNull(element.ItemGuid);
if (id == null)
{
    logs += $"[{MasterName}] Name:{name} has invalid ItemGuid:{element.ItemGuid}\n";
}
```

### FluidGuid → fluids
```csharp
var id = MasterHolder.FluidMaster.GetFluidIdOrNull(element.FluidGuid);
if (id == null)
{
    logs += $"[{MasterName}] Name:{name} has invalid FluidGuid:{element.FluidGuid}\n";
}
```

### BlockGuid → blocks
```csharp
var id = MasterHolder.BlockMaster.GetBlockIdOrNull(element.BlockGuid);
if (id == null)
{
    logs += $"[{MasterName}] Name:{name} has invalid BlockGuid:{element.BlockGuid}\n";
}
```

### 同一スキーマ内参照 (blocks → blocks等)
```csharp
// ヘルパー関数を定義
bool ExistsBlockGuid(Guid blockGuid)
{
    return Array.Exists(blocks.Data, b => b.BlockGuid == blockGuid);
}

// バリデーション
if (!ExistsBlockGuid(element.TargetBlockGuid))
{
    logs += $"[{MasterName}] Name:{name} has invalid TargetBlockGuid:{element.TargetBlockGuid}\n";
}
```

### MapObjectGuid → mapObjects
```csharp
var id = MasterHolder.MapObjectMaster.GetMapObjectIdOrNull(element.MapObjectGuid);
if (id == null)
{
    logs += $"[{MasterName}] Name:{name} has invalid MapObjectGuid:{element.MapObjectGuid}\n";
}
```

### ResearchGuid → research
```csharp
var id = MasterHolder.ResearchMaster.GetResearchIdOrNull(element.ResearchGuid);
if (id == null)
{
    logs += $"[{MasterName}] Name:{name} has invalid ResearchGuid:{element.ResearchGuid}\n";
}
```

## Common Mistakes to Avoid

### 1. ref経由のforeignKey見落とし
```yaml
# blocks.yml
- key: generateFluid
  ref: generateFluids  # ← ref先にforeignKeyがある！
```
`generateFluids.yml`内の`fluidGuid`がforeignKeyを持つ。
refを使う場合は**ref先のスキーマも確認**すること。

### 2. switch/cases内のforeignKey見落とし
```yaml
cases:
- when: GearPump
  properties:
  - key: generateFluid
    ref: generateFluids  # ← cases内も要確認
```
各caseのBlockParamごとにバリデーションが必要。

### 3. 配列内要素のforeignKey見落とし
```yaml
- key: requiredItems
  type: array
  items:
    type: object
    properties:
    - key: itemGuid
      foreignKey: ...  # ← 配列要素内のforeignKey
```
`foreach`でループしてバリデーションする。

### 4. optional: true のforeignKey
```yaml
- key: upgradeBlockGuid
  type: uuid
  optional: true
  foreignKey: ...
```
空のGuid（`Guid.Empty`）をスキップする処理が必要：
```csharp
if (element.UpgradeBlockGuid != Guid.Empty)
{
    // バリデーション
}
```

### 5. Nullable型のforeignKey
```yaml
- key: upBlockGuid
  type: uuid
  optional: true
```
`HasValue`チェックが必要：
```csharp
if (element.UpBlockGuid.HasValue && element.UpBlockGuid.Value != Guid.Empty)
{
    // バリデーション
}
```

## Validation Implementation Procedure

### Step 1: Identify foreignKeys
編集したYAMLファイルで`foreignKey:`を検索し、追加したforeignKeyを特定する。
ref先のスキーマも確認すること。

### Step 2: Find Target Validator
対応するValidatorファイルを開く（上記マッピング表参照）。

### Step 3: Locate Validation Method
`Validate`メソッド内の適切な場所を見つける。
BlockParamの場合は`BlockParamValidation()`内。

### Step 4: Add Validation Code
既存パターンに従ってバリデーションコードを追加。
コメントは日本語・英語の2行セットで記述。

### Step 5: Compile and Test
コンパイルを実行し、エラーがないことを確認。

## Checklist

スキーマ編集完了時に以下を確認：

- [ ] 追加したforeignKeyをすべてリストアップしたか？
- [ ] ref経由のforeignKeyを見落としていないか？
- [ ] switch/cases内のforeignKeyを見落としていないか？
- [ ] 配列内要素のforeignKeyを見落としていないか？
- [ ] 対応するValidatorにバリデーションコードを追加したか？
- [ ] optional/nullable型の場合、Guid.Empty/HasValueチェックを追加したか？
- [ ] コンパイルが通ることを確認したか？

## Quick Validation Audit

既存スキーマのバリデーション漏れを確認するコマンド：

```bash
# スキーマ内のforeignKey数をカウント
grep -r "foreignKey:" VanillaSchema/ | wc -l

# Validator内のバリデーション呼び出し数をカウント
grep -rE "GetItemIdOrNull|GetFluidIdOrNull|GetBlockIdOrNull|GetMapObjectIdOrNull|ExistsBlockGuid" \
  moorestech_server/Assets/Scripts/Core.Master/Validator/ | wc -l
```

数が大きく乖離している場合、バリデーション漏れの可能性がある。
