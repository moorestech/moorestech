using Game.Context;
// 出荷マスタで発射台の建造中に積荷を ICチップLv1 から Lv2 へ選び直し、見出しは今の便のまま「次の便から」が出て、今の便は Lv1 の成果を返し、次の便は Lv2 で積むことを確かめる
// On the shipped master, reselect the pad's cargo from IC chip Lv1 to Lv2 while building; the headline keeps the current flight with "from the next flight", the current flight returns Lv1's output and the next flight loads Lv2
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Root;
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
using Game.Research;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

var rocketResearch = Guid.Parse("01a167cf-af9d-7f31-817c-3069b4f6e499"); // ロケット技術（P2 の research.json）
var padPos = new Vector3Int(0, 32, 0);
return PlaytestRunner.Run("tier9-4c-rocket-cargo-reselect", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形が台の手前に湧いて照準と画面を塞ぐので、地形の表示と生成を止める（tier9-4 と同じ）
    // Terrain spawns in front of the pad and blocks aim and view, so stop its display and spawning (same as tier9-4)
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.PrepareBackgroundInputAndWaitEnabled(p);
    p.WarpPlayer(new Vector3(5f, 32.5f, -14f));

    // ロケット技術を前提ごと完了扱いにし、ICチップLv2 を解放する（前提の閉包では Lv1 だけが解放される）
    // Mark rocket technology done with its prerequisites and unlock IC chip Lv2 (the closure unlocks only Lv1)
    var lv1 = PlaytestItemOps.ResolveItemId("ICチップLv1");
    var lv2 = PlaytestItemOps.ResolveItemId("ICチップLv2");
    await p.RunOnServer("ロケット技術を前提ごと完了扱いにする", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(new[] { rocketResearch }).Select(g => g.ToString()).ToList() }));
    await p.WaitForResearch(new[] { rocketResearch }, Array.Empty<Guid>(), 15f, "ロケット技術が完了扱いになった");
    await p.RunOnServer("ICチップLv2 を解放する", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockItem(lv2));

    // 発射台と発電機を置き、広範囲電柱で給電する（tier9-4 と同じ配置）
    // Place the pad and a generator and power the pad through a wide-range pole (same layout as tier9-4)
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
    var padObject = await p.WaitBlockGameObject(padPos);
    await p.Until(() => !padObject.IsPlayingPlaceAnimation, 30f, "発射台の設置演出が終わった");

    // 一覧に Lv1・Lv2 の 2 行が出る。Lv1 を押して選び、1便を積んで建造を始める
    // The list shows two rows, Lv1 and Lv2; press Lv1, load one launch and start building
    p.Note("一覧から ICチップLv1 を選び、1便を積んで建造を始める");
    await OpenPad();
    await p.UntilUiElement(CargoRow(lv2), 10f);
    await p.Screenshot("01-cargo-list-two-kinds");
    await p.ClickUi(CargoRow(lv1));
    await p.Until(() => State().SelectedCargoItemId == lv1, 10f, "サーバーが ICチップLv1 を選んだ状態になった");
    await LoadOneLaunch(lv1);
    await p.Until(() => State().Phase == RocketLaunchPadPhase.AwaitingBuildCommand, 10f, "建造の合図待ちになった");
    await p.UntilUiElement("rocket-launch-pad-build", 10f);
    await p.ClickUi("rocket-launch-pad-build");
    await p.Until(() => State().Phase == RocketLaunchPadPhase.Building, 5f, "建造が始まった");

    // 建造中に見出しを押して Lv2 へ選び直す。選択だけが変わり、見出しは今の便（Lv1）のまま下に「次の便から」が出る（ADR 0106 の 4）
    // While building, press the headline and reselect Lv2; only the selection changes, the headline keeps the current flight (Lv1) with "from the next flight" below (ADR 0106 §4)
    p.Note("建造中に見出しを押し、ICチップLv2 へ選び直す");
    await p.ClickUi("rocket-launch-pad-cargo-header");
    await p.UntilUiElement(CargoRow(lv2), 10f);
    await p.Screenshot("02-reselect-list");
    await p.ClickUi(CargoRow(lv2));
    await p.Until(() => State().SelectedCargoItemId == lv2 && State().LoadedCargoItemId == lv1, 10f, "選択は Lv2 になり、今の便は Lv1 のまま");
    // 「次の便から」は PickingMode.Ignore で中心のヒットを待てないので、見つかって表示中かを読む（前例 uitk-block-special-panels）
    // The "from the next flight" line ignores picking and cannot wait on a centre hit, so read found-and-shown instead (precedent: uitk-block-special-panels)
    await p.Until(() => Shown("rocket-launch-pad-cargo-next-flight"), 10f, "見出しの下に「次の便から」が出た");
    await p.Screenshot("03-next-flight");

    // 今の便は Lv1 で打ち上がり、Lv1 の成果（計算データ 20 個）を返す
    // The current flight launches with Lv1 and returns Lv1's output (20 compute data)
    p.Note($"建造 {param.BuildSeconds} 秒を早送りせずに待ち、発射する");
    await WaitPhaseRefueling(RocketLaunchPadPhase.AwaitingLaunchCommand, param.BuildSeconds + 60f, "建造が実時間で終わり発射の合図待ちになった");
    await p.UntilUiElement("rocket-launch-pad-launch", 10f);
    await p.ClickUi("rocket-launch-pad-launch");
    var computeDataId = PlaytestItemOps.ResolveItemId("計算データ");
    var expected = (await p.ReadArrivalOutputs(lv1, CountPerLaunch(lv1))).Where(s => s.Id == computeDataId).Sum(s => s.Count);
    p.Assert(expected == 20, $"ICチップLv1 1便の到着成果は計算データ20個（{expected} 個）");
    await p.Until(() => CountInOutputs() == expected, param.LaunchSeconds + 30f, $"Lv1 の成果の計算データ {expected} 個が出力口に届いた");
    await p.Until(() => State().Phase == RocketLaunchPadPhase.Idle, 20f, "打ち上げが終わり待機に戻った");
    await p.Screenshot("04-lv1-output");

    // 次の便は選び直した Lv2 で積み、合図なしで建造が始まる
    // The next flight loads the reselected Lv2 and starts building without a command
    p.Note("2便目の資材と ICチップLv2 を積み、合図なしで建造が始まることを確かめる");
    await Refuel();
    await LoadOneLaunch(lv2);
    await p.Until(() => State().Phase == RocketLaunchPadPhase.Building && State().LoadedCargoItemId == lv2, 10f, "2便目は ICチップLv2 を積んで合図なしで建造が始まった");
    await p.Screenshot("05-lv2-building");
    p.Note("選び直し 完了");

    #region Internal

    IEnumerable<Guid> WithPrerequisites(IEnumerable<Guid> targets)
    {
        // 前提を辿って閉包を作る（tier9-4 と同じ）
        // Walk prerequisites into a closure (same as tier9-4)
        var (done, pending) = (new HashSet<Guid>(), new Stack<Guid>(targets));
        while (pending.TryPop(out var guid))
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        return done;
    }

    // 本番の状態配信と同じ観測口で状態を読む（RocketLaunchPadTestUtil.ReadState と同じ）
    // Read the state through the production state contract (same as RocketLaunchPadTestUtil.ReadState)
    RocketLaunchPadStateDetail State()
    {
        var detail = processor.GetBlockStateDetails().Single(d => d.Key == RocketLaunchPadStateDetail.BlockStateDetailKey);
        return MessagePack.MessagePackSerializer.Deserialize<RocketLaunchPadStateDetail>(detail.Value);
    }

    bool Shown(string testid)
    {
        var element = GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid);
        if (element == null || element.panel == null) return false;
        for (var e = element; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
        return true;
    }

    string CargoRow(ItemId itemId) => $"rocket-launch-pad-cargo-row-{itemId.AsPrimitive()}";

    int CountPerLaunch(ItemId itemId)
    {
        var guid = MasterHolder.ItemMaster.GetItemMaster(itemId).ItemGuid;
        return MasterHolder.ItemMaster.Items.OrbitalPayloads.Single(payload => payload.PayloadItemGuid == guid).CountPerLaunch;
    }

    int CountInOutputs() => pad.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Where(s => s.Id == computeDataId).Sum(s => s.Count);

    async UniTask Refuel() => await p.RunOnServer("発電機を満タンまで給油する", () => generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(100000, MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"))), default));

    async UniTask LoadOneLaunch(ItemId cargo) => await p.RunOnServer("1便ぶんの資材（マスタの launchMaterials）と選んだ積荷を積む", () =>
    {
        var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
        foreach (var material in param.LaunchMaterials) inventory.InsertItem(MasterHolder.ItemMaster.GetItemId(material.ItemGuid), material.Count);
        inventory.InsertItem(cargo, CountPerLaunch(cargo));
    });

    async UniTask WaitPhaseRefueling(RocketLaunchPadPhase phase, float timeoutSeconds, string label)
    {
        // 1秒ごとに段階を見て、10秒ごとに給油する（tier9-4 と同じ）
        // Check the phase every second and refuel every ten (same as tier9-4)
        var deadline = Time.realtimeSinceStartup + timeoutSeconds;
        for (var second = 0; State().Phase != phase && Time.realtimeSinceStartup < deadline; second++)
        {
            if (second % 10 == 0) await Refuel();
            await UniTask.Delay(1000);
        }
        await p.Until(() => State().Phase == phase, 0f, label);
    }

    async UniTask OpenPad()
    {
        // 台の南面の1m手前に着地し、Alt のカーソル照準で南面を狙って開く（tier9-4 と同じ）
        // Land 1 m south of the pad's south face and open it by aiming at the face with the Alt cursor (same as tier9-4)
        p.WarpPlayer(new Vector3(padPos.x + 5f, 32.5f, padPos.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面(y=32.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(padPos.x + 5f, 33f, padPos.z));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilUiElement("block-inventory-close", 10f);
    }

    #endregion
});
