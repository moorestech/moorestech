# Belt Removal Refund Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 通常・歯車ベルトの手動撤去で、対象マスが所有する搬送アイテムを全量手持ちへ返し、全量が入らない場合は撤去を拒否する。

**Architecture:** 共通のBeltConveyorInventoryComponentを既存IGetRefundItemsInfo契約へ参加させる。搬送の所有者BeltTransportDatastoreが非破壊スナップショットから返却スタックを作り、既存RemoveBlockProtocolの容量確認→撤去→返却へ渡す。新規通信・保存形式・毎tickの検知処理を追加しない。

**Tech Stack:** C# / Unity / NUnit / MessagePack既存プロトコル / uloop

## Requirements

- R1: 通常・歯車ベルトの平地・上り・下りで、撤去対象が所有するアイテム全量が返る。平地は両template、坂はElectricBeltUp/DownとTestGearBeltConveyorUp/Downを検証する。
- R2: 走行中は先頭マス、bufferは末尾マス、内部segmentは合流マスの所有とし、隣接マス所有のアイテムを返さない。各所有境界を実segmentで検証する。
- R3: 生存中のBeltItemのItemId・ItemInstanceIdを返却スタックで保持する。返却件数だけでなくIDを照合する。
- R4: 建設費＋搬送アイテム全量が入らないとInventoryFullで拒否し、搬送・ブロック・手持ち・建設財布を変更しない。空きを作って同じ撤去を再試行すると全量返る。
- R5: 次tickの再構築前に同segmentの複数マスを連続撤去しても二重返却しない。返却済み＋残存の総数を次tick後まで検証する。
- R6: ロード直後の未再構築状態でも対象の保存済み搬送内容を返す。照会はpending状態・Assembly・dirty状態・保存キャッシュを書き換えず、拒否後の再試行も成立する。保存データには個体IDが無いため既存ロード変換と同じ再発行を使う。
- R7: EditModeInPlayingTestで実際にゲームを起動し、内蔵サーバーの本番RemoveBlockProtocol経路と実tick後の返却を検証する。UI入力での再現とは表記しない。
- 非目標: 過負荷破壊、所有マス以外の再構築消失規則、セーブ形式、新規通信、建設財布や撤去プロトコルの設計変更。

## Global Constraints

- 作業場所は `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/belt-removal-refund`、ブランチ `fix/belt-removal-refund`。メインのUnity・他人のEditorを触らない。
- コードは1ファイル200行未満、1ディレクトリ10コードファイルまで。partial・Func・デフォルト引数は禁止。主要箇所は日本語→英語の2行コメント。
- .metaはUnity生成のみ。Unity YAMLを手書きしない。Library削除禁止。
- テストIDはForUnitTestModBlockId／ForUnitTestItemId。BeltConstants.ItemWidth=360、1マス内距離は0..359。500を使わない。
- .cs変更後のコンパイル必須。全uloopコマンドのproject-pathはこのworktreeのmoorestech_client。ドメインリロード時は45秒後に再試行。
- 進捗・レビュー記録は記録repoへ。先に `/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/README.md` を読む。Beadsは `moorestech-fbq7w`。
- バックグラウンドのポーリングで待たず全部前景で実行する。区切りごとにコミットする。自分で起動したプロセスはPIDを記録しPID指定で終了する。
- 初期dirtyは `_CompileRequester.cs` のUnity生成印と `TrainUnitNextBranchFinder.cs.meta`。実装差分へ無関係に混ぜず、終了時に由来を確認して処理する。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 独立contextで所有主体と前例を突合
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 初回の所有一致判定重複を集約し独立再検査済み

## 配置と前例

| # | 項目 | 配置先アセンブリ・層 | 使用機構・所有者 |
|---|---|---|---|
| 1 | BeltConveyorInventoryComponent.GetRefundItems | Game.Block | 自分のblock IDでLookupへ問い合わせる既存返却契約 |
| 2 | IBeltTransportLookup.GetRefundItems(BlockInstanceId) | Game.Block | 搬送状態の読み取り面。変更APIへ公開しない |
| 3 | BeltTransportDatastore.GetRefundItems(BlockInstanceId) | Game.Block | 自身のAssembly／pendingからIItemStack列を生成 |
| 4 | BeltRemovalRefundTest・BeltRemovalRefundStateTest・BeltRemovalRefundTestWorld | Server.Tests | 実DI・本番プロトコル・実segment・tickの結合テスト |
| 5 | BeltRemovalRefundPlayingTest | Client.Tests | EditModeからPlayMode遷移、内蔵サーバー検証 |

データフロー: 撤去入力→RemoveBlockProtocol→IGetRefundItemsInfo→IBeltTransportLookup→所有中搬送内容→全量InsertionCheck→RemoveBlock→InsertItem。追加コンポーネントの役割は「読み手」。

前例は `Game.Block/Blocks/FilterSplitter/VanillaFilterSplitterComponent.cs:GetRefundItems`（自分の保持内容を既存撤去契約で返す）。今回も自分のblock IDで持ち主の搬送内容を取得し、プロトコルへ返す。外から他ブロックの内部状態を直接変更しない。
`Game.Block/Blocks/BeltConveyor/Transport/Rebuild/BeltTransportSnapshot.cs` の3所有規則をそのまま使用する。DatastoreのCreateSaveStateは保存用キャッシュを使うので返却には使わない。直接RestoreItemsした後でも新しい内容を照会できる必要がある。
`Game.Block/Blocks/BeltConveyor/Save/BeltTransportLoadedStateConverter.cs` がロード内容をsnapshotへ非破壊変換する既存入口。pending照会もこれを使い、独自の保存JSON読み取り・検証を作らない。
受動統合案は既存容量確認へ返却列を渡す案。能動介入案（照会時の強制RebuildIfDirty／先に取り出す）は拒否時の状態不変を破るため採らない。

| 既存操作 | 計画後 | 根拠 |
|---|---|---|
| 空ベルト撤去／Undo | 生存 | 既存プロトコルとexpected instance検査を変更しない |
| 通常・歯車搬送／機械押し込み | 生存 | 読み取りAPIだけを追加 |
| 手持ち満杯での撤去拒否 | 生存・搬送分を含める | 既存全量InsertionCheckへ合算 |
| セーブ・ロード・初期同期・tick同期 | 生存 | フォーマット・tickを変更しない |
| 過負荷破壊 | 生存 | 手動撤去の問い合わせ以外は変更しない |

### Task 1: 共通返却契約とサーバー回帰テスト

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/BeltConveyorInventoryComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/Transport/IBeltTransportLookup.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/Transport/BeltTransportDatastore.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltRemoval/BeltRemovalRefundTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltRemoval/BeltRemovalRefundStateTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BeltRemoval/BeltRemovalRefundTestWorld.cs`

**Interfaces:**
- Consumes: `BeltTransportSnapshot.Capture(BeltTransportAssembly)`、`BeltTransportLoadedStateConverter.Convert(IReadOnlyDictionary<BlockInstanceId, BeltConveyorSaveJsonObject>, BeltTransportSnapshot)`、`IItemStackFactory.Create(ItemId, int, ItemInstanceId)`。
- Produces: `IBeltTransportLookup.GetRefundItems(BlockInstanceId blockInstanceId): IReadOnlyList<IItemStack>`、`BeltConveyorInventoryComponent.GetRefundItems(): IReadOnlyList<IItemStack>`。

- [ ] **Step 1: 読み取りAPIを実装する。** interfaceへ上記シグネチャを追加し、Datastoreへ次を加える（Core.BeltTransport／Game.Contextを参照）。Assemblyは変更しない。pendingがある対象はpendingだけから構築し、古いAssemblyと重複させない。

```csharp
public IReadOnlyList<IItemStack> GetRefundItems(BlockInstanceId blockInstanceId)
{
    // pendingは組と重ねず読む
    // Read pending state without overlapping the assembly
    BeltTransportSnapshot snapshot;
    if (_loadedStates.TryGetValue(blockInstanceId, out var pending))
    {
        snapshot = new BeltTransportSnapshot();
        BeltTransportLoadedStateConverter.Convert(new Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> { [blockInstanceId] = pending }, snapshot);
    }
    else snapshot = BeltTransportSnapshot.Capture(Assembly);
    var items = new List<IItemStack>();
    foreach (var record in snapshot.RunningItems)
        Add(record.CellBlockInstanceId, record.Item);
    foreach (var record in snapshot.BufferItems)
        Add(record.CellBlockInstanceId, record.Item);
    foreach (var record in snapshot.InternalItems)
        foreach (var state in record.States) Add(record.MergeBlockInstanceId, state.Item);
    return items;

    #region Internal
    void Add(BlockInstanceId owner, BeltItem item)
    {
        if (owner == blockInstanceId)
            items.Add(ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId));
    }
    #endregion
}
```

- [ ] **Step 2: 共通コンポーネントを既存契約へ参加させる。** クラスへIGetRefundItemsInfoを追加し、ctorで `_beltTransportLookup = ServerContext.GetService<IBeltTransportLookup>();` を取得。旧「撤去で返らない」コメントを手動撤去時返却に合わせる。

```csharp
private readonly IBeltTransportLookup _beltTransportLookup;
public IReadOnlyList<IItemStack> GetRefundItems()
{
    BlockException.CheckDestroy(this);
    return _beltTransportLookup.GetRefundItems(_blockInstanceId);
}
```

- [ ] **Step 3: creating-server-testsスキルに従い実DIの回帰テストを作る。** TestWorldはServiceProvider・player1のMainOpenableInventory・RemoveBlockProtocolを保持し、CreateServer／Place／Restore／Remove／Countを提供するテスト専用補助。既存 `Tests/Util/BeltWorldTestUtil.cs` と `Tests/CombinedTest/Server/PacketTest/RemoveBlockProtocolTest/RemoveRailRefundFailureTest.cs` の初期化・財布不変比較を使う。次の本番経路をテストへ移す。

```csharp
var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
var block = BeltWorldTestUtil.Place(ForUnitTestModBlockId.GearBeltConveyor, Vector3Int.zero, BlockDirection.North);
BeltWorldTestUtil.EnterMachinePushWindow();
var item = new BeltItem(ForUnitTestItemId.ItemId1, ItemInstanceId.Create(), BeltEntryDirection.FromBack);
BeltWorldTestUtil.SegmentAt(Vector3Int.zero).RestoreItems(new[] { new BeltItemState(item, 100) });
var inventory = services.GetService<IPlayerInventoryDataStore>().GetInventoryData(1).MainOpenableInventory;
var protocol = new RemoveBlockProtocol(services);
var response = (RemoveBlockProtocol.RemoveBlockResponseMessagePack)protocol.GetResponse(MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(Vector3Int.zero)), 1);
Assert.IsTrue(response.Success);
Assert.AreEqual(1, Enumerable.Range(0, inventory.GetSlotSize()).Sum(i => inventory.GetItem(i).Id == item.ItemId ? inventory.GetItem(i).Count : 0));
GameUpdater.UpdateOneTick();
Assert.IsEmpty(BeltWorldTestUtil.Assembly().Segments);
```

テスト一覧（各ケースで本番プロトコル結果、個数、必要な個体ID／状態をassertする）:

| クラス | ケースと入力 | 必須assert |
|---|---|---|
| BeltRemovalRefundTest | BeltConveyorId／GearBeltConveyor／ElectricBeltUp／ElectricBeltDown／TestGearBeltConveyorUp／TestGearBeltConveyorDown、距離100 | 成功・1個返却・生存中ItemInstanceId保持・次tick後に再出現なし |
| BeltRemovalRefundTest | 同segment2マス、ItemWidth-1とItemWidthは別fixtureの各1個で境界検証（互いに距離1の2個を同時に置かない） | 対象マスだけ返却、隣接マス分は次tick後も残存 |
| BeltRemovalRefundTest | 既存BeltTransportDatastoreTest.CreateMergeWorldの合流と分岐器へbuffer注入、内部layoutへ距離100で1個 | 所有マス撤去だけが返却する、上流撤去で内部segmentの品を返さない |
| BeltRemovalRefundStateTest | 全スロットを別itemのMaxStackで埋める→撤去→1スロット以上を空けて再試行 | InventoryFull、block・hash・priority・buffer・手持ち・財布不変、その後成功 |
| BeltRemovalRefundStateTest | 3マス各1個、同tick内で2マス撤去、さらに同じ位置を再撤去 | 2個だけ返る、重複撤去拒否、tick後残り1個、総数3 |
| BeltRemovalRefundStateTest | RegisterLoadedStateで走行／buffer／internal保存を登録し再構築前に照会・拒否・再試行 | Assembly不変・pending保存値不変、返却全量、tick後二重返却／復元なし |
| BeltRemovalRefundStateTest | CreateSaveStateを先に呼んだ後でRestoreItemsで別アイテムを注入 | 古いsave cacheではなく現在の品を返す |

通常坂は既存ForUnitTestModBlockId.ElectricBeltUp／ElectricBeltDownを使用する（同IDファイル107〜109行で実在確認済み）。TestModのID・JSONや本番マスタを追加する必要はない。

- [ ] **Step 4: コンパイル・対象テスト・既存回帰を実行する。** 新規サーバー.csが未認識なら自分のEditorだけ再起動しmetaを生成する。`uloop compile --project-path ./moorestech_client` はErrorCount 0。`uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value 'BeltRemovalRefund|BeltTransportDatastoreTest|RemoveBlockProtocolTest|RemoveRail.*Refund'` は全件PASS。件数・ログ・XMLを記録repoへ保存する。
- [ ] **Step 5: Task 1として上記対象だけをコミットする。**

### Task 2: PlayModeの本番撤去経路を検証

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Belt/BeltRemovalRefundPlayingTest.cs`

**Interfaces:**
- Consumes: `LoadMainGame()`、実ゲームのServerContext／ClientContext.VanillaApi.Response.Block.BlockRemove(Vector3Int, CancellationToken)／GameUpdater。
- Produces: EditModeInPlayingTestの結果と起動／撤去／返却の観測ログ。

- [ ] **Step 1: editmode-in-playing-testスキルの基本テンプレートを使って統合テストを作る。** `EnterPlayModeUtil()`→メソッド直下の`yield return new EnterPlayMode(expectDomainReload: true)`→`LoadMainGame()`→通常・歯車各1本の実block配置・実segment注入→本番RemoveBlockProtocol→返却個数assert→実tick後の残存なしassert→`ExitPlayMode`→SessionState解除。既存 `Client.Tests/EditModeInPlayingTest/Boot/LocalPlayEmbeddedServerBootTest.cs` を起動の前例とし、利用可能なマスタのブロック名はMasterHolderから実在を確かめる。次の核を上記テンプレートのasync Bodyに入れる。

```csharp
await LoadMainGame();
// 実ゲーム側のDIから本番撤去を呼ぶ
// Invoke production removal from the running game's DI
var response = await ClientContext.VanillaApi.Response.Block.BlockRemove(position, CancellationToken.None);
Assert.IsTrue(response.Success);
```

既存 `Client.Game/InGame/BlockSystem/PlaceSystem/Undo/PlaceOperationRecord.cs` と同じAPI経路を使う。RemoveBlockヘルパーはワールド直接削除なので使用しない。テスト用publicを本番へ追加しない。満杯拒否→空きを作る→成功も同じ起動内で検証する。フレームを進める前後のアイテム個数と実応答を記録する。
- [ ] **Step 2: コンパイル後 `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type class --filter-value 'Client.Tests.EditModeInPlayingTest.BeltRemovalRefundPlayingTest'` を実行しPASSを確認する。** タイムアウトは起動待ちとfailureを区別し、ドメインリロード時45秒待機。スクショやEditModeテストだけで置換しない。
- [ ] **Step 3: Task 2としてテストとUnity生成metaをコミットする。**

### Task 3: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

**Files:** コードrepoへのレビュー記録追加なし。
**Interfaces:** Consumes: origin/masterからの全ブランチ差分。Produces: codex-host runnerの発火数・失敗数・統合報告・必要な修正コミット。

- [ ] `moores-code-review` の全手順を実施する。Codexでは `references/codex-host.md` の `scripts/codex_workflow_runner/run.mjs` を用い、選ばれた系統の代行・縮退をしない。
- [ ] 実コード照合した確定バグを修正・コミット・再コンパイルし、関連テストを再実行する。判定条件・評価時点を変更した場合はTask 2も再実行する。未回答の裁定を採択済みにしない。

### Task 4: セッション終了可能状態にすること

**Files:** 必要な修正と計画、PR本文。記録はlogs repo。
**Interfaces:** Consumes: 検証済み全差分。Produces: push済みコミットとPR URL。

- [ ] pr-createとsafe-github-pushスキルに従いPRを作成する。master競合はスキル経由で解消・コンパイル確認してpush。独立レビュー待ちラベルは実際の本番変更規模とローカル規約で判断する。
- [ ] sdd-archiveを隔離worktree側で実行し、記録・報告をlogs repoへ退避する。BeadsへPRと証拠を残してclose。全変更commit／push済み、PRがマージ可能なことを確認する。
- [ ] 自分のEditorだけ終了し、cwdをメイン等へ移して `moores-wt rm belt-removal-refund` で片付ける。必要な結果を先に保存し、worktree削除で証拠を失わない。
- [ ] PR URL、変更点、実際のテスト件数、未検証／障害、commitを報告する。未完を完了と書かない。

## 判断記録（ADR）

- ユーザー裁定: `docs/adr/0086-belt-removal-refunds-carried-items.md`。質問「載っているアイテムも手持ちに返す。全部入りきらなければ撤去しない」への「進めて」。通常・歯車共通契約／所有規則／非目標は同ADR。
- 配置判断: `BeltConveyorInventoryComponent.cs` に返却契約、`IBeltTransportLookup.cs` に読み取り署名、`BeltTransportDatastore.cs` に所有アイテム列挙を置く。既存のGame.Block所有境界に従い、他層へ状態を移さない。
- agent前提: 実装1タスク＋PlayMode検証1タスクの小規模変更。物理搬送を変更せずLookupから返却指示を出す。保存用cache／強制rebuild／破壊的取り出しは使用しない。
- agent前提: ランタイム挙動変更の検証にはEditModeInPlayingTestを選ぶ。既にEditMode実DIで再現済みだがPlayMode検証の代替にしない。
- agent前提: pending保存のItemInstanceId再発行は既存ロード契約に従う。生存中の個体ID保存とは分けて検証する。

- agent判断: 所有IDの一致判定はDatastore内ローカル関数1か所へ集め、走行・buffer・内部segmentから所有者と品を渡す。追加の公開型や状態は作らない。

- シミュレーター予測の反映（agent判断）: 通常坂は既存ElectricBeltUp/Downを指定し、不要なTestMod追加分岐を削除する。根拠はForUnitTestModBlockId実在確認。ユーザーの新規裁定ではない。
