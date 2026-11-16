// ブロックインベントリの特殊族（フィルター分岐器・回転生成機・電柱・ベルト・歯車・燃料式風車・宇宙エレベーター拠点）を UITK（GameUi）で開いて操作する（S4-special）
// Opens and operates the block-inventory special family (filter splitter, electric-to-gear, pole, belt, gear, fuel windmill, space elevator base) on UITK (GameUi) (S4-special)
// 実行前に PlayMode を止め、execute-dynamic-code で Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Ui.Foundation.Root;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Blocks.FilterSplitter;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

return PlaytestRunner.Run("uitk-block-special-panels", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f }, async p =>
{
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, $"全体スイッチが UITK（{UiInputExclusivity.ActiveUiSystem}）");
    p.Note("系=GameUi");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);

    // 平地の南端に 7 種を間隔を空けて並べる（地形は照準を奪うので止める）
    // Line up the seven kinds with gaps along the south edge of the flat ground (terrain steals the aim, so stop it)
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    var (splitterPos, e2gPos, polePos, beltPos) = (new Vector3Int(-22, 32, -20), new Vector3Int(-18, 32, -20), new Vector3Int(-12, 32, -20), new Vector3Int(-8, 32, -20));
    var (gearPos, windmillPos, elevatorPos) = (new Vector3Int(-5, 32, -20), new Vector3Int(-2, 32, -20), new Vector3Int(4, 32, -20));
    var (splitter, e2g, pole) = (await Place("フィルター分岐器", splitterPos), await Place("回転生成機", e2gPos), await Place("電柱", polePos));
    foreach (var (name, pos) in new[] { ("直線歯車ベルトコンベア", beltPos), ("鉄の歯車", gearPos), ("燃料式風車", windmillPos) }) await Place(name, pos);
    var elevator = await Place("宇宙エレベーター拠点", elevatorPos);

    // フィルター分岐器: 方向0をブラックリストへ切り替え、掴んだ鉄インゴットで枠を設定し右押下で解除する
    // Filter splitter: switch direction 0 to blacklist, set a slot with a grabbed iron ingot and clear it with a right press
    var (filter, ingotId) = (splitter.GetComponent<VanillaFilterSplitterComponent>(), PlaytestItemOps.ResolveItemId("鉄インゴット"));
    await p.GiveItemDirect("鉄インゴット", 1);
    await Open("フィルター分岐器", splitterPos, 1, 1, 0.4f, "filter-splitter");
    await p.ClickUi("filter-mode-0-Blacklist");
    await p.Until(() => filter.GetMode(0) == FilterSplitterMode.Blacklist && Selected("filter-mode-0-Blacklist"), 10f, "方向0がブラックリストへ（サーバーと表示）");
    var ingotSlot = MainSlotTestId(ingotId);
    await p.ClickUi(ingotSlot);
    await p.ClickUi("filter-slot-0-0");
    await p.Until(() => filter.GetFilterItems(0)[0] == ingotId, 10f, "左押下で枠0に鉄インゴットが設定された（サーバー）");
    await Capture("filter-splitter");
    await p.HoverUi("filter-slot-0-0");
    SemanticInput.MouseButtonDown(1); await UniTask.DelayFrame(2); SemanticInput.MouseButtonUp(1);
    await p.Until(() => filter.GetFilterItems(0)[0] == ItemMaster.EmptyItemId, 10f, "右押下で枠0が解除された（サーバー）");
    await p.ClickUi(ingotSlot);
    await Close();

    // 回転生成機: 出力モード1を選ぶとサーバーの選択と表示の選択が変わる
    // Electric-to-gear: choosing output mode 1 changes the server selection and the shown selection
    await Open("回転生成機", e2gPos, 4, 4, 0.4f, "electric-to-gear-view");
    await p.ClickUi("electric-to-gear-mode-1");
    await p.Until(() => e2g.GetComponent<ElectricToGearGeneratorComponent>().SelectedIndex == 1 && Selected("electric-to-gear-mode-1") && !Selected("electric-to-gear-mode-0"), 10f, "出力モード1が選ばれた（サーバーと表示）");
    await Capture("electric-to-gear");
    await Close();

    // 電柱: 回転生成機（需要）へ配線し、電力網の区画に集約の 1 行が値つきで出る（albof.54.1 の直しの実機確認）
    // Electric pole: wire it to the electric-to-gear (a consumer); the network section shows the summary row with values (on-device check of the albof.54.1 fix)
    var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
    var wired = false;
    await p.RunOnServer("電柱 → 回転生成機を配線する", () => wired = pole.GetComponent<IElectricWireConnector>().ContainsWireConnection(e2g.BlockInstanceId) || ElectricWireSystemUtil.TryConnectBothSides(pole.GetComponent<IElectricWireConnector>(), e2g.GetComponent<IElectricWireConnector>(), free));
    p.Assert(wired, "電柱と回転生成機を電線でつないだ（サーバー配線。電線ツールのクリックは Task 10 で失敗したため代替）");
    await Open("電柱", polePos, 1, 1, 0.4f, "electric-pole-view");
    await p.Until(() => Shown("electric-network-section") && Shown("electric-network-summary") && Text("electric-network-summary").Trim().Length > 0, 15f, "電力網の区画に集約の行が出た");
    p.Note($"電柱の電力網の行={Text("electric-network-summary")}");
    await Capture("electric-pole");
    await Close();

    // 歯車ベルト（ベルトコンベアは開けないマスタ）: →/← で窓が進み・戻る（押下は短く、横移動で離れてパネルが閉じないようにする。D-32）
    // Belt: →/← advance and return the window (short presses so walking sideways does not close the panel; D-32)
    await Open("直線歯車ベルトコンベア", beltPos, 1, 1, 0.1f, "belt-throughput-section");
    var before = Text("belt-throughput-window");
    await p.PressKey(Key.RightArrow);
    await p.Until(() => Text("belt-throughput-window") != before, 5f, "→ で窓が進む");
    p.Note($"ベルトの窓 {before} → {Text("belt-throughput-window")}");
    await p.PressKey(Key.LeftArrow);
    await p.Until(() => Text("belt-throughput-window") == before, 5f, "← で戻る");
    await Capture("belt-throughput");
    await Close();

    // 歯車と燃料式風車: 歯車の区画（トルク・RPM）と歯車網の区画
    // Gear and fuel windmill: the gear section (torque, RPM) and the gear-network section
    await Open("鉄の歯車", gearPos, 1, 1, 0.4f, "gear-section");
    await p.Until(() => Shown("gear-torque") && Shown("gear-rpm"), 10f, "歯車のトルクと RPM の行");
    await Capture("gear");
    await Close();
    await Open("燃料式風車", windmillPos, 3, 3, 0.4f, "gear-network-section");
    await Capture("gear-generator");
    await Close();

    // 宇宙エレベーター拠点: ケーブル不足の状態文 → 1 便の軌道到着 → UI の接続ボタンで接続済みになり進みが出る（接続に電力は要らない）
    // Space elevator base: insufficient-cable status → one cable arrival → the UI connect button connects and shows progress (connecting needs no power)
    await Open("宇宙エレベーター拠点", elevatorPos, 9, 9, 0.4f, "space-elevator-view");
    await p.Until(() => Shown("space-elevator-status") && Text("space-elevator-status").Trim().Length > 0, 15f, "状態文が出た");
    p.Note($"宇宙エレベーターの状態文={Text("space-elevator-status")} server={Detail().Status}");
    await Capture("space-elevator");
    var cableId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("01a16290-f793-748b-a0b0-91d44be0d4ab"));
    var cablesPerElevator = ((SpaceElevatorBaseBlockParam)elevator.BlockMasterElement.BlockParam).CablesPerElevator;
    await p.RunOnServer("ケーブル1便を軌道へ到着させる", () => ServerContext.GetService<IOrbitalArrivalService>().Arrive(cableId, cablesPerElevator));
    await p.UntilUiElement("space-elevator-connect", 10f);
    await p.ClickUi("space-elevator-connect");
    await p.Until(() => Detail().IsConnected, 10f, "拠点が接続済みになった（サーバーの状態）");
    // 進みのゲージは PickingMode.Ignore で中心のヒットを待てないので、見つかって表示中かを読む
    // The progress gauge ignores picking and cannot wait on a centre hit, so read found-and-shown instead
    await p.Until(() => Shown("space-elevator-progress") && !Shown("space-elevator-connect"), 10f, "接続後の進みが出て接続ボタンが消えた");
    p.Note("宇宙エレベーターの接続を UI から押し、サーバーが接続済みになり進みが出た");
    await Capture("space-elevator-connected");
    await Close();

    #region Internal

    async UniTask<IBlock> Place(string blockName, Vector3Int position)
    {
        var block = await p.PlaceBlockDirect(blockName, position, BlockDirection.North);
        await p.WaitSeconds(0.5f);
        var blockObject = await p.WaitBlockGameObject(position);
        await p.Until(() => !blockObject.IsPlayingPlaceAnimation, 30f, $"{blockName} の設置演出が終わった");
        return block;
    }

    // 南面の手前に着地し、Alt のカーソル照準で南面を狙って操作キーで開く（前例 uitk-machine-family-open）
    // Land just south of the face and open with the interact key, aiming at the south face with the Alt cursor (precedent uitk-machine-family-open)
    async UniTask Open(string blockName, Vector3Int origin, int width, int depth, float aimHeight, string bodyTestId)
    {
        var center = origin.x + width / 2f;
        p.WarpPlayer(new Vector3(center, 32.5f, origin.z - 0.7f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, $"{blockName} の前に着地");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(center, origin.y + aimHeight, origin.z + Mathf.Min(depth / 2f, 1.5f)));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.Until(() => Shown(bodyTestId), 15f, $"{blockName}: {bodyTestId} が出る");
    }

    // 1 状態を 720p と 2560×1080 の 2 種で撮り、720p へ戻す（設計書「比べ方」）
    // Capture one state at 720p and 2560x1080, then return to 720p (design doc comparison)
    async UniTask Capture(string name)
    {
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
        await p.Screenshot($"{name}@720");
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 2560, 1080);
        await p.Screenshot($"{name}@2560x1080");
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    }
    async UniTask Close()
    {
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    // 要素が見つかり、自分と祖先のどれも隠れていない（中心のヒットは問わない）
    // The element exists and neither it nor any ancestor is hidden (no centre-hit requirement)
    bool Shown(string testid)
    {
        var element = GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid);
        if (element == null || element.panel == null) return false;
        for (var e = element; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
        return true;
    }
    string MainSlotTestId(ItemId itemId)
    {
        var inventory = ServerContext.GetService<Game.PlayerInventory.Interface.IPlayerInventoryDataStore>().GetInventoryData(Client.Game.InGame.Context.ClientContext.PlayerConnectionSetting.PlayerId).MainOpenableInventory;
        var index = inventory.InventoryItems.ToList().FindIndex(s => s.Id == itemId);
        p.Assert(index >= 0, $"持ち物に {itemId} がある");
        return $"main-slot-{index}";
    }
    SpaceElevatorBaseStateDetail Detail()
    {
        var detail = elevator.GetComponent<SpaceElevatorClimberComponent>().GetBlockStateDetails().Single(d => d.Key == SpaceElevatorBaseStateDetail.BlockStateDetailKey);
        return MessagePack.MessagePackSerializer.Deserialize<SpaceElevatorBaseStateDetail>(detail.Value);
    }
    bool Selected(string testid) => GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid)?.ClassListContains("mode-switch__option--selected") == true;
    string Text(string testid) => (GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid) as TextElement)?.text ?? "";

    #endregion
});
