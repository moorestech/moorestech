// 出荷マスタ全体で起動し、半導体製造の研究から IPA と Lv1/Lv2 の ICチップが室内ラインで作れることを通しで確かめる
// Boot on the full shipped master and confirm IPA and Lv1/Lv2 IC chips come out of the clean room line after the semiconductor research
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

var research = new[] { "019e3ad9-46e2-73fd-9555-8c1d0245d26d", "01a167f3-bd6b-70c9-be6e-52e41907b21f", "019e3ae2-5005-7439-b242-9b7b3191a2c3", "01a167f3-bd6e-7745-84c0-0e1cf105c2cc",
    "01a167f3-bd74-73f8-8c43-1869db833299", "01a167f3-bd77-7013-b982-5f4cdf3c86a9", "01a167f3-bd71-707c-b8e6-615063559cc1", "01a167f3-bd7a-75c4-a266-8b9a4d5d1900" }.Select(Guid.Parse).ToArray();
return PlaytestRunner.Run("tier9-1a-chip-line", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形(メサ・岩)は足場を突き抜けて照準と画面を狂わせるので、cleanroom-room-usability と同じく地形の表示と生成を止める
    // Terrain (mesas, rocks) pokes through the scaffold and skews aim and view, so stop its display and spawning like cleanroom-room-usability
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(7.5f, 33.5f, -9f));
    var ipaId = FluidIdOf("イソプロピルアルコール");
    var (stages, consumers) = (new List<IBlock>(), new List<IBlock>());

    // 半導体製造と S1 の研究を前提ごと完了扱いにする（解放アクションも再実行される）
    // Mark the semiconductor and S1 research done with all prerequisites (unlock actions are replayed)
    p.Note("半導体製造と S1 の研究を前提ごと完了させる");
    await p.RunOnServer("研究を前提ごと完了扱いにする", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(research).Select(g => g.ToString()).ToList() }));
    await p.WaitForResearch(research, Array.Empty<Guid>(), 15f, "S1 research completed");

    // 化学プラントで ナフサ＋水 → IPA を回す。液体は機械のタンクへ直接入れる（tick 外にパイプへ注入すると流体網の複製がサーバーと食い違うため）
    // Run naphtha + water -> IPA; fluids go straight into machine tanks (injecting into pipes outside the tick desyncs the fluid replica)
    p.Note("化学プラントへナフサと水を入れ、IPA のレシピを選ぶ");
    var chem = await Place("化学プラント", new Vector3Int(0, 32, -26));
    var ipaOutPipe = await p.PlaceBlockDirect("鋼鉄のパイプ", new Vector3Int(2, 33, -15), BlockDirection.North);
    await SelectRecipe(chem, MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.OutputFluids.Any(f => MasterHolder.FluidMaster.GetFluidId(f.FluidGuid) == ipaId)));

    // 部屋を建て、室内に専用機5種と清浄機2台を置いて工程レシピを選ぶ
    // Build the room, place the five machines and two air filters inside, and select each stage recipe
    p.Note("クリーンルームを建て、専用機5種と清浄機を置いて給電する");
    await BuildRoom();
    var cutter = await PlaceStage("ウェハ切断機", new Vector3Int(2, 33, 1), "シリコンウェハ");
    var depo = await PlaceStage("成膜装置", new Vector3Int(6, 33, 1), "トランジスタ設置済みウェハG1");
    var euv = await PlaceStage("EUV露光式半導体製造装置", new Vector3Int(10, 33, 1), "1層パターンウェハG1");
    var dicing = await PlaceStage("ダイシング装置", new Vector3Int(2, 33, 5), "シリコンダイLv1");
    var packaging = await PlaceStage("パッケージング装置", new Vector3Int(6, 33, 5), "ICチップLv1");
    foreach (var x in new[] { 10, 12 }) await GiveWithFluids(await Place("クリーンルーム空気清浄機", new Vector3Int(x, 33, 5)), 0, "クリーンルームフィルター", 5);
    await PowerAll();
    await p.WaitForCleanRoom(cutter, true, 30f, "clean room classified");
    foreach (var cell in new[] { new Vector3Int(14, 36, 10), new Vector3Int(7, 33, 0), new Vector3Int(0, 32, -26) }) await p.WaitBlockGameObject(cell);
    await p.Screenshot("01-room-and-line");

    // IPA が化学プラントから出て、切断 → 成膜 → EUV の各工程が出力を出す
    // IPA comes out of the chemical plant, then slicing, deposition and EUV each produce output
    await p.RunOnServer("IPA・ナフサ・水をタンクいっぱいまで入れる", FillFluids);
    await p.Until(() => ipaOutPipe.GetComponent<Game.Block.Blocks.Fluid.IFluidInventory>().GetFluidInventory().Any(f => f.FluidId == ipaId && f.Amount > 0), 60f, "IPA produced");
    p.Note("高純度シリコン→ウェハ→成膜→EUV の順に各工程へ素材を入れる");
    await GiveWithFluids(cutter, 0, "高純度シリコン", 20);
    await p.Until(() => Count(cutter, "シリコンウェハ") > 0, 60f, "wafer sliced");
    await GiveWithFluids(depo, 0, "シリコンウェハ", 2);
    await p.Until(() => Count(depo, "トランジスタ設置済みウェハ") > 0, 60f, "transistor wafer deposited");
    await GiveWithFluids(euv, 0, "トランジスタ設置済みウェハG1", 4);
    await p.Until(() => Count(euv, "1層パターンウェハ") + Count(euv, "失敗したパターンウェハ") > 0, 60f, "EUV layer exposed");

    // ダイシング（G2 完成ウェハ）で Lv1 と Lv2 のダイが出る
    // Dicing a G2 finished wafer yields Lv1 and Lv2 dies
    p.Note("G2 の完成ウェハをダイシングして Lv1/Lv2 ダイを取る");
    await GiveWithFluids(dicing, 0, "完成パターンウェハG2", 1);
    await p.Until(() => Count(dicing, "シリコンダイLv1") >= 10 && Count(dicing, "シリコンダイLv2") >= 10, 60f, "Lv1 and Lv2 dies diced");
    await ShootMachine(dicing, "02-dies");

    // パッケージングで Lv1 → ICチップLv1、Lv2 → ICチップLv2（継承レシピ）。出力スロットは1つなので Lv1 を取り出してから Lv2 を流す
    // Packaging turns Lv1 dies into IC chip Lv1 and Lv2 into IC chip Lv2 (inheritance); the single output slot means Lv1 is taken out before Lv2
    p.Note("Lv1 ダイと Lv2 ダイを順にパッケージングする");
    await GiveWithFluids(packaging, 1, "ICチップ基板", 2);
    await MoveDies("シリコンダイLv1");
    await p.Until(() => Count(packaging, "ICチップLv1") > 0, 30f, "IC chip Lv1 packaged");
    await ShootMachine(packaging, "03-chips-lv1");
    p.Note($"ICチップLv1 x{Count(packaging, "ICチップLv1")} を取り出し、Lv2 ダイを入れる");
    await p.RunOnServer("ICチップLv1 を取り出す", () => { foreach (var slot in Enumerable.Range(0, Slots(packaging).GetSlotSize()).Where(s => Slots(packaging).GetItem(s).Id == PlaytestItemOps.ResolveItemId("ICチップLv1"))) Slots(packaging).SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty()); });
    await MoveDies("シリコンダイLv2");
    await p.Until(() => Count(packaging, "ICチップLv2") > 0, 30f, "IC chip Lv2 packaged");
    await ShootMachine(packaging, "03-chips");
    p.Note("段1a 完了");
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
    IOpenableBlockInventoryComponent Slots(IBlock block) => block.GetComponent<IOpenableBlockInventoryComponent>();
    async UniTask GiveWithFluids(IBlock block, int slot, string item, int count) => await p.RunOnServer($"{item} x{count} を {block.BlockMasterElement.Name} へ入れ、液体を満たす", () => { Slots(block).SetItem(slot, PlaytestItemOps.ResolveItemId(item), count); FillFluids(); });
    int Count(IBlock block, string prefix) => Slots(block).InventoryItems.Where(s => s.Count > 0 && MasterHolder.ItemMaster.GetItemMaster(s.Id).Name.StartsWith(prefix)).Sum(s => s.Count);
    FluidId FluidIdOf(string name) => MasterHolder.FluidMaster.GetFluidId(MasterHolder.FluidMaster.Fluids.Data.First(f => f.Name == name).FluidGuid);
    async UniTask<IBlock> PlaceStage(string name, Vector3Int origin, string firstOutputName)
    {
        // 室内の工程機械を置き、最初の出力で工程レシピを選ぶ（未選択の機械は加工しない）
        // Place an in-room stage machine and select its stage recipe by first output (unselected machines never process)
        var (machine, output) = (await Place(name, origin), MasterHolder.ItemMaster.GetItemMaster(PlaytestItemOps.ResolveItemId(firstOutputName)).ItemGuid);
        stages.Add(machine);
        await SelectRecipe(machine, MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == machine.BlockMasterElement.BlockGuid && r.OutputItems.Any() && r.OutputItems.First().ItemGuid == output));
        return machine;
    }
    void FillFluids()
    {
        // サーバー tick 内で工程機械へ IPA、化学プラントへナフサと水をタンクいっぱいまで入れる（各工程は1回の加工で足り、溢れた分は捨てる）
        // Inside the server tick, fill stage tanks with IPA and the chemical plant with naphtha/water (one craft per stage suffices; overflow is dropped)
        foreach (var stage in stages) stage.GetComponent<VanillaMachineFluidInventoryComponent>().AddLiquid(new FluidStack(100000, ipaId), default);
        foreach (var fluid in new[] { "ナフサ", "水" }) chem.GetComponent<VanillaMachineFluidInventoryComponent>().AddLiquid(new FluidStack(100000, FluidIdOf(fluid)), default);
    }
    async UniTask MoveDies(string dieName) => await p.RunOnServer($"{dieName} x10 をダイシングからパッケージングへ移し、液体を満たす", () =>
    {
        var (id, dicingSlots) = (PlaytestItemOps.ResolveItemId(dieName), Slots(dicing));
        var slot = Enumerable.Range(0, dicingSlots.GetSlotSize()).First(s => dicingSlots.GetItem(s).Id == id && dicingSlots.GetItem(s).Count >= 10);
        dicingSlots.SetItem(slot, id, dicingSlots.GetItem(slot).Count - 10);
        Slots(packaging).SetItem(0, id, 10);
        FillFluids();
    });
    async UniTask SelectRecipe(IBlock block, Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement recipe)
    {
        block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        var result = MachineRecipeSelectionResult.Success;
        await p.RunOnServer($"{block.BlockMasterElement.Name} のレシピを選ぶ", () => result = selector.SetSelectedRecipe(recipe, new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10)));
        p.Assert(result == MachineRecipeSelectionResult.Success, $"select recipe {recipe.MachineRecipeGuid}");
    }
    async UniTask ShootMachine(IBlock block, string screenshotName)
    {
        // F は射程2mなので南面の1m手前に着地し、Alt のカーソル照準で南面を狙って機械 UI を開いて撮る（cleanroom-room-usability と同じ手順）
        // F reaches 2 m, so land 1 m south of the face, open the machine UI by aiming at it with the Alt cursor, and shoot (same as cleanroom-room-usability)
        var origin = block.BlockPositionInfo.OriginalPos;
        p.WarpPlayer(new Vector3(origin.x + 0.3f, 33.5f, origin.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 33f) < 0.05f, 5f, "床パネルの上(y=33.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(origin.x + 1f, 34f, origin.z + 0.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilWebUiElement("block-inventory-close", 10f);
        await p.Screenshot(screenshotName);
        await p.ClickWebUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }
    async UniTask BuildRoom()
    {
        // 内寸 x1..13 / z1..9 / y33..36。室内で機械 UI を開くとき三人称カメラ（足元+約2.9m）が天井より下に来るよう、天井は y37 にする
        // Interior x1..13 / z1..9 / y33..36; the ceiling sits at y37 so the third-person camera (about 2.9 m above the feet) stays under it when opening UIs inside
        var door = new Vector3Int(7, 33, 0);
        for (var x = 0; x <= 14; x++)
        for (var z = 0; z <= 10; z++)
        {
            await p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 32, z), BlockDirection.North);
            await p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, 37, z), BlockDirection.North);
            if (x != 0 && x != 14 && z != 0 && z != 10) continue;
            for (var y = x == door.x && z == door.z ? 35 : 33; y <= 36; y++) await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, z), BlockDirection.North);
        }
        await p.PlaceBlockDirect("クリーンルームドア", door, BlockDirection.North);
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