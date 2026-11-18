// ダイシングの出力のような Lv1/Lv2 の混ざったダイが届くと電気精密組立機（パッケージング）が止まって理由を出し、フィルター分岐器で仕分けると両方流れることを確かめる
// Mixed Lv1/Lv2 dies (like dicing output) stall the electric precision assembler (packaging) with a shown reason; sorting with a filter splitter lets both flow
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.UiElements;
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
    // 裏口: 日本語の期待文言は機械共有の言語設定に依らないよう、保存しない一時言語として日本語を明示する（t31r3b受入と同じ）
    // Backdoor: pin Japanese as a non-persisted temporary language so the Japanese expectations never depend on the machine-shared setting (as in t31r3b)
    p.Assert((bool)typeof(Client.Localization.Localize).GetMethod("TryApplyLanguage", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { "japanese", Client.Localization.LanguageOrigin.Unchosen }), "保存しない一時言語として日本語を適用した");
    // 地形(メサ・岩)は足場を突き抜けて照準と画面を狂わせるので、cleanroom-room-usability と同じく地形の表示と生成を止める
    // Terrain (mesas, rocks) pokes through the scaffold and skews aim and view, so stop its display and spawning like cleanroom-room-usability
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.PrepareBackgroundInputAndWaitEnabled(p);
    p.WarpPlayer(new Vector3(6.5f, 32.5f, 1.5f));
    var consumers = new List<IBlock>();
    await p.RunOnServer("研究を前提ごと完了扱いにする", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(research).Select(g => g.ToString()).ToList() }));
    await p.WaitForResearch(research, Array.Empty<Guid>(), 15f, "半導体製造の研究が完了扱いになった");
    // パッケージングはクリーンルーム外の電気精密組立機（7x4x5・南面に入口4つ）なので地面に2台並べる。A の東端の入口 (5,32,6)、B の西端の入口 (8,32,6) の手前に搬入路を作る
    // Packaging is the electric precision assembler outside the clean room (7x4x5, four south inlets), so two sit on the ground; feed paths end in front of A's east inlet (5,32,6) and B's west inlet (8,32,6)
    p.Note("電気精密組立機 A・B を置き、給電する");
    var packagingA = await Place("電気精密組立機", new Vector3Int(-1, 32, 6));
    var packagingB = await Place("電気精密組立機", new Vector3Int(8, 32, 6));
    var dieLv1Guid = MasterHolder.ItemMaster.GetItemMaster(PlaytestItemOps.ResolveItemId("シリコンダイLv1")).ItemGuid;
    var packagingRecipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == packagingA.BlockMasterElement.BlockGuid && r.InputItems.Any(i => i.ItemGuid == dieLv1Guid)).MachineRecipeGuid;
    foreach (var machine in new[] { packagingA, packagingB }) await SelectRecipe(machine, packagingRecipe);
    await PowerAll();
    foreach (var machine in new[] { packagingA, packagingB }) await Give(machine, 1, "ICチップ基板", 20);
    // A の継承元に Lv1 を置き、分岐器→ベルトで Lv2 を届けると止まって理由が出る。ベルトへの押し込みは tick 内だけ受け付けるので、分岐器（block 更新で押す）へ入れる
    // With Lv1 in A's source slot, Lv2 via splitter -> belt stalls it with a reason; belts accept pushes only in-tick, so items go into the splitter (it pushes during block updates)
    p.Note("組立機A に Lv1 ダイを置き、分岐器とベルトで Lv2 ダイを届ける");
    await Give(packagingA, 0, "シリコンダイLv1", 1);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(5, 32, 5), BlockDirection.North);
    var splitter = (await p.PlaceBlockDirect("フィルター分岐器", new Vector3Int(6, 32, 5), BlockDirection.North)).GetComponent<VanillaFilterSplitterComponent>();
    p.Assert(splitter.DirectionCount == 3, $"フィルター分岐器の方向数が3（{splitter.DirectionCount}）");
    await AllowOnly(splitter, 0, "シリコンダイLv1", "シリコンダイLv2");
    await p.WaitSeconds(0.5f);
    await InsertIntoSplitter("シリコンダイLv2");
    await OpenMachine(packagingA);
    await PlaytestUiElementOps.WaitUiTextContains("machine-input-stall", "シリコンダイLv2", 10f);
    var stall = await PlaytestUiElementOps.FindUi("machine-input-stall");
    p.Assert(stall.IsFound && stall.Text.Contains("レベルの違う品") && stall.Text.Contains("シリコンダイLv2"), $"A に混在レベルで止まった理由が出た: {stall.Text}");
    await p.Screenshot("04-mixed-level-stall");
    await p.ClickUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    // 止まっていた Lv2 はベルトごと撤去し、B への搬入路を足して、分岐器の -x 側（A）は Lv1 だけ、+x 側（B）は Lv2 だけを通す
    // Remove the stuck Lv2 with its belt, add the path to B, and let the splitter's -x (A) pass only Lv1 and +x (B) only Lv2
    p.Note("フィルター分岐器で Lv1 を A、Lv2 を B へ仕分ける");
    await p.RemoveBlock(new Vector3Int(5, 32, 5));
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(5, 32, 5), BlockDirection.North);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(7, 32, 5), BlockDirection.East);
    await p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(8, 32, 5), BlockDirection.North);
    await AllowOnly(splitter, 0, "シリコンダイLv1");
    await AllowOnly(splitter, 2, "シリコンダイLv2");
    p.Note($"分岐器の設定: {splitter.GetBlueprintSettingsJson()}");
    await p.WaitSeconds(0.5f);
    foreach (var die in Enumerable.Range(0, 19).Select(i => i % 2 == 0 ? "シリコンダイLv2" : "シリコンダイLv1")) await InsertIntoSplitter(die);
    // 両方の組立機が本当に ICチップを作り、A の止まった理由は消える
    // Both packaging machines really make IC chips and A's stall reason clears
    await p.Until(() => Count(packagingA, "ICチップLv1") >= 1 && Count(packagingB, "ICチップLv2") >= 1, 90f, "仕分けた後に Lv1 と Lv2 の ICチップが両方出た");
    p.Assert(Count(packagingA, "ICチップLv2") == 0 && Count(packagingB, "ICチップLv1") == 0, "A は Lv1 だけ、B は Lv2 だけを作った");
    await OpenMachine(packagingA);
    p.Assert(await StallCleared(), "仕分けた後は A の止まった理由が消えた");
    await p.Screenshot("05-sorted-both-flowing");
    await p.ClickUi("block-inventory-close");
    p.Note("段1b 完了");
    #region Internal
    IEnumerable<Guid> WithPrerequisites(IEnumerable<Guid> targets)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var (done, pending) = (new HashSet<Guid>(), new Stack<Guid>(targets));
        while (pending.TryPop(out var guid))
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        return done;
    }
    async UniTask<IBlock> Place(string name, Vector3Int pos) { var block = await p.PlaceBlockDirect(name, pos, BlockDirection.North); consumers.Add(block); return block; }
    async UniTask Give(IBlock block, int slot, string item, int count) => await p.RunOnServer($"{item} x{count} を {block.BlockMasterElement.Name} へ入れる", () => block.GetComponent<IOpenableBlockInventoryComponent>().SetItem(slot, PlaytestItemOps.ResolveItemId(item), count));
    async UniTask InsertIntoSplitter(string die)
    {
        // 分岐器が空くまで tick 末尾で入れ直す
        // Retry at the tick end until the splitter takes it
        var (rest, deadline) = (1, Time.realtimeSinceStartup + 10f);
        while (rest > 0 && Time.realtimeSinceStartup < deadline)
            await p.RunOnServer($"分岐器へ {die} を入れる", () => rest = splitter.InsertItem(ServerContext.ItemStackFactory.Create(PlaytestItemOps.ResolveItemId(die), 1), InsertItemContext.Empty).Count);
        await p.Until(() => rest == 0, 0f, $"分岐器へ {die} を入れた");
    }
    int Count(IBlock block, string itemName) => block.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Where(s => s.Count > 0 && s.Id == PlaytestItemOps.ResolveItemId(itemName)).Sum(s => s.Count);
    async UniTask SelectRecipe(IBlock block, Guid recipeGuid)
    {
        block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        var result = MachineRecipeSelectionResult.Success;
        await p.RunOnServer("電気精密組立機のレシピを選ぶ", () => result = selector.SetSelectedRecipe(MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid), new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10)));
        p.Assert(result == MachineRecipeSelectionResult.Success, $"select recipe {recipeGuid}");
    }
    async UniTask AllowOnly(VanillaFilterSplitterComponent component, int direction, params string[] itemNames)
    {
        // 方向の添字は outputConnects の順（北向きで 0=-x・1=+z・2=+x）。0 は A 側、2 は B 側。指定外のフィルター枠は空にする
        // Direction indices follow outputConnects (facing north 0=-x, 1=+z, 2=+x); 0 leads to A and 2 to B; unlisted filter slots are cleared
        await p.RunOnServer($"分岐器の方向{direction}を {string.Join("・", itemNames)} だけにする", () =>
        {
            component.SetMode(direction, FilterSplitterMode.Whitelist);
            for (var slot = 0; slot < component.FilterSlotCountPerDirection; slot++)
                component.SetFilterItem(direction, slot, slot < itemNames.Length ? PlaytestItemOps.ResolveItemId(itemNames[slot]) : ItemMaster.EmptyItemId);
        });
    }
    async UniTask OpenMachine(IBlock block)
    {
        // F は射程2mなので南面の1m手前に着地し、Alt のカーソル照準で南面を狙って開く（cleanroom-room-usability と同じ手順）
        // F reaches 2 m, so land 1 m south of the face and open by aiming at the south face with the Alt cursor (same as cleanroom-room-usability)
        var origin = block.BlockPositionInfo.OriginalPos;
        p.WarpPlayer(new Vector3(origin.x + 0.3f, 32.5f, origin.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面(y=32.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(origin.x + 1f, 33f, origin.z + 0.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilUiElement("block-inventory-close", 10f);
    }
    async UniTask<bool> StallCleared()
    {
        // 表示の更新は最大1秒なので、3秒まで欄が消えるのを待つ
        // The display refreshes within a second, so wait up to 3 seconds for the row to go away
        var deadline = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < deadline)
            if (!(await PlaytestUiElementOps.FindUi("machine-input-stall")).IsFound) return true; else await UniTask.Delay(200);
        return false;
    }
    async UniTask PowerAll()
    {
        // 室外の発電機 → 広範囲電柱 → 全消費ブロックへ配線する（結線で電力網の再構築は自動で予約される）
        // Wire the outdoor generator -> wide-range pole -> every consumer block (wiring schedules the network rebuild)
        var generator = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-12, 32, -20), BlockDirection.North);
        var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-2, 32, -3), BlockDirection.North)).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var wired = new List<(bool connected, string label)>();
        await p.RunOnServer("発電機に給油して全消費ブロックへ配線する", () =>
        {
            generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"))), default);
            wired.Add((ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), pole, free), "wire generator -> pole"));
            foreach (var block in consumers) wired.Add((ElectricWireSystemUtil.TryConnectBothSides(pole, block.GetComponent<IElectricWireConnector>(), free), $"wire pole -> {block.BlockMasterElement.Name}"));
        });
        foreach (var (connected, label) in wired) p.Assert(connected, label);
    }
    #endregion
});