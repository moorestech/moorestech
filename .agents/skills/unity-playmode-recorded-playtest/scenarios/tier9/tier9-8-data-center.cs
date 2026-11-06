// 出荷マスタで GPU設計を研究済みにしてデータセンターを置き、GPU Lv1 を入れると計算データがレシピどおり出ることと、仮モデルが画面に出ることを確かめる
// With the shipped master, mark GPU design done, place a Data Center, feed one GPU Lv1, and check the recipe's compute data appears and the placeholder model shows
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
using Mooresmaster.Model.MachineRecipesModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var gpuDesignResearch = Guid.Parse("01a167f3-bd74-73f8-8c43-1869db833299"); // GPU設計
return PlaytestRunner.Run("tier9-8-data-center", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形が機械の手前に湧いて照準と画面を塞ぐので、地形の表示と生成を止める
    // Terrain spawns in front of the machine and blocks aim and view, so stop its display and spawning
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(1.5f, 32.5f, -8f));

    // GPU設計とその前提を完了扱いにし、データセンターが解放されたことを確かめる
    // Mark GPU design and its prerequisites done and check the Data Center is unlocked
    await p.RunOnServer("GPU設計とその前提を完了扱いにする", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(new[] { gpuDesignResearch }).Select(g => g.ToString()).ToList() }));
    await p.WaitForResearch(new[] { gpuDesignResearch }, Array.Empty<Guid>(), 15f, "GPU設計が完了扱いになった");

    // データセンターを置いて給電し、GPU Lv1 のレシピを選ぶ
    // Place and power the Data Center and select the GPU Lv1 recipe
    p.Note("データセンターを置いて給電し、GPU Lv1 → 計算データのレシピを選ぶ");
    var gpuLv1 = PlaytestItemOps.ResolveItemId("GPULv1");
    var computeData = PlaytestItemOps.ResolveItemId("計算データ");
    var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(PlaytestBlockOps.ResolveBlockId("データセンター")).BlockGuid;
    var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.Single(r => r.BlockGuid == blockGuid && r.InputItems.Single().ItemGuid == MasterHolder.ItemMaster.GetItemMaster(gpuLv1).ItemGuid);
    var expected = recipe.OutputItems.Single().Count;
    p.Assert(expected == 5, $"GPU Lv1 1個から計算データ 5 個のレシピである（{expected} 個）");
    var machine = await p.PlaceBlockDirect("データセンター", new Vector3Int(0, 32, 0), BlockDirection.North);
    var generator = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-12, 32, -20), BlockDirection.North);
    await PowerMachine();
    await SelectRecipe(recipe);
    var machineObject = await p.WaitBlockGameObject(machine.BlockPositionInfo.OriginalPos);
    await p.Until(() => !machineObject.IsPlayingPlaceAnimation, 15f, "データセンターの設置演出が終わった");
    await p.Screenshot("01-data-center-placed");

    // GPU Lv1 を1個入れ、計算データが出力欄に出るまで待つ
    // Insert one GPU Lv1 and wait for compute data in the output slots
    p.Note("GPU Lv1 を1個入れ、計算データ 5 個が出るのを待つ");
    var rest = -1;
    await p.RunOnServer("GPU Lv1 を1個入れる", () => rest = machine.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(ServerContext.ItemStackFactory.Create(gpuLv1, 1)).Count);
    p.Assert(rest == 0, "GPU Lv1 が入った");
    await Refuel();
    await p.Until(() => Count(computeData) == expected, recipe.Time + 30f, $"計算データ {expected} 個が出力欄に出た");
    await p.Until(() => Count(gpuLv1) == 0, 5f, "GPU Lv1 が消費された");
    await ShootMachine("02-compute-data-output");
    p.Note("データセンター 完了");

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

    async UniTask SelectRecipe(MachineRecipeMasterElement target)
    {
        machine.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        var result = MachineRecipeSelectionResult.Success;
        await p.RunOnServer("データセンターのレシピを選ぶ", () => result = selector.SetSelectedRecipe(target, new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10)));
        p.Assert(result == MachineRecipeSelectionResult.Success, $"select recipe {target.MachineRecipeGuid}");
    }

    async UniTask PowerMachine()
    {
        // 発電機 → 広範囲電柱 → データセンターを配線する（結線で電力網の再構築は自動で予約される）
        // Wire generator -> wide-range pole -> data center (wiring schedules the network rebuild)
        var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-2, 32, -3), BlockDirection.North)).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var wired = new List<bool>();
        await p.RunOnServer("発電機 → 電柱 → データセンターを配線する", () =>
        {
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), pole, free));
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(pole, machine.GetComponent<IElectricWireConnector>(), free));
        });
        p.Assert(wired.Count == 2 && wired.All(w => w), "発電機 → 電柱 → データセンターを配線した");
        await Refuel();
    }

    // 発電機のタンクは数十秒で空になるので、加工を待つ前に満タンまで入れ直す（溢れた分は捨てる）
    // The generator tank empties within tens of seconds, so top it up before waiting on a craft (overflow is dropped)
    async UniTask Refuel() => await p.RunOnServer("発電機を満タンまで給油する", () => generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(100000, MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"))), default));

    async UniTask ShootMachine(string screenshotName)
    {
        // F は射程2mなので南面の1m手前に着地し、Alt のカーソル照準で南面を狙って機械 UI を開き、出力欄が見える状態で撮る
        // F reaches 2 m, so land 1 m south of the face, open the machine UI by aiming at it with the Alt cursor, and shoot with the output slots visible
        var origin = machine.BlockPositionInfo.OriginalPos;
        p.WarpPlayer(new Vector3(origin.x + 1.5f, 32.5f, origin.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面(y=32.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(origin.x + 1.5f, 33f, origin.z));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilUiElement("block-inventory-close", 10f);
        await p.Screenshot(screenshotName);
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    int Count(ItemId id) => machine.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Where(s => s.Id == id).Sum(s => s.Count);

    #endregion
});
