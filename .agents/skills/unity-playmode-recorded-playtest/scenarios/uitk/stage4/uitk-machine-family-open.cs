// ポンプ・タンク・パイプ・歯車の機械・歯車の採掘機・発電機を UITK（GameUi）で開き、各区画が出て Error が 0 件、歯車駆動の電力率が不足の赤にならないことを通す（S4-machine）
// Opens a pump, tank, pipe, gear machine, gear miner and generator on UITK (GameUi): each section shows, zero errors, and gear-driven power rates never turn shortage-red (S4-machine)
// 実行前に PlayMode を止め、execute-dynamic-code で UiSystemEditorSwitch.Set(UiSystem.GameUi) を立ててから run-scenario.sh を呼ぶ
// Stop PlayMode and call UiSystemEditorSwitch.Set(UiSystem.GameUi) via execute-dynamic-code before run-scenario.sh
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// 名前・置き場所・幅と奥行き・開いたら出る区画・赤くならないはずの電力率（無ければ null）
// Name, origin, width/depth, the section expected when open, and the power rate that must not be red (null if none)
var targets = new (string Name, Vector3Int Origin, int Width, int Depth, string Section, string GearRate)[]
{
    ("歯車ポンプ", new Vector3Int(-24, 32, -20), 2, 2, "pump-section", null),
    ("液体タンク", new Vector3Int(-20, 32, -20), 9, 9, "fluid-pipe-fluid-slots", null),
    ("鉄のパイプ", new Vector3Int(-8, 32, -20), 1, 1, "fluid-pipe-fluid-slots", null),
    ("原始的な加工機", new Vector3Int(-4, 32, -20), 3, 3, "machine-section", "machine-power-rate"),
    ("原始的な採掘機", new Vector3Int(2, 32, -20), 2, 2, "miner-section", "miner-power-rate"),
    ("核融合炉", new Vector3Int(6, 32, -20), 19, 19, "generator-section", null),
};
return PlaytestRunner.Run("uitk-machine-family-open", new PlaytestRunOptions { Record = true, ScenarioTimeoutSeconds = 300f }, async p =>
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

        // 1. 平地（-25〜25）の南端に 6 種を並べる（地形は照準を奪うので止める）
        // 1. Line up the six kinds along the south edge of the flat ground (-25..25); terrain steals the aim, so stop it
        await p.SetupFlatGround();
        await p.SkipOpeningSkitIfPlaying();
        await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen");
        Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
        foreach (var target in targets) await p.PlaceBlockDirect(target.Name, target.Origin, BlockDirection.North);
        foreach (var target in targets) await p.WaitBlockGameObject(target.Origin);

        // 2. 1 つずつ開き、区画が出て、歯車駆動の電力率が赤くないことを見て閉じる
        // 2. Open each, check its section shows and a gear-driven power rate is not red, then close
        foreach (var target in targets)
        {
            if (!await TryOpen(target.Name, target.Origin, target.Width, target.Depth)) continue;
            await p.Until(() => Shown("block-inventory"), 10f, $"{target.Name}: block-inventory が出る");
            await p.WaitSeconds(1f);
            p.Assert(Shown(target.Section), $"{target.Name}: {target.Section} が出る");
            if (target.GearRate != null)
            {
                var rate = GameUiRoot.Instance.GetRootElement().Q<VisualElement>(target.GearRate);
                p.Note($"{target.Name}: {target.GearRate}='{(rate as Label)?.text}' classes={string.Join(",", rate?.GetClasses() ?? Enumerable.Empty<string>())}");
                p.Assert(rate != null && !rate.ClassListContains("lack-highlight-text--insufficient"), $"{target.Name}: 歯車駆動の {target.GearRate} が不足の赤（lack-highlight-text--insufficient）にならない");
            }
            await p.Screenshot($"open-{target.Section}-{target.Origin.x}");
            await p.ClickUi("block-inventory-close");
            await p.WaitUiState(UIStateEnum.GameScreen, 10f);
        }
    }
    finally
    {
        Application.logMessageReceived -= CountErrors;
    }

    // 3. 途中の Error ログが 0 件
    // 3. No error logs during the run
    p.Note($"Error ログ {errorLogs.Count} 件: {string.Join(" | ", errorLogs.Take(5))}");
    p.Assert(errorLogs.Count == 0, $"Error ログ 0 件 (count={errorLogs.Count})");

    #region Internal

    bool Shown(string testid)
    {
        var element = GameUiRoot.Instance.GetRootElement().Q<VisualElement>(testid);
        return element != null && element.panel != null && element.resolvedStyle.display == DisplayStyle.Flex;
    }

    // 正面の手前に立ち Alt のカーソル照準で操作キーを押す。開かなければ理由を書いて偽を返す（Assert で落とす）
    // Stand just in front and interact via the Alt cursor aim; if it does not open, record why and return false (fails an Assert)
    async UniTask<bool> TryOpen(string name, Vector3Int origin, int width, int depth)
    {
        var center = origin.x + width / 2f;
        p.WarpPlayer(new Vector3(center, 32.5f, origin.z - 0.7f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, $"{name} の前に着地");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(center, 32.4f, origin.z + Mathf.Min(depth / 2f, 1.5f)));
        await p.PressInteract();
        var deadline = Time.realtimeSinceStartup + 5f;
        while (p.CurrentUiState != UIStateEnum.SubInventory && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
        SemanticInput.KeyUp(Key.LeftAlt);
        var opened = p.CurrentUiState == UIStateEnum.SubInventory;
        p.Assert(opened, $"{name}: 操作キーで開く（UI 状態={p.CurrentUiState}）");
        return opened;
    }

    #endregion
});
