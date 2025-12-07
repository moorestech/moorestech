# Research & Design Decisions

## Summary
- **Feature**: chain-energy-transmission
- **Discovery Scope**: Extension / Complex Integration (拡張 / 複雑な統合)
- **Key Findings**:
  - `GearNetwork`はグラフ構築のために`IGearEnergyTransformer.GetGearConnects()`に依存している。これをコンポーネント内で正しく実装すれば、隣接していない接続も可能になる。
  - `blocks.yml`がブロックのスキーマを定義している。新しい`ChainPole`ブロックタイプが必要。
  - プロトコルはMessagePackを使用している。接続/切断用の新しいプロトコルが必要。
  - 実行時のブロックの主キーは`BlockInstanceId`である。

## Research Log

### Gear Network Integration
- **Context**: 離れたブロック間でエネルギーを伝送するには？
- **Sources Consulted**: `IGearEnergyTransformer.cs`, `GearNetwork.cs`
- **Findings**:
  - `GearNetwork`は`GetGearConnects()`を使ってグラフを走査する。
  - `GearConnect`構造体は接続されたTransformerをラップする。
- **Implications**: `ChainPoleComponent`は`IGearEnergyTransformer`を実装する必要がある。その`GetGearConnects()`メソッドは、接続されたチェーンポールを有効なターゲットとして返す必要がある。

### Block Schema Definition
- **Context**: 新しいブロックをどう定義するか？
- **Sources Consulted**: `VanillaSchema/blocks.yml`
- **Findings**:
  - `blockType` enumに`ChainPole`を追加する必要がある。
  - `blockParam`に`ChainPole`のcaseを追加する必要がある。
  - ベースで標準的なギア接続を許可するために`IGearConnectors`を実装すべき。
- **Implications**: コード生成の前にスキーマ変更が必要。

### Network Protocol
- **Context**: クライアントはどうやって接続をリクエストするか？
- **Sources Consulted**: `PlaceTrainCarOnRailProtocol.cs`
- **Findings**:
  - プロトコルは`IPacketResponse`を実装する。
  - シリアライズにはMessagePackを使用。
  - リクエストペイロードには通常座標やIDが含まれる。
- **Implications**: `ConnectChainProtocol`と`DisconnectChainProtocol`を定義する。識別のために`BlockInstanceId`または`Vector3Int`を使用する。`BlockInstanceId`は内部サーバー状態であるため、クライアントリクエストには`Vector3Int`（座標）の方が安全である（再起動またぎの安定性など）。※自己修正：`BlockInstanceId`は通常サーバー側で使用される。クライアントは配置には座標を使用するが、既存ブロックとのインタラクションでも座標が一般的。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| Component-based Connection | 接続ターゲットを`ChainPoleComponent`に保存 | `GetGearConnects`へのアクセスが高速 | セーブロード時の循環参照の扱いに注意 | 推奨 |
| Centralized Chain Manager | 全接続を中央マネージャーで保存 | グローバルなバリデーションが容易 | `GetGearConnects`が低速になる（検索が必要） | |

## Design Decisions

### Decision: Connection Storage
- **Context**: 2つのチェーンポール間のリンクをどこに保存するか？
- **Selected Approach**: `ChainPoleComponent`に保存する。
- **Rationale**: `GearNetwork`は頻繁に`GetGearConnects`を呼び出す。直接参照が最も効率的。
- **Trade-offs**: セーブ/ロード時、全ブロックロード後に参照（ID）をインスタンスに解決する必要がある。

### Decision: Protocol Identification
- **Context**: `ConnectChainRequest`でブロックをどう識別するか？
- **Selected Approach**: `Vector3Int`（座標）を使用する。
- **Rationale**: クライアントはクリックしたブロックの座標を確実に知っている。`BlockInstanceId`は内部サーバー状態。
- **Follow-up**: サーバーが座標からブロックを効率的に検索できることを確認する（WorldBlockDatastore）。

## Risks & Mitigations
- **Risk**: 不注意による`GetGearConnects`での無限再帰。
  - **Mitigation**: `GearNetwork`は既に訪問済みチェック（`_checkedGearComponents`）を行っている。
- **Risk**: サーバー側でチェーン距離制限が強制されない。
  - **Mitigation**: `ChainSystem`は接続前に距離を検証しなければならない。