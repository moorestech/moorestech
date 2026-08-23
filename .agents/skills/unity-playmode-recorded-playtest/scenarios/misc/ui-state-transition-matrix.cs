using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

// 画面遷移キーの網羅検証。各画面へ正規化してからキーを1回タップし、期待state到達を記録する
// Exhaustive screen-transition check: normalize to each screen, tap the key once, and record whether the expected state is reached
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("ui-state-transition-matrix", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();

    var control = Object.FindFirstObjectByType<UIStateControl>();
    var log = new List<string>();

    async UniTask<bool> Poll(UIStateEnum expected, float seconds)
    {
        var start = Time.realtimeSinceStartup;
        while (p.CurrentUiState != expected)
        {
            if (seconds < Time.realtimeSinceStartup - start) return false;
            await UniTask.Yield();
        }
        return true;
    }

    // 前提stateへはWeb遷移要求で強制的に寄せる（キー経路の合否を汚さないため）
    // Force the precondition state through the web transition request so the key path's verdict stays clean
    async UniTask Normalize(UIStateEnum from)
    {
        if (p.CurrentUiState == from) return;
        control.RequestTransition(from);
        if (!await Poll(from, 5f)) log.Add($"[SETUP-FAIL] could not normalize to {from} (actual {p.CurrentUiState})");
    }

    async UniTask Case(UIStateEnum from, Key key, UIStateEnum expected)
    {
        await Normalize(from);
        p.Note($"{from} + {key} -> expect {expected}");
        await p.PressKey(key);
        var ok = await Poll(expected, 2f);
        var actual = p.CurrentUiState;
        log.Add($"{(ok ? "PASS" : "FAIL")}  {from} + {key} => expected {expected}, actual {actual}");
        p.Assert(ok, $"{from} + {key} -> {expected} (actual {actual})");
    }

    await Case(UIStateEnum.GameScreen, Key.Tab, UIStateEnum.PlayerInventory);
    await Case(UIStateEnum.PlayerInventory, Key.R, UIStateEnum.ResearchTree);
    await Case(UIStateEnum.ResearchTree, Key.Tab, UIStateEnum.PlayerInventory);
    await Case(UIStateEnum.PlayerInventory, Key.Escape, UIStateEnum.GameScreen);
    await Case(UIStateEnum.GameScreen, Key.R, UIStateEnum.ResearchTree);
    await Case(UIStateEnum.ResearchTree, Key.R, UIStateEnum.GameScreen);
    await Case(UIStateEnum.GameScreen, Key.T, UIStateEnum.ChallengeList);
    await Case(UIStateEnum.ChallengeList, Key.Tab, UIStateEnum.PlayerInventory);
    await Case(UIStateEnum.ChallengeList, Key.T, UIStateEnum.GameScreen);
    await Case(UIStateEnum.GameScreen, Key.B, UIStateEnum.BuildMenu);
    await Case(UIStateEnum.BuildMenu, Key.Tab, UIStateEnum.PlayerInventory);
    await Case(UIStateEnum.BuildMenu, Key.B, UIStateEnum.GameScreen);
    await Case(UIStateEnum.GameScreen, Key.G, UIStateEnum.DeleteBar);
    await Case(UIStateEnum.DeleteBar, Key.Tab, UIStateEnum.PlayerInventory);
    await Case(UIStateEnum.DeleteBar, Key.B, UIStateEnum.BuildMenu);
    await Case(UIStateEnum.DeleteBar, Key.G, UIStateEnum.GameScreen);
    await Case(UIStateEnum.GameScreen, Key.Escape, UIStateEnum.PauseMenu);
    await Case(UIStateEnum.PauseMenu, Key.Escape, UIStateEnum.GameScreen);

    p.Note("=== RESULT ===");
    foreach (var line in log) p.Note(line);
    await p.Screenshot("99-final");
});
