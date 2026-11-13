// 進行中チャレンジ HUD を列車の入れ子ポーズで UITK（GameUi）に通す（Web challenge.spec の TrainHUDScreen＋PauseMenuScreen・pause-menu）: ゲーム画面と同じ位置・寸法のまま、ポーズの中身より上
// Runs the current-challenge HUD through the train nested pause on UITK (GameUi) (Web challenge.spec TrainHUDScreen + PauseMenuScreen, pause-menu): same rect as the game screen, above the pause content
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.UnlockState;
using MessagePack;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;
using TrainCarEntityObject = Client.Game.InGame.Train.View.Object.Core.TrainCarEntityObject;

return PlaytestRunner.Run("uitk-challenge-hud-resident-train", new PlaytestRunOptions { Record = true }, async p =>
{
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var root = GameUiRoot.Instance.GetRootElement();
    var world = await HudRect("ゲーム画面");

    // 列車に乗り、列車 HUD の入れ子ポーズを開く（前例 uitk-nested-pause-train）
    // Ride a train and open the train HUD nested pause (precedent uitk-nested-pause-train)
    await PrepareAndRideTrain();
    var trainHud = resolver.Resolve<UIStateDictionary>().GetState(UIStateEnum.TrainHUDScreen) as TrainHUDScreenState;
    await p.Until(() => p.CurrentUiState == UIStateEnum.TrainHUDScreen && trainHud.IsRiding, 15f, "列車HUDで乗車済み");
    p.Note($"乗車中の HUD {root.Q("challenge-hud")?.worldBound}");
    await p.PressKey(Key.Escape);
    await p.Until(() => trainHud.SubState == NestedPauseSubStateEnum.PauseMenuScreen, 15f, "列車HUDの入れ子ポーズ表示");
    await p.UntilUiElement("pause-menu", 10f);

    // 入れ子ポーズ: ゲーム画面と同じ位置・寸法で、ポーズの中身より上（上の安全帯のあるメニュー）
    // Nested pause: same rect as the game screen and above the pause content (a menu with an upper safe area)
    var paused = await HudRect("列車の入れ子ポーズ");
    var same = Mathf.Abs(world.x - paused.x) <= 1f && Mathf.Abs(world.y - paused.y) <= 1f && Mathf.Abs(world.width - paused.width) <= 1f && Mathf.Abs(world.height - paused.height) <= 1f;
    p.Assert(same, $"列車の入れ子ポーズの HUD がゲーム画面と同じ位置・寸法（{paused} / {world}）");
    p.Assert(paused.yMax <= root.Q("pause-menu").worldBound.yMin + 0.5f, "列車の入れ子ポーズで HUD が pause-menu より上");
    await p.Screenshot("01-train-nested-pause");
    await p.PressKey(Key.Escape);
    await p.Until(() => trainHud.SubState == NestedPauseSubStateEnum.GameScreen, 15f, "入れ子ポーズを閉じる");

    #region Internal

    // HUD が出るまで待って矩形を読む
    // Wait for the HUD and read its rect
    async UniTask<Rect> HudRect(string where)
    {
        // HUD は Pick を拾わない（UiPickingIgnore）ので UntilUiElement（Clickable 待ち）は使わず、存在と幅だけを待つ
        // The HUD ignores picking, so skip UntilUiElement (waits for Clickable) and wait only for existence and width
        await UniTask.WaitUntil(() => root.Q("challenge-hud") is { } shown && shown.worldBound.width > 0f).TimeoutWithoutException(System.TimeSpan.FromSeconds(10));
        var hud = root.Q("challenge-hud");
        p.Assert(hud.resolvedStyle.display == DisplayStyle.Flex && hud.worldBound.width > 0f, $"{where} で HUD が出ている");
        p.Note($"{where} の HUD {hud.worldBound}");
        return hud.worldBound;
    }

    // 下の 2 つは uitk-nested-pause-train.cs の同名のローカル関数の写し（レール 2 本・列車の解放・本番プロトコルでの配置・乗車）
    // The two below copy the same-named local functions of uitk-nested-pause-train.cs (two rails, unlocking the car, placing it via the production protocol, riding)
    async UniTask<TrainCarEntityObject> PrepareAndRideTrain()
    {
        var a = new Vector3Int(10, 32, 6); var b = new Vector3Int(10, 32, 22);
        await PlacePier(a); await PlacePier(b);
        await p.WaitBlockGameObject(a); await p.WaitBlockGameObject(b);
        var railA = p.GetBlock(a).GetComponent<RailComponent>(); var railB = p.GetBlock(b).GetComponent<RailComponent>();
        var master = MasterHolder.TrainUnitMaster.Train.TrainCars.First(trainCar => trainCar.Length <= 7);
        await p.RunOnServer("レール2本を接続して列車を解放する", () =>
        {
            railA.FrontNode.ConnectNode(railB.FrontNode); railB.BackNode.ConnectNode(railA.BackNode);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockTrainCar(master.TrainCarGuid);
        });
        foreach (var required in master.RequiredItems) await p.GiveItemDirect(MasterHolder.ItemMaster.GetItemMaster(required.ItemGuid).Name, required.Count);
        var position = new RailPosition(new List<IRailNode> { railA.BackNode, railB.BackNode }, TrainLengthConverter.ToRailUnits(master.Length), 0);
        var response = await ClientContext.VanillaApi.Response.Train.PlaceTrainOnRail(position, master.TrainCarGuid, CancellationToken.None);
        p.Assert(response != null && response.Success, "乗車用の列車を本番プロトコルで配置できる");
        TrainCarEntityObject Spawned() => Object.FindObjectsByType<TrainCarEntityObject>(FindObjectsSortMode.None).FirstOrDefault();
        await p.Until(() => Spawned() != null, 15f, "乗車用列車のクライアント表示");
        var spawned = Spawned(); var collider = spawned.GetComponentInChildren<Collider>();
        p.WarpPlayer(collider.bounds.center + new Vector3(0f, 0f, -2f));
        await p.AimAt(collider.bounds.center); await p.PressRide();
        return spawned;
    }
    async UniTask PlacePier(Vector3Int position)
    {
        var blockId = PlaytestBlockOps.ResolveBlockId("レール橋脚");
        var detail = new RailBridgePierComponentStateDetail(RailComponent.ToVector3(BlockDirection.North));
        var parameters = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(detail)) };
        var placed = false;
        await p.RunOnServer($"レール橋脚を配置 {position}", () => placed = ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North, parameters, out _));
        p.Assert(placed, $"レール橋脚を配置 {position}");
    }

    #endregion
});
