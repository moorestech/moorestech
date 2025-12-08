# Gap Analysis: GearChainPole Connect PlaceSystem

## 1. Current State Investigation

### 1.1 既存アセット・モジュール

#### サーバー側（実装済み）
| ファイル | 役割 |
|---------|------|
| `Server.Protocol/PacketResponse/GearChainConnectionEditProtocol.cs` | 接続・切断プロトコル定義（Request/Response） |
| `Server.Protocol/PacketResponse/Util/GearChain/GearChainSystemUtil.cs` | 接続・切断のビジネスロジック |
| `Game.Block.Interface/Component/IGearChainPole.cs` | GearChainPoleインターフェース |
| `Game.Block/Blocks/GearChainPole/GearChainPoleComponent.cs` | GearChainPoleコンポーネント実装 |

#### クライアント側（未実装・要作成）
| 必要なファイル | 役割 | 状態 |
|---------------|------|------|
| `GearChainConnectPlaceSystem.cs` | PlaceSystem実装 | **Missing** |
| `GearChainConnectPreviewObject.cs` | 接続プレビュー表示 | **Missing** |
| `IGearChainPoleCollider.cs` | レイキャスト検出用インターフェース | **Missing** |
| VanillaApiSendOnly拡張 | API送信メソッド | **Missing** |
| PlaceSystemSelector拡張 | チェーンアイテム判定 | **Missing** |
| VanillaSchema/placeSystem.yml拡張 | PlaceMode追加 | **Missing** |

### 1.2 既存パターン・規約

#### PlaceSystemパターン
```
IPlaceSystem
├── Enable()      - 有効化時の初期化
├── ManualUpdate(context) - 毎フレーム更新
└── Disable()     - 無効化時のクリーンアップ
```

#### 2ステップ接続パターン（TrainRailConnectSystemより）
```csharp
// ステップ1: 接続元を選択
if (_connectFromArea == null && InputManager.Playable.ScreenLeftClick.GetKeyDown)
{
    _connectFromArea = GetComponent();
}

// ステップ2: 接続先を選択し、送信
if (connectToArea != null && InputManager.Playable.ScreenLeftClick.GetKeyDown)
{
    ClientContext.VanillaApi.SendOnly.Connect(...);
    _connectFromArea = null;
}
```

#### PlaceSystemSelector選択ロジック
- `MasterHolder.PlaceSystemMaster.PlaceSystem.Data`からマッチする要素を検索
- `PlaceModeConst`のenumに基づいてシステムを選択
- 優先度（Priority）が高い要素を採用

### 1.3 統合ポイント

| 統合ポイント | 説明 |
|-------------|------|
| **PlaceSystemSelector** | チェーンアイテム保持時にGearChainConnectPlaceSystemを返す |
| **MainGameStarter** | DIコンテナへのPlaceSystem登録 |
| **VanillaApiSendOnly** | `ConnectGearChain` / `DisconnectGearChain`メソッド追加 |
| **VanillaSchema/placeSystem.yml** | `GearChainConnect` PlaceMode追加 |
| **mods/vanilla/master/placeSystem.json** | チェーンアイテムのPlaceSystem設定追加 |
| **Prefab** | プレビューオブジェクトのSerializeField登録 |

---

## 2. Requirements Feasibility Analysis

### 2.1 技術的要件マップ

| 要件 | 技術的要件 | ギャップ |
|------|-----------|---------|
| Req1: 接続操作UI | IPlaceSystem実装、レイキャスト、2ステップ操作 | **Missing**: GearChainConnectPlaceSystem |
| Req2: 視覚的フィードバック | プレビューオブジェクト、LineRenderer/Shader | **Missing**: プレビューシステム |
| Req3: サーバー通信 | VanillaApiSendOnly拡張 | **Missing**: APIメソッド（プロトコルは既存） |
| Req4: 切断操作 | Disconnect処理（サーバー側実装済み） | **Missing**: クライアントUI |
| Req5: PlaceSystem登録 | PlaceSystemSelector、placeSystem.yml | **Missing**: enum追加・選択ロジック |
| Req6: コスト表示 | gearChainItems設定参照、UI表示 | **Missing**: コスト計算ユーティリティ |

### 2.2 不明・要調査項目

| 項目 | 説明 | 対応 |
|------|------|------|
| GearChainPoleブロック検出 | レイキャストでGearChainPoleを特定する方法 | **Research Needed**: クライアント側にIGearChainPoleCollider相当が必要か、BlockGameObjectから特定するか |
| 接続範囲プレビュー | 範囲表示のビジュアル実装（球/円のメッシュか、Gizmoか） | **Research Needed**: 既存のDisplayEnergizedRangeなどの実装参考 |
| 既存接続の可視化 | サーバーからのStateDetailを元にクライアントで接続線を表示 | **Research Needed**: GearChainPoleStateDetailの同期タイミング |

### 2.3 複雑度シグナル

- **操作フロー**: 2ステップ接続（TrainRailConnectと同等）- **中程度**
- **プレビュー表示**: 範囲表示＋ライン表示＋色変化 - **中程度**
- **サーバー連携**: 既存プロトコル活用 - **低**
- **マスターデータ統合**: placeSystem.yml拡張 - **低**

---

## 3. Implementation Approach Options

### Option A: TrainRailConnectSystemの完全模倣

**説明**: TrainRailConnectSystemと同じ構造で専用の新規クラスを作成

**変更箇所**:
- 新規: `GearChainConnect/GearChainConnectPlaceSystem.cs`
- 新規: `GearChainConnect/GearChainConnectPreviewObject.cs`
- 新規: `GearChainConnect/IGearChainPoleConnectCollider.cs`
- 変更: `PlaceSystemSelector.cs` - switch文追加
- 変更: `VanillaApiSendOnly.cs` - 2メソッド追加
- 変更: `MainGameStarter.cs` - DI登録追加
- 変更: `VanillaSchema/placeSystem.yml` - enum追加
- 変更: Prefab - SerializeField追加

**Trade-offs**:
- ✅ 既存パターンに完全準拠、理解しやすい
- ✅ TrainRailConnectSystemをテンプレートに実装可能
- ✅ 単体テスト・変更が容易
- ❌ 新規ファイルが多い（4ファイル）
- ❌ Prefab編集が必要

### Option B: CommonBlockPlaceSystemの拡張

**説明**: 共通ブロック配置システムにモードを追加

**評価**: **非推奨**
- 接続操作はブロック配置と根本的に異なる操作フロー
- CommonBlockPlaceSystemを複雑化させる
- 単一責任原則に違反

### Option C: ハイブリッドアプローチ（段階実装）

**説明**: 最小限の実装から始め、段階的に機能追加

**Phase 1**: 基本接続機能
- GearChainConnectPlaceSystem（最小実装）
- VanillaApiSendOnly拡張
- PlaceSystemSelector拡張

**Phase 2**: プレビュー機能
- プレビューオブジェクト実装
- 範囲表示・色分け

**Phase 3**: 高度な機能
- コスト表示UI
- 既存接続の可視化

**Trade-offs**:
- ✅ 早期に動作確認可能
- ✅ リスク分散
- ❌ 複数回のPR/レビューが必要
- ❌ 一時的に不完全な状態が存在

---

## 4. Implementation Complexity & Risk

### Effort: **M (3-7 days)**

**理由**:
- 既存のTrainRailConnectSystemパターンを踏襲可能
- サーバー側プロトコルは実装済み
- 新規ファイル数は4-5個程度
- プレビュー表示の実装に時間がかかる可能性

### Risk: **Low-Medium**

**理由**:
- **Low要因**: 既存パターンに準拠、既知の技術スタック
- **Medium要因**: Prefab編集が必要（ユーザー介入）、GearChainPole検出方法の確認が必要

---

## 5. Recommendations for Design Phase

### 推奨アプローチ: **Option A（TrainRailConnectSystemの完全模倣）**

**理由**:
1. 既存の成功パターンを再利用
2. コードベースの一貫性を維持
3. 将来の保守性が高い

### 設計フェーズで確認すべき項目

1. **GearChainPole検出方法**
   - クライアント側でGearChainPoleブロックを特定するためのColliderインターフェース設計
   - BlockGameObjectから特定する方法、または専用Colliderコンポーネントの追加

2. **プレビューオブジェクト設計**
   - LineRendererベースの接続ラインプレビュー
   - 範囲表示（maxConnectionDistance）の実装方法

3. **マスターデータ設計**
   - placeSystem.ymlへの`GearChainConnect`追加
   - gearChainItemsとの連携方法

4. **Prefab構成**
   - プレビューオブジェクトのPrefab構造
   - MainGameStarter.csへのSerializeField追加

### 追加調査項目（Research Needed）

- [ ] DisplayEnergizedRangeの実装を参考に範囲表示の実装方法を確認
- [ ] 既存接続のクライアント側可視化（GearChainPoleStateDetailの活用）
- [ ] チェーンアイテムのguidリストをクライアント側で取得する方法

---

## 6. Requirement-to-Asset Map

| 要件 | 必要なアセット | 状態 |
|------|---------------|------|
| Req1.1 | PlaceSystemSelector.cs | 変更 |
| Req1.2-1.5 | GearChainConnectPlaceSystem.cs | **Missing** |
| Req2.1-2.5 | GearChainConnectPreviewObject.cs | **Missing** |
| Req3.1-3.3 | VanillaApiSendOnly.cs | 変更 |
| Req3.4 | エラー通知UI | **Unknown** |
| Req4.1-4.3 | GearChainConnectPlaceSystem.cs | **Missing** |
| Req5.1 | PlaceSystemSelector.cs, placeSystem.yml | 変更 |
| Req5.2-5.3 | GearChainConnectPlaceSystem.cs | **Missing** |
| Req6.1-6.3 | GearChainConnectPreviewObject.cs | **Missing** |
