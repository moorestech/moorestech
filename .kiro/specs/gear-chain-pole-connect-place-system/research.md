# Research & Design Decisions: GearChainPole Connect PlaceSystem

---
**Purpose**: 設計判断の根拠となる調査結果と意思決定プロセスを記録する。
---

## Summary
- **Feature**: `gear-chain-pole-connect-place-system`
- **Discovery Scope**: Extension（既存PlaceSystemパターンの拡張）
- **Key Findings**:
  - TrainRailConnectSystemが2ステップ接続の完全なテンプレートとして使用可能
  - LineRenderer + InventoryConnectorLineViewパターンが接続線プレビューに適用可能
  - EnergizedRangeObjectパターンが範囲表示に適用可能（スケール変更による実装）
  - サーバー側プロトコル（GearChainConnectionEditProtocol）は完全実装済み

## Research Log

### PlaceSystemアーキテクチャパターン
- **Context**: 新しいPlaceSystemの追加方法を確認
- **Sources Consulted**:
  - `PlaceSystemSelector.cs` - PlaceSystem選択ロジック
  - `PlaceSystemStateController.cs` - PlaceSystemのライフサイクル管理
  - `TrainRailConnectSystem.cs` - 2ステップ接続パターンの参照実装
- **Findings**:
  - PlaceModeはenumベースでVanillaSchema/placeSystem.ymlで定義
  - PlaceSystemSelectorはマスターデータから`usePlaceItems`を検索してPlaceSystemを選択
  - IPlaceSystemインターフェース: `Enable()`, `ManualUpdate(context)`, `Disable()`
  - 2ステップ接続: 接続元選択 → 接続先選択 → プロトコル送信
- **Implications**: TrainRailConnectSystemと同一構造で実装可能

### 接続線プレビュー実装パターン
- **Context**: 接続元から接続先への視覚的なライン表示方法を調査
- **Sources Consulted**:
  - `InventoryConnectorLineView.cs` - LineRendererによるライン表示
  - `GearConnectorView.cs` - ギア接続プレビュー
  - `GearConnectorLineView.prefab` - 既存のギアラインプレビュー
- **Findings**:
  - LineRendererを使用した2点間のライン描画パターンが確立済み
  - `SetPoints(Vector3Int start, Vector3Int end)`メソッドでポイント設定
  - Update()内でリアルタイム更新
  - 色変更はLineRenderer.materialで制御可能
- **Implications**: InventoryConnectorLineViewをベースにGearChainConnectLineViewを作成

### 範囲表示実装パターン
- **Context**: maxConnectionDistanceの視覚的表示方法を調査
- **Sources Consulted**:
  - `EnergizedRangeObject.cs` - 電力範囲表示
  - `DisplayEnergizedRange.cs` - 範囲オブジェクト管理
- **Findings**:
  - Prefabのスケール変更で範囲表示を実現
  - `transform.localScale = new Vector3(range, height, range)`
  - MonoBehaviourベースのシンプルな実装
- **Implications**: 同一パターンでGearChainConnectRangeObjectを作成可能

### GearChainPole検出方法
- **Context**: クライアント側でGearChainPoleブロックを特定する方法
- **Sources Consulted**:
  - `TrainRailConnectAreaCollider.cs` - レール接続エリア検出
  - `IRailComponentConnectAreaCollider.cs` - レイキャスト検出インターフェース
  - `PlaceSystemUtil.TryGetRaySpecifiedComponentHit<T>()` - 汎用レイキャスト検出
- **Findings**:
  - 専用Colliderコンポーネント + インターフェースパターンが確立済み
  - `TryGetRaySpecifiedComponentHit<IGearChainPoleCollider>()`で検出
  - Colliderコンポーネントは配置されたブロックのGameObjectにアタッチ
- **Implications**: IGearChainPoleColliderインターフェースとGearChainPoleColliderコンポーネントを作成

### サーバープロトコル連携
- **Context**: クライアント→サーバー通信の実装方法
- **Sources Consulted**:
  - `GearChainConnectionEditProtocol.cs` - プロトコル定義
  - `VanillaApiSendOnly.cs` - APIメソッド定義
  - `ConnectRail()`, `DisconnectRail()` - 既存の接続APIパターン
- **Findings**:
  - `GearChainConnectionEditRequest.CreateConnectRequest(posA, posB, playerId, itemId)`
  - `GearChainConnectionEditRequest.CreateDisconnectRequest(posA, posB, playerId)`
  - VanillaApiSendOnlyに2メソッド追加するパターン
- **Implications**: 既存パターンに従いConnectGearChain/DisconnectGearChainメソッドを追加

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Option A: 完全模倣 | TrainRailConnectSystemと同一構造 | 既存パターン準拠、テストしやすい | 新規ファイル数多め | **採用** |
| Option B: 共通化 | CommonBlockPlaceSystem拡張 | ファイル数削減 | 単一責任違反、複雑化 | 非推奨 |
| Option C: 段階実装 | 最小→段階的追加 | 早期検証可能 | 複数PRが必要 | 中間案 |

## Design Decisions

### Decision: TrainRailConnectSystemパターンの採用
- **Context**: GearChainPole接続用PlaceSystemの実装アプローチ選定
- **Alternatives Considered**:
  1. TrainRailConnectSystemの完全模倣
  2. CommonBlockPlaceSystemへのモード追加
  3. 汎用ConnectPlaceSystemの抽象化
- **Selected Approach**: Option A - TrainRailConnectSystemの完全模倣
- **Rationale**:
  - 既存の成功パターンを再利用することでリスクを最小化
  - コードベースの一貫性を維持
  - 将来的な保守性が高い
- **Trade-offs**:
  - メリット: 理解しやすい、テストしやすい、変更が容易
  - デメリット: 新規ファイルが4-5個必要
- **Follow-up**: なし

### Decision: LineRendererベースの接続プレビュー
- **Context**: 接続元から接続先への視覚的フィードバック方法
- **Alternatives Considered**:
  1. LineRenderer使用（InventoryConnectorLineViewパターン）
  2. Mesh生成による接続表示
  3. パーティクルシステム
- **Selected Approach**: LineRenderer使用
- **Rationale**:
  - InventoryConnectorLineView/GearConnectorLineViewで確立済みパターン
  - 色変更が容易（material変更）
  - パフォーマンス効率が良い
- **Trade-offs**:
  - メリット: 既存パターン活用、シンプル
  - デメリット: 3D曲線表現は限定的
- **Follow-up**: マテリアル色定義（緑/赤/黄）

### Decision: マスターデータからのgearChainItems取得
- **Context**: チェーンアイテムの判定方法
- **Alternatives Considered**:
  1. BlockMaster.Blocks.GearChainItemsから取得
  2. PlaceSystemMaster経由で判定
  3. 専用のアイテムタイプフラグ
- **Selected Approach**: PlaceSystemMaster経由 + BlockMaster.Blocks.GearChainItems併用
- **Rationale**:
  - PlaceSystemMasterのusePlaceItemsでPlaceSystem選択
  - gearChainItemsでコスト計算に必要な情報取得
- **Trade-offs**:
  - メリット: 既存マスターデータシステムを活用
  - デメリット: 2箇所のマスターデータ参照が必要
- **Follow-up**: placeSystem.jsonにチェーンアイテム設定を追加

## Risks & Mitigations

- **Prefab編集が必要** — ユーザーに手順書を提供し、MainGameStarterへのSerializeField追加を依頼
- **GearChainPoleColliderのアタッチ** — ブロックPrefabにコンポーネント追加が必要。VanillaGearChainPoleTemplateまたはブロック生成時に動的追加を検討
- **既存接続の可視化** — GearChainPoleStateDetailの同期タイミングによりクライアント側で接続状態が取得できない可能性。初期実装では接続操作時のみの表示に限定

## References
- `TrainRailConnectSystem.cs` - 2ステップ接続パターンの参照実装
- `InventoryConnectorLineView.cs` - LineRendererによるライン表示パターン
- `EnergizedRangeObject.cs` - 範囲表示パターン
- `GearChainConnectionEditProtocol.cs` - サーバープロトコル定義
- `VanillaSchema/placeSystem.yml` - PlaceMode定義
- `VanillaSchema/blocks.yml` - gearChainItems定義
