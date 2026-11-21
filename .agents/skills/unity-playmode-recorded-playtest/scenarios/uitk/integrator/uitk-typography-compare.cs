// 文字組みの並べ比べ撮影: GameUi（UITK）のポーズの題・設定の節の題・清浄機 30 台の部屋の清浄機の画面・回転発電機（停止）を日本語と英語で開き、Game View の実画面を 1280×720 と 2560×1080 で撮る
// 部屋欄は固定の中身（クラス C・クラス B まであと 5 台・清浄機 30 台・この清浄機 電力 100% / 残量 50%）と題（電気機械）に差し替える。段組・色・寸法は本番のまま
// Typography comparison shots: open the pause title, settings section titles, the purifier screen in a 30-purifier room and the stopped rotary generator on GameUi (UITK) in Japanese and English, and shoot the real Game View at 1280x720 and 2560x1080
// The room section is swapped to fixed contents (class C, five more to class B, 30 purifiers, this purifier at 100% power / 50% life) and title (電気機械); layout, colors and sizes stay production
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Common;
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
var options = new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 600f };
return PlaytestRunner.Run("uitk-typography-compare", options, async p =>
{
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
        await p.Until(() => Root().Q<VisualElement>("clean-room-air-filter-count") != null, 10f, "部屋欄に清浄機の台数");
        await ShootReferenceRoom();
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
                var labels = Labels();
                if (name == "cleanroom30")
                {
                    // 固定値が撮影直前にも実画面に残っていることを確かめる
                    // Verify the fixed values still appear just before capture
                    var headline = lang == "japanese" ? "クラスC（クラスBまであと5台）" : "Class C (5 more to class B)";
                    var count = lang == "japanese" ? "清浄機 30台" : "Purifiers: 30";
                    p.Assert(labels.Contains($"{headline}@") && labels.Contains($"{count}@") && labels.Contains("100%") && labels.Contains("50%"), $"固定の部屋欄 {lang} {w}x{h}");
                }
                p.Note($"{name} {lang} {w}x{h}: {labels}");
                await p.Screenshot($"{name}-{lang}-{w}x{h}");
            }
        }
    }

    async UniTask ShootReferenceRoom()
    {
        // 通信の再描画は生の部屋欄が受け、撮影用の欄だけに固定の値を出す
        // Keep live refreshes on the original section and render fixed values in a capture-only section
        var live = Root().Q<CleanRoomSectionView>();
        var fixture = new CleanRoomSectionView();
        var position = live.style.position;
        var visibility = live.style.visibility;
        live.style.position = Position.Absolute;
        live.style.visibility = Visibility.Hidden;
        live.parent.Insert(live.parent.IndexOf(live), fixture);
        try
        {
            await Shoot("cleanroom30", () => MatchReferenceRoom(fixture));
        }
        finally
        {
            fixture.RemoveFromHierarchy();
            live.style.position = position;
            live.style.visibility = visibility;
        }
    }

    // 部屋欄と題を固定の中身にする（行の UiText は本番の組み立てで作る）
    // Swap the room section and title to fixed contents (production builds the UiText rows)
    void MatchReferenceRoom(CleanRoomSectionView fixture)
    {
        var status = CleanRoomAirFilterRoomStatus.WithUnitsToNextClass(FilterCount, 0, 1, 0.5, "B", 5);
        var room = CleanRoomSummary.ForAirFilter(CleanRoomHeadline.Class("C"), status);
        fixture.Render(SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(room, CleanRoomHaltReason.None)));
        var title = Root().Q("block-inventory").Q<LocalizedLabel>(className: "game-panel__title");
        title.SetText(UiText.FromVerbatim(shootingLanguage == "japanese" ? "電気機械" : "Electric Machine"));
    }

    string Labels()
    {
        var labels = Root().Query<LocalizedLabel>().ToList().Where(l => l.resolvedStyle.display == DisplayStyle.Flex && l.resolvedStyle.visibility == Visibility.Visible && l.worldBound.height > 0 && l.worldBound.yMin < 360f);
        return string.Join(" | ", labels.Select(l => $"{l.SourceText}@x={l.worldBound.xMin:F1},y={l.worldBound.yMin:F1},w={l.worldBound.width:F1},h={l.worldBound.height:F1},c={l.resolvedStyle.color}"));
    }

    #endregion
});
