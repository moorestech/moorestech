// 出荷マスタ全体で起動し、S1 の研究後に 4種のモジュールLv1 と GPULv1 が作れ、Lv2 チップは製作上限で拒まれることを確かめる
// Boot on the full shipped master and confirm the four Lv1 modules and GPULv1 are craftable after S1 research, and Lv2 chips are refused by the craft cap
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var research = new[]
{
    "019e3ad9-46e2-73fd-9555-8c1d0245d26d", "01a167f3-bd6b-70c9-be6e-52e41907b21f", "019e3ae2-5005-7439-b242-9b7b3191a2c3",
    "01a167f3-bd6e-7745-84c0-0e1cf105c2cc", "01a167f3-bd74-73f8-8c43-1869db833299", "01a167f3-bd77-7013-b982-5f4cdf3c86a9",
    "01a167f3-bd71-707c-b8e6-615063559cc1", "01a167f3-bd7a-75c4-a266-8b9a4d5d1900",
}.Select(Guid.Parse).ToArray();
var productRecipes = new (string recipe, string product)[]
{
    ("01a167f3-bd88-747d-b30e-13f23f169ce4", "品質モジュールLv1"), ("01a167f3-bd8b-7732-badd-8c1a011f7c26", "速度モジュールLv1"),
    ("01a167f3-bd8e-7525-af33-5cce09108374", "省エネモジュールLv1"), ("01a167f3-bd90-7307-91a6-e45243f99379", "生産性モジュールLv1"),
    ("01a167f3-bd94-76d2-ada6-617fdbf3d9b3", "GPULv1"),
};
return PlaytestRunner.Run("tier9-2-modules-gpu", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形(メサ・岩)が工作装置の手前に湧いて照準と画面を塞ぐので、地形の表示と生成を止める
    // Terrain (mesas, rocks) spawns in front of the workshops and blocks aim and view, so stop its display and spawning
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(8f, 32.5f, -9f));

    // 半導体製造と S1 の研究を前提ごと完了扱いにする（解放アクションも再実行される）
    // Mark the semiconductor and S1 research done with all prerequisites (unlock actions are replayed)
    p.Note("半導体製造と S1 の研究を前提ごと完了させる");
    var researchStore = p.ServerService<IResearchDataStore>();
    await p.RunOnServer("半導体製造と S1 の研究を完了扱いにする", () => researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(research).Select(g => g.ToString()).ToList() }));
    await p.Until(() => research.All(researchStore.IsResearchCompleted), 15f, "S1 research completed");

    // 電気汎用工作装置を5台並べて給電し、各レシピを選んで材料を入れる
    // Place five powered electric workshops, select each recipe and insert its inputs
    p.Note("電気汎用工作装置5台でモジュール4種と GPULv1 のレシピを選び材料を入れる");
    var crafters = new IBlock[productRecipes.Length];
    for (var i = 0; i < productRecipes.Length; i++) crafters[i] = await p.PlaceBlockDirect("電気汎用工作装置", new Vector3Int(i * 4, 32, 0), BlockDirection.North);
    await PowerAll();
    for (var i = 0; i < productRecipes.Length; i++)
    {
        var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(productRecipes[i].recipe));
        await SelectRecipe(crafters[i], recipe.MachineRecipeGuid);
        foreach (var input in recipe.InputItems) p.Assert((await Insert(crafters[i], input.ItemGuid, input.Count)).Count == 0, $"{productRecipes[i].product} の材料 {MasterHolder.ItemMaster.GetItemMaster(input.ItemGuid).Name} x{input.Count} が入った");
    }
    foreach (var crafter in crafters) await p.WaitBlockGameObject(crafter.BlockPositionInfo.OriginalPos);
    // 列の南へ着地し、設置演出が終わって機械が描画される最初の完成(品質モジュール)まで待ってから撮る
    // Land south of the row and wait for the first product (quality module), by which the placement effect has finished, then shoot
    p.WarpPlayer(new Vector3(9.5f, 32.5f, -8f));
    await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面(y=32.0)に着地した");
    await p.Until(() => Count(crafters[0], productRecipes[0].product) > 0, 60f, $"{productRecipes[0].product} crafted");
    await p.Screenshot("01-workshops");

    // 5製品の Lv1 がそれぞれ出る（最後は GPU の機械が GPULv1 を本当に出すところ）
    // Each of the five products comes out at Lv1 (last is the GPU workshop really producing GPULv1)
    for (var i = 0; i < productRecipes.Length; i++)
    {
        var index = i;
        await p.Until(() => Count(crafters[index], productRecipes[index].product) > 0, 60f, $"{productRecipes[index].product} crafted");
    }
    await ShootMachine(crafters[4], "02-modules-gpu");

    // 製作上限 Lv1 のため ICチップLv2 は品質モジュールの工作装置へ入らない（S2 の研究まで）
    // With the craft cap at Lv1, IC chip Lv2 does not enter the quality-module workshop (until S2 research)
    p.Note("製作上限 Lv1 の工作装置へ ICチップLv2 を入れて拒まれることを見る");
    var lv2 = MasterHolder.ItemMaster.GetItemMaster(PlaytestItemOps.ResolveItemId("ICチップLv2")).ItemGuid;
    var rest = await Insert(crafters[0], lv2, 5);
    p.Assert(rest.Count == 5, "IC chip Lv2 is refused by the Lv1-capped module recipe");
    p.Note("段2 完了");

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

    async UniTask<IItemStack> Insert(IBlock block, Guid itemGuid, int count)
    {
        IItemStack rest = null;
        await p.RunOnServer($"{MasterHolder.ItemMaster.GetItemMaster(itemGuid).Name} x{count} を工作装置へ入れる", () => rest = block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(ServerContext.ItemStackFactory.Create(itemGuid, count)));
        return rest;
    }

    int Count(IBlock block, string itemName)
    {
        var inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
        var id = PlaytestItemOps.ResolveItemId(itemName);
        return Enumerable.Range(0, inventory.GetSlotSize()).Select(inventory.GetItem).Where(s => s.Id == id).Sum(s => s.Count);
    }

    async UniTask SelectRecipe(IBlock block, Guid recipeGuid)
    {
        var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
        block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        var result = MachineRecipeSelectionResult.Success;
        await p.RunOnServer("工作装置のレシピを選ぶ", () => result = selector.SetSelectedRecipe(MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid), overflow));
        p.Assert(result == MachineRecipeSelectionResult.Success, $"select recipe {recipeGuid}");
    }

    async UniTask ShootMachine(IBlock block, string screenshotName)
    {
        // F は射程2mなので南面の1m手前に着地し、Alt のカーソル照準で南面を狙って機械 UI を開き、出力欄が見える状態で撮る
        // F reaches 2 m, so land 1 m south of the face, open the machine UI by aiming at it with the Alt cursor, and shoot with the output slots visible
        var origin = block.BlockPositionInfo.OriginalPos;
        p.WarpPlayer(new Vector3(origin.x + 1.5f, 32.5f, origin.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面(y=32.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(origin.x + 1.5f, 33f, origin.z));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilWebUiElement("block-inventory-close", 10f);
        await p.Screenshot(screenshotName);
        await p.ClickWebUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    async UniTask PowerAll()
    {
        // 発電機 → 広範囲電柱 → 工作装置5台へ配線する（結線で電力網の再構築は自動で予約される）
        // Wire generator -> wide-range pole -> the five workshops (wiring schedules the network rebuild)
        var generator = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-12, 32, -20), BlockDirection.North);
        var gasoline = MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"));
        var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-2, 32, -3), BlockDirection.North)).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var wired = new List<(bool connected, string label)>();
        await p.RunOnServer("発電機に給油して工作装置5台へ配線する", () =>
        {
            generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, gasoline), default);
            wired.Add((ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), pole, free), "wire generator -> pole"));
            foreach (var crafter in crafters) wired.Add((ElectricWireSystemUtil.TryConnectBothSides(pole, crafter.GetComponent<IElectricWireConnector>(), free), "wire pole -> workshop"));
        });
        foreach (var (connected, label) in wired) p.Assert(connected, label);
    }

    #endregion
});
