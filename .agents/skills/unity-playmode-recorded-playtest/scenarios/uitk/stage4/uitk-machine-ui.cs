// 電気炉を UITK（GameUi）で開き、レシピを選び、main の Shift で素材を入れて、開いたまま進捗・状態・出力が変わり、ヘッダで選択へ戻ることを通す（S4-machine）
// Opens an electric furnace on UITK (GameUi), selects a recipe, feeds materials via Shift on main, and checks progress, state and output update while open and the header returns to selection (S4-machine)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.UnlockState;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

var furnaceA = new Vector3Int(0, 32, 0);
var furnaceB = new Vector3Int(6, 32, 0);
var steelRecipe = Guid.Parse("019e3f94-b701-70d3-a615-419faec45f27"); // 鉄板 5 → 鋼鉄板 1（3 秒）
var ingotRecipe = Guid.Parse("019ea1d4-ec8f-7517-8d01-af7375e0e2d0"); // 鉄鉱石の粉 1 → 鉄インゴット 2
var copperRecipe = Guid.Parse("019ea1f2-08dd-762d-a5b5-15cb8ec51733"); // 開いたまま解放する 3 つ目 / the third one unlocked while open
return PlaytestRunner.Run("uitk-machine-ui", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f }, async p =>
{
    // 実行中の Error ログを数えて最後に 0 件を主張する
    // Count error logs during the run and assert zero at the end
    var errorLogs = new List<string>();
    void CountErrors(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorLogs.Add(condition);
    }
    Application.logMessageReceived += CountErrors;
    try
    {
        p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, $"全体スイッチが UITK（UiInputExclusivity.ActiveUiSystem={UiInputExclusivity.ActiveUiSystem}）");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        SemanticInput.EnsureDevices();
        foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);

        // 1. 電気炉 2 台・発電機・電柱を置いて配線し、レシピを 2 つ解放する
        // 1. Place two furnaces, a generator and a pole, wire them, and unlock two recipes
        await p.SetupFlatGround();
        await p.SkipOpeningSkitIfPlaying();
        await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
        // 地形（岩など）が照準を奪うので cleanroom-room-usability と同じく地形の表示と生成を止める
        // Terrain (rocks etc.) steals the aim, so stop its display and spawning like cleanroom-room-usability
        UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
        var a = await p.PlaceBlockDirect("電気炉", furnaceA, BlockDirection.North);
        var b = await p.PlaceBlockDirect("電気炉", furnaceB, BlockDirection.North);
        var generator = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-15, 32, -18), BlockDirection.North);
        var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(3, 32, -4), BlockDirection.North)).GetComponent<IElectricWireConnector>();
        var (free, wired) = (new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>()), new List<bool>());
        await p.RunOnServer("給油・配線・レシピ 2 つの解放", () =>
        {
            generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"))), default);
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), pole, free));
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(pole, a.GetComponent<IElectricWireConnector>(), free));
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(pole, b.GetComponent<IElectricWireConnector>(), free));
            foreach (var recipe in new[] { steelRecipe, ingotRecipe }) ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(recipe);
        });
        p.Assert(wired.Count == 3 && wired.All(w => w), "発電機→電柱→電気炉 2 台の配線");
        foreach (var origin in new[] { furnaceA, furnaceB }) await p.WaitBlockGameObject(origin);
        var resolver = ClientDIContext.DIContainer.DIContainerResolver;
        var root = GameUiRoot.Instance.GetRootElement();

        // 2. 炉 A を開く → 大型・未選択の選択モード。開いたまま 3 つ目を解放すると行が増える（後送り b）
        // 2. Open furnace A -> large, unselected selection mode; unlocking a third recipe while open adds a row (deferred b)
        await Open(furnaceA);
        await p.Until(() => Shown("machine-recipe-selection"), 10f, "machine-recipe-selection（未選択）");
        p.Assert(root.Q<VisualElement>("block-inventory").ClassListContains("block-inventory--large"), "block-inventory が大型（block-inventory--large）");
        var rowsBefore = Vm().Rows.Count;
        await p.RunOnServer("3 つ目のレシピを解放", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(copperRecipe));
        await p.Until(() => Vm().Rows.Count == rowsBefore + 1 && Shown($"machine-recipe-{copperRecipe}"), 10f, $"解放で行が 1 つ増える（{rowsBefore} → {rowsBefore + 1}）");
        await p.Screenshot("01-selection-unselected");

        // 3. 鋼鉄板の行を押す → サーバーの選択が届き本体へ
        // 3. Press the steel row -> the server selection arrives and the body shows
        await p.ClickUi($"machine-recipe-{steelRecipe}");
        await p.Until(() => Vm().Detail.Selection.Matches(steelRecipe) && Shown("machine-selected-recipe") && Shown("machine-inventory-body"), 10f, "選択が届き machine-selected-recipe と machine-inventory-body");

        // 4. 鉄板を main へ渡し、main の枠を Shift＋左押下 → 束縛で入力の番地へ配分される
        // 4. Give iron plates to main and Shift+left the main slot -> the binding routes them to input slots
        await p.GiveItem("鉄板", 10);
        var plate = PlaytestItemOps.ResolveItemId("鉄板");
        var local = resolver.Resolve<ILocalPlayerInventory>();
        await p.Until(() => MainSlotOf(local, plate) >= 0, 10f, "鉄板が main に届く");
        var uitkModifiers = typeof(PanelEventHandler).GetField("s_Modifiers", BindingFlags.NonPublic | BindingFlags.Static);
        if (uitkModifiers == null) throw new InvalidOperationException("PanelEventHandler.s_Modifiers が見つからない（Unity の版で内部名が変わった）ので Shift を注入できない");
        SemanticInput.KeyDown(Key.LeftShift);
        uitkModifiers.SetValue(null, EventModifiers.Shift);
        await p.ClickUi($"main-slot-{MainSlotOf(local, plate)}");
        uitkModifiers.SetValue(null, EventModifiers.None);
        SemanticInput.KeyUp(Key.LeftShift);
        await p.Until(() => Count(plate) > 0 || Vm().Detail.State == MachineProcessDisplayState.Processing, 10f, "鉄板が機械へ入る");
        p.Assert(Shown("machine-input-slot-0"), "machine-input-slot-0 がある");
        await p.Screenshot("02-after-insert");

        // 5. 開いたまま: 進捗が動き、状態が稼働中、出力に鋼鉄板が増える
        // 5. While open: progress moves, the state is processing, steel plates appear in the output
        await p.Until(() => Vm().Detail.State == MachineProcessDisplayState.Processing && Vm().Detail.Progress > 0.1f, 15f, "加工中で進捗が進む");
        p.Note($"状態={Vm().Detail.State} 進捗={Vm().Detail.Progress:F2} 状態ラベル='{root.Q<Label>("machine-state-label")?.text}' 電力率='{root.Q<Label>("machine-power-rate")?.text}'");
        p.Assert(Shown("machine-progress-arrow"), "machine-progress-arrow が出ている");
        await p.Screenshot("03-processing");
        await p.Until(() => Count(PlaytestItemOps.ResolveItemId("鋼鉄板")) >= 1, 20f, "出力に鋼鉄板");
        p.Assert(Shown("machine-output-slots"), "machine-output-slots が出ている");
        await p.Screenshot("04-output");

        // 6. ヘッダで選択へ戻り、同じ行で本体へ戻る
        // 6. The header returns to selection; the same row returns to the body
        await p.ClickUi("machine-selected-recipe");
        await p.Until(() => Shown("machine-recipe-selection") && !Shown("machine-inventory-body"), 10f, "選択リストへ戻る");
        await p.ClickUi($"machine-recipe-{steelRecipe}");
        await p.Until(() => Shown("machine-inventory-body") && !Shown("machine-recipe-selection"), 10f, "同じ行で本体へ戻る");
        p.Assert(Vm().Detail.Selection.Matches(steelRecipe), "選択は鋼鉄板のまま");

        // 7. 閉じる
        // 7. Close
        await Close();

        // 後送り a: 状態が配られたことのある未選択の炉 B を開き直しても選択モードで出る
        // Deferred a: reopening the unselected furnace B whose state was already delivered still shows selection mode
        await Open(furnaceB);
        await p.Until(() => Shown("machine-recipe-selection"), 10f, "炉 B（1 回目）が選択モード");
        await Close();
        await Open(furnaceB);
        await p.Until(() => Shown("machine-recipe-selection") && !Shown("machine-inventory-body"), 10f, "炉 B（状態配信済み・未選択）を開き直しても選択モード");
        await Close();
    }
    finally
    {
        Application.logMessageReceived -= CountErrors;
    }

    // 8. 途中の Error ログが 0 件
    // 8. No error logs during the run
    p.Note($"Error ログ {errorLogs.Count} 件: {string.Join(" | ", errorLogs.Take(5))}");
    p.Assert(errorLogs.Count == 0, $"Error ログ 0 件 (count={errorLogs.Count})");

    #region Internal

    MachineSectionViewModel Vm()
    {
        var current = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMachineSectionModel>().Current.Value;
        if (!current.IsPresent) throw new InvalidOperationException("機械の区画の値が無い（機械を開いていない）");
        return current.Value;
    }

    bool Shown(string testid)
    {
        var element = GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid);
        return element != null && element.resolvedStyle.display == DisplayStyle.Flex && element.panel != null;
    }

    int Count(ItemId itemId) => Vm().ItemSlots.Where(s => s.ItemId == itemId).Sum(s => s.Count);

    int MainSlotOf(ILocalPlayerInventory inventory, ItemId itemId) => Enumerable.Range(0, inventory.MainSlotCount).Where(i => inventory[i].Id == itemId && 0 < inventory[i].Count).DefaultIfEmpty(-1).First();

    // 炉の正面に立ち、Alt のカーソル照準で操作キーを押して開く
    // Stand in front of the furnace and open it by interacting with the Alt cursor aim
    async UniTask Open(Vector3Int origin)
    {
        p.WarpPlayer(new Vector3(origin.x + 1.5f, 32.5f, origin.z - 0.7f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "着地");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(origin.x + 1.5f, 32.5f, origin.z + 1.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.Until(() => ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMachineSectionModel>().Current.Value.IsPresent, 10f, "機械の区画の値が届く");
    }

    async UniTask Close()
    {
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        await UniTask.DelayFrame(2);
        p.Assert(!Shown("block-inventory"), "閉じると block-inventory が隠れる");
    }

    #endregion
});
