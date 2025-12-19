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
- 接続判定で`contactShape`の一致をチェック

---

## 変更対象ファイル

| ファイル | 変更内容 |
|---------|---------|
| `VanillaSchema/blocks.yml` | gearContactShapesマスター定義を追加 |
| `VanillaSchema/ref/blockConnectInfo.yml` | GearのconnectOptionにcontactShapeを追加 |
| `moorestech_server/.../BlockConnectorComponent.cs` | 接続判定にcontactShapeチェックを追加 |
| `moorestech_server/.../Core.Master/Dummy.cs` | SourceGenerator再生成トリガー |
| `mods/vanilla/master/blocks.json` | gearContactShapes実データ追加 |
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

`moorestech_server/Assets/Scripts/Core.Master/Dummy.cs` の `dummyText` を変更してリビルド。

これにより `GearConnectOption` クラスに `ContactShape` プロパティが自動生成される。

---

### Step 3: 接続判定ロジック修正

**ファイル**: `moorestech_server/Assets/Scripts/Game.Block/Component/BlockConnectorComponent.cs`

`OnPlaceBlock` メソッドの接続判定に contactShape チェックを追加：

```csharp
// 既存の位置チェック後、オプションチェックを追加
if (isConnect && selfOption is GearConnectOption selfGear && targetOption is GearConnectOption targetGear)
{
    // contactShapeが異なれば接続しない
    // contactShapeが同じものだけが接続できる
    if (selfGear.ContactShape != targetGear.ContactShape)
    {
        isConnect = false;
    }
}
```

**変更箇所**: 84～91行付近（位置マッチング成功後）

---

### Step 4: マスターデータ追加

#### 4.1 本番用 blocks.json

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

#### 4.2 テスト用 blocks.json

`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` に同様の形状定義を追加。

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

---

## 関連ファイルパス

```
VanillaSchema/blocks.yml
VanillaSchema/ref/blockConnectInfo.yml
moorestech_server/Assets/Scripts/Game.Block/Component/BlockConnectorComponent.cs
moorestech_server/Assets/Scripts/Core.Master/Dummy.cs
moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json
moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/GearNetworkTest.cs
```
