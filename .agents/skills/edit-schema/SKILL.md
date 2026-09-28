---
name: edit-schema
description: |
  マスターデータのYAMLスキーマを編集するためのガイド。スキーマの追加・変更・削除と、foreignKey追加時のC#バリデーション追加を扱う。
  Use when:1.VanillaSchemaのymlファイル(blocks.yml,items.yml等)を編集する必要がある時2.新しいブロックタイプやパラメータを追加する
  3.既存スキーマの構造を変更する4.SourceGeneratorのトリガー方法を確認する5.foreignKey(Guid参照)を追加しバリデーションを書く時
---

# Schema Editing Guide

YAMLの書き方（プロパティ・型・設定オプション）は [yaml_spec.md](references/yaml_spec.md) が正本。YAMLを書く前に該当箇所を読むこと。

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
スキーマファイルの追加・削除時は `moorestech_server/Assets/Scripts/Core.Master/csc.rsp` を編集：
```
# 追加時
/additionalfile:Assets/../../VanillaSchema/newSchema.yml

# 削除時は該当行を削除
```

### 3. Trigger SourceGenerator
`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` を変更してコミットする：
```csharp
private const string dummyText = "new-value-here";
```

### 4. Rebuild
uloopでコンパイル。生成コードは `Mooresmaster.Model.*Module` 名前空間に配置される。

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

- **新しいトップ階層スキーマ（VanillaSchema直下のyml）の新設は原則禁止** — 新しい定義は既存のトップ階層スキーマ（blocks.yml, items.yml, buildMenu.yml等）のプロパティとして追加する。既存のどのトップ階層にも意味的に入れられない場合に限り新設を許可する。新設はcsc.rsp・MasterHolder・全modのJSONファイル追加を伴い、レビューで統合先の提示とともに差し戻される（PR1042でblockCategories.ymlがbuildMenu.ymlへ統合された実績）
- **`optional: true` は原則禁止** — 新規フィールドは必須とし、`default` をYAMLに定義した上で全JSON（下記「JSONデータ配置先」）へ値を追記するのが正規手順。optionalが正当なのは「存在しないことに意味がある」フィールド（コネクタ形状の `directions`/`shapeGuid` 等）のみで、数値パラメータのoptional化はほぼ常に誤り。「既存JSONを壊さないため」は理由にならない（後方互換は考慮不要・AGENTS.md）。optionalにすると読み取り側に `?? Default` フォールバックが増殖し、レビューで必須化+全JSON更新に差し戻される（PR978で44箇所修正の実績）
- C#側に `Default*` 定数や `?? Default` フォールバックを書いてマスタ欠損を吸収しない（欠損はスキーマとJSONで解決する）
- 手動で `Mooresmaster.Model.*` クラスを作成しない

## プロパティのリネーム・削除時のJSONデータ更新

スキーマのプロパティ名を変更・削除した場合はすべてのJSONデータを更新する。漏れるとCIで `MooresmasterLoaderException` が出る。

**JSONデータ配置先：**
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/`
- `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/`
- `../moorestech_master/` 配下全体
- `mooresmaster/mooresmaster.SandBox/`

```bash
grep -r '"旧プロパティ名"' --include='*.json' . ../moorestech_master/
```

## プロパティ追加時の生成コンストラクタ破壊

プロパティを追加すると（`optional: true` でも）、SourceGenerator が生成する要素クラス（例 `BlockMasterElement`）の**コンストラクタに必須の末尾引数が1つ増える**。手書きで `new XxxMasterElement(...)` している箇所（主にテスト）は CS7036 になるので、末尾に引数を足す。JSONローダー経由のロードは影響を受けない。

```bash
grep -rn 'new <要素クラス名>(' --include='*.cs' moorestech_server moorestech_client | grep -v '/obj/'
```
生成順は `PropertyTable` 順なので、**末尾プロパティとして足す**と既存の引数順が崩れず差分が最小になる。

CIはクライアントプロジェクトからEditModeテストを実行する。スキーマ変更の影響テストはクライアントのproject-pathで回す。

## foreignKey追加時のC#バリデーション

SourceGeneratorは `foreignKey` からバリデーションを**自動生成しない**。手動で足さないと、存在しないGuidが実行時に `InvalidOperationException` を起こす。

- 置き場: `moorestech_server/Assets/Scripts/Core.Master/Validator/` のスキーマ別 `*MasterUtil.cs`（blocks → `BlockMasterUtil.cs`、BlockParam系は `Validator/Block/` にも分割あり）。追加先は同じ参照先を検証している既存行を grep して決める
- 参照先ごとの書き方（既存パターン）:

```csharp
// items / fluids / blocks は Master の IdOrNull で引く
// Resolve items / fluids / blocks via the master's IdOrNull
var id = MasterHolder.ItemMaster.GetItemIdOrNull(element.ItemGuid); // GetFluidIdOrNull / GetBlockIdOrNull
if (id == null)
{
    logs += $"[{MasterName}] Name:{name} has invalid ItemGuid:{element.ItemGuid}\n";
}

// mapObjects は要素を引く
// Resolve mapObjects by element lookup
var mapObjectElement = MasterHolder.MapObjectMaster.GetMapObjectElementOrNull(element.MapObjectGuid);

// IdOrNull を持たない参照先（research・challenge・同一スキーマ内参照）はローカル関数で存在確認
// Targets without IdOrNull (research, challenge, same-schema refs) use a local existence check
bool ExistsResearchGuid(Guid researchGuid) => Array.Exists(research.Data, r => r.ResearchNodeGuid == researchGuid);
```

見落としやすい箇所: `ref:` 先のスキーマ内の foreignKey（例 `generateFluids` の `fluidGuid`）、`switch/cases` の各case、配列要素内の foreignKey（`foreach` で回す）。optional な Guid は未設定時（`Guid.Empty` / `HasValue` 無し）をスキップする。

## SourceGenerator Troubleshooting

`Mooresmaster` 名前空間が見つからない等、生成されていないことによるコンパイルエラーが出たら、まず csc.rsp の登録漏れとYAMLの書き方を疑う（SourceGeneratorは worktree・CIでも動く）。YAML全体を [yaml_spec.md](references/yaml_spec.md) と照らして直す。
