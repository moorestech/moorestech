// 文字組みの並べ比べ撮影: GameUi（UITK）のポーズの題・設定の節の題・清浄機 30 台の部屋欄・回転発電機（停止）を日本語と英語で開き、Game View の実画面を 1280×720 と 2560×1080 で撮る
// 部屋欄は参照（mock-host の cleanRoomAirFilter30Filters）と同じ中身（クラス ISO 5・濃度 1.2・換気 0.8・清浄機 30 台が電力 100%・5 個・50%）と題（電気機械）に差し替える。段組・色・寸法は本番のまま
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Typography comparison shots: open the pause title, settings section titles, the 30-purifier room section and the stopped rotary generator on GameUi (UITK) in Japanese and English, and shoot the real Game View at 1280x720 and 2560x1080
// The room section is swapped to the reference contents (mock-host cleanRoomAirFilter30Filters: class ISO 5, concentration 1.2, air changes 0.8, 30 purifiers at 100% / 5 / 50%) and title (電気機械); layout, colors and sizes stay production
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.Text;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

const int FilterCount = 30;
var shootingLanguage = "japanese";
var options = new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f };
return PlaytestRunner.Run("uitk-typography-compare", options, async p =>
{
    p.Assert(UiInputExclusivity.ActiveUiSystem == UiSystem.GameUi, "全体スイッチが UITK（GameUi）");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
    Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);

    // DSL の操作ログ（OnGUI）は撮る間だけ隠す
    // Hide the DSL action log (OnGUI) while shooting
    var actionLog = Object.FindFirstObjectByType<PlaytestOverlayView>();
    actionLog.enabled = false;
    try
    {
        // 1. ポーズの題と設定の節の題
        // 1. Pause title and settings section titles
        await p.PressKey(Key.Escape);
        await p.UntilUiElement("pause-menu", 10f);
        await Shoot("pause-top", null);
        await p.ClickUi("pause-menu-open-settings");
        await p.UntilUiElement("pause-menu-back", 10f);
        await Shoot("settings", null);
        await p.ClickUi("pause-menu-back");
        await p.UntilUiElement("pause-menu-open-settings", 10f);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // 2. 清浄機 30 台の部屋（x・z 2〜11 を壁と床・天井で閉じる）
        // 2. A room of 30 purifiers (x/z 2..11 closed by walls, floor and ceiling)
        foreach (var y in new[] { 32, 36 })
        foreach (var x in new[] { 2, 7 })
        foreach (var z in new[] { 2, 7 })
            await p.PlaceBlockDirect("クリーンルームパネル", new Vector3Int(x, y, z), BlockDirection.North);
        for (var y = 33; y <= 35; y++)
        for (var x = 2; x <= 11; x++)
        for (var z = 2; z <= 11; z++)
            if (x == 2 || x == 11 || z == 2 || z == 11) await p.PlaceBlockDirect("クリーンルームブロック", new Vector3Int(x, y, z), BlockDirection.North);
        IBlock firstFilter = null;
        for (var i = 0; i < FilterCount; i++)
        {
            var filter = await p.PlaceBlockDirect("クリーンルーム空気清浄機", new Vector3Int(3 + i % 8, 33, 10 - i / 8), BlockDirection.North);
            firstFilter ??= filter;
        }
        await p.WaitForCleanRoom(firstFilter, false, 15f, "部屋が検出された");
        p.WarpPlayer(new Vector3(6.5f, 33.5f, 5.7f));
        await p.Until(() => 32.5f < p.PlayerPosition.y && p.PlayerPosition.y < 33.2f, 5f, "床パネルの上");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(6.5f, 33.6f, 7.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.Until(() => Root().Q<VisualElement>($"clean-room-air-filter-{FilterCount - 1}") != null, 10f, "部屋欄に 30 台");
        await Shoot("cleanroom30", MatchReferenceRoom);
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);

        // 3. 回転発電機（停止）
        // 3. Rotary generator (stopped)
        var genPos = new Vector3Int(20, 32, 20);
        await p.PlaceBlockDirect("回転発電機", genPos, BlockDirection.North);
        var genObject = await p.WaitBlockGameObject(genPos);
        await p.Until(() => !genObject.IsPlayingPlaceAnimation, 30f, "設置演出");
        p.WarpPlayer(new Vector3(genPos.x + 0.5f, 32.5f, genPos.z - 0.7f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "発電機の前に着地");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(genPos.x + 0.5f, genPos.y + 0.4f, genPos.z + 0.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilUiElement("gear-to-electric-section", 15f);
        await p.WaitSeconds(2f);
        await Shoot("rotary-generator-stopped", null);
        await p.ClickUi("block-inventory-close");
    }
    finally
    {
        actionLog.enabled = true;
        Localize.TrySetChosenLanguage("english");
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    }

    #region Internal

    VisualElement Root() => GameUiRoot.Instance.GetRootElement();

    // 言語ごと・解像度ごとに整えてから撮り、ラベルの位置を記録する
    // Shoot per language and resolution after preparing, recording label positions
    async UniTask Shoot(string name, System.Action prepare)
    {
        foreach (var lang in new[] { "japanese", "english" })
        {
            Localize.TrySetChosenLanguage(lang);
            shootingLanguage = lang;
            foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
            {
                await PlaytestGameViewOps.SetResolution(p.CancellationToken, w, h);
                await UniTask.DelayFrame(10);
                prepare?.Invoke();
                await UniTask.DelayFrame(4);
                p.Note($"{name} {lang} {w}x{h}: {Labels()}");
                await p.Screenshot($"{name}-{lang}-{w}x{h}");
            }
        }
    }

    // 部屋欄と題を参照と同じ中身にする（清浄機の行と同じ書式の UiText を本番の組み立てで作る）
    // Swap the room section and title to the reference contents (production builds the UiText rows)
    void MatchReferenceRoom()
    {
        var filters = Enumerable.Range(0, FilterCount).Select(_ => new CleanRoomAirFilter(1, 5, 0.5)).ToArray();
        var room = new CleanRoomSummary(CleanRoomHeadline.Class("ISO 5"), 1.2, 0.8, filters);
        Root().Q<CleanRoomSectionView>().Render(ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.Enclosed(room, CleanRoomHaltReason.None)));
        var title = Root().Q("block-inventory").Q<LocalizedLabel>(className: "game-panel__title");
        title.SetText(UiText.FromVerbatim(shootingLanguage == "japanese" ? "電気機械" : "Electric Machine"));
    }

    string Labels()
    {
        var labels = Root().Query<LocalizedLabel>().ToList().Where(l => l.resolvedStyle.display == DisplayStyle.Flex && l.worldBound.height > 0 && l.worldBound.yMin < 360f);
        return string.Join(" | ", labels.Select(l => $"{l.SourceText}@x={l.worldBound.xMin:F1},y={l.worldBound.yMin:F1},w={l.worldBound.width:F1},h={l.worldBound.height:F1},c={l.resolvedStyle.color}"));
    }

    #endregion
});
