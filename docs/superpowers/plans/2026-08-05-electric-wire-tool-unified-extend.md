# 電柱・電線接続改修（延長モデル統一・明示配線化・プレビュー端点統一） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 電線ツールを「起点→終点への延長（終点=既存ブロック or 新設電柱、成功したら終点が次の起点）」の単一モデルに統一し、プレビューと実描画の電線位置ズレ・単品電柱が置けない問題・コスト表示と実消費の食い違いを解消する。

**Architecture:** サーバーは `va:electricWireExtend` を Operation 3種（既存接続/延長設置/孤立設置）に統合し、応答は常に「成否＋終点InstanceId」。電線ツール経由の配線は明示した1本のみ（周辺自動配線なし）。クライアントは GearChainPole と同型のインスタンス型 RequestSender に一本化し、応答確認後に起点を終点へチェーンする。プレビュー端点は実描画と同じ `ElectricWireConnectionPoint` 解決に統一する。

**Tech Stack:** Unity / C# / MessagePack / UniTask / NUnit（uloop CLI でコンパイル・テスト実行）

## Requirements

設計ADR: `docs/adr/0008-electric-wire-tool-unified-extend-and-explicit-wiring.md`

1. **プレビュー端点統一** — 電線ツール延長プレビューと通常設置の自動接続プレビューの両方が、実描画と同じ端点解決（`ElectricWireConnectionPoint` マーカー→無ければ上面中央）を使う。受け入れ基準: 電柱（マーカーY+4.44）への接続プレビュー線が電柱先端から出る。カテナリー・SagRatio は既に一致済み（0.1）なので端点のみが差分。
2. **プロトコル統合と起点チェーン** — `va:electricWireExtend` が「既存ブロックへの接続」「電柱延長設置」「孤立設置」の3 Operation を持ち、応答は共通で成否＋終点InstanceId。クライアントはサーバー応答の成功確認後にのみ起点を終点へ移す。受け入れ基準: 既存ブロックへ接続成功→次のプレビュー起点が接続先になる。失敗時は起点が動かない。
3. **孤立設置** — 電線ツールで起点未選択のとき、空間クリックで電柱を単体設置できる。自動接続は一切行わない。設置成功したらその電柱が起点になる。受け入れ基準: 近くに電気ブロックがあっても接続ゼロ本の電柱が置ける。
4. **周辺自動配線の廃止（電線ツール経由）** — 延長設置でも新設電柱周辺の未接続機械への自動配線・課金を行わない。引くのは起点との1本のみ。受け入れ基準: プレビューの「電線 xN」表示と実消費が一致する。
5. **通常設置ゲート維持＋拒否理由表示** — ビルドメニューからの通常設置の「電線を賄えなければ設置拒否」は維持し、拒否理由（電線不足）をラベルで表示する。さらに、近傍に電気ブロックはあるが接続範囲外で配線されない場合は「接続範囲外のため配線されません」を情報表示する（設置は成功する。ユーザー裁定 2026-08-05・シミュレーター予測→ユーザー承認）。電線ツールのプレビューも不可時に理由（範囲外・接続上限・電線不足等）を表示する。
6. **解放フィルタバグ修正** — クライアントの自動接続プレビューが電線connectToolを解放状態でフィルタし、サーバー判定（`ConnectToolSelector.UnlockedByToolType`）と一致する。
7. **起点キャンセル操作** — 右クリックで起点を解除できる。
8. **電柱種サイクル＋回転** — 電線ツール使用中、マウススクロールで解放済み電柱種を切替、`BlockPlaceRotation` キー（+Shiftで垂直）で向きを変更できる。選択中の電柱種はゴーストと名前ラベルで表示。

**やらないこと（スコープ境界）:**
- `va:placeBlock`（通常設置）のサーバー側自動接続ロジック・ゲートは変更しない。
- GearChainPole / TrainRail 系の接続システムは変更しない（前例として参照のみ）。
- ローカライズキー化はしない（既存の `"電線 x{n}"` 直書きパターンを踏襲する）。
- 電線切断の挙動（返却・InventoryFull判定）は変更しない（プロトコル名の変更のみ）。

## Global Constraints

- 1ファイル200行以下。partial禁止。`Func<>`禁止。try-catch原則禁止。デフォルト引数禁止。
- コメントは日本語→英語の2行セット（3〜10行ごと、各1行）。
- `#region Internal` はメソッド内ローカル関数のみ。クラス直下private群の囲いは禁止。
- イベントはUniRx（本planでは新規イベント不要）。
- .metaファイルは手動作成禁止。既存ファイルのリネームは `git mv` で .cs と .cs.meta を同時に移動する。新規.csはUnity起動時に.metaが自動生成される。
- コンパイル: `uloop compile --project-path ./moorestech_client`（.cs変更後は必ず実行）。
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`（サーバーテストもクライアントプロジェクトから実行できる）。
- ドメインリロードエラー時は45秒待ってリトライ。
- プロトコル規約: Request/ResponseのMessagePack KeyはKey(2)から。enumはintに変換せずそのまま送る。Operationごとに必要フィールドが違うRequestはprivateコンストラクタ+static factory。1プロトコル=1 VanillaApiメソッド。
- 各タスク完了時に必ずコミットする（worktree運用のため作業消失防止）。

## 配置と前例（spec-architecture-review済み）

| 項目 | 配置先 | 前例 |
|---|---|---|
| Operation統合Request（static factory） | `Server.Protocol/PacketResponse/ElectricWireExtendProtocol.cs` 内ネスト | `RailConnectionEditRequest` / `FilterSplitterStateRequest` のfactoryパターン |
| 接続実行ロジック | `ElectricWireSystemUtil.TryConnect`（既存を呼ぶ。移動しない） | 現行 `ElectricWireConnectionEditProtocol` が同じutilを呼ぶ |
| 切断専用プロトコルへのリネーム | `ElectricWireDisconnectProtocol.cs`（`git mv`） | 「名前は実処理と一致させる」規約 |
| クライアント送信・応答・世代管理 | `ElectricWireExtendRequestSender`（staticからインスタンスへ） | `GearChainPoleExtendRequestSender`（世代トークン・IsAwaitingResponse・TryConsume） |
| 孤立設置の判断分岐 | `ElectricWireEditMode`（起点なし側モード） | `GearChainPolePlaceExtendMode.DecideIsolatedPlace` |
| 電柱ゴースト共通部 | 新設 `Parts/ElectricWirePoleGhostPart.cs` | `ElectricWireExtendMode.ExtendToEmptySpace` の既存ロジックを抽出（機構変更なし） |
| 電柱種サイクル・向き状態 | 新設 `Parts/ElectricWirePoleSelection.cs`（ツールシステム保持） | 向き状態は `CommonBlockPlaceSystem._currentBlockDirection`、スクロール読取は `BlueprintCopySystem`（`Mouse.current.scroll` → legacyフォールバック） |
| 端点解決の共通化 | 新設 `StateProcessor/ElectricWire/ElectricWireEndpointResolver.cs` | `ElectricWireLineViewElement.ResolveEndpoint` の既存ロジックを昇格（実描画が正） |
| ゴーストGameObjectアクセサ | `IPlacementPreviewBlockGameObjectController.TryGetPreviewBlock(int, out BlockPreviewObject)` | 汎用基盤にはドメイン非依存の形で追加。電線マーカー探索は電線側コードが行う |
| 失敗理由の文言変換 | 新設 `ElectricWireConnect/Parts/ElectricWirePlacementFailureText.cs` | `"電線 x{n}"` 直書きパターン（ElectricWireExtendPreviewObject） |
| 解放フィルタ | `ElectricWireAutoConnectPreview` に `IGameUnlockStateData` 注入 | `GearChainPoleConnectSystem` のコンストラクタ注入と同型 |

**データフロー地図（電線ツール操作系）:**
```
（入力）→（EditMode/ExtendMode: 判断）→［ElectricWireExtendRequestSender: 送信+応答保持］→（ElectricWireConnectSystem: TryConsumeで起点更新）→（次フレームの判断へ）
```
新設コンポーネントはすべてこの一方向ループの既存の駅に収まる。サーバー可変状態（接続集合）の同期は既存の BlockState イベント（`ElectricWireStateChangeProcessor`）のままで、新規の同期経路は作らない（3点セット新設は不要 — 新しいサーバー可変状態を追加しないため）。

**機能パリティ（死活表）:**

| 現在使える操作 | 計画後 | 根拠 |
|---|---|---|
| 電線ツール: ワイヤークリック切断 | 生きる | EditModeの切断分岐は維持（プロトコル名のみ変更） |
| 電線ツール: 電気ブロッククリックで起点選択 | 生きる | EditModeの選択分岐は維持 |
| 電線ツール: 起点→既存ブロック接続 | 生きる（起点が接続先へ移るよう変化） | ユーザー裁定 2026-08-05（起点チェーン） |
| 電線ツール: 起点→空間クリックで電柱設置+接続 | 生きる（周辺自動配線は廃止） | ユーザー裁定 2026-08-05（明示1本のみ） |
| 電線ツール: 接続後の起点維持連続接続 | 形が変わる（起点維持→接続先チェーン） | ユーザー裁定 2026-08-05 |
| ビルドメニュー: 電柱・機械の通常設置+自動接続 | 生きる（拒否理由表示が加わる） | ユーザー裁定 2026-08-05（ゲート維持+tooltip） |
| ビルドメニュー: ドラッグ連続設置 | 生きる | CommonBlockPlaceSystemのゲート機構は不変 |
| スポイト（PlacementTargetPickService）の電線ツール解決 | 生きる | ConnectToolCatalog.TryResolveDefaultConnectToolGuidは不変 |

---

### Task 1: サーバー — 電線ツール経由設置の意味論変更（周辺自動配線廃止・孤立設置の真孤立化）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireExtendService.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireExtendProtocolTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireExtendProtocolFailureTest.cs`

**Interfaces:**
- Consumes: 既存の `ElectricWireExtendService.Execute(bool hasFromConnector, Vector3Int fromPos, PlaceInfoMessagePack polePlaceInfo, int playerId, BlockId poleBlockId, Guid connectToolGuid)`（シグネチャ不変。Task 2で変更する）
- Produces: 延長設置=起点との1本のみ接続、孤立設置=接続ゼロ・自動接続評価なし、という新しい意味論

- [ ] **Step 1: 既存テストを新意味論に書き換える（失敗するテストにする）**

`ElectricWireExtendProtocolTest.cs` の以下3テストを書き換える。テストユーティリティ（`SetupInventory`/`SendExtend`/`SendIsolatedPlace`/`CountItem`）は変更しない。

`起点あり延長で電柱を設置し起点と機械へ接続して消費する` を置き換え:

```csharp
[Test]
public void 起点あり延長は起点との1本のみ接続し周辺機械へは配線しない()
{
    // 起点電柱と、新電柱の機械範囲内の未接続機械を用意する
    // Prepare an origin pole and an unconnected machine inside the new pole's machine range
    var worldBlockDatastore = ServerContext.WorldBlockDatastore;
    var fromPos = Vector3Int.zero;
    var newPolePos = new Vector3Int(3, 0, 0);
    var machinePos = new Vector3Int(5, 0, 0);
    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, machinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

    var inventory = SetupInventory(materialCount: 1, wireCount: 10);
    var fromConnector = fromPole.GetComponent<IElectricWireConnector>();
    var machineConnector = machine.GetComponent<IElectricWireConnector>();

    // 起点あり延長を実行する（起点距離3の電線3本だけが消費される）
    // Run extend with origin; only 3 wires for the origin distance are consumed
    var response = SendExtend(fromPos, newPolePos);

    Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());
    var newConnector = worldBlockDatastore.GetBlock(newPolePos).GetComponent<IElectricWireConnector>();

    // 接続は起点との1本のみで、周辺機械へは配線されない
    // Exactly one edge to the origin; the nearby machine stays unwired
    Assert.AreEqual(1, newConnector.WireConnections.Count);
    Assert.IsTrue(fromConnector.ContainsWireConnection(newConnector.BlockInstanceId));
    Assert.AreEqual(0, machineConnector.WireConnections.Count);
    Assert.AreEqual(7, CountItem(inventory, _wireItemId));
    Assert.AreEqual(0, CountItem(inventory, _materialItemId));
}
```

`起点なし設置でも近傍電柱へ通常設置と同様に自動接続される` を置き換え:

```csharp
[Test]
public void 起点なし孤立設置は近傍に電柱があっても一切接続しない()
{
    // 既存電柱の探索範囲内へ起点なしで電柱を設置する
    // Place a pole without origin inside the existing pole's search range
    var worldBlockDatastore = ServerContext.WorldBlockDatastore;
    var existingPolePos = Vector3Int.zero;
    var newPolePos = new Vector3Int(3, 0, 0);
    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, existingPolePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var existingPole);

    var inventory = SetupInventory(materialCount: 1, wireCount: 10);
    var response = SendIsolatedPlace(newPolePos);

    Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());

    // 接続ゼロ・電線消費ゼロで電柱のみ設置される
    // The pole is placed alone: zero connections and zero wire consumption
    var newConnector = worldBlockDatastore.GetBlock(newPolePos).GetComponent<IElectricWireConnector>();
    Assert.AreEqual(0, newConnector.WireConnections.Count);
    Assert.AreEqual(0, existingPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
    Assert.AreEqual(10, CountItem(inventory, _wireItemId));
    Assert.AreEqual(0, CountItem(inventory, _materialItemId));
}
```

`未接続機械を起点にした延長で二重接続や電線二重消費が起きない` は「機械収集がなくなった」ため回帰の対象自体が消えるが、機械起点の延長が正しく1本で済むテストとして残す（アサートは現行のまま通るはず — 起点1本のみ・電線2本消費は新意味論と一致する。コメントの「機械収集で再収集される回帰ケース」の記述を「機械を起点にした延長の基本ケース」に改める）。

`起点なし孤立設置は電線消費なしで電柱のみ設置する` は現行のまま維持。

`ElectricWireExtendProtocolFailureTest.cs`: `電線不足の延長は失敗し状態が一切変化しない` テストのセットアップコメントと必要本数を新意味論に合わせる（機械距離2の加算が消えるため「起点距離3=必要数3」に対し電線2本で不足させる。テスト本文の座標・所持数・アサートを「wireCount: 2、必要3本」に調整する。状態不変のアサート構造は変更しない）。他の失敗系テスト（範囲外・上限・未解放等）は意味論変更の影響を受けないため無変更。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireExtendProtocol"`
Expected: FAIL（起点あり延長は機械へも配線され electricWire 消費が5本になるため / 孤立設置は近傍電柱へ接続するため）

- [ ] **Step 3: ElectricWireExtendService を新意味論へ変更する**

`ExecuteExtendWithOrigin` から機械収集ループを削除する。具体的には L106-118（`CollectPoleMachineTargets` 呼び出しと `foreach (var machineTarget in machineTargets)` ループ全体）を削除し、`targets` リストは起点接続1件のみになる。あわせて L63-64 のコメントを更新:

```csharp
// 起点ありは起点との明示1本のみ、起点なしは接続なしの単体設置
// With origin: only the explicit origin wire; without: place the pole alone with no wiring
return hasFromConnector
    ? ExecuteExtendWithOrigin()
    : ExecuteIsolatedPlace();
```

`ExecuteIsolatedPlace` を全置換:

```csharp
// 起点なし設置。自動接続は行わず電柱単体のみを設置する
// Placement without origin; place the pole alone with no auto-connect
ExtendResult ExecuteIsolatedPlace()
{
    if (!TryPlacePole(polePlaceInfo, blockId, out var selfConnector))
        return ExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

    // 建設コストのみ消費する
    // Consume only the construction cost
    ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);

    return ExtendResult.Success(polePlaceInfo.Position, selfConnector.BlockInstanceId.AsPrimitive());
}
```

孤立設置は電線を使わないため、冒頭の connectTool 解放チェック（L47-48）は `hasFromConnector` 条件付きのまま維持でよい。`ElectricWireAutoConnectService` への using（`Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect`）は `ElectricWireBlockParamResolver` が使うため残る。未使用になった using は削除する。

- [ ] **Step 4: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireExtendProtocol"`
Expected: PASS（全件）

- [ ] **Step 5: コミットする**

```bash
git add -A moorestech_server/Assets/Scripts
git commit -m "refactor: 電線ツール経由の電柱設置から周辺自動配線を廃止し孤立設置を真に孤立化"
```

---

### Task 2: サーバー — extendプロトコルへ既存ブロック接続Operationを統合し応答をEndpoint化

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireExtendProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireExtendService.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireExtendProtocolTest.cs`

**Interfaces:**
- Produces（Task 4のクライアントが使う契約）:
  - `ElectricWireExtendProtocol.ElectricWireExtendOperation { ConnectToExisting, ExtendToNewPole, PlaceIsolatedPole }`（Request内ネストenum）
  - `ElectricWireExtendRequest.CreateConnectRequest(int playerId, Vector3Int fromPos, Vector3Int toPos, Guid connectToolGuid)`
  - `ElectricWireExtendRequest.CreateExtendRequest(int playerId, Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, Guid connectToolGuid)`（既存シグネチャ維持）
  - `ElectricWireExtendRequest.CreateIsolatedPlaceRequest(int playerId, BlockId poleBlockId, PlaceInfo polePlaceInfo)`（connectToolGuid引数を削除）
  - `ElectricWireExtendResponse { bool IsSuccess, ElectricWirePlacementFailureReason FailureReason, Vector3IntMessagePack EndpointPos, int EndpointBlockInstanceId }`
  - `ElectricWireExtendService.Execute(ElectricWireExtendProtocol.ElectricWireExtendOperation operation, Vector3Int fromPos, Vector3Int toPos, PlaceInfoMessagePack polePlaceInfo, int playerId, BlockId poleBlockId, Guid connectToolGuid)`

- [ ] **Step 1: 接続Operationの失敗するテストを書く**

`ElectricWireExtendProtocolTest.cs` に追加（`SendConnect` ユーティリティも `#region TestUtil` へ追加）:

```csharp
[Test]
public void 既存ブロック接続Operationで接続され終点InstanceIdが返る()
{
    // 範囲内の電柱2本を用意して接続する
    // Prepare two poles in range and connect them
    var worldBlockDatastore = ServerContext.WorldBlockDatastore;
    var fromPos = Vector3Int.zero;
    var toPos = new Vector3Int(3, 0, 0);
    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, toPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var toPole);

    var inventory = SetupInventory(materialCount: 0, wireCount: 10);
    var response = SendConnect(fromPos, toPos);

    // 接続成功し、終点（接続先）のInstanceIdが次の起点として返る
    // Connection succeeds and the endpoint (target) InstanceId is returned as the next origin
    Assert.IsTrue(response.IsSuccess, response.FailureReason.ToString());
    var toConnector = toPole.GetComponent<IElectricWireConnector>();
    Assert.AreEqual(toPos, (Vector3Int)response.EndpointPos);
    Assert.AreEqual(toConnector.BlockInstanceId.AsPrimitive(), response.EndpointBlockInstanceId);
    Assert.IsTrue(fromPole.GetComponent<IElectricWireConnector>().ContainsWireConnection(toConnector.BlockInstanceId));
    Assert.AreEqual(7, CountItem(inventory, _wireItemId));
}

[Test]
public void 既存ブロック接続Operationは電線不足で失敗し理由が返る()
{
    // 電線を持たずに接続を要求する
    // Request a connection while holding no wires
    var worldBlockDatastore = ServerContext.WorldBlockDatastore;
    var fromPos = Vector3Int.zero;
    var toPos = new Vector3Int(3, 0, 0);
    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, fromPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fromPole);
    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, toPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

    SetupInventory(materialCount: 0, wireCount: 0);
    var response = SendConnect(fromPos, toPos);

    Assert.IsFalse(response.IsSuccess);
    Assert.AreEqual(ElectricWirePlacementFailureReason.NoWireItem, response.FailureReason);
    Assert.AreEqual(0, fromPole.GetComponent<IElectricWireConnector>().WireConnections.Count);
}
```

```csharp
private ElectricWireExtendProtocol.ElectricWireExtendResponse SendConnect(Vector3Int fromPos, Vector3Int toPos)
{
    var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateConnectRequest(PlayerId, fromPos, toPos, ConnectToolGuid));
    var responses = _packet.GetPacketResponse(payload, new PacketResponseContext(null));
    return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
}
```

`using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;` をテストファイルに追加（`ElectricWirePlacementFailureReason` 参照のため。既にあれば不要）。

- [ ] **Step 2: コンパイルエラー（CreateConnectRequest未定義）を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CreateConnectRequest` / `EndpointPos` 未定義のコンパイルエラー

- [ ] **Step 3: Request/ResponseをOperation形式へ書き換える**

`ElectricWireExtendProtocol.cs` の Request/Response を置換:

```csharp
[MessagePackObject]
public class ElectricWireExtendRequest : ProtocolMessagePackBase
{
    [Key(2)] public ElectricWireExtendOperation Operation { get; set; }
    [Key(3)] public Vector3IntMessagePack FromPos { get; set; }
    [Key(4)] public Vector3IntMessagePack ToPos { get; set; }
    [Key(5)] public PlaceInfoMessagePack PolePlaceInfo { get; set; }
    [Key(6)] public int PlayerId { get; set; }
    [Key(7)] public int PoleBlockIdInt { get; set; }
    [Key(8)] public Guid ConnectToolGuid { get; set; }

    [IgnoreMember] public Vector3Int FromPosVector => FromPos;
    [IgnoreMember] public Vector3Int ToPosVector => ToPos;
    [IgnoreMember] public BlockId PoleBlockId => new(PoleBlockIdInt);

    [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
    public ElectricWireExtendRequest() { Tag = ElectricWireExtendProtocol.Tag; }

    // Operationごとに必要フィールドが異なるため、生成はstatic factory経由に限定する
    // Creation is restricted to static factories since required fields differ per operation
    private ElectricWireExtendRequest(ElectricWireExtendOperation operation, Vector3Int fromPos, Vector3Int toPos, PlaceInfoMessagePack polePlaceInfo, int playerId, int poleBlockIdInt, Guid connectToolGuid)
    {
        Tag = ElectricWireExtendProtocol.Tag;
        Operation = operation;
        FromPos = new Vector3IntMessagePack(fromPos);
        ToPos = new Vector3IntMessagePack(toPos);
        PolePlaceInfo = polePlaceInfo;
        PlayerId = playerId;
        PoleBlockIdInt = poleBlockIdInt;
        ConnectToolGuid = connectToolGuid;
    }

    public static ElectricWireExtendRequest CreateConnectRequest(int playerId, Vector3Int fromPos, Vector3Int toPos, Guid connectToolGuid)
        => new(ElectricWireExtendOperation.ConnectToExisting, fromPos, toPos, new PlaceInfoMessagePack(new PlaceInfo()), playerId, 0, connectToolGuid);

    public static ElectricWireExtendRequest CreateExtendRequest(int playerId, Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, Guid connectToolGuid)
        => new(ElectricWireExtendOperation.ExtendToNewPole, fromPos, Vector3Int.zero, new PlaceInfoMessagePack(polePlaceInfo), playerId, poleBlockId.AsPrimitive(), connectToolGuid);

    public static ElectricWireExtendRequest CreateIsolatedPlaceRequest(int playerId, BlockId poleBlockId, PlaceInfo polePlaceInfo)
        => new(ElectricWireExtendOperation.PlaceIsolatedPole, Vector3Int.zero, Vector3Int.zero, new PlaceInfoMessagePack(polePlaceInfo), playerId, poleBlockId.AsPrimitive(), Guid.Empty);
}

public enum ElectricWireExtendOperation
{
    ConnectToExisting,
    ExtendToNewPole,
    PlaceIsolatedPole,
}

[MessagePackObject]
public class ElectricWireExtendResponse : ProtocolMessagePackBase
{
    [Key(2)] public bool IsSuccess { get; set; }
    [Key(3)] public ElectricWirePlacementFailureReason FailureReason { get; set; }
    [Key(4)] public Vector3IntMessagePack EndpointPos { get; set; }
    [Key(5)] public int EndpointBlockInstanceId { get; set; }

    [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
    public ElectricWireExtendResponse() { Tag = ElectricWireExtendProtocol.Tag; }

    public static ElectricWireExtendResponse CreateSuccess(Vector3Int endpointPos, int endpointBlockInstanceId)
    {
        return new ElectricWireExtendResponse
        {
            Tag = ElectricWireExtendProtocol.Tag,
            IsSuccess = true,
            FailureReason = ElectricWirePlacementFailureReason.None,
            EndpointPos = new Vector3IntMessagePack(endpointPos),
            EndpointBlockInstanceId = endpointBlockInstanceId,
        };
    }

    public static ElectricWireExtendResponse CreateFailure(ElectricWirePlacementFailureReason failureReason)
    {
        return new ElectricWireExtendResponse
        {
            Tag = ElectricWireExtendProtocol.Tag,
            IsSuccess = false,
            FailureReason = failureReason,
            EndpointPos = new Vector3IntMessagePack(Vector3Int.zero),
            EndpointBlockInstanceId = 0,
        };
    }
}
```

enum `ElectricWireExtendOperation` は `ElectricWireExtendProtocol` クラス直下にネストする（既存 `WireEditMode` が `ElectricWireConnectionEditProtocol` クラス内ネストである前例に合わせる。上記コードブロックのenumはクラス内へ移して読むこと。参照は常に `ElectricWireExtendProtocol.ElectricWireExtendOperation`）。

`ExtendResult` 構造体は `Util/ElectricWire/ElectricWireExtendResult.cs` へ別ファイル分離する（`ElectricWireExtendService.cs` が現在229行で200行規則超過のため、本タスクで分離して規則内へ収める）。

`GetResponse` の呼び出しを更新:

```csharp
// 検証と設置・接続・消費をサービスに委ね、結果を応答へ変換する
// Delegate validation, placement, wiring and consumption to the service; map its result to a response
var result = ElectricWireExtendService.Execute(
    request.Operation, request.FromPosVector, request.ToPosVector, request.PolePlaceInfo,
    request.PlayerId, request.PoleBlockId, request.ConnectToolGuid);

return result.IsSuccess
    ? ElectricWireExtendResponse.CreateSuccess(result.EndpointPos, result.EndpointBlockInstanceId)
    : ElectricWireExtendResponse.CreateFailure(result.FailureReason);
```

- [ ] **Step 4: ElectricWireExtendService.ExecuteをOperation対応へ書き換える**

シグネチャと分岐を変更。既存の検証群（占有・ブロック解放・建設コスト）は電柱設置系Operationのみに適用する:

```csharp
public static ExtendResult Execute(ElectricWireExtendProtocol.ElectricWireExtendOperation operation, Vector3Int fromPos, Vector3Int toPos, PlaceInfoMessagePack polePlaceInfo, int playerId, BlockId poleBlockId, Guid connectToolGuid)
{
    var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

    // 既存ブロック接続は設置系検証を通らず既存utilに委ねる
    // ConnectToExisting skips placement validations and delegates to the existing util
    if (operation == ElectricWireExtendProtocol.ElectricWireExtendOperation.ConnectToExisting)
        return ExecuteConnectToExisting();

    // ...（既存の占有・解放・電柱パラメータ・建設コスト検証。hasFromConnector 条件は
    //     operation == ExtendToNewPole に読み替える）...

    return operation == ElectricWireExtendProtocol.ElectricWireExtendOperation.ExtendToNewPole
        ? ExecuteExtendWithOrigin()
        : ExecuteIsolatedPlace();

    #region Internal

    // 既存ブロック同士を接続し、成功時は接続先を終点として返す
    // Connect two existing blocks; on success return the target as the endpoint
    ExtendResult ExecuteConnectToExisting()
    {
        if (!ElectricWireSystemUtil.TryConnect(fromPos, toPos, playerId, connectToolGuid, out var failureReason))
            return ExtendResult.Failure(failureReason);

        // TryConnect成功直後なので終点コネクタは必ず解決できる
        // The endpoint connector always resolves right after a successful TryConnect
        ElectricWireSystemUtil.TryGetWireConnector(toPos, out var toConnector);
        return ExtendResult.Success(toPos, toConnector.BlockInstanceId.AsPrimitive());
    }

    // （ExecuteExtendWithOrigin / ExecuteIsolatedPlace は Task 1 の形を維持）

    #endregion
}
```

`ExtendResult` のフィールド名を `PlacedPolePos`→`EndpointPos`、`PlacedBlockInstanceId`→`EndpointBlockInstanceId` にリネームし、`Success(Vector3Int endpointPos, int endpointBlockInstanceId)` に合わせる。`using Server.Protocol.PacketResponse;` を追加（Operation enum参照のため。名前空間循環はない — ServiceはServer.Protocol.PacketResponse.Util配下で同一asmdef）。

connectTool 解放チェック（旧 `hasFromConnector &&`）は `ExtendToNewPole` のときのみ実施する（`ConnectToExisting` は `TryConnect` 内部で検証済み、`PlaceIsolatedPole` は電線を使わない）。

- [ ] **Step 5: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件（クライアント側の `ExtendElectricWire` 呼び出しは `CreateExtendRequest` シグネチャ不変・`response.IsSuccess` 参照のみのため、`PlacedBlockInstanceId`→`EndpointBlockInstanceId` の参照箇所 `ElectricWireExtendRequestSender.cs:58` と `VanillaApiWithResponse.cs:346` の `CreateIsolatedPlaceRequest` 引数変化があればこのステップで一緒に追随修正する。`ElectricWireExtendRequestSender.cs:58` の `response.PlacedBlockInstanceId` は `response.EndpointBlockInstanceId` へ置換）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireExtendProtocol"`
Expected: PASS（Task 1のテスト含め全件。`PlacedPolePos` 参照テストは `EndpointPos` へ追随済みであること）

- [ ] **Step 6: コミットする**

```bash
git add -A
git commit -m "feat: 電線接続をva:electricWireExtendのOperationへ統合し応答を終点InstanceIdに統一"
```

---

### Task 3: 切断プロトコルの専用化 — ElectricWireDisconnectProtocolへリネーム

**Files:**
- Rename: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireConnectionEditProtocol.cs` → `ElectricWireDisconnectProtocol.cs`（`git mv`、.metaも）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（登録タグ・クラス名更新。該当行は `ElectricWireConnectionEditProtocol.Tag` で検索）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs:169-183`
- Rename: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireConnectionEditProtocolTest.cs` → `ElectricWireDisconnectProtocolTest.cs`（`git mv`、.metaも）

**Interfaces:**
- Produces: `ElectricWireDisconnectProtocol`（Tag=`"va:electricWireDisconnect"`）、`ElectricWireDisconnectRequest.CreateDisconnectRequest(Vector3Int posA, Vector3Int posB, int playerId)`、`ElectricWireDisconnectResponse { IsSuccess, FailureReason }`
- Consumes: `ElectricWireSystemUtil.TryDisconnect`（不変）

- [ ] **Step 1: `git mv` で本体とテストをリネームする**

```bash
git mv moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireConnectionEditProtocol.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireDisconnectProtocol.cs
git mv moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireConnectionEditProtocol.cs.meta moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireDisconnectProtocol.cs.meta
git mv moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireConnectionEditProtocolTest.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireDisconnectProtocolTest.cs
git mv moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireConnectionEditProtocolTest.cs.meta moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireDisconnectProtocolTest.cs.meta
```

- [ ] **Step 2: プロトコルを切断専用へ書き換える**

クラス名 `ElectricWireDisconnectProtocol`、Tag `"va:electricWireDisconnect"`。`WireEditMode` enum・`Mode`フィールド・`ConnectToolGuid`フィールド・`CreateConnectRequest` を削除し、`GetResponse` は `TryDisconnect` 直呼びに簡素化:

```csharp
public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
{
    // 要求データをデシリアライズし切断を実行する
    // Deserialize the request and run the disconnect
    var request = MessagePackSerializer.Deserialize<ElectricWireDisconnectRequest>(payload);
    var success = ElectricWireSystemUtil.TryDisconnect(request.PosAVector, request.PosBVector, request.PlayerId, out var failureReason);
    return new ElectricWireDisconnectResponse(success, failureReason);
}
```

Request は `PosA`/`PosB`/`PlayerId`（Key(2)〜Key(4)）と `CreateDisconnectRequest` のみ。Response はクラス名変更のみで構造維持。

- [ ] **Step 3: 登録とクライアント送信を追随させる**

`PacketResponseCreator.cs` の登録行を `ElectricWireDisconnectProtocol.Tag, new ElectricWireDisconnectProtocol(serviceProvider)` へ変更。

`VanillaApiSendOnly.cs`: `ConnectElectricWire` メソッド（L169-176相当）を削除し、`DisconnectElectricWire` の Request 生成を `ElectricWireDisconnectProtocol.ElectricWireDisconnectRequest.CreateDisconnectRequest(posA, posB, _playerId)` へ変更。`ElectricWireExtendRequestSender.Connect`（クライアント）は Task 4 で extend 経由に置き換わるため、このタスク時点では `Connect` メソッドを一時的に `ClientContext.VanillaApi.SendOnly` から extend 応答版へ切り替えず、**コンパイルを通すために `ElectricWireExtendRequestSender.Connect` の中身を未送信化せず、`Response.ExtendElectricWire` ベースの暫定実装にはしない**。最小変更として `Connect` メソッドを削除し、呼び出し元 `ElectricWireExtendMode.cs:81` を `ElectricWireExtendRequestSender.Extend` と同じ fire-and-forget 形の暫定コード（`UniTask.Create` で `CreateConnectRequest` を送る。応答は無視）に置き換える。Task 4 で正式なチェーン実装に置き換わる。

- [ ] **Step 4: テストを書き換えて実行する**

`ElectricWireDisconnectProtocolTest.cs`: クラス名変更、Connect系テストを削除（接続はTask 2の `ElectricWireExtendProtocolTest` がカバー）、Disconnect系テストの Request 生成を新factoryへ変更。

Run: `uloop compile --project-path ./moorestech_client` → エラー0件
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireDisconnect|ElectricWireExtendProtocol"`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add -A
git commit -m "refactor: connectionEditプロトコルを切断専用のElectricWireDisconnectProtocolへ縮小"
```

---

### Task 4: クライアント — 送信・応答・起点チェーンの1系統化＋右クリックキャンセル

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs:337-348`
- Rewrite: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWireExtendRequestSender.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/ElectricWireConnectSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Modes/ElectricWireEditMode.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Modes/ElectricWireExtendMode.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWireToolContext.cs`

**Interfaces:**
- Consumes: Task 2 の `CreateConnectRequest`/`CreateExtendRequest`/`CreateIsolatedPlaceRequest`/`EndpointBlockInstanceId`
- Produces（Task 5が使う契約）:
  - `VanillaApiWithResponse.SendElectricWireExtend(ElectricWireExtendProtocol.ElectricWireExtendRequest request, CancellationToken ct)` — 1プロトコル=1メソッド
  - `ElectricWireExtendRequestSender`（インスタンス化）: `bool IsAwaitingResponse { get; }` / `void Invalidate()` / `bool TryConsumeEndpoint(out BlockGameObject endpointBlock)` / `void SendConnect(Vector3Int fromPos, Vector3Int toPos, Guid connectToolGuid)` / `void SendExtend(Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, Guid connectToolGuid)` / `void SendIsolatedPlace(BlockId poleBlockId, PlaceInfo polePlaceInfo)` / `static void Disconnect(Vector3Int posA, Vector3Int posB)`
  - `ElectricWireToolContext.RequestSender`（senderをcontext経由で両モードへ共有）

- [ ] **Step 1: VanillaApiを1メソッド化する**

`VanillaApiWithResponse.cs` の `ExtendElectricWire`（L337-348）を置換:

```csharp
public async UniTask<ElectricWireExtendProtocol.ElectricWireExtendResponse> SendElectricWireExtend(
    ElectricWireExtendProtocol.ElectricWireExtendRequest request, CancellationToken ct)
{
    return await _packetExchangeManager.GetPacketResponse<ElectricWireExtendProtocol.ElectricWireExtendResponse>(request, ct);
}
```

PlayerId は呼び出し側（sender）が `ClientContext.PlayerConnectionSetting.PlayerId` で解決して Request factory に渡す。

- [ ] **Step 2: RequestSenderをインスタンス型に書き換える**

`GearChainPoleExtendRequestSender` と同型の世代トークン方式。全置換:

```csharp
using System;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電線延長プロトコルの送信と、応答で確定した次起点（終点ブロック）の保持。
    /// コールバックは持たず、結果は上位がTryConsumeEndpointでループ先頭から取り込む一方向構造。
    /// Sends the electric wire extend protocol and holds the resolved next-origin endpoint from the response.
    /// No callbacks: the upper layer consumes the result via TryConsumeEndpoint at the top of its loop.
    /// </summary>
    public class ElectricWireExtendRequestSender
    {
        // 新規電柱のエンティティ生成を待つ上限秒数
        // Timeout seconds for waiting the new pole entity to spawn
        private const float EndpointSpawnWaitSeconds = 1f;

        private readonly BlockGameObjectDataStore _blockDataStore;

        // 応答待ち中の無効化・再送信で古い応答を捨てるための世代トークン
        // Generation token that discards stale responses across invalidation or re-sending
        private int _generation;
        private BlockGameObject _resolvedEndpoint;

        public bool IsAwaitingResponse { get; private set; }

        public ElectricWireExtendRequestSender(BlockGameObjectDataStore blockDataStore)
        {
            _blockDataStore = blockDataStore;
        }

        /// <summary>
        /// 進行中の応答と未取り込みの結果を無効化する（ツール無効化・起点解除時に呼ぶ）
        /// Invalidate pending responses and any unconsumed result (call on tool disable or origin release)
        /// </summary>
        public void Invalidate()
        {
            _generation++;
            IsAwaitingResponse = false;
            _resolvedEndpoint = null;
        }

        /// <summary>
        /// 応答で確定した次起点（終点ブロック）を一度だけ取り出す
        /// Consume the resolved next-origin endpoint from the response exactly once
        /// </summary>
        public bool TryConsumeEndpoint(out BlockGameObject endpointBlock)
        {
            endpointBlock = _resolvedEndpoint;
            _resolvedEndpoint = null;
            return endpointBlock != null;
        }

        public void SendConnect(Vector3Int fromPos, Vector3Int toPos, Guid connectToolGuid)
        {
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            Send(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateConnectRequest(playerId, fromPos, toPos, connectToolGuid));
        }

        public void SendExtend(Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, Guid connectToolGuid)
        {
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            Send(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateExtendRequest(playerId, fromPos, poleBlockId, polePlaceInfo, connectToolGuid));
        }

        public void SendIsolatedPlace(BlockId poleBlockId, PlaceInfo polePlaceInfo)
        {
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            Send(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateIsolatedPlaceRequest(playerId, poleBlockId, polePlaceInfo));
        }

        public static void Disconnect(Vector3Int posA, Vector3Int posB)
        {
            ClientContext.VanillaApi.SendOnly.DisconnectElectricWire(posA, posB);
        }

        private void Send(ElectricWireExtendProtocol.ElectricWireExtendRequest request)
        {
            var generation = ++_generation;
            IsAwaitingResponse = true;
            _resolvedEndpoint = null;

            UniTask.Create(async () =>
            {
                // 応答を待ち、成功時のみ終点ブロックの生成を待って次起点を解決する
                // Await the response, then resolve the next origin only on success
                var response = await ClientContext.VanillaApi.Response.SendElectricWireExtend(request, CancellationToken.None);
                var endpoint = response is { IsSuccess: true } ? await WaitForEndpoint(new BlockInstanceId(response.EndpointBlockInstanceId)) : null;

                // 世代が進んでいたら破棄済みの結果として捨てる
                // Discard the result when the generation has advanced
                if (generation != _generation) return;
                IsAwaitingResponse = false;
                _resolvedEndpoint = endpoint;
            });
        }

        private async UniTask<BlockGameObject> WaitForEndpoint(BlockInstanceId endpointId)
        {
            // 設置イベントの反映を待ってから終点GameObjectを解決する（既存終点なら即時解決）
            // Wait for the placement event to apply, then resolve the endpoint (existing endpoints resolve instantly)
            await UniTask.WhenAny(
                UniTask.WaitForSeconds(EndpointSpawnWaitSeconds),
                UniTask.WaitUntil(() => _blockDataStore.TryGetBlockGameObject(endpointId, out _)));

            return _blockDataStore.TryGetBlockGameObject(endpointId, out var endpointBlock) ? endpointBlock : null;
        }
    }
}
```

- [ ] **Step 3: Context・System・両モードを新senderへ配線する**

`ElectricWireToolContext`: `public readonly ElectricWireExtendRequestSender RequestSender;` を追加しコンストラクタで受け取る。

`ElectricWireConnectSystem`:
- コンストラクタで `var requestSender = new ElectricWireExtendRequestSender(blockGameObjectDataStore);` を生成しcontextへ渡す。`_toolEpoch` フィールドは削除（世代管理はsenderへ移譲）。
- `ManualUpdate` を置換:

```csharp
protected override void ManualUpdate(ConnectToolPlacementTarget target, bool isSelectionChanged)
{
    // 応答で確定した終点を取り込み、次の起点にする（チェーン）
    // Adopt the endpoint resolved from a response as the next origin (chaining)
    if (_context.RequestSender.TryConsumeEndpoint(out var endpointBlock)) _sourceBlock = endpointBlock;

    // 右クリックで起点を解除し、進行中の応答を無効化する
    // Release the origin on right click and invalidate any pending response
    if (_sourceBlock != null && InputManager.Playable.ScreenRightClick.GetKeyDown)
    {
        _sourceBlock = null;
        _context.RequestSender.Invalidate();
    }

    // 起点未選択なら選択・切断・孤立設置、選択済みなら接続・延長を処理する
    // No origin: select, disconnect or isolated-place; with origin: connect or extend
    if (_sourceBlock == null)
    {
        _sourceBlock = _editMode.Update(new PlaceSystemUpdateContext(target, isSelectionChanged));
        return;
    }

    _extendMode.Update(new PlaceSystemUpdateContext(target, isSelectionChanged), _sourceBlock);
}
```

- `Enable`/`Disable`: `_toolEpoch++` を `_context.RequestSender.Invalidate()` に置き換える。
- `using Client.Input;` を追加。

`ElectricWireExtendMode.Update`: 戻り値を `void` にし、送信は sender 経由・応答待ち中は送信しない:
- `ConnectToTarget` 内のクリック分岐を `if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi() && judgement.IsPlaceable && !_context.RequestSender.IsAwaitingResponse) _context.RequestSender.SendConnect(fromPos, toPos, connectToolGuid);` へ変更（コメント「起点は応答確認後に接続先へ移る / The origin moves to the target after the response confirms」）。
- `ExtendToEmptySpace` 内の送信を `_context.RequestSender.SendExtend(fromPos, poleBlockId, placeInfo, connectToolGuid);` へ変更し、`toolEpoch` 引数と `return true/false` を除去。送信後のプレビュー消灯は維持。

`ElectricWireEditMode.Update`: シグネチャを `Update(PlaceSystemUpdateContext ctx)` に変更（Task 5 で孤立設置分岐を追加するための土台。このタスクでは引数追加のみで中身は不変）。`Disconnect` 呼び出しは `ElectricWireExtendRequestSender.Disconnect`（static）のまま。

- [ ] **Step 4: コンパイル・既存テスト確認**

Run: `uloop compile --project-path ./moorestech_client` → エラー0件
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire"` → PASS

- [ ] **Step 5: コミットする**

```bash
git add -A moorestech_client
git commit -m "feat: 電線ツールの送信を応答付き1系統に統合し起点チェーンと右クリック解除を実装"
```

---

### Task 5: クライアント — 電柱種サイクル・回転・孤立設置UI

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleSelection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePoleGhostPart.cs`
- Modify: `ElectricWireEditMode.cs` / `ElectricWireExtendMode.cs` / `ElectricWireConnectSystem.cs` / `ElectricWireToolContext.cs`（前タスクと同ディレクトリ）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（`new ElectricWireConnectSystem(...)` に `IGameUnlockStateData` を追加。`GearChainPoleConnectSystem` へ渡している同じインスタンスを使う）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWirePoleSelectionTest.cs`（Create。ディレクトリも新設）

**Interfaces:**
- Consumes: Task 4 の `RequestSender.SendIsolatedPlace` / `IsAwaitingResponse`
- Produces:
  - `ElectricWirePoleSelection`: `void UpdateInput()`（スクロールでサイクル・回転キーで向き変更）/ `bool TryGetSelectedPole(out BlockId blockId, out BlockMasterElement blockMaster)` / `BlockDirection CurrentDirection { get; }` / `static IReadOnlyList<BlockId> ListUnlockedPoles(IGameUnlockStateData unlockState)`（SortPriority昇順・純関数、テスト対象）/ `void CycleNext()` `void CyclePrevious()`（純ロジック、テスト対象）
  - `ElectricWirePoleGhostPart`: `bool TryEvaluateGhost(ElectricWirePoleSelection selection, out PlaceInfo placeInfo, out BlockMasterElement poleMaster, out BlockId poleBlockId, out bool groundClear, out bool canAffordPole)` — レイキャスト→PlaceInfo計算→ゴースト表示→地面判定→建設コスト判定（`ExtendToEmptySpace` の該当部を抽出。向き・電柱種は selection から取得）

- [ ] **Step 1: 純ロジックの失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    public class ElectricWirePoleSelectionTest
    {
        [Test]
        public void サイクルは末尾の次に先頭へ戻り前送りは先頭の前に末尾へ回る()
        {
            // 3種の電柱リストでインデックスの循環だけを検証する
            // Verify index wrap-around with a three-pole list
            var poles = new List<BlockId> { new(1), new(2), new(3) };
            var selection = new ElectricWirePoleSelection(poles);

            Assert.AreEqual(new BlockId(1), selection.SelectedBlockId);
            selection.CycleNext();
            selection.CycleNext();
            selection.CycleNext();
            Assert.AreEqual(new BlockId(1), selection.SelectedBlockId);
            selection.CyclePrevious();
            Assert.AreEqual(new BlockId(3), selection.SelectedBlockId);
        }
    }
}
```

プロダクション側は `ElectricWirePoleSelection(IReadOnlyList<BlockId> unlockedPoles)` を通常コンストラクタとして持ち、毎フレームのリスト更新は保持側（system）が `RefreshUnlockedPoles(IGameUnlockStateData)` で行う設計にする。テスト専用のfactoryは作らない（「デバッグ/テスト専用publicをプロダクションに残さない」規約）。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWirePoleSelection"`
Expected: FAIL（クラス未定義のコンパイルエラー）

- [ ] **Step 2: ElectricWirePoleSelection を実装する**

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電線ツールで設置する電柱の種類と向きの選択状態。スクロールでサイクル、回転キーで向き変更する
    /// Selected pole type and direction for the wire tool; scroll cycles the type and the rotate key turns it
    /// </summary>
    public class ElectricWirePoleSelection
    {
        // スクロール1ノッチの閾値（ホットバーと同じスケールで読む）
        // Scroll threshold per notch, read at the hot bar's scale
        private const float ScrollThreshold = 0.5f;

        private IReadOnlyList<BlockId> _unlockedPoles;
        private int _selectedIndex;

        public BlockDirection CurrentDirection { get; private set; } = BlockDirection.North;
        public BlockId SelectedBlockId => _unlockedPoles[_selectedIndex];
        public bool HasSelectablePole => 0 < _unlockedPoles.Count;

        public ElectricWirePoleSelection(IReadOnlyList<BlockId> unlockedPoles)
        {
            _unlockedPoles = unlockedPoles;
        }

        /// <summary>
        /// 解放済み電柱リストを更新する。選択中の種が残っていれば選択を維持する
        /// Refresh the unlocked pole list, keeping the current selection when it survives
        /// </summary>
        public void RefreshUnlockedPoles(IGameUnlockStateData unlockState)
        {
            var previousSelected = HasSelectablePole ? SelectedBlockId : (BlockId?)null;
            _unlockedPoles = ListUnlockedPoles(unlockState);
            var index = previousSelected.HasValue ? IndexOf(previousSelected.Value) : 0;
            _selectedIndex = index < 0 ? 0 : index;
        }

        /// <summary>
        /// スクロールで種をサイクルし、回転キーで向きを変更する
        /// Cycle the type by scroll and rotate the direction by the rotate key
        /// </summary>
        public void UpdateInput()
        {
            var scroll = ReadScroll();
            if (ScrollThreshold < scroll) CycleNext();
            else if (scroll < -ScrollThreshold) CyclePrevious();

            // 通常設置と同じ回転キー（+Shiftで垂直回転）を適用する
            // Apply the same rotate key as normal placement (vertical with Shift)
            if (InputManager.Playable.BlockPlaceRotation.GetKeyDown)
                CurrentDirection = HybridInput.GetKey(KeyCode.LeftShift) ? CurrentDirection.VerticalRotation() : CurrentDirection.HorizonRotation();
        }

        public void CycleNext()
        {
            if (!HasSelectablePole) return;
            _selectedIndex = (_selectedIndex + 1) % _unlockedPoles.Count;
        }

        public void CyclePrevious()
        {
            if (!HasSelectablePole) return;
            _selectedIndex = (_selectedIndex - 1 + _unlockedPoles.Count) % _unlockedPoles.Count;
        }

        /// <summary>
        /// 解放済みElectricPoleブロックをSortPriority昇順で列挙する
        /// List unlocked ElectricPole blocks in ascending SortPriority
        /// </summary>
        public static IReadOnlyList<BlockId> ListUnlockedPoles(IGameUnlockStateData unlockState)
        {
            return MasterHolder.BlockMaster.Blocks.Data
                .Where(block => block.BlockType == BlockMasterElement.BlockTypeConst.ElectricPole)
                .Where(block => unlockState.BlockUnlockStateInfos.TryGetValue(block.BlockGuid, out var info) && info.IsUnlocked)
                .OrderBy(block => block.SortPriority ?? 0)
                .ThenBy(block => block.BlockGuid)
                .Select(block => MasterHolder.BlockMaster.GetBlockId(block.BlockGuid))
                .ToList();
        }

        private int IndexOf(BlockId blockId)
        {
            for (var i = 0; i < _unlockedPoles.Count; i++)
                if (_unlockedPoles[i] == blockId) return i;
            return -1;
        }

        private static float ReadScroll()
        {
            // InputSystemスクロールを読み、無ければlegacyへフォールバック（BlueprintCopySystemと同一）
            // Read Input System scroll with a legacy fallback, identical to BlueprintCopySystem
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 100f : UnityEngine.Input.mouseScrollDelta.y;
        }
    }
}
```

`TryGetSelectedPole(out BlockId blockId, out BlockMasterElement blockMaster)` も追加（`HasSelectablePole` 判定→`MasterHolder.BlockMaster.GetBlockMaster`）。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWirePoleSelection"` → PASS

- [ ] **Step 3: ゴースト共通部を抽出し、EditModeに孤立設置・ExtendModeにselection適用を実装する**

`ElectricWirePoleGhostPart`（新規）: `ExtendToEmptySpace` から以下を移設した1クラス。コンストラクタで `ElectricWireToolContext` と `CommonBlockPlacePointCalculator` を受け取る。

```csharp
/// <summary>
/// 電柱ゴーストの位置計算・表示・地面/建設コスト判定を行う共通部。延長設置と孤立設置で共有する
/// Shared pole-ghost logic (position, display, ground and cost checks) used by extend and isolated placement
/// </summary>
public class ElectricWirePoleGhostPart
{
    // ExtendToEmptySpaceのL87-119相当を移設:
    // TryGetSelectedPole→建設コスト判定→TryGetRayHitBlockPosition（direction=selection.CurrentDirection）
    // →ElectricPoleBlockParam検証→CalculatePoint→SetPreviewAndGroundDetect→groundClear算出
    // placeInfosはCalculatePointの結果リスト（UpdatePlaceableColorsへそのまま渡せる）。placeInfo = placeInfos[0]
    // placeInfos is the CalculatePoint result list (pass it straight to UpdatePlaceableColors); placeInfo = placeInfos[0]
    public bool TryEvaluateGhost(ElectricWirePoleSelection selection, out List<PlaceInfo> placeInfos, out PlaceInfo placeInfo, out BlockMasterElement poleMaster, out BlockId poleBlockId, out ElectricPoleBlockParam poleParam, out bool groundClear, out bool canAffordPole)
}
```

（実装は `ExtendToEmptySpace` の該当行を機械的に移し、`BlockDirection.North` 固定を `selection.CurrentDirection`、`ConnectToolCatalog.TryGetPlaceBlock` を `selection.TryGetSelectedPole` に置換する。選択中の電柱名ラベルはゴースト上に `TextMeshPro` で表示 — `ElectricWireExtendPreviewObject` のラベル生成と同じ形で `poleMaster.Name` を表示する）

`ElectricWireEditMode.Update`: 切断・起点選択のどちらにも該当しない場合の分岐を追加:

```csharp
// 何もない空間なら電柱の孤立設置ゴーストを表示し、クリックで設置する
// Over empty space, show the isolated pole ghost and place it on click
if (!_context.PoleGhostPart.TryEvaluateGhost(_context.PoleSelection, out var placeInfos, out var placeInfo, out _, out var poleBlockId, out _, out var groundClear, out var canAfford))
    return null;

var placeable = groundClear && canAfford;
placeInfo.Placeable = placeable;
_context.PreviewBlockController.UpdatePlaceableColors(placeInfos);

if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi() && placeable && !_context.RequestSender.IsAwaitingResponse)
{
    _context.RequestSender.SendIsolatedPlace(poleBlockId, placeInfo);
}
return null;
```

（注: 現行 `EditMode.Update` 冒頭の「ヒットなしなら何もしない」early return構造を、ブロック非ヒット時にゴースト表示へ落ちる構造に組み替える。クリック判定は分岐ごとに行う）

`ElectricWireExtendMode.ExtendToEmptySpace`: 抽出済み `PoleGhostPart`/`PoleSelection` を使う形に書き換え（ワイヤー判定・`WirePreview.Show`・`SendExtend` は現行のまま）。

`ElectricWireConnectSystem`: コンストラクタに `IGameUnlockStateData gameUnlockStateData` を追加し、`ElectricWirePoleSelection` を生成・保持。`RefreshUnlockedPoles` は毎フレームではなく `Enable()` 時と `isSelectionChanged` 時のみ呼ぶ（毎tickのLINQ再構築を避ける。解放イベント購読はYAGNIで見送り — ツール再選択で反映される）。`ManualUpdate` 冒頭で `_poleSelection.UpdateInput();` を呼ぶ。`ElectricWireToolContext` に `PoleSelection`/`PoleGhostPart` を追加。`MainGameStarter.cs` の `new ElectricWireConnectSystem(...)` に `GearChainPoleConnectSystem` と同じ `IGameUnlockStateData` インスタンスを渡す。

- [ ] **Step 4: 装備ホイール切替との衝突を抑止する（必須 — これが無いとスクロール1ノッチ目でツールが外れる）**

装備の循環選択はWebUI側 `moorestech_web/webui/src/features/inventory/EquipmentPanel/index.tsx` の `useGameLayerWheel` が担っており、接続ツール使用中もホイールで装備が切り替わる。装備が切り替わると `PlaceSystemSelector` 経由で `ElectricWireConnectSystem` がDisableされ、電柱種サイクルが機能しない。

`EquipmentPanel` の `useGameLayerWheel` ハンドラ冒頭に抑止を追加:

```tsx
// ホイールを占有する建築ツール（接続ツールの電柱種サイクル・BPコピーの高さ変更）中は装備切替へ流さない
// Build tools that own the wheel (connect-tool pole cycling, blueprint-copy height) suppress equipment switching
const placementMode = readTopic(Topics.placementMode);
if (placementMode?.selectedTargetType === "connectTool" || placementMode?.selectedTargetType === "blueprintCopy") return;
```

（`ui.placement_mode` トピックは既存で `connectTool`/`blueprintCopy` バリアントを持つ — `bridge/contract/schemas/ui.ts:36-63`。新規配線は不要。blueprintCopy側は既存衝突「高さ変更スクロールと装備切替の同時発火」の修正を兼ねる — ユーザー裁定 2026-08-05）。WebUI側テスト: `EquipmentPanel` の既存テストファイルに「placementModeがconnectTool/blueprintCopyのときwheelで装備が切り替わらない」ケースを追加する（既存のwheelテストと同じモック形式に従う）。webui変更のため `webui-design` スキルの様式確認を実装時に行うこと。

- [ ] **Step 5: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0件
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire"` → PASS
Run: webuiのテスト（`moorestech_web/webui` で `npm test` 相当。既存のテスト実行方法に従う）→ PASS

```bash
git add -A moorestech_client moorestech_web
git commit -m "feat: 電線ツールに電柱孤立設置・種サイクル・回転を実装し装備ホイールとの衝突を抑止"
```

---

### Task 6: クライアント — プレビュー端点を実描画と統一（Resolver新設）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/ElectricWireEndpointResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/ElectricWire/ElectricWireLineViewElement.cs:94-104`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/IPlacementPreviewBlockGameObjectController.cs` / `PlacementPreviewBlockGameObjectController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWireExtendPreviewObject.cs`
- Modify: `ElectricWireExtendMode.cs`（呼び出し側の端点解決）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/AutoConnectWirePreviewRenderer.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ElectricWireAutoConnectPreview.cs`

**Interfaces:**
- Produces:
  - `ElectricWireEndpointResolver.Resolve(BlockGameObject block) : Vector3`（マーカー→上面中央）
  - `ElectricWireEndpointResolver.ResolveFromGhost(BlockPreviewObject ghost, PlaceInfo placeInfo, BlockMasterElement blockMaster) : Vector3`（ゴースト子のマーカー→ゴーストAABB上面中央）
  - `IPlacementPreviewBlockGameObjectController.TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)`（ドメイン非依存の汎用アクセサ）
  - `ElectricWireExtendPreviewObject.Show(Vector3 startWorldPos, Vector3 endWorldPos, bool placeable, int wireCostCount)`（Vector3Int+固定オフセット→解決済みワールド座標へ変更）
  - `AutoConnectWirePreviewRenderer.Show(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, int totalWireCost)`

- [ ] **Step 1: Resolverを新設し実描画をリファクタする**

```csharp
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire
{
    /// <summary>
    /// 電線の端点座標を解決する唯一の正。実描画・全プレビューがこれを共有する
    /// The single source of truth for wire endpoint positions, shared by rendering and all previews
    /// </summary>
    public static class ElectricWireEndpointResolver
    {
        /// <summary>
        /// 専用接続点があればそこへ、無ければブロック上面中央へ接続する
        /// Connect to the dedicated point when present, otherwise to the block top center
        /// </summary>
        public static Vector3 Resolve(BlockGameObject block)
        {
            var connectionPoint = block.GetComponentInChildren<ElectricWireConnectionPoint>(true);
            if (connectionPoint != null) return connectionPoint.transform.position;

            var min = block.BlockPosInfo.MinPos;
            var max = block.BlockPosInfo.MaxPos + Vector3Int.one;
            return new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f);
        }

        /// <summary>
        /// 未設置ゴーストの端点を解決する。ゴースト内のマーカー→無ければ設置予定AABBの上面中央
        /// Resolve an unplaced ghost's endpoint: the marker inside the ghost, else the planned AABB top center
        /// </summary>
        public static Vector3 ResolveFromGhost(BlockPreviewObject ghost, PlaceInfo placeInfo, BlockMasterElement blockMaster)
        {
            var connectionPoint = ghost.GetComponentInChildren<ElectricWireConnectionPoint>(true);
            if (connectionPoint != null) return connectionPoint.transform.position;

            var ghostInfo = new BlockPositionInfo(placeInfo.Position, placeInfo.Direction, blockMaster.BlockSize);
            var min = ghostInfo.MinPos;
            var max = ghostInfo.MaxPos + Vector3Int.one;
            return new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f);
        }
    }
}
```

`ElectricWireLineViewElement.TryBuildLine` のローカル関数 `ResolveEndpoint` を削除し、`var start = ElectricWireEndpointResolver.Resolve(fromBlock); var end = ElectricWireEndpointResolver.Resolve(toBlock);` に置換。

`IPlacementPreviewBlockGameObjectController` にアクセサを追加し、`PlacementPreviewBlockGameObjectController` で実装:

```csharp
public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)
{
    // アクティブなプレビューブロックをインデックスで取り出す（SetPreviewAndGroundDetectの順序と一致）
    // Fetch an active preview block by index, matching SetPreviewAndGroundDetect ordering
    previewBlock = index < _activePreviewBlocks.Count ? _activePreviewBlocks[index] : null;
    return previewBlock != null;
}
```

（確認済み: `BlockPreviewObject` は本番プレハブ複製で生成されるため `ElectricWireConnectionPoint` マーカーを含む — `PreviewBlockCreator.cs:12-15`。AABB上面中央フォールバックはマーカーを持たないブロック用）

- [ ] **Step 2: 電線ツールプレビューの端点を差し替える**

`ElectricWireExtendPreviewObject.Show` のシグネチャを `Show(Vector3 startWorldPos, Vector3 endWorldPos, bool placeable, int wireCostCount)` に変更し、`BlockCenterOffset` フィールドと加算を削除（`start`/`end` はそのまま使用）。コメント「描画設定は本描画（Task10）と揃えて」を「端点・カテナリーとも実描画（ElectricWireLineViewElement）と同一計算 / Endpoints and catenary match the actual rendering」に更新。

`ElectricWireExtendMode` の呼び出し側:
- `ConnectToTarget`: `_context.WirePreview.Show(ElectricWireEndpointResolver.Resolve(source), ElectricWireEndpointResolver.Resolve(targetBlock), judgement.IsPlaceable, ...)`
- `ExtendToEmptySpace`: ゴースト端点は `_context.PreviewBlockController.TryGetPreviewBlock(0, out var ghost)` → `ElectricWireEndpointResolver.ResolveFromGhost(ghost, placeInfo, poleMaster)`、起点端点は `Resolve(source)`。
- `EditMode` の孤立設置ゴースト（Task 5）はワイヤー線を表示しないため変更不要。

- [ ] **Step 3: 自動接続プレビューの端点を差し替える**

`AutoConnectWirePreviewRenderer.Show` を `Show(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, int totalWireCost)` に変更し、`BlockCenterOffset` を削除。`WireLine.Draw` は変更不要。

`ElectricWireAutoConnectPreview`:
- `ResolveTargetPositions` を `List<Vector3>` を返す形へ変更し、各ターゲットの `TargetPos`（ブロック座標）を `_blockDataStore.TryGetBlockGameObject(Vector3Int, out BlockGameObject)`（`BlockGameObjectDataStore.cs:42` に実在確認済み）で解決し `ElectricWireEndpointResolver.Resolve` を通す。
- 起点（設置予定ブロック自身）の端点は `cursorInfo` のインデックス（`placeInfos.IndexOf(cursorInfo)`）で `TryGetPreviewBlock` からゴーストを取り `ResolveFromGhost`。取得失敗時はAABB上面中央（`ResolveFromGhost` のフォールバックと同式）。

- [ ] **Step 4: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0件
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire|CatenaryWire"` → PASS

```bash
git add -A moorestech_client
git commit -m "feat: 電線プレビューの端点解決を実描画と共通のResolverへ統一"
```

---

### Task 7: クライアント — 解放フィルタ修正＋拒否理由表示

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/ElectricWireConnect/Parts/ElectricWirePlacementFailureText.cs`
- Modify: `ElectricWireAutoConnectPreview.cs` / `AutoConnectWirePreviewRenderer.cs` / `CommonBlockPlaceSystem.cs` / `MainGameStarter.cs`
- Modify: `ElectricWireExtendPreviewObject.cs` / `ElectricWireExtendMode.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ElectricWireConnect/ElectricWirePlacementFailureTextTest.cs`（Create）

**Interfaces:**
- Consumes: `ElectricWirePlacementFailureReason`（サーバー共有enum）、Task 6 の renderer シグネチャ
- Produces:
  - `ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason reason) : string`
  - `ElectricWireExtendPreviewObject.Show(Vector3 start, Vector3 end, bool placeable, int wireCostCount, string failureText)`（不可時にラベルへ理由を併記）
  - `AutoConnectWirePreviewRenderer.Show(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, int totalWireCost, string noticeText, bool isFailure)`（noticeText非空ならコスト表示の代わりに表示。isFailure=trueは不可色、falseは通常色の情報表示）
  - `ClientElectricWireAutoConnectCollector.ExistsOutOfRangeElectricNeighbor(Vector3Int position, BlockGameObjectDataStore blockDataStore, int inRangeTargetCount) : bool`

- [ ] **Step 1: 文言変換の失敗するテストを書く**

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    public class ElectricWirePlacementFailureTextTest
    {
        [Test]
        public void 主要な失敗理由が個別の文言に変換される()
        {
            Assert.AreEqual("接続範囲外です", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.OutOfRange));
            Assert.AreEqual("電線が足りません", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.NoWireItem));
            Assert.AreEqual("接続上限です", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.ConnectionLimit));
            Assert.AreEqual("接続済みです", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.AlreadyConnected));
            Assert.AreEqual(string.Empty, ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.None));
        }
    }
}
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWirePlacementFailureText"` → FAIL

- [ ] **Step 2: 変換クラスを実装する**

```csharp
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// ワイヤー設置失敗理由をプレビュー表示用の文言へ変換する
    /// Convert wire placement failure reasons into preview label text
    /// </summary>
    public static class ElectricWirePlacementFailureText
    {
        public static string ToText(ElectricWirePlacementFailureReason reason)
        {
            return reason switch
            {
                ElectricWirePlacementFailureReason.None => string.Empty,
                ElectricWirePlacementFailureReason.OutOfRange => "接続範囲外です",
                ElectricWirePlacementFailureReason.AlreadyConnected => "接続済みです",
                ElectricWirePlacementFailureReason.ConnectionLimit => "接続上限です",
                ElectricWirePlacementFailureReason.NoWireItem => "電線が足りません",
                ElectricWirePlacementFailureReason.NoPoleItem => "電柱が足りません",
                ElectricWirePlacementFailureReason.InvalidTarget => "接続できない対象です",
                ElectricWirePlacementFailureReason.PositionOccupied => "設置位置が埋まっています",
                ElectricWirePlacementFailureReason.InventoryFull => "インベントリがいっぱいです",
                ElectricWirePlacementFailureReason.NotConnected => "接続されていません",
                ElectricWirePlacementFailureReason.NotUnlocked => "未解放です",
                ElectricWirePlacementFailureReason.InsufficientItems => "素材が足りません",
                _ => "設置できません",
            };
        }
    }
}
```

Run: 同regex → PASS

- [ ] **Step 3: 電線ツールプレビューへ理由表示を配線する**

`ElectricWireExtendPreviewObject.Show` に `string failureText` 引数を追加（デフォルト引数禁止のため全呼び出し側を更新）。`UpdateCostLabel` を変更: `wireCostCount <= 0 && failureText空` のときのみ非表示、テキストは placeable なら `$"電線 x{wireCostCount}"`、不可なら `$"電線 x{wireCostCount}\n{failureText}"`（コスト0なら理由のみ）。

`ElectricWireExtendMode`: `judgement.FailureReason` を `ElectricWirePlacementFailureText.ToText` で変換して渡す。`canAffordPole` 不成立時は `InsufficientItems` 相当の文言 `"素材が足りません"` を渡す（judgement成功でも `placeable=false` になるケース）。

- [ ] **Step 4: 通常設置プレビューへ解放フィルタと理由表示を配線する**

`CommonBlockPlaceSystem` コンストラクタに `IGameUnlockStateData gameUnlockStateData` を追加し `ElectricWireAutoConnectPreview` へ引き渡す。`MainGameStarter.cs` の `new CommonBlockPlaceSystem(...)` に `GearChainPoleConnectSystem` と同じインスタンスを渡す。

`ElectricWireAutoConnectPreview`:
- コンストラクタで `IGameUnlockStateData` を保持。
- `TrySelectConnectTool` の electricWire 収集ループに解放フィルタを追加:

```csharp
foreach (var element in MasterHolder.ConnectToolMaster.All)
{
    if (element.ToolType != ConnectToolMasterElement.ToolTypeConst.electricWire) continue;
    // サーバーのConnectToolSelector.UnlockedByToolTypeと同じ解放済みフィルタを適用する
    // Apply the same unlocked filter as the server's ConnectToolSelector.UnlockedByToolType
    if (!_gameUnlockStateData.ConnectToolUnlockStateInfos.TryGetValue(element.ConnectToolGuid, out var info) || !info.IsUnlocked) continue;
    electricWireTools.Add(element);
}
```

（解放済みが0件なら現行どおり `true`（配線なし設置可）— サーバーの `unlockedTools.Count == 0` 分岐と一致する）
- カーソルセルの `wirePlaceable` が false のとき `renderer.Show` へ `ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.NoWireItem)` を `isFailure: true` で渡す（自動接続プレビューの拒否理由は電線不足のみ。範囲外は拒否されず次ステップの情報表示で扱う）。

`AutoConnectWirePreviewRenderer.Show` に `string noticeText, bool isFailure` を追加。noticeText非空ならコストラベルの代わりに表示し、isFailure=trueは `WithAlpha(MaterialConst.NotPlaceableColor)`＋ワイヤー線も不可色（`WireLine` にマテリアル色切替を追加）、falseは既存の `WithAlpha(MaterialConst.PlaceableColor)` の情報表示。

- [ ] **Step 4.5: 範囲外で配線されない場合の情報表示を追加する**

`ClientElectricWireAutoConnectCollector` に近傍判定を追加（列挙元は `Collect` の `BuildReceivedCandidates` と同じ受信ブロック辞書）:

```csharp
// 情報表示用の近傍探索半径。これ以内に電気ブロックがあるのに1件も配線されないとき「範囲外」と案内する
// Neighbor search radius for the info label; electric blocks within it but none connectable means "out of range"
private const float InfoSearchRadius = 32f;

/// <summary>
/// 設置セル近傍に電気ブロックはあるが接続範囲外で1件も配線されない状況かを判定する
/// Judge whether electric blocks exist near the cell while none are wire-connectable
/// </summary>
public static bool ExistsOutOfRangeElectricNeighbor(Vector3Int position, BlockGameObjectDataStore blockDataStore, int inRangeTargetCount)
{
    if (0 < inRangeTargetCount) return false;
    foreach (var block in blockDataStore.BlockGameObjectByInstanceIdDictionary.Values)
    {
        if (!block.TryGetComponent<ElectricWireStateChangeProcessor>(out _)) continue;
        if (Vector3Int.Distance(block.BlockPosInfo.OriginalPos, position) <= InfoSearchRadius) return true;
    }
    return false;
}
```

`ElectricWireAutoConnectPreview.ApplyAutoConnect`: カーソルセルが `wirePlaceable` かつ `cursorTargets.Count == 0` かつ `ExistsOutOfRangeElectricNeighbor(cursorInfo.Position, _blockDataStore, cursorTargets.Count)` のとき、`renderer.Show(originEndpoint, cursorTargets, 0, "接続範囲外のため配線されません", isFailure: false)` を呼ぶ（設置は許可のまま。サーバーロジック不変 — 表示だけの追加。出所: ユーザー裁定 2026-08-05・シミュレーター予測→ユーザー承認）。

- [ ] **Step 5: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0件
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire"` → PASS

```bash
git add -A moorestech_client
git commit -m "fix: 自動接続プレビューへ解放フィルタを適用し設置不可理由をラベル表示"
```

---

### Task 8: 最終確認 — 全テスト・moores-code-review

- [ ] **Step 1: 電線関連の全テストを流す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire|ElectricPole|ConnectTool"`
Expected: PASS（全件）

- [ ] **Step 1.5: 実機（PlayMode）で受け入れ基準を確認する**

`uloop-control-play-mode` でPlayModeに入り、以下を実際に操作して確認する（uloop-screenshot / uloop-get-logs 併用。目視系の受け入れ基準はユニットテストで担保できないため必須）:
1. 電線ツールで電柱に起点を取り、別電柱へのプレビュー線が電柱先端（接続点マーカー）から出ること
2. 既存ブロックへ接続→次のプレビュー起点が接続先へ移ること
3. 起点なし空間クリックで電柱が単体設置され（接続0本）、それが起点になること
4. スクロールで電柱種が切り替わり、装備が切り替わらないこと。回転キーで向きが変わること
5. 右クリックで起点が解除されること
6. 電線を持たずに通常設置で電柱を既存設備近くへ置こうとすると「電線が足りません」が表示され設置されないこと

- [ ] **Step 2: 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

指摘の機械的修正を適用し、設計判断はAskUserQuestionでまとめて仰ぐ。

- [ ] **Step 3: 最終コミット**

```bash
git add -A
git commit -m "chore: 電柱接続改修のレビュー反映"
```

---

## 判断記録（ADR）

設計セッションの正: `docs/adr/0008-electric-wire-tool-unified-extend-and-explicit-wiring.md`
関連裁定: `.decisions/2026-08-05-電線プレビューは端点もカテナリーも実描画と統一する.md` / `2026-08-05-電線接続の起点チェーンは応答確認後に行う.md` / `2026-08-05-電線接続はextendプロトコルへ統合しconnectionEditは切断専用にする.md` / `2026-08-05-電線ツールは起点なし空間クリックで電柱を孤立設置し新電柱を起点にする.md` / `2026-08-05-電線ツールの孤立設置は自動接続を行わない.md` / `2026-08-05-電線ツール経由の設置は周辺自動配線を行わず明示の1本のみ.md` / `2026-08-05-通常設置の自動接続ゲートは維持し拒否理由をtooltip表示する.md` / `2026-08-05-電柱接続改修のスコープは隣接ギャップ3件も含める.md` / `2026-08-05-電線ツールの電柱種はキー・スクロールでサイクル選択する.md`

Planning中に新たに生じた判断:

- **connectionEditプロトコルを`ElectricWireDisconnectProtocol`（Tag: `va:electricWireDisconnect`）へリネームする** — Connect廃止後の実処理は切断のみのため。出所: agent前提（「名前は実処理と一致させる」規約）
- **RequestSenderをstaticからインスタンス型へ変更し世代トークン方式にする** — チェーンの応答管理が3 Operationに広がるため。出所: agent前提（`GearChainPoleExtendRequestSender` 同役割前例。置換対象の static+epoch 方式から前例の世代トークン方式へ揃える機構変更であり、両者は同目的（stale応答破棄）の同型機構）
- **ゴーストGameObjectアクセサ `TryGetPreviewBlock(int, out BlockPreviewObject)` を汎用プレビュー基盤へ追加する** — 電線マーカー探索は電線側コードが行い、基盤はドメイン非依存を保つ。出所: agent前提（「汎用基盤にドメイン語彙を持ち込まない」原則）
- **拒否理由表示はワールド空間ラベル直書き日本語（既存 `"電線 x{n}"` と同形）で行い、ローカライズキー化しない** — 既存プレビューラベルの前例踏襲。出所: agent前提（`ElectricWireExtendPreviewObject` 前例）
- **電柱種サイクルのスクロール読取は `Mouse.current.scroll`→legacyフォールバック** — 出所: agent前提（`BlueprintCopySystem` 前例）
- **孤立設置Requestから `ConnectToolGuid` を削除** — 真孤立化により電線を使わないため。出所: agent前提（裁定「自動接続を行わない」の帰結）
- **応答フィールドを `PlacedPolePos`/`PlacedBlockInstanceId` から `EndpointPos`/`EndpointBlockInstanceId` へリネーム** — 接続Operationでは設置が発生しないため。出所: agent前提（「名前は実処理と一致させる」規約）
- **接続ツール/BPコピー使用中はWebUI装備ホイール切替を抑止する** — 電柱種サイクルのスクロールが1ノッチでツール解除される衝突の回避。BPコピーの既存同種衝突も同ゲートで修正。出所: シミュレーター予測→ユーザー承認 2026-08-05（`.decisions/2026-08-05-建築ツールのホイール占有は接続ツールとBPコピー両方で装備切替を抑止する.md`）
- **通常設置で範囲外により配線されない場合の情報表示を追加する** — クライアント側近傍探索（半径32）で判定し「接続範囲外のため配線されません」を通常色表示。サーバー不変。出所: シミュレーター予測→ユーザー承認 2026-08-05（`.decisions/2026-08-05-通常設置は範囲外で配線されない場合も情報表示する.md`）

**判断台帳掲載義務対象**: 本planの `Modify:`/`Create:` にはサーバープロトコル・PlaceSystem系（moores-code-reviewレンズのpaths発火対象）が含まれる。上記の裁定リンクと判断記録がその掲載にあたる。
