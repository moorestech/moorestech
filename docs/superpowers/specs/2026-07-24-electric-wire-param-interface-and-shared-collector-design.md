# 電線接続パラメータのinterface化と自動接続選定ロジックの共通コア化

PR1057レビュー（review 4772089898）の2指摘への対応設計。

- 指摘①（blocks.yml）: 電気系8ブロック種に同一3キーがコピペされている → interfaceに組み込めないか
- 指摘②（ClientElectricWireAutoConnectCollector.cs）: 自動接続の候補選定ロジックがサーバーとクライアントで二重実装になっている → 共通化できないか

## スコープ

- 対象ブランチ: `feature/fix-eletric-connect`（PR1057）への追加コミット
- 指摘①: スキーマの `defineInterface` による3キーの共通化と、`ElectricWireBlockParamResolver` のswitch縮約
- 指摘②: 自動接続の候補選定アルゴリズムを純粋な共有コアへ抽出し、サーバー/クライアント双方を薄いアダプタ化
- スコープ外: クライアント未受信ブロックに起因するプレビュー誤差（データ鮮度の問題。サーバー結果が権威であり、接続結果はイベントでクライアントに訂正される）

## 設計1: スキーマ interface（指摘①）

`VanillaSchema/blocks.yml` の `defineInterface` に以下を追加する（`IMachineParam` と同形の先行パターン）。

```yaml
- interfaceName: IElectricWireConnectParam
  properties:
  - key: maxWireConnectionCount
    type: integer
    default: 2
  - key: connectionRange
    type: integer
    default: 30
  - key: connectionHeightRange
    type: integer
    default: 20
```

- 適用対象8種: ElectricMachine / ElectricGenerator / ElectricMiner / ElectricPump / GearToElectricGenerator / ElectricToGearGenerator / CleanRoomAirFilter / CleanRoomMachine
- 各ブロック種の `implementationInterface` に `IElectricWireConnectParam` を追加する。**3キーの定義は各ケースに残す** — 生成器はinterfaceプロパティを実装型へ注入せず、実装ケース側の同名キー宣言でinterfaceメンバーが満たされる（IMachineParam実装のElectricMachineが全キーを自前宣言している前例と同形）
- プロパティ定義・JSONキーとも不変のため、**JSONデータ移行は不要**
- したがって指摘①の実利は**C#側の共通化**（resolver縮約＋今後の電気ブロック追加がinterface付与だけで済む）であり、yaml上のキー重複自体は現行生成器では残る
- ElectricPole は対電柱/対機械の非対称4キー（pole/machineConnection(Height)Range）＋接続上限という別形状のため、interfaceに含めない（単一実装のinterface新設はYAGNI）
- 電柱は同名キー `maxWireConnectionCount`（default 8）を個別プロパティのまま残す。resolverの電柱分岐は個別プロパティ参照を維持する
- スキーマ編集は edit-schema スキルの手順に従う

## 設計2: resolver縮約

`ElectricWireBlockParamResolver.TryGetWireRangeParam` の9分岐switchを3分岐へ縮約する。

```csharp
switch (blockParam)
{
    case ElectricPoleBlockParam pole:      // 電柱: 非対称プロファイル
    case IElectricWireConnectParam machine: // 機械系8種を一括処理
    default:                                // 非電気系
}
```

今後の電気ブロック追加は、スキーマで `implementationInterface` を付与するだけでresolver・選定コアに自動対応する。

## 設計3: 選定コアの抽出（指摘②）

### 現状の二重化

「最寄り電柱1本 → 未接続機械を残容量まで、距離順→InstanceId順」の選定ポリシー約70行が
`ElectricWireAutoConnectTargetCollector`（サーバー）と `ClientElectricWireAutoConnectCollector`（クライアント）に同文で存在する。
ジオメトリ判定（`ElectricConnectionRangeService` / `ConnectionRangeProfile` / resolver）は既にソース共有済みで、重複は選定ポリシーのみ。

### 共有コア

`Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/` に純粋静的クラスを新設する。
`ElectricWirePlacementEvaluator`（クライアントが再利用している共有純粋ロジック）と同じ配置・同じ形に従う。

- **候補struct** `ElectricWireConnectCandidate`: `BlockInstanceId` / `IBlockParam` / `BlockPositionInfo` / 現在接続数
- **選定コア** `ElectricWireAutoConnectSelector`（純粋静的クラス）: 候補列と自分側の情報（BlockParam・位置情報・使用済み接続数）を受け取り、選定結果（InstanceId＋距離の順序付きリスト）を返す
- コアの内部で resolver 適用・相互範囲判定・容量判定・未接続判定・距離順→InstanceId順ソートまで全て行う。アダプタ側に判定ロジックを一切残さない
- 公開APIは既存3操作に対応: 電柱設置用（最寄り電柱1本＋機械）／電柱の機械のみ収集用（extend用 `usedCount` 引数あり）／機械設置用（最寄り電柱1本）

### アダプタ化

- サーバー `ElectricWireAutoConnectTargetCollector`: ワールド全ブロック列挙 → struct変換（接続数は `IElectricWireConnector.WireConnections.Count`）→ コア呼び出し → InstanceId から Connector を復元して返す
- クライアント `ClientElectricWireAutoConnectCollector`: 受信ブロック列挙 → struct変換（接続数は `ElectricWireStateChangeProcessor.CurrentPartnerIds.Count`）→ コア呼び出し → 座標に変換して返す

### 判定意味論の一本化（判定源の統一）

現サーバーと現クライアントの判定は**結果としては等価**（機械の未接続=接続数0かつ容量未満、容量判定=満杯除外を両者とも実装済み）だが、判定源と実装が二重化している:

| 判定 | 現サーバー | 現クライアント | 統一後（コア） |
|---|---|---|---|
| 電柱かどうか | `EnergyRole is IElectricTransformer`（実行時コンポーネント） | resolver（マスタ由来） | resolver に一本化 |
| 容量判定 | `IsWireConnectionFull`（コンポーネント実装） | `capacity <= count`（コレクタ内） | コア内で `count < capacity` |
| 機械の未接続 | `WireConnections.Count == 0` | `GetPartnerCount == 0` | コア内で `ConnectionCount == 0` |

挙動変化はなく、`EnergyRole` 参照と両側の個別実装を廃止して判定源をコア1箇所（マスタパラメータ＋resolver）にする構造的一本化である（`IElectricTransformer` 実装は電柱1種のみで等価性を裏取り済み）。

## セルフ反証

最凶ケース: **同距離の候補が2つあり、列挙順がサーバーとクライアントで異なる**。
サーバーは `WorldBlockDatastore.BlockMasterDictionary`、クライアントは `BlockGameObjectByInstanceIdDictionary` を列挙するため順序は一致しない。
コアが距離→InstanceId の全順序ソートを一元的に行うため、列挙順に依存せず両側で同一の選定結果になる。
現行実装はこのtie-breakを両側に個別に書いており、片側で書き漏れると即座に結果がズレる構造だった — 本設計はその構造自体を消す。

## エラーハンドリング

- 非電気系BlockParam: resolverが `false` を返しコアが候補から除外（既存挙動維持）
- 容量0のブロック: 自分側なら空リスト即返し、相手側なら候補除外（既存挙動維持）
- try-catch は使わない（AGENTS.md 規約。全て条件分岐で処理）

## テスト

- **新設**: 選定コア単体テスト — 同距離tie-break（InstanceId順）、容量境界（残1本・残0本）、`usedCount` 差し引き、機械の未接続判定（接続済み機械の除外）、電柱優先順位。配置は既存 `ElectricConnectionRangeServiceTest` と同じ `Tests/UnitTest/Server/`
- **既存維持**: `ElectricWireAutoConnectPlaceTest` / `ElectricWireExtendProtocolTest` / `ElectricConnectionRangeServiceTest` がアダプタ化後もそのまま通ることで後方等価を担保
- コンパイル: `uloop compile --project-path ./moorestech_client`（スキーマ再生成含む）

## 判断記録（ADR）

- **スコープ＝レビュー2件のみ・PR1057追加コミット**（B: レビュー対応の標準運用）
- **interface名 `IElectricWireConnectParam`・3キー・デフォルト値現行維持**（B: IMachineParam等の既存命名規約に準拠）
- **電柱をinterfaceに含めない**（B: 非対称4キーで形状が異なる。単一実装interfaceはYAGNI）
- **共有コアはサーバーアセンブリ側に配置**（A: クライアントが既に `Server.Protocol...ElectricWire` を参照。`ElectricWirePlacementEvaluator` が同形の先行パターン）
- **判定意味論はサーバー側に統一・電柱判定はresolverに一本化**（B: SSOT。サーバーが権威）
- **案2（データソース抽象interface注入）不採用**（B: `IElectricWireConnector` のクライアント側ダミー実装が必要になり抽象が重い。案1が同効果を軽く達成 — 無料の上位互換）
- **案3（現状維持＋同値性テスト）不採用**（B: ズレの検出はできても発生を防げず、二重ロジック指摘の解消にならない）
- **電柱の同名キー並存・テスト配置先**: シミュレーターWarningをspecへ反映（出所: シミュレーター予測。前提の裏取りは全件成立・Critical指摘なし）
- **「interfaceプロパティは実装型へ注入される」前提の撤回**: mooresmaster生成器はinterface実装の名前付与のみ行い、プロパティは各ケースの自前宣言が必須（ElectricMachineのIMachineParam実装で確認）。3キーは各ケースに残し、interface付与のみ行う（出所: plan段階のシミュレーター反証→実コード検証で確定）
- **yaml上の3キー×8箇所はあるべき姿であり問題ではない**: 各ブロック種がプロパティを明示宣言する現行スキーマの形が意図されたもの。生成器へのプロパティ注入機能追加は不要。指摘①の解消対象はC#側の分岐重複（resolver switch）のみ（出所: 2026-07-25 ユーザー裁定）
- **設計一括承認**: 2026-07-24 ユーザー承認済み（出所: 設計提示→「ok」）
