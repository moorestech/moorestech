// 計算データを研究ツリーで消費して製作上限を Lv2 へ上げ、断られていた Lv2 の継承元から上位品が本当に作られることを確かめる
// Spend compute data in the research tree to raise the craft cap to Lv2, then a refused Lv2 source really makes the higher product
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
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.MachineRecipesModule;
using Mooresmaster.Model.ResearchModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var productBaseName = "GPULv1"; // 判断記録 A12 の名前（族の基底 = Lv1）。下で研究の対象に入っているかを assert して食い違いを捕まえる
return PlaytestRunner.Run("tier9-5-compute-research", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形(メサ・岩)が工作装置の手前に湧いて照準と画面を塞ぐので、地形の表示と生成を止める
    // Terrain (mesas, rocks) spawns in front of the workshop and blocks aim and view, so stop its display and spawning
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(1.5f, 32.5f, -8f));

    // Lv2 へ上げるアップグレード研究を名前ではなく効果で引き、前提だけを完了扱いにする
    // Find the Lv2 upgrade by its effect (not its name) and mark only its prerequisites done
    var upgrade = MasterHolder.ResearchMaster.GetAllResearches().Single(r => CraftLevel(r)?.Level == 2);
    var productBaseGuid = MasterHolder.ItemMaster.GetItemMaster(PlaytestItemOps.ResolveItemId(productBaseName)).ItemGuid;
    p.Assert(CraftLevel(upgrade).TargetItemGuids.Contains(productBaseGuid), $"{productBaseName} がアップグレード Lv2（{upgrade.ResearchNodeName}）の対象に入っている");
    var researchStore = p.ServerService<IResearchDataStore>();
    await p.RunOnServer("アップグレード Lv2 の前提を完了扱いにする", () => researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(upgrade.PrevResearchNodeGuids).Select(g => g.ToString()).ToList() }));
    await p.Until(() => upgrade.PrevResearchNodeGuids.All(researchStore.IsResearchCompleted) && !researchStore.IsResearchCompleted(upgrade.ResearchNodeGuid), 15f, "アップグレード Lv2 の前提だけが完了扱いになった");

    // 製品の上限つき継承レシピの機械を置いて給電し、上限1では Lv2 の継承元が断られる
    // Place and power the machine of the product's capped recipe; at cap 1 the Lv2 source is refused
    p.Note($"{productBaseName} の上限つき継承レシピの機械を置き、上限1で Lv2 の継承元が断られるのを見る");
    var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.Single(r => r.OutputItems.Any(o => o.ItemGuid == productBaseGuid) && r.LevelInheritance == MachineRecipeMasterElement.LevelInheritanceConst.InheritUpToUnlocked);
    var machine = await p.PlaceBlockDirect(MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockGuid).Name, new Vector3Int(0, 32, 0), BlockDirection.North);
    var generator = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-12, 32, -20), BlockDirection.North);
    await PowerMachine();
    await SelectRecipe(recipe);
    var inventory = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
    p.Assert(MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape), "製品のレシピが継承レシピである");
    var sourceLv2 = MachineRecipeLevelInheritanceUtil.ResolveInputItemId(recipe, shape.SourceInputIndex, 2);
    var refusedBeforeResearch = await CheckLv2Refused();
    p.Assert(refusedBeforeResearch, $"上限1では Lv2 の継承元（{MasterHolder.ItemMaster.GetItemMaster(sourceLv2).Name}）が断られる");
    var machineObject = await p.WaitBlockGameObject(machine.BlockPositionInfo.OriginalPos);
    await p.Until(() => !machineObject.IsPlayingPlaceAnimation, 15f, "工作装置の設置演出が終わった");
    await p.Screenshot("01-lv2-refused-before-research");

    // 計算データを渡し、研究ツリー（R）から UI で研究する
    // Hand over compute data and research through the tree UI (R)
    var cost = upgrade.ConsumeItems.Single();
    var computeDataName = MasterHolder.ItemMaster.GetItemMaster(cost.ItemGuid).Name;
    p.Note($"{computeDataName} x{cost.ItemCount} を持ち、研究ツリーで {upgrade.ResearchNodeName} を研究する");
    await p.GiveItem(computeDataName, cost.ItemCount);
    await p.Until(() => p.CountItem(computeDataName) == cost.ItemCount, 10f, $"{computeDataName} x{cost.ItemCount} を持った");
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.ClickWebUi($"research-node-{upgrade.ResearchNodeGuid}");
    await p.UntilWebUiElement($"research-button-{upgrade.ResearchNodeGuid}", 10f);
    await p.Screenshot("02-upgrade-node-detail");
    await p.ClickWebUi($"research-button-{upgrade.ResearchNodeGuid}");
    await p.Until(() => researchStore.IsResearchCompleted(upgrade.ResearchNodeGuid), 10f, "UI からアップグレード Lv2 を研究した");
    await p.Until(() => p.CountItem(computeDataName) == 0, 10f, "計算データが研究で消費された");
    p.Assert(p.ServerService<ILevelInheritanceCraftCapLookup>().GetCraftCap(productBaseGuid) == 2, "製作上限が 2 になった");
    await p.Screenshot("03-upgrade-researched");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 断られていた Lv2 の継承元を入れると、上位品が本当に作られる
    // The formerly refused Lv2 source now really makes the higher product
    p.Note("Lv2 の継承元と残りの材料を入れ、上位品の加工を待つ");
    var lv2Rests = new List<int>();
    await Refuel();
    await p.RunOnServer("Lv2 の継承元と残りの材料を工作装置へ入れる", () =>
    {
        for (var i = 0; i < recipe.InputItems.Length; i++)
            lv2Rests.Add(inventory.InsertItem(ServerContext.ItemStackFactory.Create(MachineRecipeLevelInheritanceUtil.ResolveInputItemId(recipe, i, 2), recipe.InputItems[i].Count)).Count);
    });
    p.Assert(lv2Rests.All(r => r == 0), $"研究後は Lv2 の継承元を含む材料が全部入った（残り {string.Join(",", lv2Rests)}）");
    var productLv2 = MachineRecipeLevelInheritanceUtil.ResolveOutputItemId(recipe, 0, 2);
    await p.Until(() => Count(productLv2) >= 1, recipe.Time + 30f, $"{MasterHolder.ItemMaster.GetItemMaster(productLv2).Name} が本当に作られた");
    await ShootMachine("04-lv2-product-crafted");
    p.Note("段5 完了");

    #region Internal

    UnlockLevelInheritanceCraftLevelGameActionParam CraftLevel(ResearchNodeMasterElement node) =>
        node.ClearedActions.items.Where(a => a.GameActionType == GameActionElement.GameActionTypeConst.unlockLevelInheritanceCraftLevel).Select(a => (UnlockLevelInheritanceCraftLevelGameActionParam)a.GameActionParam).FirstOrDefault();

    IEnumerable<Guid> WithPrerequisites(IEnumerable<Guid> targets)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var (done, pending) = (new HashSet<Guid>(), new Stack<Guid>(targets));
        while (pending.TryPop(out var guid))
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        return done;
    }

    int Count(ItemId id) => machine.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Where(s => s.Id == id).Sum(s => s.Count);

    async UniTask<bool> CheckLv2Refused()
    {
        var refused = false;
        await p.RunOnServer("上限1で Lv2 の継承元が置けるかを問い合わせる", () => refused = Enumerable.Range(0, recipe.InputItems.Length).Any(s => inventory.CheckPlacement(s, ServerContext.ItemStackFactory.Create(sourceLv2, 1)) == MachineSlotPlacementCheck.LevelAboveCraftCap));
        return refused;
    }

    async UniTask SelectRecipe(MachineRecipeMasterElement target)
    {
        machine.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        var result = MachineRecipeSelectionResult.Success;
        await p.RunOnServer("工作装置のレシピを選ぶ", () => result = selector.SetSelectedRecipe(target, new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10)));
        p.Assert(result == MachineRecipeSelectionResult.Success, $"select recipe {target.MachineRecipeGuid}");
    }

    async UniTask PowerMachine()
    {
        // 発電機 → 広範囲電柱 → 工作装置を配線する（結線で電力網の再構築は自動で予約される）
        // Wire generator -> wide-range pole -> workshop (wiring schedules the network rebuild)
        var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-2, 32, -3), BlockDirection.North)).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var wired = new List<bool>();
        await p.RunOnServer("発電機 → 電柱 → 工作装置を配線する", () =>
        {
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), pole, free));
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(pole, machine.GetComponent<IElectricWireConnector>(), free));
        });
        p.Assert(wired.Count == 2 && wired.All(w => w), "発電機 → 電柱 → 工作装置を配線した");
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
        await p.UntilWebUiElement("block-inventory-close", 10f);
        await p.Screenshot(screenshotName);
        await p.ClickWebUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    #endregion
});
