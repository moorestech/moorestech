# ベルコン以外の搬出元撤去による内部segment返却 Implementation Plan

> **For the controller session:** moores-subagent-driven-developmentの単一subagent実装モードでTask 1〜2を実行する。末尾のレビュー・PR更新はcontrollerが担う。

**Goal:** チェスト・駅などベルコン以外の搬出元を手動撤去するとき、失われる内部segmentの中身を手持ちへ返す。
**Architecture:** 搬送Datastoreの読み取り契約で追加返却物を計算し、RemoveBlockProtocolの既存容量確認へ合算する。撤去確定の既存イベントで消失対象キーを記録し、返却・保存・再構築snapshotから除外する。live Assemblyは次tickの再構築まで保持し、内部segmentの保存上の所有者は合流blockのまま保つ。
**Tech Stack:** C# / Unity / NUnit / UniRx / MessagePack。

## Requirements

- ベルコン以外の搬出元はブロック名によらず接続情報で扱う。チェストと異なる種類の機械または駅のfixtureで確認する。
- 消える内部segment全てを返却する。2→1入力では合流解消に伴う別入力側も返し、3→2入力では存続する側を返さない。
- 一つの搬出元から複数の合流へ出す場合、全ての対象を一度ずつ返却する。
- 通常インベントリ・建設費・レール等との合計が入らない場合は撤去を拒否し、搬送・pending保存・手持ちを変えない。空きを作った再試行は成功する。
- 同tickで搬出元→合流、合流→搬出元、複数搬出元を連続撤去しても重複や消失を起こさない。次tickの再構築後も総量を保持する。
- 搬送通知後の撤去で境界ハッシュと同tick全量を変えない。消失対象は返却・保存から即座に除外し、物理的には次tickの再構築で消費する。
- 実状態のItemInstanceIdを保持する。pendingロードは既存converterの検証とID生成規則を使い、実状態と重複計上しない。
- pendingロード後の初回rebuild前にも動く。成功時は対象internal項目だけを消費し、running/buffer/priority/存続する内部方向を保つ。
- 既存の通常・歯車ベルト撤去を回帰確認する。セーブ形式・ベルト接続構築・物理搬送・描画の変更はしない。
- 現在の `fix/belt-removal-refund` の追加修正としてコミットし、既存PRを更新する。

## Global Constraints

- AGENTS.mdの200行/ファイル・10コードファイル/ディレクトリ・日英コメント・Func/partial禁止を守る。
- `.meta` は手書きしない。Unityの自動生成を待つ。Unity固有YAMLは編集しない。
- ユーザーのUnityはLibrary再生成中。終了・Library操作を行わず、隔離worktreeでソース編集し、検証は再生成終了後の既存Editorで行う。
- 本体に元からある `moorestech_client/.uloop/project-runner-pin.json` と `TrainUnitNextBranchFinder.cs.meta` は本作業の変更に混ぜない。
- 差分の土台は `189fbfbed`（ユーザー指定の既存ブランチ）。origin/masterは7コミット進んでいるため、今回の実装に無関係なmaster変更を混ぜずPR更新時に競合を確認する。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・具体性補足1件 / 全体撤去イベントは座標イベント・Destroyより前
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 発火なし

## 配置と前例

| 役割 | 配置 | 前例と主体 |
|---|---|---|
| 内部segmentの消失判定と中身列挙 | Game.Block/Blocks/BeltConveyor/Transport/Removal | 既存Datastoreが所有する搬送状態をGetRefundItemsで読み出す |
| 追加返却物の問い合わせ | IBeltTransportLookup | RailRemovalRefundCalculatorと同じくprotocolは追加返却物を合算するだけ |
| 撤去時の搬送状態更新 | BeltTransportDatastoreのOnBlockRemoveEvent購読 | 既存の同Datastore購読が構成変更を検知する。WorldBlockDatastoreはDestroy前に発火する |
| pendingの検証 | BeltTransportLoadedStateConverter | 既存GetRefundItemsと同じくowner単位でpendingを優先する |

既存機構はそのまま駆動し、返却照会と成功イベントの状態反映を拡張する。別の搬送状態・保存形式・通信経路は作らない。

| 操作 | 変更後 |
|---|---|
| ベルト本体撤去 | 従来の所有マス返却を維持 |
| ベルコン以外の手動撤去 | 消失する内部segment分を加算 |
| 手持ち満杯で撤去 | 全量容量確認で拒否し状態保持 |
| 自動破壊 | プレイヤーへの返却は増やさず、消える内部状態だけ整理 |
| セーブ/ロード | 同じ保存形式で未返却分だけ保存 |
| tick同期 | 既存dirty/rebuild→全量通知の順序を維持 |

### Task 1: 搬出元撤去の追加返却と成功時の消費を実装する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/Transport/BeltTransportDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/Transport/IBeltTransportLookup.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/Transport/Removal/BeltConnectionRemovalRefund.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/Transport/Removal/BeltOwnedItemRefund.cs`（既存列挙の抽出でDatastoreを200行以内に保つ）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltRemoval/BeltConnectionRemovalRefundTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltRemoval/BeltConnectionRemovalRefundStateTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltRemoval/BeltConnectionRemovalRefundSyncTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltRemoval/BeltConnectionRemovalRefundPendingTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltRemoval/BeltRemovalRefundTestWorld.cs`

**Interfaces:**
- Produces: `IBeltTransportLookup.GetConnectionRemovalRefundItems(BlockInstanceId blockInstanceId)` returns `IReadOnlyList<IItemStack>`.
- Consumes: `BeltTopologyBuilder.Build(world)`, `BeltSegmentLayoutBuilder.Build(cells)`, `BeltTransportSnapshot.Capture(assembly)`, `BeltTransportLoadedStateConverter.Convert`, existing removal event.
- Helper types are internal; protocol sees only the refund list. Existing owned-item lookup is retained.

- [ ] **Step 1: 本番プロトコルを通す再現fixtureを書く。**
  `creating-server-tests`を読み、既存BeltRemovalRefundTestWorldとMachinePortTestTemplateを使う。2入力合流の内部segmentに1個置き、非belt sourceを撤去する。旧コードなら返却0・次tickの搬送0となることを証拠に残す。通常インベントリが空でも追加返却が発生することを確認する。

```csharp
var item = BeltRemovalRefundTestWorld.Item();
internalSegment.RestoreItems(new[] { new BeltItemState(item, 100) });
Assert.IsTrue(world.Remove(sourcePosition).Success);
Assert.AreEqual(1, world.Count(item.ItemId));
GameUpdater.UpdateOneTick();
Assert.AreEqual(1, world.Count(item.ItemId));
```

- [ ] **Step 2: 返却判定を搬送側へ追加する。**
  現在ワールドからlayoutを計算し、対象非beltを入力に持つ合流を調べる。削除対象IDを除く入力数が2未満ならその合流の全internal方向、そうでなければ削除IDからのinternal方向だけを対象とする。現在layoutに存在するキーだけを選び、既に消えた接続を再び返さない。複数合流を全数走査する。非belt判定は既存IBeltSpeedParamの分類に従う。

```csharp
// IBeltTransportLookup
IReadOnlyList<IItemStack> GetConnectionRemovalRefundItems(BlockInstanceId blockInstanceId);
// RemoveBlockProtocol.GetRefundItems: 種類や合流の判定は行わない
result.AddRange(_beltTransportLookup.GetConnectionRemovalRefundItems(block.BlockInstanceId));
```

  選ばれた `(MergeBlockInstanceId, InputDirection)` でsnapshot.InternalItemsを読む。返却照会・再構築とも、pending所有者はassemblyの同所有者を走行・buffer・内部区間の全カテゴリで除き、converter結果を使う。Itemsは既存ItemStackFactoryで1個ずつ生成する。読み取りでは再構築・消費・cache更新をしない。

```csharp
// internal helper: GetRemovedInputs(IBlock block, IWorldBlockDatastore world)
// returns HashSet<(BlockInstanceId MergeBlock, BeltDirection Direction)>
var layouts = BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(world));
foreach (var layout in layouts)
{
    if (!layout.IsInternal) continue;
    var merge = layouts[layout.Outputs[0].PartnerSegmentIndex];
    var survivingInputs = 0;
    var removesInput = false;
    foreach (var input in merge.Inputs)
        if (input.Connection.PartnerBlock.BlockInstanceId == block.BlockInstanceId) removesInput = true;
        else survivingInputs++;
    if (removesInput && (survivingInputs < 2 || layout.Inputs[0].Connection.PartnerBlock.BlockInstanceId == block.BlockInstanceId))
        removedInputs.Add((merge.Cells[0].BlockInstanceId, layout.Inputs[0].Direction));
}
```

- [ ] **Step 3: 成功した撤去のイベントで消失対象を記録する。**
  既存OnBlockRemove購読を専用privateメソッドへ繋ぐ。イベントはworldからの削除前に発火するため、対象IDを除く予測で同じキー集合を算出する。Datastoreが対象キーを次tickの再構築まで保持し、live snapshotの返却・保存・復元対象から除外する。pendingは該当internal方向を除いた保存内容へ置き換える。pendingの入力オブジェクトを破壊せず、他の保存項目を保持する。`_capturedSaveStates`を無効化し、既存通りdirtyにする。原子的な同期protocol内では照会からイベントまでtickを挟まない。

```csharp
worldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(properties => OnBlockRemoved(properties.BlockData.Block));
// 成功イベントで除外キーを記録し、物理状態は次tickまで保つ
_removedInternalInputs.UnionWith(removedInputs);
_capturedSaveStates = null;
MarkDirtyIfAffectsLayout(block);
```

  `CaptureRemainingItems` は現Assemblyのsnapshotから記録済み内部方向を除く。所有物返却・接続撤去返却・保存・次回再構築がこれを使用する。次tick先頭でこのsnapshotから新Assemblyへ復元し、除外記録をクリアする。搬送通知から境界ハッシュまでの物理状態は同一に保ち、既存の全量/差分同期経路を使う。

```csharp
// pending owner全体をconverterで検証した後、成功時のみ対象方向を除く
var remaining = new List<BeltInternalItemSaveJsonObject>();
foreach (var saved in pending.InternalItems)
    if (!removedInputs.Contains((owner, (BeltDirection)saved.InputDirection))) remaining.Add(saved);
loadedStates[owner] = new BeltConveyorSaveJsonObject
{
    Items = pending.Items, BufferItem = pending.BufferItem,
    PriorityOrder = pending.PriorityOrder, InternalItems = remaining
};
```

- [ ] **Step 4: 回帰ケースを加えて検証する。**
  2→1、3→2、複数出力、満杯拒否→再試行、同tickの撤去両順序、複数source連続撤去、pendingのみ、pending＋live重複、保存cache更新、無関係block撤去、既存ベルト撤去を検証。IDの検査は実状態の返却リストで行う。pendingは現行の再発行契約に従う。テスト用publicを本番へ足さない。

```csharp
// 代表fixture: 既存テスト用portを使い左からチェストを合流させる
var world = BeltRemovalRefundTestWorld.CreateServer();
BeltTransportTestUtil.InstallMachinePorts(new[] { Vector3Int.right }, new[] { Vector3Int.back });
for (var z = 0; z <= 2; z++) world.Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z));
var sourcePosition = new Vector3Int(-1, 0, 1);
world.Place(ForUnitTestModBlockId.ChestId, sourcePosition);
EnterMachinePushWindow();
var layout = Assembly().Layouts.Single(entry => entry.IsInternal);
var item = BeltRemovalRefundTestWorld.Item();
Assembly().Segments[layout.Index].RestoreItems(new[] { new BeltItemState(item, 100) });
var hash = world.Transport.ComputeStateHash();
world.FillInventory();
Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InventoryFull, world.Remove(sourcePosition).FailureReason);
Assert.AreEqual(hash, world.Transport.ComputeStateHash());
world.ClearInventory();
Assert.IsTrue(world.Remove(sourcePosition).Success);
Assert.IsTrue(world.Remove(new Vector3Int(0, 0, 1)).Success);
Assert.AreEqual(1, world.Count(item.ItemId));
GameUpdater.UpdateOneTick();
Assert.AreEqual(1, world.Count(item.ItemId));
```

  pending代表ケースでは同じfixtureの合流blockへ `RegisterLoadedState` で対象internalの保存1個を登録し、古いliveには撤去側・存続側の内部区間、running、bufferを置く。照会1個・拒否時JSON不変・成功後pending対象方向と後続返却0・同tickの物理ハッシュ不変・次tick後の総量保持と全segmentのCount<=Capacityを検査する。破損pendingはログを出し所有者全体を除外する既存契約も回帰確認する。
  同期テストでは合流・下流を詰めて対象を内部に保ち、transport.Tick→撤去→境界hashが複製と一致することを検証する。同tickに同位置へ搬出元を置き直しても返却済み品が復元せず、次回再構築後の新しい搬送品は返却できることも検証する。
- [ ] **Step 5: `uloop compile` と関連EditModeテストを実行しTask 1をコミットする。**
  Library再生成中はcontrollerへソース完成を連絡して待つ。controllerが既存Editorへ変更を反映した後、同Editorの操作を直列化して実施する。`--filter-type regex --filter-value 'BeltConnectionRemovalRefund|BeltRemovalRefund|BeltConveyorRemoval|BeltTransportRebuildBuffer'`。未検証は成功と記録しない。

### Task 2: 実ゲームの撤去経路を検証する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Belt/BeltConnectionRemovalRefundPlayingTest.cs`
**Interfaces:** Consumes existing LoadMainGame, master blocks, transport and ClientContext.VanillaApi.Response.Block.BlockRemove. Produces gameplay log evidence.

- [ ] `editmode-in-playing-test`を読み、既存BeltRemovalRefundPlayingTestと同じ起動/終了方法を使う。LoadMainGame既定の固定テストマスタから量子チェストと歯車合流beltを配置し、内部segmentを確認してitemを注入する。満杯拒否→空きを作る→API撤去→次tickまで総量維持を観測する。API応答直後には物理状態がまだ残りうるため、搬送品の消費は次の実tick後に検証する。

```csharp
var response = await ClientContext.VanillaApi.Response.Block.BlockRemove(sourcePosition, CancellationToken.None);
Assert.IsTrue(response.Success);
Assert.AreEqual(1, inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count));
```

- [ ] `uloop compile` の後、`--test-mode EditMode --filter-type class --filter-value BeltConnectionRemovalRefundPlayingTest`を実行する。再構築前後の返却数と搬送数をログへ残し、Task 2としてテストとUnity生成metaをコミットする。

### Task 3: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] 全ブランチの機械チェック・独立レビューを実施し、実コードで確認できた指摘を反映する。判定変更後は焦点テストとTask 2を再実行する。欠員・未実行を明記する。

### Task 4: セッション終了可能状態にすること

- [ ] 本修正を元のfix/belt-removal-refundへ取り込み、全作業をコミットする。既存のユーザー/Unity変更は混ぜない。
- [ ] pr-createで既存PRを更新しpushする。競合確認・必要な解消と再検証を行う。未検証や環境障害を本文と最終報告に明記する。

## 判断記録（ADR）

- [ADR 0086](../../adr/0086-belt-removal-refunds-carried-items.md) の2026-10-20追記が要件の正本。ユーザーの「ベルコン以外」をブロック列挙へ圧縮しない。
- agent前提: 消失判定は現在の接続から求め、再構築前の古い入力数を使わない。2→1と3→2の差は既存layout規則が決める。
- agent前提: Datastoreの既存撤去購読を拡張して状態消費し、protocolへ搬送操作や新しいcommit契約を足さない。拒否時はイベントが発火しない。
- agent前提: pendingとliveの優先順位は既存GetRefundItemsと同じ。保存DTOの形式とworldVersionは変更しない。
- ユーザー裁定: 専用codex-audit不在を独立subagentで代替することに「ok」。独立質問チェックの残問0件。
- agent前提: Library再生成を妨げずソースworktreeのみ作り、検証には元のEditorを使う。作業用に別Editorを起動しない。
- user-simulator review: 適用推奨0・Warning0・要裁定0。過去裁定コーパスはこのPCに無く未参照。新しいユーザー承認とは扱わない。
