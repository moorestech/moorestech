// ホットバーの列の上のホイール素通し・選択を受けない画面での入替・未解決枠のドラッグ・解放の直後の解決を UITK（GameUi）で通す（S3-hotbar-modal・D-18）
// Runs the hotbar row's wheel pass-through, swap on a non-accepting screen, dragging an unresolved slot and resolve-right-after-unlock on UITK (GameUi) (S3-hotbar-modal, D-18)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Hotbar;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UnlockState;
using Client.Input;
using Client.Input.Pointer;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Hotbar;
using Common.Debug;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.UnlockState;
using Server.Event.EventReceive;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 240f };
return PlaytestRunner.Run("uitk-hotbar-unlock-resolve", options, async p =>
{
    // 1. 系の主張と入力の準備。サーバーは未解放の割当を拒むので、無料設置の間に割り当ててから無料設置を切る（前例 placement-reason-tooltip）
    // 1. Assert the UI system and prepare input; the server refuses locked assignments, so assign under free placement and then turn it off (precedent placement-reason-tooltip)
    p.Note($"全体スイッチ 系={UiInputExclusivity.ActiveUiSystem}");
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var hotbar = resolver.Resolve<ClientHotbarDatastore>();
    var model = resolver.Resolve<HotbarModel>();
    var unlockState = resolver.Resolve<IGameUnlockStateData>();
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    var chestId = hotbar.Assignments[0];
    var locked = PickLockedBlock();
    if (locked == Guid.Empty) throw new InvalidOperationException("未解放のブロックが無く、流れ 8 を確かめられない");
    var lockedName = MasterHolder.BlockMaster.GetBlockMaster(locked).Name;
    hotbar.RequestAssign(8, locked);
    await p.Until(() => hotbar.Assignments[8] == locked, 5f, $"無料設置の間に枠 8 へ未解放の {lockedName} が割り当たる");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig { FreeBlockPlacement = false, CreateFlatGround = false, SpawnPosition = p.PlayerPosition });
    p.Note($"無料設置={DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)}");

    // 2. 列の上のホイールは装備側へ素通しされる（premise #8。実マスタの装備は 1 枠なので選択の変化でなく判定と流れで見る）
    // 2. The wheel over the row passes through to equipment (premise #8; the real master has one equipment slot, so check the judgment and stream, not a selection change)
    var picker = resolver.Resolve<IEquipmentWheelPointerPicker>();
    var notches = new List<float>();
    using var wheelSubscription = resolver.Resolve<InputSystemPointerWheelStream>().OnWheelNotchesScrolled.Subscribe(notches.Add);
    SemanticInput.KeyDown(Key.LeftAlt);
    await p.WaitSeconds(0.5f);
    foreach (var testid in new[] { "hotbar-slot-0", "hotbar-slot-5" })
    {
        SemanticInput.MouseMoveTo(await CenterOf(testid));
        await p.WaitSeconds(0.5f);
        var picked = picker.PickAt(SemanticInput.CurrentMousePosition());
        p.Assert(picked != null && !EquipmentWheelPickJudge.IsBlockingUi(picked), $"{testid} の上は装備のホイールを奪わない（拾い {picked?.name}）");
        SemanticInput.MouseWheel(-1f);
        await p.WaitSeconds(0.5f);
    }
    SemanticInput.KeyUp(Key.LeftAlt);
    p.Assert(notches.Count == 2, $"列の上の 2 ノッチが本番のホイールの流れに届く [{string.Join(",", notches)}]");
    await p.Screenshot("01-wheel-over-row");

    // 3. 選択を受けない画面（持ち物）では列が減光され、それでも枠のドラッグで入替が通る
    // 3. On a non-accepting screen (inventory) the row is dimmed, yet dragging still swaps
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
    await p.Until(() => Row().ClassListContains("hotbar-hud__row--select-disabled"), 5f, "持ち物では列が選択を受けない減光の状態");
    await DragAsync(await CenterOf("hotbar-slot-0"), await CenterOf("hotbar-slot-3"));
    await p.Until(() => hotbar.Assignments[3] == chestId && hotbar.Assignments[0] == Guid.Empty, 5f, "減光の列でも入替: 枠 3 が木のチェスト");
    await p.Screenshot("02-swap-on-select-disabled-row");
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);

    // 4. 無料設置を切った後の枠 8 は未解決（減光）になり（Tab の往復の組み直しで反映）、そのドラッグで枠 7 へ移せる
    // 4. With free placement off, slot 8 is unresolved (dimmed; reflected by the Tab round-trip rebuild), and dragging it moves it to slot 7
    p.Assert(model.Current.Value.Slots[8] is HotbarUnresolvedSlot, $"枠 8 が未解決 実際:{model.Current.Value.Slots[8].GetType().Name}");
    p.Assert(Cell(8).ClassListContains("hotbar-hud__cell--unresolved"), "枠 8 の箱が未解決の減光の状態");
    await p.Screenshot("03-unresolved");
    SemanticInput.KeyDown(Key.LeftAlt);
    await p.WaitSeconds(0.5f);
    await DragAsync(await CenterOf("hotbar-slot-8"), await CenterOf("hotbar-slot-7"));
    SemanticInput.KeyUp(Key.LeftAlt);
    await p.Until(() => hotbar.Assignments[7] == locked && hotbar.Assignments[8] == Guid.Empty, 5f, "未解決の枠のドラッグで枠 7 へ移る");

    // 5. 解放パケットの配送の中で枠 7 が解決済みになる（間引き無しの同期の組み直し。D-18）
    // 5. Slot 7 resolves within the unlock packet delivery (synchronous rebuild without throttling; D-18)
    bool? resolvedAtPacket = null;
    using (ClientContext.VanillaApi.Event.SubscribeEventResponse(UnlockedEventPacket.EventTag, _ => resolvedAtPacket ??= model.Current.Value.Slots[7] is HotbarAssignedSlot))
    {
        await p.UnlockBlock(lockedName);
        await p.Until(() => resolvedAtPacket.HasValue, 10f, "解放パケットが届く");
    }
    p.Assert(resolvedAtPacket == true, $"解放パケットの配送の中で枠 7 が解決済みになる 実際:{model.Current.Value.Slots[7].GetType().Name}");
    await UniTask.DelayFrame(2);
    p.Assert(!Cell(7).ClassListContains("hotbar-hud__cell--unresolved"), "解決後は枠 7 の減光が外れる");
    await p.Screenshot("04-resolved-after-unlock");

    #region Internal

    VisualElement Row() => GameUiRoot.Instance.GetRootElement().Q<VisualElement>("hotbar-grid");

    VisualElement Cell(int index) => GameUiRoot.Instance.GetRootElement().Q<VisualElement>($"hotbar-slot-{index}").parent;

    async UniTask<Vector2> CenterOf(string testid) => (await PlaytestUiElementOps.FindUi(testid)).ScreenRect.center;

    // 前例の石窯を先に、無ければ未解放の先頭を選ぶ
    // Prefer the precedent's stone oven, else the first locked block
    Guid PickLockedBlock()
    {
        var infos = unlockState.BlockUnlockStateInfos.Values.Where(info => !info.IsUnlocked).ToList();
        var oven = infos.FirstOrDefault(info => MasterHolder.BlockMaster.GetBlockMaster(info.BlockGuid).Name == "石窯");
        return (oven ?? infos.FirstOrDefault())?.BlockGuid ?? Guid.Empty;
    }

    async UniTask DragAsync(Vector2 from, Vector2 to)
    {
        p.Note($"ドラッグ {from} → {to}");
        SemanticInput.MouseMoveTo(from);
        await p.WaitSeconds(0.5f);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(2);
        await SemanticInput.MouseGlideTo(to, 0.4f);
        SemanticInput.MouseButtonUp(0);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
