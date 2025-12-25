---
name: edit-schema
description: |
  マスターデータのYAMLスキーマを編集するためのガイド。スキーマの追加・変更・削除を行う際に使用する。
  Use when: (1) VanillaSchemaのYAMLファイルを編集する (2) 新しいブロックタイプやパラメータを追加する
  (3) 既存スキーマの構造を変更する (4) SourceGeneratorのトリガー方法を確認する
---

# Schema Editing Guide

## Directory Structure

```
VanillaSchema/
├── blocks.yml, items.yml, fluids.yml ...  # メインスキーマ
└── ref/                                    # 再利用可能なスキーマ部品
    ├── inventoryConnects.yml
    ├── gearConnects.yml
    └── ...
```

## Editing Procedure

### 1. Edit Schema YAML
`VanillaSchema/` 配下の該当YAMLファイルを編集。

### 2. Update csc.rsp (Add/Delete Schema)
スキーマの追加・削除時に `moorestech_server/Assets/Scripts/Core.Master/csc.rsp` を編集：
```
# 追加時
/additionalfile:Assets/../../VanillaSchema/newSchema.yml

# 削除時は該当行を削除
```

### 3. Trigger SourceGenerator
`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` を変更：
```csharp
private const string dummyText = "new-value-here";
```

### 4. Rebuild
MCPまたはUnityでリビルド。生成コードは `Mooresmaster.Model.*Module` 名前空間に配置される。

## Key Patterns

### ref (Reusable Schema)
```yaml
- key: inventoryConnectors
  ref: inventoryConnects  # VanillaSchema/ref/inventoryConnects.yml を参照
```

### switch/cases (Conditional Properties)
```yaml
- key: blockParam
  switch: ./blockType
  cases:
  - when: Chest
    type: object
    properties:
    - key: itemSlotCount
      type: integer
```

### defineInterface (Shared Properties)
```yaml
defineInterface:
- interfaceName: IChestParam
  properties:
  - key: itemSlotCount
    type: integer

# 使用時
implementationInterface:
- IChestParam
```

### foreignKey (Reference to Other Schema)
```yaml
- key: itemGuid
  type: uuid
  foreignKey:
    schemaId: items
    foreignKeyIdPath: /data/[*]/itemGuid
    displayElementPath: /data/[*]/name
```

## Important Rules

- `optional: true` は本当に必要な場合のみ使用
- 手動で `Mooresmaster.Model.*` クラスを作成しない
- スキーマ変更後は必ず `_CompileRequester.cs` を更新してコミット

## Reference

**MUST**: IF もし今から実行しようとしているタスクがYAMLを編集する必要がある場合 THEN 必ず [yaml_spec.md](references/yaml_spec.md) を確認してください。利用可能なプロパティ、型、設定オプションの完全なリファレンスが記載されています。
