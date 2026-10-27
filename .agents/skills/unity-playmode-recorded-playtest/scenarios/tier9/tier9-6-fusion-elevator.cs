// 出荷マスタで核融合炉を動かして宇宙エレベーター拠点へ給電し、ケーブルで接続して GPU衛星Lv1 が昇り切り出口に成果が出ることを確かめる
// On the shipped master, run a fusion reactor to power the space elevator base, connect it with cables, and see a GPU satellite Lv1 climb all the way and deliver
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.EnergySystem;
using Game.Fluid;
using Game.Orbit;
using Game.Research;
using Game.SpaceElevator;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var research = new[] { "01a16291-4094-7369-a87f-5dfe2197d25e", "019e3af0-297f-726e-8a02-0a8518124e6d" }.Select(Guid.Parse).ToArray(); // 軌道エレベーターの建設・核融合炉
var (reactorPos, elevatorPos) = (new Vector3Int(-20, 32, 0), new Vector3Int(2, 32, 0));
return PlaytestRunner.Run("tier9-6-fusion-elevator", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形(メサ・岩)が炉と拠点の手前に湧いて照準と画面を塞ぐので、地形の表示と生成を止める
    // Terrain (mesas, rocks) spawns in front of the reactor and base and blocks aim and view, so stop its display and spawning
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(0f, 32.5f, -15f));
    var deuteriumId = MasterHolder.FluidMaster.GetFluidId(Guid.Parse("01a167e9-21b2-7bb0-b6a3-0f17d349704e"));
    var cableId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("01a16290-f793-748b-a0b0-91d44be0d4ab"));
    var satelliteId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("01a167cf-af8b-7894-8de6-593ee30289c0"));

    // 軌道エレベーターの建設と核融合炉を前提ごと完了扱いにし、拠点が研究から解放されたことを確かめる
    // Mark orbital elevator construction and fusion reactor done with prerequisites, then confirm the base is unlocked by research
    var researchStore = p.ServerService<IResearchDataStore>();
    await p.RunOnServer("軌道エレベーターの建設と核融合炉を前提ごと完了扱いにする", () => researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(research).Select(g => g.ToString()).ToList() }));
    await p.Until(() => research.All(researchStore.IsResearchCompleted), 15f, "軌道エレベーターの建設と核融合炉が完了扱いになった");
    p.Assert(p.ServerService<IGameUnlockStateDataController>().BlockUnlockStateInfos[Guid.Parse("01a162aa-cec0-738c-93c5-f58adc87fa7f")].IsUnlocked, "軌道エレベーターの建設で拠点が解放された");

    // 核融合炉に重水素を入れて開き、稼働表示と燃料タンクを確かめる
    // Fuel the fusion reactor with deuterium, open it and check the operating display and fuel tank
    p.Note("核融合炉に重水素を入れて開き、発電の稼働表示と燃料タンクが出ることを確かめる");
    var reactor = await p.PlaceBlockDirect("核融合炉", reactorPos, BlockDirection.North);
    await Refuel();
    await WaitPlaced(reactorPos);
    await OpenBlock(reactorPos, 9.5f);
    await p.UntilWebUiElement("generator-section", 15f);
    await p.UntilWebUiElement("generator-fluid-slots", 15f);
    await p.Screenshot("01-fusion-reactor-operating");
    await CloseUi();

    // 拠点を置き、炉から広範囲電柱経由で給電する
    // Place the base and power it from the reactor through a wide-range pole
    p.Note("宇宙エレベーター拠点を置き、核融合炉と電線でつなぐ");
    var elevator = await p.PlaceBlockDirect("宇宙エレベーター拠点", elevatorPos, BlockDirection.North);
    var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(0, 32, -3), BlockDirection.North)).GetComponent<IElectricWireConnector>();
    var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
    var wired = new List<bool>();
    await p.RunOnServer("炉 → 電柱 → 拠点を配線する", () =>
    {
        wired.Add(ElectricWireSystemUtil.TryConnectBothSides(reactor.GetComponent<IElectricWireConnector>(), pole, free));
        wired.Add(ElectricWireSystemUtil.TryConnectBothSides(pole, elevator.GetComponent<IElectricWireConnector>(), free));
    });
    p.Assert(wired.Count == 2 && wired.All(w => w), "炉と拠点を電線でつないだ");
    var elevatorObject = await WaitPlaced(elevatorPos);

    // ケーブルが無いと接続できず理由が出る
    // Without cables the base cannot connect and shows why
    await OpenBlock(elevatorPos, 4.5f);
    await p.UntilWebUiElement("space-elevator-section", 15f);
    await p.Until(() => Detail().Status == SpaceElevatorBaseStatus.InsufficientCable, 10f, "サーバーがケーブル不足と判定した");
    await p.UntilWebUiElement("space-elevator-status", 15f);
    await p.Screenshot("02-elevator-not-enough-cable");

    // ケーブル1便の軌道到着でプールを満たし、接続する（本物の打ち上げは段4で確かめた）
    // Fill the pool with one cable launch arriving in orbit, then connect (the real launch was covered in stage 4)
    var cablesPerElevator = ((SpaceElevatorBaseBlockParam)elevator.BlockMasterElement.BlockParam).CablesPerElevator;
    p.Note($"グラフェンケーブル1便（{cablesPerElevator}本）の軌道到着をサーバーで起こし、接続ボタンを押す");
    await p.RunOnServer("ケーブル1便を軌道へ到着させる", () => p.ServerService<IOrbitalArrivalService>().Arrive(cableId, cablesPerElevator));
    await p.Until(() => Detail().CablePoolAmount == cablesPerElevator, 10f, $"プールが{cablesPerElevator}本になった");
    await p.ClickWebUi("space-elevator-connect");
    await p.Until(() => Detail().IsConnected, 10f, "拠点が接続済みになった");
    await p.Until(() => p.ServerService<SpaceElevatorCablePool>().Amount == 0, 10f, "プールから1エレベーター分が引かれた");
    var cableObject = elevatorObject.GetComponentsInChildren<Transform>(true).First(t => t.name == "Cable").gameObject;
    var climberObject = elevatorObject.GetComponentsInChildren<Transform>(true).First(t => t.name == "Climber");
    await p.Until(() => cableObject.activeSelf, 10f, "ケーブルが現れた");
    await p.Screenshot("03-elevator-connected");

    // 衛星を入れると電力を使って昇り始める。UI を閉じて南から見上げる
    // A satellite in the inlet starts climbing on power; close the UI and look up from the south
    p.Note("GPU衛星Lv1を入口に入れ、昇降機がケーブルを昇ることを確かめる");
    await Refuel();
    await p.RunOnServer("GPU衛星Lv1 を拠点の入口へ入れる", () => elevator.GetComponent<SpaceElevatorBaseInventoryComponent>().SetItem(0, satelliteId, 1));
    await p.Until(() => Detail().Status is SpaceElevatorBaseStatus.Climbing or SpaceElevatorBaseStatus.LowPower, 15f, "昇降が始まった");
    await CloseUi();
    var startY = climberObject.localPosition.y;
    p.WarpPlayer(new Vector3(elevatorPos.x + 4.5f, 32.5f, -20f));
    for (var frame = 0; frame < 40; frame++) { SemanticInput.MouseDragBy(new Vector2(0f, 4f)); await UniTask.Yield(); }
    await p.WaitSeconds(10f);
    p.Assert(climberObject.localPosition.y > startY, $"昇降機が上がった start={startY:F2} now={climberObject.localPosition.y:F2}");
    p.Assert(Detail().ClimbProgress > 0f, $"昇降の進みが増えた progress={Detail().ClimbProgress:F3}");
    await p.Screenshot("04-elevator-climbing");

    // 昇り切ると到着の表どおりの成果が出口に出る（電力不足で遅くなる分を見込んで待ちは3倍）
    // On reaching the top the arrival-table output appears at the outlet (the wait is tripled for low-power slowdown)
    var climbSeconds = ((SpaceElevatorBaseBlockParam)elevator.BlockMasterElement.BlockParam).ClimbSeconds;
    var expected = p.ServerService<IOrbitalArrivalService>().PreviewArrivalOutputs(satelliteId, 1);
    p.Assert(expected.Count > 0, $"GPU衛星Lv1 1便の到着成果がある（先頭 {MasterHolder.ItemMaster.GetItemMaster(expected[0].Id).Name} x{expected[0].Count}）");
    p.Note($"昇降 {climbSeconds} 秒を早送りせずに待つ");
    var deadline = Time.realtimeSinceStartup + climbSeconds * 3f + 10f;
    for (var second = 0; !Delivered() && Time.realtimeSinceStartup < deadline; second++)
    {
        if (second % 10 == 0) await Refuel();
        await UniTask.Delay(1000);
    }
    await p.Until(Delivered, 0f, "昇り切って出口に成果が出た");
    await OpenBlock(elevatorPos, 4.5f);
    await p.UntilWebUiElement("space-elevator-output-grid", 10f);
    await p.Screenshot("05-elevator-delivered");
    await CloseUi();

    // 接続済みの拠点を撤去すると1エレベーター分がプールへ戻る
    // Removing the connected base returns one elevator's worth to the pool
    p.Note($"接続済みの拠点を撤去し、プールに{cablesPerElevator}本が戻ることを確かめる");
    p.Assert(await p.RemoveBlock(elevatorPos), "拠点を撤去した");
    await p.Until(() => p.ServerService<SpaceElevatorCablePool>().Amount == cablesPerElevator, 10f, $"プールに{cablesPerElevator}本が戻った");
    p.Note("段6 完了");

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

    SpaceElevatorBaseStateDetail Detail()
    {
        var detail = elevator.GetComponent<SpaceElevatorClimberComponent>().GetBlockStateDetails().Single(d => d.Key == SpaceElevatorBaseStateDetail.BlockStateDetailKey);
        return MessagePack.MessagePackSerializer.Deserialize<SpaceElevatorBaseStateDetail>(detail.Value);
    }

    bool Delivered() => elevator.GetComponent<SpaceElevatorBaseInventoryComponent>().InventoryItems.Skip(1).Any(s => s.Id == expected[0].Id && s.Count > 0);

    // 炉のタンクは数十秒で空になるので、長い待ちの前と間に満タンまで入れ直す（溢れた分は捨てる）
    // The reactor tank empties within tens of seconds, so top it up before and during long waits (overflow is dropped)
    async UniTask Refuel() => await p.RunOnServer("核融合炉へ重水素を満タンまで入れる", () => reactor.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(100000, deuteriumId), default));

    async UniTask<BlockGameObject> WaitPlaced(Vector3Int pos)
    {
        var blockObject = await p.WaitBlockGameObject(pos);
        await p.Until(() => !blockObject.IsPlayingPlaceAnimation, 30f, $"{pos} の設置演出が終わった");
        return blockObject;
    }

    async UniTask OpenBlock(Vector3Int origin, float faceCenterOffsetX)
    {
        // F は射程2mなので南面の1m手前に着地し、Alt のカーソル照準で南面を狙って開く（cleanroom-room-usability と同じ手順）
        // F reaches 2 m, so land 1 m south of the south face and open it by aiming at the face with the Alt cursor (same as cleanroom-room-usability)
        p.WarpPlayer(new Vector3(origin.x + faceCenterOffsetX, 32.5f, origin.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面(y=32.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(origin.x + faceCenterOffsetX, 33f, origin.z));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilWebUiElement("block-inventory-close", 10f);
    }

    async UniTask CloseUi()
    {
        await p.ClickWebUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    #endregion
});
