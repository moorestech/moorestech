// 出荷マスタで発射台に1便を積み、UI の建造・発射ボタンから実時間の建造と上昇を経て計算データが届き、2便目は合図なしで建造が始まることを確かめる
// On the shipped master, load one launch, build and launch from the UI in real time, receive compute data, and see the second launch build without commands
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.EnergySystem;
using Game.Fluid;
using Game.Orbit;
using Game.Research;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var rocketResearch = Guid.Parse("01a167cf-af9d-7f31-817c-3069b4f6e499"); // ロケット技術（P2 の research.json）
var padPos = new Vector3Int(0, 32, 0);
return PlaytestRunner.Run("tier9-4-rocket-launch", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形(メサ・岩)が台の手前に湧いて照準と画面を塞ぐので、地形の表示と生成を止める
    // Terrain (mesas, rocks) spawns in front of the pad and blocks aim and view, so stop its display and spawning
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(5f, 32.5f, -14f));

    // ロケット技術を前提ごと完了扱いにする（発射台と資材のレシピが解放される）
    // Mark rocket technology done with its prerequisites (unlocks the pad and material recipes)
    var researchStore = p.ServerService<IResearchDataStore>();
    await p.RunOnServer("ロケット技術を前提ごと完了扱いにする", () => researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(new[] { rocketResearch }).Select(g => g.ToString()).ToList() }));
    await p.Until(() => researchStore.IsResearchCompleted(rocketResearch), 15f, "ロケット技術が完了扱いになった");

    // 発射台と発電機を置き、広範囲電柱で給電する
    // Place the pad and a generator and power the pad through a wide-range pole
    p.Note("発射台と発電機を置いて配線し、1便ぶんの資材と GPU衛星Lv1 を積む");
    var pad = await p.PlaceBlockDirect("ロケット発射台", padPos, BlockDirection.North);
    var generator = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-20, 32, -20), BlockDirection.North);
    var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-3, 32, -3), BlockDirection.North)).GetComponent<IElectricWireConnector>();
    var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
    var param = (RocketLaunchPadBlockParam)pad.BlockMasterElement.BlockParam;
    var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
    var wired = new List<bool>();
    await p.RunOnServer("発電機 → 電柱 → 発射台を配線する", () =>
    {
        wired.Add(ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), pole, free));
        wired.Add(ElectricWireSystemUtil.TryConnectBothSides(pole, pad.GetComponent<IElectricWireConnector>(), free));
    });
    p.Assert(wired.Count == 2 && wired.All(w => w), "発電機 → 電柱 → 発射台を配線した");
    await Refuel();

    // 1便ぶんの資材と衛星を積むと、初打ち上げ前の台は建造の合図を待つ
    // With one launch of materials and a satellite loaded, a pad before the first launch waits for the build command
    var satelliteId = PlaytestItemOps.ResolveItemId("GPU衛星Lv1");
    await LoadOneLaunch();
    await p.Until(() => Phase() == RocketLaunchPadPhase.AwaitingBuildCommand, 10f, "建造の合図待ちになった");
    await p.WaitBlockGameObject(padPos);
    await p.Screenshot("01-awaiting-build");

    // 発射台を開いて建造ボタンを押し、建造は実時間で待つ（DSL に時間を進める手段は無い）
    // Open the pad and press build; the build is awaited in real time (the DSL cannot advance time)
    p.Note($"建造ボタンを押し、建造 {param.BuildSeconds} 秒を早送りせずに待つ");
    await OpenPad();
    await p.UntilWebUiElement("rocket-launch-pad-build", 10f);
    await p.Screenshot("02-panel-build-button");
    await p.ClickWebUi("rocket-launch-pad-build");
    await p.Until(() => Phase() == RocketLaunchPadPhase.Building, 5f, "建造が始まった");
    await p.Screenshot("03-building");
    await WaitPhaseRefueling(RocketLaunchPadPhase.AwaitingLaunchCommand, param.BuildSeconds + 60f, "建造が実時間で終わり発射の合図待ちになった");
    await p.UntilWebUiElement("rocket-launch-pad-launch", 10f);
    await p.Screenshot("04-awaiting-launch");

    // 発射の合図は打ち上げ待ちへ移すだけで、次の tick の進行が打ち上げる
    // The launch command only moves to ready-to-launch; the next tick's advance performs the launch
    await p.ClickWebUi("rocket-launch-pad-launch");
    await p.Until(() => Phase() is RocketLaunchPadPhase.ReadyToLaunch or RocketLaunchPadPhase.Launching, 5f, "打ち上げ待ちへ移った");
    await p.Until(() => Phase() == RocketLaunchPadPhase.Launching, 5f, "上昇が始まった");
    await p.ClickWebUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    p.WarpPlayer(new Vector3(5f, 32.5f, -40f));
    await p.WaitSeconds(Mathf.Min(5f, param.LaunchSeconds * 0.5f));
    await p.Screenshot("05-rising");

    // 計算データが到着の表どおり出力口に入り、初打ち上げ済みになる
    // Compute data lands in the output slots as the arrival table says, and the world is marked as launched
    var computeDataId = PlaytestItemOps.ResolveItemId("計算データ");
    var expected = p.ServerService<IOrbitalArrivalService>().PreviewArrivalOutputs(satelliteId, 1).Where(s => s.Id == computeDataId).Sum(s => s.Count);
    p.Assert(expected > 0, $"GPU衛星Lv1 1便の到着成果に計算データがある（{expected} 個）");
    await p.Until(() => CountInOutputs() == expected, param.LaunchSeconds + 30f, $"計算データ {expected} 個が出力口に届いた");
    p.Assert(p.ServerService<RocketLaunchHistory>().HasLaunchedOnce, "first launch recorded");
    await p.Until(() => Phase() == RocketLaunchPadPhase.Idle, 20f, "打ち上げが終わり待機に戻った");
    await OpenPad();
    await p.UntilWebUiElement("rocket-launch-pad-output-slots", 10f);
    await p.Screenshot("06-output");
    await p.ClickWebUi("block-inventory-close");
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);

    // 2便目は合図なしで建造が始まる
    // The second launch starts building without any command
    p.Note("2便目の資材と衛星を積み、合図なしで建造が始まることを確かめる");
    await Refuel();
    await LoadOneLaunch();
    await p.Until(() => Phase() == RocketLaunchPadPhase.Building, 10f, "2便目は合図なしで建造が始まった");
    await p.Screenshot("07-auto-building");
    p.Note("段4 完了");

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

    // 本番の状態配信と同じ観測口で段階を読む（RocketLaunchPadTestUtil.ReadState と同じ）
    // Read the phase through the production state contract (same as RocketLaunchPadTestUtil.ReadState)
    RocketLaunchPadPhase Phase()
    {
        var detail = processor.GetBlockStateDetails().Single(d => d.Key == RocketLaunchPadStateDetail.BlockStateDetailKey);
        return MessagePack.MessagePackSerializer.Deserialize<RocketLaunchPadStateDetail>(detail.Value).Phase;
    }

    int CountInOutputs() => pad.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Where(s => s.Id == computeDataId).Sum(s => s.Count);

    // 発電機のタンクは数十秒で空になるので、長い待ちの前と間に満タンまで入れ直す（溢れた分は捨てる）
    // The generator tank empties within tens of seconds, so top it up before and during long waits (overflow is dropped)
    async UniTask Refuel() => await p.RunOnServer("発電機を満タンまで給油する", () => generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(100000, MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"))), default));

    async UniTask LoadOneLaunch() => await p.RunOnServer("1便ぶんの資材（マスタの launchMaterials）と GPU衛星Lv1 を積む", () =>
    {
        var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
        foreach (var material in param.LaunchMaterials) inventory.InsertItem(MasterHolder.ItemMaster.GetItemId(material.ItemGuid), material.Count);
        inventory.InsertItem(satelliteId, 1);
    });

    async UniTask WaitPhaseRefueling(RocketLaunchPadPhase phase, float timeoutSeconds, string label)
    {
        // 1秒ごとに段階を見て、10秒ごとに給油する。期限切れは Until の失敗として記録する
        // Check the phase every second and refuel every ten; a timeout is recorded as an Until failure
        var deadline = Time.realtimeSinceStartup + timeoutSeconds;
        for (var second = 0; Phase() != phase && Time.realtimeSinceStartup < deadline; second++)
        {
            if (second % 10 == 0) await Refuel();
            await UniTask.Delay(1000);
        }
        await p.Until(() => Phase() == phase, 0f, label);
    }

    async UniTask OpenPad()
    {
        // F は射程2mなので台の南面の1m手前に着地し、Alt のカーソル照準で南面を狙って開く（cleanroom-room-usability と同じ手順）
        // F reaches 2 m, so land 1 m south of the pad's south face and open it by aiming at the face with the Alt cursor (same as cleanroom-room-usability)
        p.WarpPlayer(new Vector3(padPos.x + 5f, 32.5f, padPos.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面(y=32.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(padPos.x + 5f, 33f, padPos.z));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilWebUiElement("block-inventory-close", 10f);
    }

    #endregion
});
