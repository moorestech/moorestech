// S5-crosscut T19 の撮り直し（ブロック画面・レシピ）: フィルター分岐器（10 枠）・チェスト・機械・電柱・宇宙エレベーター拠点・レシピのクラフト選択を日本語で 1280×720 と 2560×1080 に撮る（S5-ladder）
// S5-crosscut T19 retake (block screens and recipe): shoot the filter splitter (10 slots), chest, machine, pole, space elevator base and recipe craft selection in Japanese at 1280x720 and 2560x1080 (S5-ladder)
// 実行は stage5/driver/winunity-scenario-run.sh <branch> <GameUi|Web> normal <この .cs> <name> から。Web の系は R9 のフィルター分岐器だけを撮って終える（先に撮る並び）
// Run via stage5/driver/winunity-scenario-run.sh <branch> <GameUi|Web> normal <this .cs> <name>; on Web only the R9 filter splitter is shot (it comes first)
using System;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Client.Playtest.Overlay;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Recipe.Catalog;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.UnlockState;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

return PlaytestRunner.Run("uitk-s5-retake-blocks", new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f }, async p =>
{
    // 1. 系の記録・入力・平地・日本語（Windows の PlayerPrefs は前の録画の言語が残るので撮る前に当てる）
    // 1. Record the system, input, flat ground and Japanese (Windows PlayerPrefs keep the last run's language, so apply it before shooting)
    var system = UiInputExclusivity.ActiveUiSystem;
    var shotPrefix = system == UiSystem.GameUi ? "uitk" : "web-live";
    p.Note($"系={system}");
    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
    SemanticInput.EnsureDevices();
    foreach (var device in new InputDevice[] { Keyboard.current, Mouse.current }) InputSystem.EnableDevice(device);
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    var languageBefore = Localize.GetCurrentLanguageCode();
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "撮影の言語 japanese を当てられる");
    var backgroundSkit = UnityEngine.Object.FindFirstObjectByType<Client.Game.InGame.BackgroundSkit.BackgroundSkitManager>(FindObjectsInactive.Include);
    await p.Until(() => !backgroundSkit.IsPlayingSkit, 120f, "背景スキットが終わる");
    foreach (var challenge in MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements.SelectMany(c => c.Challenges)) TutorialPresentationStateStore.Instance.EndSession(challenge.ChallengeGuid);
    var overlay = UnityEngine.Object.FindFirstObjectByType<PlaytestOverlayView>();
    overlay.enabled = false;
    try
    {
        // 2. フィルター分岐器（マスタの 10 枠のまま。R9 は両系で撮る）
        // 2. Filter splitter (the master's 10 slots; R9 is shot on both systems)
        await Place("フィルター分岐器", new Vector3Int(-22, 32, -20));
        await Open("フィルター分岐器", new Vector3Int(-22, 32, -20), 1, 1, 0.4f, "filter-splitter");
        await ShootBoth("4-block-filterSplitter");
        await Close();
        if (system != UiSystem.GameUi) return;

        // 3. 木のチェスト（2 枠に物を入れる。参照と同じ形）
        // 3. Wooden chest with items in two slots (same shape as the reference)
        var chestPos = new Vector3Int(-16, 32, -20);
        var chest = await Place("木のチェスト", chestPos);
        await p.RunOnServer("チェストの 2 枠に物を入れる", () => { var inventory = chest.GetComponent<IOpenableBlockInventoryComponent>(); inventory.SetItem(0, PlaytestItemOps.ResolveItemId("原木"), 7); inventory.SetItem(1, PlaytestItemOps.ResolveItemId("石"), 4); });
        await Open("木のチェスト", chestPos, 1, 1, 0.5f, "chest-grid-slot-0");
        await ShootBoth("4-block-chest");
        await Close();

        // 4. 機械（電気炉）
        // 4. Machine (electric furnace)
        var furnacePos = new Vector3Int(-12, 32, -20);
        await Place("電気炉", furnacePos);
        await Open("電気炉", furnacePos, 3, 3, 0.6f, "block-inventory");
        await p.WaitSeconds(1f);
        await ShootBoth("4-block-machine");
        await Close();

        // 5. 電柱: 回転生成機へ配線し、電力網の行（日本語の「発電」）を出す（R12。前例 uitk-block-special-panels）
        // 5. Electric pole wired to an electric-to-gear so the network row (Japanese 「発電」) shows (R12; precedent uitk-block-special-panels)
        var (polePos, e2gPos) = (new Vector3Int(-6, 32, -20), new Vector3Int(-4, 32, -20));
        var pole = await Place("電柱", polePos);
        var e2g = await Place("回転生成機", e2gPos);
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var wired = false;
        await p.RunOnServer("電柱 → 回転生成機を配線する", () => wired = ElectricWireSystemUtil.TryConnectBothSides(pole.GetComponent<IElectricWireConnector>(), e2g.GetComponent<IElectricWireConnector>(), free));
        p.Assert(wired, "電柱と回転生成機を電線でつないだ");
        await Open("電柱", polePos, 1, 1, 0.4f, "electric-network-summary");
        p.Note($"電柱の電力網の行={Text("electric-network-summary")}");
        await ShootBoth("4-block-electricPole");
        await Close();

        // 6. 宇宙エレベーター拠点（日本語の状態文。R12）
        // 6. Space elevator base (Japanese status text; R12)
        var elevatorPos = new Vector3Int(4, 32, -20);
        await Place("宇宙エレベーター拠点", elevatorPos);
        await Open("宇宙エレベーター拠点", elevatorPos, 9, 9, 0.4f, "space-elevator-status");
        p.Note($"宇宙エレベーターの状態文={Text("space-elevator-status")}");
        await ShootBoth("4-block-spaceElevator");
        await Close();

        // 7. レシピ: Tab で開き、解放済みのクラフトの結果のセルを選ぶ（参照は日本語）
        // 7. Recipe: open with Tab and select an unlocked craft's result cell (the reference is Japanese)
        var unlockState = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IGameUnlockStateData>();
        var recipe = CraftRecipeCatalog.BuildUnlocked(unlockState).First();
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
        await p.UntilUiElement("item-list-panel", 5f);
        var resultCell = $"item-list-cell-{recipe.ResultItemId.AsPrimitive()}";
        await PlaytestUiElementOps.ScrollDownUntilUiElementIsClickable(resultCell, "item-list-scroll", 10f);
        await p.ClickUi(resultCell);
        await p.UntilUiElement($"craft-button-{recipe.RecipeGuid}", 5f);
        await ShootBoth("4-recipe-selected-craft");
        await p.PressKey(Key.Tab);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }
    finally
    {
        // 撮影の前の言語へ戻す
        // Restore the pre-shoot language
        overlay.enabled = true;
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
        var restored = Localize.TrySetChosenLanguage(languageBefore);
        p.Note($"撮影の前の言語={languageBefore}（戻せた={restored}）");
        p.Assert(restored, $"撮影の前の言語 {languageBefore} へ戻せる");
    }

    #region Internal

    async UniTask<IBlock> Place(string blockName, Vector3Int position)
    {
        var block = await p.PlaceBlockDirect(blockName, position, BlockDirection.North);
        var blockObject = await p.WaitBlockGameObject(position);
        await p.Until(() => !blockObject.IsPlayingPlaceAnimation, 30f, $"{blockName} の設置演出が終わった");
        return block;
    }

    // 南面の手前に着地し、Alt のカーソル照準で南面を狙って開く（前例 uitk-block-special-panels）
    // Land just south of the face and open aiming at the south face with the Alt cursor (precedent uitk-block-special-panels)
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
        if (system == UiSystem.GameUi) await p.Until(() => Shown(bodyTestId), 15f, $"{blockName}: {bodyTestId} が出る");
        else await p.UntilUiElement(bodyTestId, 30f);
        await p.WaitSeconds(1f);
    }

    async UniTask Close()
    {
        await p.ClickUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    async UniTask ShootBoth(string stateId)
    {
        SemanticInput.MouseMoveTo(new Vector2(Screen.width * 0.5f, Screen.height * 0.97f));
        foreach (var (w, h) in new[] { (1280, 720), (2560, 1080) })
        {
            await PlaytestGameViewOps.SetResolution(p.CancellationToken, w, h);
            await UniTask.DelayFrame(3);
            p.Assert(Screen.width == w && Screen.height == h, $"Screen が {w}x{h} (実際 {Screen.width}x{Screen.height})");
            await p.WaitSeconds(system == UiSystem.GameUi ? 0.5f : 2f);
            await p.Screenshot($"{shotPrefix}-{stateId}-{w}x{h}");
        }
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
    }

    // 要素があり自分と祖先のどれも隠れていない（中心のヒットは問わない）
    // The element exists and neither it nor any ancestor is hidden (no centre-hit requirement)
    bool Shown(string testid)
    {
        var element = GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid);
        if (element == null || element.panel == null) return false;
        for (var e = element; e != null; e = e.parent) if (e.resolvedStyle.display == DisplayStyle.None) return false;
        return true;
    }

    string Text(string testid) => (GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid) as TextElement)?.text ?? "";

    #endregion
});
