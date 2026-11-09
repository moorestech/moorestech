// 掴み絵（GrabOverlay）・分割ドラッグ・舞台の上段 3 列を UITK（GameUi）で通す
// Runs the grab picture (GrabOverlay), split drag and the top-row stage columns on UITK (GameUi)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.UiElements;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Screens.GrabOverlay;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;
using Client.Ui.State.Inventory.Grab;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("uitk-grab-overlay", options, async p =>
{
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");

    // 背景Editorでも注入入力が届くよう焦点無視へ切り替える
    // Ignore focus so injected input reaches a background Editor
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    InputSystem.EnableDevice(Keyboard.current);
    InputSystem.EnableDevice(Mouse.current);

    // 鉄インゴットを Web の参照と同じ個数だけ持って持ち物を開く（画面は未実装でも UI の状態と画面の判定は持ち物になる）
    // Give as many iron ingots as the Web reference holds and open the inventory (the screen is unimplemented, but UI state and routing become the inventory)
    const int GrabCount = 52;
    await p.SkipOpeningSkitIfPlaying();
    await p.ExitToGameScreen();
    await p.GiveItem("鉄インゴット", GrabCount);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);

    // 舞台の上段 3 列が Screen 層にあり、持ち物では左の余白 40
    // The top-row columns exist on the Screen layer with a 40px left padding on the inventory
    var columns = GameUiRoot.Instance.GetRootElement().Q<VisualElement>("stage-columns");
    p.Assert(columns != null, "舞台の上段 3 列（stage-columns）がある");
    p.Note($"列の左の余白={columns?.resolvedStyle.paddingLeft}");
    p.Assert(columns != null && Mathf.Abs(columns.resolvedStyle.paddingLeft - 40f) < 0.5f, "持ち物では左の余白 40");
    p.Assert(columns != null && columns.parent == GameUiRoot.Instance.GetLayer(UiLayer.Screen, UiFamily.Stage), "stage-columns は舞台の Screen 層の直下");

    // 鉄インゴットのあるメインの枠を探し、本番の実行器で掴む（S4-alpha の左押下と同じ口）
    // Find the main slot holding the ingots and pick it up with the production executor (same port as S4-alpha's left press)
    var resolver = ClientDIContext.DIContainer.DIContainerResolver;
    var operations = resolver.Resolve<IPlayerInventoryOperations>();
    var executor = resolver.Resolve<PlayerItemActionExecutor>();
    var commands = resolver.Resolve<IPlayerSlotDragCommands>();
    var button = resolver.Resolve<IPrimaryPointerButton>();

    // C4-e の登録が本番コンテナで解決できる（解決できなければ例外で止まる）
    // The C4-e registrations resolve in the production container (an unresolved one throws and stops)
    p.Note($"解決: {resolver.Resolve<PlayerGrabModel>().GetType().Name} {resolver.Resolve<GrabOverlayModel>().GetType().Name} {resolver.Resolve<IGrabOverlayPointer>().GetType().Name} {commands.GetType().Name} {button.GetType().Name} {resolver.Resolve<StageColumnsLayout>().GetType().Name}");
    var snapshot = operations.ReadSnapshot();
    var source = Enumerable.Range(0, snapshot.MainSlots.Count).First(i => snapshot.MainSlots[i].Count == GrabCount);
    var empties = Enumerable.Range(0, snapshot.MainSlots.Count).Where(i => snapshot.MainSlots[i].IsEmpty).Take(3).ToArray();
    p.Note($"掴む枠={source} 配る枠={string.Join(",", empties)}");
    var pointA = new Vector2(Screen.width * 0.3f, Screen.height * 0.5f);
    SemanticInput.MouseMoveTo(pointA);
    executor.Execute(new PlayerItemAction[] { new PickUpToGrabAction(PlayerSlotRef.Main(source), GrabCount) });
    await p.Until(() => operations.ReadGrab().Count == GrabCount, 5f, $"grab に {GrabCount} 個");
    p.Assert(await IsCenteredAsync(pointA, "A"), "掴んだ瞬間に絵がポインタ A を中心に出る");
    var shownOverlay = GameUiRoot.Instance.GetRootElement().Q<VisualElement>("grab-overlay");
    p.Assert(shownOverlay.parent == GameUiRoot.Instance.GetLayer(UiLayer.Grab, UiFamily.Viewport), "grab-overlay は viewport の Grab 層の直下");

    // 動かすと絵が付いて来る
    // Moving the pointer makes the picture follow
    var pointB = new Vector2(Screen.width * 0.7f, Screen.height * 0.35f);
    SemanticInput.MouseMoveTo(pointB);
    p.Assert(await IsCenteredAsync(pointB, "B"), "絵がポインタ B へ付いて来る");
    await p.Screenshot("grab-follows");

    // Web の参照と同じ舞台座標 (700, 560) へ動かして並べ比べ用に撮る（舞台は高さに合わせて横は中央）
    // Move to the Web reference's stage point (700, 560) and shoot for comparison (stage fits the height, centered horizontally)
    var stageScale = Screen.height / 720f;
    var pointC = new Vector2((Screen.width - 1280f * stageScale) / 2f + 700f * stageScale, Screen.height - 560f * stageScale);
    SemanticInput.MouseMoveTo(pointC);
    p.Assert(await IsCenteredAsync(pointC, "C"), "絵が比べる点 C へ付いて来る");
    await p.Screenshot("grab-compare");

    // 左ボタンを押したまま 3 枠を通り、注入した解放の同じフレームの LateTick で等分が確定する（52 ÷ 3 = 17、端数 1。D-29）
    // Hold the left button through three slots; the share commits in LateTick of the injected release's frame (52 / 3 = 17, remainder 1; D-29)
    SemanticInput.MouseButtonDown(0);
    await p.WaitSeconds(0.2f);
    p.Assert(button.IsPressed(), "BeginSplitDrag の時点で左ボタンが押されている（離れていれば Begin が確定待ちに入り 1 枠で確定する。D-29）");
    commands.BeginSplitDrag(PlayerSlotRef.Main(empties[0]));
    commands.EnterWithLeftButton(PlayerSlotRef.Main(empties[1]));
    commands.EnterWithLeftButton(PlayerSlotRef.Main(empties[2]));
    // 何フレームか LateTick を通しても、解放が無ければ確定しない
    // Even after several LateTicks, nothing commits without a release
    await p.WaitSeconds(0.2f);
    p.Assert(operations.ReadGrab().Count == GrabCount, "解放の前は LateTick を通っても確定せず grab は 52 のまま");
    SemanticInput.MouseButtonUp(0);
    await p.Until(() => operations.ReadGrab().Count == GrabCount % 3, 5f, $"解放の後の LateTick の確定で grab に端数 {GrabCount % 3}");
    var after = operations.ReadSnapshot();
    p.Assert(empties.All(i => after.MainSlots[i].Count == GrabCount / 3), $"通った 3 枠に {GrabCount / 3} 個ずつ");
    await p.Screenshot("split-drag-done");
    p.Assert(shownOverlay.resolvedStyle.display == DisplayStyle.Flex, "端数の 1 個を掴んだまま、持ち物を開いている間は絵が出ている");

    // 持ち物を閉じると絵が隠れる
    // Closing the inventory hides the picture
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    var hidden = GameUiRoot.Instance.GetRootElement().Q<VisualElement>("grab-overlay");
    p.Note($"閉じた後の grab の個数={operations.ReadGrab().Count}（0 でなければ画面の購読で隠れている）");
    p.Assert(hidden != null && hidden.resolvedStyle.display == DisplayStyle.None, "持ち物を閉じると絵が隠れる");

    #region Internal

    // 絵の中心（左下原点の画面 px）がポインタから 2px×倍率以内
    // The picture's center (bottom-left-origin screen px) lies within 2px x scale of the pointer
    async UniTask<bool> IsCenteredAsync(Vector2 pointer, string label)
    {
        await p.WaitSeconds(0.5f);
        var overlay = await PlaytestUiElementOps.FindUi("grab-overlay");
        if (!overlay.IsFound)
        {
            p.Note($"絵が見つからない（{overlay.Status}）");
            return false;
        }
        var scale = GameUiRoot.Instance.GetRootElement().panel.scaledPixelsPerPoint;
        var distance = Vector2.Distance(overlay.ScreenRect.center, pointer);
        p.Note($"{label}: 絵の中心={overlay.ScreenRect.center} ポインタ={pointer} 距離={distance:F1}px 倍率={scale}");
        return distance <= 2f * scale;
    }

    #endregion
});
