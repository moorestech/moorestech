// 出荷マスタ全体で起動し、専用機5種の見た目が互いに違い、成膜・EUV・ダイシングの機械UIに抽選の中身が出ることを確かめる
// Boot on the full shipped master and confirm the five machines look distinct and the deposition, EUV and dicing UIs show the draw values
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

var machineNames = new[] { "ウェハ切断機", "成膜装置", "EUV露光式半導体製造装置", "ダイシング装置", "パッケージング装置" };
var semiconductorResearch = Guid.Parse("019e3ad9-46e2-73fd-9555-8c1d0245d26d");
return PlaytestRunner.Run("tier9-3-yield-ui", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形(メサ・岩)は足場を突き抜けて照準と画面を狂わせるので、cleanroom-room-usability と同じく地形の表示と生成を止める
    // Terrain (mesas, rocks) pokes through the scaffold and skews aim and view, so stop its display and spawning like cleanroom-room-usability
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    var consumers = new List<IBlock>();

    // 半導体製造の研究を前提ごと完了扱いにする
    // Mark the semiconductor research done with all prerequisites
    var researchStore = p.ServerService<IResearchDataStore>();
    await p.RunOnServer("半導体製造の研究を完了扱いにする", () => researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(semiconductorResearch).Select(g => g.ToString()).ToList() }));
    p.Assert(researchStore.IsResearchCompleted(semiconductorResearch), "semiconductor research completed");

    // 床のパネルを敷き、専用機5種を1列に置いて、壁を建てる前に設置演出の終わった見た目を撮る
    // Lay the panel floor, line up the five machines and shoot their looks (after the placement effect) before the walls go up
    p.Note("専用機5種を1列に置き、見た目の違いを撮る");
    for (var x = 0; x <= 22; x++)
    for (var z = 0; z <= 10; z++)
        await p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 32, z), BlockDirection.North);
    var machines = new IBlock[machineNames.Length];
    for (var i = 0; i < machineNames.Length; i++) machines[i] = await Place(machineNames[i], new Vector3Int(2 + i * 4, 33, 6));
    p.WarpPlayer(new Vector3(11.5f, 32.5f, -6f));
    await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面(y=32.0)に着地した");
    foreach (var machine in machines)
    {
        var blockObject = await p.WaitBlockGameObject(machine.BlockPositionInfo.OriginalPos);
        await p.Until(() => !blockObject.IsPlayingPlaceAnimation, 15f, $"{machine.BlockMasterElement.Name} の設置演出が終わった");
    }
    await p.Screenshot("01-five-machines");

    // 壁・ドア・天井で閉じ、清浄機2台にフィルターを入れて給電し、クラスが付くまで待つ
    // Close the room with walls, door and ceiling, load two air filters, power everything and wait for a class
    p.Note("部屋を閉じて清浄機を動かし、クラスが付くのを待つ");
    await BuildWallsAndCeiling();
    foreach (var x in new[] { 1, 21 }) await Give(await Place("クリーンルーム空気清浄機", new Vector3Int(x, 33, 2)), 0, "クリーンルームフィルター", 5);
    await PowerAll();
    await p.Until(() => p.ServerService<CleanRoomDatastore>().TryGetCleanRoom(machines[1], out var r) && r.ThresholdIndex < MasterHolder.CleanRoomMaster.OutThresholdIndex, 30f, "clean room classified");

    // 抽選欄は役割レシピを選んだ機械にだけ出るので、成膜・EUV1層・ダイシングを選ぶ
    // The draw section only shows for a selected role recipe, so select deposition, EUV layer 1 and dicing
    await SelectRecipeByFirstOutput(machines[1], "トランジスタ設置済みウェハG1");
    await SelectRecipeByFirstOutput(machines[2], "1層パターンウェハG1");
    await SelectRecipeByFirstOutput(machines[3], "シリコンダイLv1");

    // 室内に入り、3台の機械UIで抽選欄の行を確かめて撮る
    // Step inside and check and shoot the draw rows in the three machine UIs
    p.Note("成膜・EUV・ダイシングの機械UIを開き、抽選欄を確かめる");
    await CheckYieldUi(machines[1], "02-deposition-yield", new[] { "clean-room-yield-max-grade", "clean-room-yield-promotion-chance" });
    await CheckYieldUi(machines[2], "03-euv-yield", new[] { "clean-room-yield-layer-fail-rate" });
    await CheckYieldUi(machines[3], "04-dicing-yield", new[] { "clean-room-yield-die-loss-rate" });
    p.Note("段3 完了");

    #region Internal

    IEnumerable<Guid> WithPrerequisites(Guid target)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var done = new HashSet<Guid>();
        var pending = new Stack<Guid>(new[] { target });
        while (pending.TryPop(out var guid))
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        return done;
    }

    async UniTask<IBlock> Place(string name, Vector3Int pos)
    {
        var block = await p.PlaceBlockDirect(name, pos, BlockDirection.North);
        consumers.Add(block);
        return block;
    }

    async UniTask Give(IBlock block, int slot, string item, int count) => await p.RunOnServer($"{item} x{count} を {block.BlockMasterElement.Name} へ入れる", () => block.GetComponent<IOpenableBlockInventoryComponent>().SetItem(slot, PlaytestItemOps.ResolveItemId(item), count));

    async UniTask BuildWallsAndCeiling()
    {
        // 内寸 x1..21 / z1..9 / y33..36。室内で機械 UI を開くとき三人称カメラ（足元+約2.9m）が天井より下に来るよう、天井は y37 のパネルにする
        // Interior x1..21 / z1..9 / y33..36; the ceiling is y37 panels so the third-person camera (about 2.9 m above the feet) stays under it inside
        var door = new Vector3Int(11, 33, 0);
        for (var x = 0; x <= 22; x++)
        for (var z = 0; z <= 10; z++)
        {
            await p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 37, z), BlockDirection.North);
            if (x != 0 && x != 22 && z != 0 && z != 10) continue;
            for (var y = x == door.x && z == door.z ? 35 : 33; y <= 36; y++) await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, z), BlockDirection.North);
        }
        await p.PlaceBlockDirect("クリーンルームドア", door, BlockDirection.North);
    }

    async UniTask SelectRecipeByFirstOutput(IBlock block, string firstOutputName)
    {
        // その機械の工程レシピのうち、最初の出力が指定アイテムのものを選ぶ
        // Select the machine's stage recipe whose first output is the given item
        var output = MasterHolder.ItemMaster.GetItemMaster(PlaytestItemOps.ResolveItemId(firstOutputName)).ItemGuid;
        var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == block.BlockMasterElement.BlockGuid && r.OutputItems.Any() && r.OutputItems.First().ItemGuid == output);
        var overflow = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10);
        block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        var result = MachineRecipeSelectionResult.Success;
        await p.RunOnServer($"{block.BlockMasterElement.Name} のレシピを選ぶ", () => result = selector.SetSelectedRecipe(recipe, overflow));
        p.Assert(result == MachineRecipeSelectionResult.Success, $"select recipe -> {firstOutputName}");
    }

    async UniTask CheckYieldUi(IBlock machine, string screenshotName, string[] rowTestIds)
    {
        // F は射程2mなので南面の1m手前に着地し、Alt のカーソル照準で南面を狙って開く。抽選欄と行を待って撮り、Tab で閉じる
        // F reaches 2 m, so land 1 m south of the face and open it by aiming with the Alt cursor; wait for the section and rows, shoot, close with Tab
        var origin = machine.BlockPositionInfo.OriginalPos;
        p.WarpPlayer(new Vector3(origin.x + 1.3f, 33.5f, origin.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 33f) < 0.05f, 5f, "床パネルの上(y=33.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(origin.x + 1.5f, 34f, origin.z + 0.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilWebUiElement("clean-room-yield-section", 10f);
        foreach (var rowTestId in rowTestIds) await p.UntilWebUiElement(rowTestId, 10f);
        await p.Screenshot(screenshotName);
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    async UniTask PowerAll()
    {
        // 室外の発電機 → 広範囲電柱 → 室内の機械と清浄機へ配線する（結線で電力網の再構築は自動で予約される）
        // Wire the outdoor generator -> wide-range pole -> the machines and air filters inside (wiring schedules the network rebuild)
        var generator = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-6, 32, -14), BlockDirection.North);
        var gasoline = MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"));
        var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(11, 32, -3), BlockDirection.North)).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var wired = new List<(bool connected, string label)>();
        await p.RunOnServer("発電機に給油して全消費ブロックへ配線する", () =>
        {
            generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, gasoline), default);
            wired.Add((ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), pole, free), "wire generator -> pole"));
            foreach (var block in consumers) wired.Add((ElectricWireSystemUtil.TryConnectBothSides(pole, block.GetComponent<IElectricWireConnector>(), free), $"wire pole -> {block.BlockMasterElement.Name}"));
        });
        foreach (var (connected, label) in wired) p.Assert(connected, label);
    }

    #endregion
});
