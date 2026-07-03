---
name: edit-schema
description: |
  マスターデータのYAMLスキーマを編集するためのガイド。スキーマの追加・変更・削除を行う際に使用する。
  Use when:1.VanillaSchemaのymlファイル(blocks.yml,items.yml等)を編集する必要がある時2.新しいブロックタイプやパラメータを追加する
  3.既存スキーマの構造を変更する4.SourceGeneratorのトリガー方法を確認する
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

## プロパティのリネーム・削除時のJSONデータ更新（CRITICAL）

スキーマのプロパティ名を変更・削除した場合、**すべてのJSONデータを漏れなく更新すること**。
更新漏れがあるとCIでMooresmasterLoaderExceptionが発生する。

**更新対象のJSONデータ配置先（すべて更新すること）：**
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/`
- `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/`
- `../moorestech_master/` 配下全体
- `mooresmaster/mooresmaster.SandBox/`

**必ずgrepで旧プロパティ名の残存がないことを確認する：**
```bash
grep -r '"旧プロパティ名"' --include='*.json' . ../moorestech_master/ | grep -v '.claude/worktrees'
```

## プロパティ追加時の生成コンストラクタ破壊（CRITICAL）

`optional: true` を付けたプロパティを追加しても、SourceGenerator が生成する要素クラス（例 `BlockMasterElement`）の**コンストラクタには必須の末尾引数が1つ増える**（C# のデフォルト値は付かない）。そのため、手書きで `new XxxMasterElement(...)` している箇所（主に**テスト**）は全て CS7036（`There is no argument given ...`）でコンパイルエラーになる。JSON ローダー経由のロードは影響を受けないが、手動構築箇所は必ず引数追加が必要。

**プロパティ追加後、必ず手動構築箇所を洗って末尾に引数を足す：**
```bash
grep -rn 'new <要素クラス名>(' --include='*.cs' moorestech_server moorestech_client | grep -v '/obj/'
```
optional なら末尾に `null`（または既定値相当）を渡す。追加位置はスキーマ上どこでも生成順は `PropertyTable` 順なので、原則**末尾プロパティとして足す**と既存の引数順が崩れず差分が最小になる。

## スキーマ変更後の最終検証（CRITICAL）

スキーマ変更に伴うすべてのタスク（コード修正・JSON更新・テスト修正）が完了したら、**クライアントプロジェクトの全テストを実行すること**。CIはクライアントプロジェクトからEditModeテストを実行するため、サーバー側テストだけでは検証が不十分。

## Validation for foreignKey (CRITICAL)

**MUST**: foreignKeyを持つプロパティを追加した場合、**必ず `/validate-schema` スキルを実行**してC#バリデーションを追加すること。

SourceGeneratorはforeignKeyからバリデーションコードを自動生成しない。手動追加を怠ると実行時エラー（InvalidOperationException）の原因となる。

## SourceGenerator Troubleshooting

SourceGeneratorはどのような環境（git worktree、root repo、CI/CD）でも動作します。

もしSourceGeneratorでコードが生成されていないことによるコンパイルエラー（例：`The type or namespace name 'Mooresmaster' could not be found (are you missing a using directive or an assembly reference?)` 等）が発生した場合、**100%スキーマの書き方に問題があります**。

このような時は：
1. YAMLファイル全体を見直して不具合がないかチェック
2. [yaml_spec.md](references/yaml_spec.md) でYAMLの書き方の仕様を確認
3. コンパイルエラーが解消するまで修正を続ける

## Reference

**MUST**: IF もし今から実行しようとしているタスクがYAMLを編集する必要がある場合 THEN 必ず [yaml_spec.md](references/yaml_spec.md) を確認してください。利用可能なプロパティ、型、設定オプションの完全なリファレンスが記載されています。
