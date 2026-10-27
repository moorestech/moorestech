// ダイシングの出力のような Lv1/Lv2 の混ざったダイが届くとパッケージングが止まって理由を出し、フィルター分岐器で仕分けると両方流れることを確かめる
// Mixed Lv1/Lv2 dies (like dicing output) stall packaging with a shown reason; sorting with a filter splitter lets both flow
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.WebUi;
using Core.Inventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.FilterSplitter;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var research = new[] { "019e3ad9-46e2-73fd-9555-8c1d0245d26d" }.Select(Guid.Parse).ToArray(); // 半導体製造（段1a と同じ先頭 GUID）。閉包で前提ごと完了させる
return PlaytestRunner.Run("tier9-1b-mixed-level-sorting", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形(メサ・岩)は足場を突き抜けて照準と画面を狂わせるので、cleanroom-room-usability と同じく地形の表示と生成を止める
    // Terrain (mesas, rocks) pokes through the scaffold and skews aim and view, so stop its display and spawning like cleanroom-room-usability
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(6.5f, 33.2f, 1.5f));
    var ipaId = MasterHolder.FluidMaster.GetFluidId(MasterHolder.FluidMaster.Fluids.Data.First(f => f.Name == "イソプロピルアルコール").FluidGuid);
    var consumers = new List<IBlock>();
    var researchStore = p.ServerService<IResearchDataStore>();
    researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(research).Select(g => g.ToString()).ToList() });
    await p.Until(() => research.All(researchStore.IsResearchCompleted), 15f, "半導体製造の研究が完了扱いになった");

    // 部屋の中にパッケージング2台を並べる。A の入口 (5,33,6) の手前、B の入口 (8,33,6) の手前に搬入路を作る
    // Place two packaging machines in the room; feed paths end in front of A's inlet (5,33,6) and B's inlet (8,33,6)
    p.Note("クリーンルームにパッケージング装置 A・B を置き、給電する");
    BuildRoom();
    var packagingA = Place("パッケージング装置", new Vector3Int(4, 33, 6));
    var packagingB = Place("パッケージング装置", new Vector3Int(7, 33, 6));
    var packagingRecipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == packagingA.BlockMasterElement.BlockGuid).MachineRecipeGuid;
    foreach (var machine in new[] { packagingA, packagingB }) SelectRecipe(machine, packagingRecipe);
    foreach (var x in new[] { 11, 12 }) Give(Place("クリーンルーム空気清浄機", new Vector3Int(x, 33, 8)), 0, "クリーンルームフィルター", 5);
    PowerAll();
    await p.Until(() => p.ServerService<CleanRoomDatastore>().TryGetCleanRoom(packagingA, out var r) && r.ThresholdIndex < MasterHolder.CleanRoomMaster.OutThresholdIndex, 30f, "クリーンルームが判定された");
    foreach (var machine in new[] { packagingA, packagingB }) Give(machine, 1, "ICチップ基板", 20);

    // A の継承元に Lv1 を置き、分岐器→ベルトで Lv2 を届けると止まって理由が出る。ベルトへの押し込みは tick 内だけ受け付けるので、分岐器（block 更新で押す）へ入れる
    // With Lv1 in A's source slot, Lv2 via splitter -> belt stalls it with a reason; belts accept pushes only in-tick, so items go into the splitter (it pushes during block updates)
    p.Note("パッケージングA に Lv1 ダイを置き、分岐器とベルトで Lv2 ダイを届ける");
    Give(packagingA, 0, "シリコンダイLv1", 1);
    p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(5, 33, 5), BlockDirection.North);
    var splitter = p.PlaceBlockDirect("フィルター分岐器", new Vector3Int(6, 33, 5), BlockDirection.North).GetComponent<VanillaFilterSplitterComponent>();
    p.Assert(splitter.DirectionCount == 3, $"フィルター分岐器の方向数が3（{splitter.DirectionCount}）");
    AllowOnly(splitter, 0, "シリコンダイLv1", "シリコンダイLv2");
    await p.WaitSeconds(0.5f);
    await p.Until(() => splitter.InsertItem(ServerContext.ItemStackFactory.Create(PlaytestItemOps.ResolveItemId("シリコンダイLv2"), 1), InsertItemContext.Empty).Count == 0, 10f, "分岐器へ シリコンダイLv2 を入れた");
    await OpenMachine(packagingA);
    await PlaytestWebUiOps.WaitWebUiTextContains("machine-input-stall", "シリコンダイLv2", 10f);
    var stall = await PlaytestDomQuery.Query("machine-input-stall", 3f);
    p.Assert(stall.Found && stall.Text.Contains("レベルの違う品") && stall.Text.Contains("シリコンダイLv2"), $"A に混在レベルで止まった理由が出た: {stall.Text}");
    await p.Screenshot("04-mixed-level-stall");
    await p.ClickWebUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 止まっていた Lv2 はベルトごと撤去し、B への搬入路を足して、分岐器の -x 側（A）は Lv1 だけ、+x 側（B）は Lv2 だけを通す
    // Remove the stuck Lv2 with its belt, add the path to B, and let the splitter's -x (A) pass only Lv1 and +x (B) only Lv2
    p.Note("フィルター分岐器で Lv1 を A、Lv2 を B へ仕分ける");
    p.RemoveBlock(new Vector3Int(5, 33, 5));
    p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(5, 33, 5), BlockDirection.North);
    p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(7, 33, 5), BlockDirection.East);
    p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(8, 33, 5), BlockDirection.North);
    AllowOnly(splitter, 0, "シリコンダイLv1");
    AllowOnly(splitter, 2, "シリコンダイLv2");
    p.Note($"分岐器の設定: {splitter.GetBlueprintSettingsJson()}");
    await p.WaitSeconds(0.5f);
    foreach (var die in Enumerable.Range(0, 19).Select(i => i % 2 == 0 ? "シリコンダイLv2" : "シリコンダイLv1"))
        await p.Until(() => { Feed(); return splitter.InsertItem(ServerContext.ItemStackFactory.Create(PlaytestItemOps.ResolveItemId(die), 1), InsertItemContext.Empty).Count == 0; }, 10f, $"分岐器へ {die} を入れた");

    // 両方のパッケージングが本当に ICチップを作り、A の止まった理由は消える
    // Both packaging machines really make IC chips and A's stall reason clears
    await p.Until(() => { Feed(); return Count(packagingA, "ICチップLv1") >= 1 && Count(packagingB, "ICチップLv2") >= 1; }, 90f, "仕分けた後に Lv1 と Lv2 の ICチップが両方出た");
    p.Assert(Count(packagingA, "ICチップLv2") == 0 && Count(packagingB, "ICチップLv1") == 0, "A は Lv1 だけ、B は Lv2 だけを作った");
    await OpenMachine(packagingA);
    p.Assert(await StallCleared(), "仕分けた後は A の止まった理由が消えた");
    await p.Screenshot("05-sorted-both-flowing");
    await p.ClickWebUi("block-inventory-close");
    p.Note("段1b 完了");
    #region Internal

    IEnumerable<Guid> WithPrerequisites(IEnumerable<Guid> targets)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var done = new HashSet<Guid>();
        var pending = new Stack<Guid>(targets);
        while (pending.TryPop(out var guid))
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        return done;
    }

    IBlock Place(string name, Vector3Int pos) { var block = p.PlaceBlockDirect(name, pos, BlockDirection.North); consumers.Add(block); return block; }
    void Give(IBlock block, int slot, string item, int count) => block.GetComponent<IOpenableBlockInventoryComponent>().SetItem(slot, PlaytestItemOps.ResolveItemId(item), count);

    // パッケージングの IPA は機械のタンクへ直接入れる（tick 外にパイプへ注入すると流体網の複製がサーバーと食い違うため）
    // Pour packaging IPA straight into machine tanks (injecting into pipes outside the tick desyncs the fluid replica)
    void Feed() { foreach (var machine in new[] { packagingA, packagingB }) machine.GetComponent<VanillaMachineFluidInventoryComponent>().AddLiquid(new FluidStack(20, ipaId), default); }

    int Count(IBlock block, string itemName) => block.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Where(s => s.Count > 0 && s.Id == PlaytestItemOps.ResolveItemId(itemName)).Sum(s => s.Count);

    void SelectRecipe(IBlock block, Guid recipeGuid)
    {
        block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        p.Assert(selector.SetSelectedRecipe(MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid), new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10)) == MachineRecipeSelectionResult.Success, $"select recipe {recipeGuid}");
    }

    void AllowOnly(VanillaFilterSplitterComponent component, int direction, params string[] itemNames)
    {
        // 方向の添字は outputConnects の順（北向きで 0=-x・1=+z・2=+x）。0 は A 側、2 は B 側。指定外のフィルター枠は空にする
        // Direction indices follow outputConnects (facing north 0=-x, 1=+z, 2=+x); 0 leads to A and 2 to B; unlisted filter slots are cleared
        component.SetMode(direction, FilterSplitterMode.Whitelist);
        for (var slot = 0; slot < component.FilterSlotCountPerDirection; slot++)
            component.SetFilterItem(direction, slot, slot < itemNames.Length ? PlaytestItemOps.ResolveItemId(itemNames[slot]) : ItemMaster.EmptyItemId);
    }

    async UniTask OpenMachine(IBlock block)
    {
        // F は射程2mなので南面の1m手前（搬入ベルトの西隣）に着地し、Alt のカーソル照準で南面を狙って開く（cleanroom-room-usability と同じ手順）
        // F reaches 2 m, so land 1 m south of the face (west of the feed belt) and open by aiming at the south face with the Alt cursor (same as cleanroom-room-usability)
        var origin = block.BlockPositionInfo.OriginalPos;
        p.WarpPlayer(new Vector3(origin.x + 0.3f, 33.5f, origin.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 33f) < 0.05f, 5f, "床パネルの上(y=33.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(origin.x + 1f, 34f, origin.z + 0.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilWebUiElement("block-inventory-close", 10f);
    }

    async UniTask<bool> StallCleared()
    {
        // 表示の更新は最大1秒なので、3秒まで欄が消えるのを待つ
        // The display refreshes within a second, so wait up to 3 seconds for the row to go away
        var deadline = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (!(await PlaytestDomQuery.Query("machine-input-stall", 1f)).Found) return true;
            await UniTask.Delay(200);
        }
        return false;
    }

    void BuildRoom()
    {
        // 内寸 x1..13 / z1..9 / y33..36。室内で機械 UI を開くとき三人称カメラ（足元+約2.9m）が天井より下に来るよう、天井は y37 にする
        // Interior x1..13 / z1..9 / y33..36; the ceiling sits at y37 so the third-person camera (about 2.9 m above the feet) stays under it when opening UIs inside
        var door = new Vector3Int(10, 33, 0);
        for (var x = 0; x <= 14; x++)
        for (var z = 0; z <= 10; z++)
        {
            p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 32, z), BlockDirection.North);
            p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 37, z), BlockDirection.North);
            if (x != 0 && x != 14 && z != 0 && z != 10) continue;
            for (var y = x == door.x && z == door.z ? 35 : 33; y <= 36; y++) p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, z), BlockDirection.North);
        }
        p.PlaceBlockDirect("クリーンルームドア", door, BlockDirection.North);
    }

    void PowerAll()
    {
        // 室外の発電機 → 広範囲電柱 → 全消費ブロックへ配線する（結線で電力網の再構築は自動で予約される）
        // Wire the outdoor generator -> wide-range pole -> every consumer block (wiring schedules the network rebuild)
        var generator = p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-12, 32, -20), BlockDirection.North);
        generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"))), default);
        var pole = p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-2, 32, -3), BlockDirection.North).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        p.Assert(ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), pole, free), "wire generator -> pole");
        foreach (var block in consumers)
            p.Assert(ElectricWireSystemUtil.TryConnectBothSides(pole, block.GetComponent<IElectricWireConnector>(), free), $"wire pole -> {block.BlockMasterElement.Name}");
    }

    #endregion
});
