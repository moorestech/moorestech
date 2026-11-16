// I-fix7 V の撮り直し: 機械 4 種（石窯・原始的な加工機・電気炉・核融合炉）とチャレンジ一覧を日本語で 1280×720 と 2560×1080 に撮る
// I-fix7 V retake: shoot four machines (stone kiln, primitive gear machine, electric furnace, fusion reactor) and the challenge list in Japanese at 1280x720 and 2560x1080
// 実行は winunity play <branch> <この .cs> --ui-system GameUi|Web --name <name>。Web では同じ状態の Web 版を web-live の名で撮る（加工行の記録は GameUi だけ）
// Run via winunity play <branch> <this .cs> --ui-system GameUi|Web --name <name>; on Web it shoots the same state as web-live (the process-row note is GameUi only)
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
using Game.Block.Interface.Extension;
using Game.EnergySystem;
using Game.UnlockState;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using VContainer;

return PlaytestRunner.Run("uitk-i-fix7-visual-machines", new PlaytestRunOptions { Record = false, ScenarioTimeoutSeconds = 600f }, async p =>
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
        // 2. 機械 4 種を順に置いて開き、加工行（核融合炉は発電の区画）が出てから撮る
        // 2. Place and open the four machines in turn and shoot once the process row (the generator section for the fusion reactor) shows
        var machines = new (string Name, Vector3Int Origin, int Width, int Depth, string Body, string ShotId)[]
        {
            ("石窯", new Vector3Int(-24, 32, -20), 3, 3, "block-inventory", "4-block-machine-kiln"),
            ("原始的な加工機", new Vector3Int(-18, 32, -20), 3, 3, "block-inventory", "4-block-machine-gear"),
            ("電気炉", new Vector3Int(-12, 32, -20), 3, 3, "block-inventory", "4-block-machine-electricFurnace"),
            ("核融合炉", new Vector3Int(0, 32, -20), 19, 19, "block-inventory", "4-block-machine-fusion"),
        };
        foreach (var machine in machines)
        {
            await Place(machine.Name, machine.Origin);
            await Open(machine.Name, machine.Origin, machine.Width, machine.Depth, 0.6f, machine.Body);
            if (system == UiSystem.GameUi) NoteProcessRow(machine.Name);
            await ShootBoth(machine.ShotId);
            await Close();
        }

        // 3. チャレンジ一覧（T で開く）
        // 3. Challenge list (opened with T)
        await p.PressKey(Key.T);
        await p.WaitUiState(UIStateEnum.ChallengeList, 10f);
        await p.UntilUiElement("challenge-panel", 5f);
        await ShootBoth("5-challenge-list");
        await p.PressKey(Key.T);
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }
    finally
    {
        // 撮影の前の言語へ戻す
        // Restore the pre-shoot language
        overlay.enabled = true;
        var restored = Localize.TrySetChosenLanguage(languageBefore);
        p.Note($"撮影の前の言語={languageBefore}（戻せた={restored}）");
        p.Assert(restored, $"撮影の前の言語 {languageBefore} へ戻せる");
        await PlaytestGameViewOps.SetResolution(p.CancellationToken, 1280, 720);
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

    // 加工行の帯・格子・枠の幅と祖先の幅を記録する（帯が潰れる元を実画面で切り分ける）
    // Record the widths of the process row's bands, grids, frames and ancestors (to locate the squeeze in the real game)
    void NoteProcessRow(string blockName)
    {
        var root = GameUiRoot.Instance.GetRootElement();
        var row = root.Q<VisualElement>("machine-process-row");
        if (row == null) { p.Note($"{blockName}: 加工行なし"); return; }
        var grid = row.Q<VisualElement>("machine-input-slots");
        var frames = string.Join(",", grid.Query<VisualElement>(className: "slot-frame").ToList().Select(f => $"{f.layout.width:0.#}"));
        var chain = new System.Collections.Generic.List<string>();
        for (var e = grid; e != null && chain.Count < 14; e = e.hierarchy.parent) chain.Add($"{e.name}[{string.Join(".", e.GetClasses().Take(2))}] w={e.layout.width:0.#} minW={e.resolvedStyle.minWidth} shrink={e.resolvedStyle.flexShrink} basis={e.resolvedStyle.flexBasis}");
        p.Note($"{blockName}: frames={frames} / {string.Join(" < ", chain)}");
    }

    string Text(string testid) => (GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid) as TextElement)?.text ?? "";

    #endregion
});
