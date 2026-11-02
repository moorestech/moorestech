// 核融合炉の電力で反物質を作り、それを使って反物質爆弾を作り、UI の最終研究で爆弾を消費して終わることを確かめる
// Make antimatter on fusion power, use it to make an antimatter bomb, and finish by spending bombs in the final research through the UI
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
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var antimatterRecipeGuid = Guid.Parse("01a167e9-21df-7b2a-81e3-04be55cb89b0");
var bombRecipeGuid = Guid.Parse("01a167e9-21e2-7c4e-91be-8ad747466bde");
var finalResearchGuid = Guid.Parse("01a167e9-21f6-7d26-a990-e7d6d78750d0");
var deuteriumGuid = Guid.Parse("01a167e9-21b2-7bb0-b6a3-0f17d349704e");
return PlaytestRunner.Run("tier9-7-antimatter-final-research", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形(メサ・岩)が機械の手前に湧いて照準と画面を塞ぐので、地形の表示と生成を止める
    // Terrain (mesas, rocks) spawns in front of the machines and blocks aim and view, so stop its display and spawning
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(5f, 32.5f, -12f));
    var deuterium = MasterHolder.FluidMaster.GetFluidId(deuteriumGuid);
    var finalResearch = MasterHolder.ResearchMaster.GetResearch(finalResearchGuid);
    await p.RunOnServer("最終研究の前提を完了扱いにする", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(finalResearch.PrevResearchNodeGuids).Select(g => g.ToString()).ToList() }));
    await p.WaitForResearch(finalResearch.PrevResearchNodeGuids, new[] { finalResearchGuid }, 15f, "最終研究の前提だけが完了扱いになった");

    // 反物質製造装置と電気汎用工作装置を置き、核融合炉で給電する。炉の出力が製造装置の要求以上であることを先に確かめる
    // Place the antimatter maker and the electric workshop powered by a fusion reactor; first check the reactor output covers the maker
    p.Note("反物質製造装置と電気汎用工作装置を置き、核融合炉から給電する");
    var antimatterRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(antimatterRecipeGuid);
    var bombRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(bombRecipeGuid);
    var maker = await p.PlaceBlockDirect(MasterHolder.BlockMaster.GetBlockMaster(antimatterRecipe.BlockGuid).Name, new Vector3Int(2, 32, 0), BlockDirection.North);
    var workbench = await p.PlaceBlockDirect(MasterHolder.BlockMaster.GetBlockMaster(bombRecipe.BlockGuid).Name, new Vector3Int(8, 32, 0), BlockDirection.North);
    var reactor = await p.PlaceBlockDirect("核融合炉", new Vector3Int(-22, 32, -5), BlockDirection.North);
    var reactorPower = ((ElectricGeneratorBlockParam)reactor.BlockMasterElement.BlockParam).FuelFluids.Max(f => f.Power);
    p.Assert(reactorPower >= ((ElectricMachineBlockParam)maker.BlockMasterElement.BlockParam).RequiredPower, $"炉の出力 {reactorPower} が反物質製造装置の要求以上");
    await WireAll();
    await SelectRecipe(maker, antimatterRecipe);
    await SelectRecipe(workbench, bombRecipe);

    // 反物質を1個本当に作る。重水素は製造装置のタンクへ直接入れる（パイプ注入は流体網の複製と食い違うため）
    // Really make one antimatter; deuterium goes straight into the maker tank (pipe injection desyncs the fluid replica)
    var antimatterId = MasterHolder.ItemMaster.GetItemId(antimatterRecipe.OutputItems[0].ItemGuid);
    await WaitCountRefueling(maker, antimatterId, antimatterRecipe.Time * 2f + 30f, "反物質が本当に1個できた");
    await ShootMachine(maker, "01-antimatter-made");

    // 作った反物質を出力口から抜いて工作装置へ移し、不足分は直渡しで足して反物質爆弾を1個本当に作る
    // Move the made antimatter from the outputs into the workshop, hand over the rest, and really make one bomb
    p.Note("作った反物質1個を工作装置へ移し、不足分を足して反物質爆弾を作る");
    var rests = new List<int>();
    await p.RunOnServer("作った反物質を工作装置へ移し、爆弾の材料の残りを入れる", () =>
    {
        TakeFromOutputs(maker, antimatterId);
        var inventory = workbench.GetComponent<VanillaMachineBlockInventoryComponent>();
        foreach (var input in bombRecipe.InputItems) rests.Add(inventory.InsertItem(ServerContext.ItemStackFactory.Create(input.ItemGuid, input.Count)).Count);
    });
    p.Assert(rests.All(r => r == 0), $"反物質爆弾の材料が全部入った（残り {string.Join(",", rests)}）");
    var bombId = MasterHolder.ItemMaster.GetItemId(bombRecipe.OutputItems[0].ItemGuid);
    await WaitCountRefueling(workbench, bombId, bombRecipe.Time * 2f + 30f, "反物質爆弾が本当に1個できた");
    await ShootMachine(workbench, "02-bomb-made");

    // 作った1個を持ち、残りを渡して、研究ツリー（R）から最終研究を行う
    // Carry the made bomb, receive the rest, and run the final research from the tree (R)
    var bombName = MasterHolder.ItemMaster.GetItemMaster(bombId).Name;
    var bombCost = finalResearch.ConsumeItems.Single(c => c.ItemGuid == bombRecipe.OutputItems[0].ItemGuid).ItemCount;
    p.Note($"作った{bombName}1個を持ち、残り {bombCost - 1} 個を受け取って最終研究を行う");
    await p.RunOnServer($"作った{bombName}を工作装置の出力口から抜く", () => TakeFromOutputs(workbench, bombId));
    await p.GiveItemDirect(bombName, 1);
    await p.GiveItem(bombName, bombCost - 1);
    await p.Until(() => p.CountItem(bombName) == bombCost, 10f, $"{bombName} x{bombCost} を持った");
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.ClickWebUi($"research-node-{finalResearchGuid}");
    await p.UntilWebUiElement($"research-button-{finalResearchGuid}", 10f);
    await p.Screenshot("03-final-research-detail");
    await p.ClickWebUi($"research-button-{finalResearchGuid}");
    await p.WaitForResearch(new[] { finalResearchGuid }, Array.Empty<Guid>(), 10f, "UI から最終研究を完了した");
    await p.Until(() => p.CountItem(bombName) == 0, 10f, $"{bombName} {bombCost} 個が消費された");
    await p.Screenshot("04-final-research-complete");
    p.Note("段7 完了");

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

    int Count(IBlock block, ItemId id) => block.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Where(s => s.Id == id).Sum(s => s.Count);

    void TakeFromOutputs(IBlock block, ItemId itemId)
    {
        var inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
        var slot = Enumerable.Range(0, inventory.GetSlotSize()).First(s => inventory.GetItem(s).Id == itemId && inventory.GetItem(s).Count >= 1);
        inventory.SetItem(slot, itemId, inventory.GetItem(slot).Count - 1);
    }

    // 炉と製造装置のタンクは数十秒で空になるので、待つ間に重水素を満タンまで入れ直す（溢れた分は捨てる）
    // Reactor and maker tanks empty within tens of seconds, so top up deuterium while waiting (overflow is dropped)
    async UniTask Refuel() => await p.RunOnServer("核融合炉と反物質製造装置へ重水素を満タンまで入れる", () =>
    {
        reactor.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(100000, deuterium), default);
        maker.GetComponent<VanillaMachineFluidInventoryComponent>().AddLiquid(new FluidStack(100000, deuterium), default);
    });

    async UniTask WaitCountRefueling(IBlock block, ItemId id, float timeoutSeconds, string label)
    {
        // 1秒ごとに数を見て、10秒ごとに重水素を足す。期限切れは Until の失敗として記録する
        // Check the count every second and add deuterium every ten; a timeout is recorded as an Until failure
        var deadline = Time.realtimeSinceStartup + timeoutSeconds;
        for (var second = 0; Count(block, id) < 1 && Time.realtimeSinceStartup < deadline; second++)
        {
            if (second % 10 == 0) await Refuel();
            await UniTask.Delay(1000);
        }
        await p.Until(() => Count(block, id) >= 1, 0f, label);
    }

    async UniTask WireAll()
    {
        // 炉 → 広範囲電柱 → 製造装置・工作装置を配線する（結線で電力網の再構築は自動で予約される）
        // Wire reactor -> wide-range pole -> maker and workshop (wiring schedules the network rebuild)
        var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(0, 32, -3), BlockDirection.North)).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var wired = new List<bool>();
        await p.RunOnServer("炉 → 電柱 → 製造装置・工作装置を配線する", () =>
        {
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(reactor.GetComponent<IElectricWireConnector>(), pole, free));
            foreach (var block in new[] { maker, workbench }) wired.Add(ElectricWireSystemUtil.TryConnectBothSides(pole, block.GetComponent<IElectricWireConnector>(), free));
        });
        p.Assert(wired.Count == 3 && wired.All(w => w), "炉 → 電柱 → 製造装置・工作装置を配線した");
    }

    async UniTask SelectRecipe(IBlock block, MachineRecipeMasterElement recipe)
    {
        block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        var result = MachineRecipeSelectionResult.Success;
        await p.RunOnServer($"{block.BlockMasterElement.Name} のレシピを選ぶ", () => result = selector.SetSelectedRecipe(recipe, new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10)));
        p.Assert(result == MachineRecipeSelectionResult.Success, $"select recipe {recipe.MachineRecipeGuid}");
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

    #endregion
});
