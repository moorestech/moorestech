using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.ProgressBar;
using Client.Input;
using Client.Playtest;
using Client.Playtest.UiElements;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using static Client.Playtest.Operations.Ui.DeleteBoxScenarioInput;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
// 本番操作でモード外のF解放と連続確定抑止を検証する
// Verify off-mode F releases and continuous-hold protection through production input
return PlaytestRunner.Run("delete-hold-mode-reentry", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -2f));
    var a = new Vector3Int(2,32,4); var b = new Vector3Int(4,32,4);
    foreach (var cell in new[] { a, b })
    {
        p.Assert(p.GetBlock(cell) == null, $"fixture空き:{cell}");
        p.PlaceBlockDirect("木のチェスト", cell, BlockDirection.North);
        await p.WaitBlockGameObject(cell);
    }
    await p.WaitSeconds(3f);
    // F満了後の未解放状態を本番操作だけで作る
    // Establish the post-confirmation release gate through production actions
    var progress = ClientDIContext.DIContainer.DIContainerResolver.Resolve<ProgressBarState>();
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 5f);
    await AimAtCell(p, a);
    p.Note("Fを満了しても離さず、モードを往復する対照");
    SemanticInput.KeyDown(Key.F);
    await p.Until(() => p.GetBlock(a) == null, 15f, "最初のF満了でAを撤去");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    await AimAtGround(p, new Vector3Int(8,32,8));
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 5f);
    await AimAtCell(p, b);
    await p.WaitSeconds(2.5f);
    p.Assert(Keyboard.current.fKey.isPressed && p.GetBlock(b) != null && !progress.IsShown, "F未解放の再入場ではBを撤去しない");
    await p.Screenshot("01-no-release-control");
    await AimAtGround(p, new Vector3Int(8,32,8));
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    // 抑止と実キー状態を別々に観測する
    // Observe suppression and physical key state separately
    p.Note("モード外でFを解放し再押下、保持したまま再入場する");
    SemanticInput.KeyUp(Key.F);
    await p.WaitSeconds(0.5f);
    p.Note($"解放: rawF={Keyboard.current.fKey.isPressed} focus={WebUiInputExclusivity.IsTextInputFocused} suppressed={WebUiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard)} state={p.CurrentUiState}");
    p.Assert(!Keyboard.current.fKey.isPressed, "モード外で実F解放を観測");
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(0.5f);
    p.Note($"再押下: rawF={Keyboard.current.fKey.isPressed} focus={WebUiInputExclusivity.IsTextInputFocused} suppressed={WebUiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard)} state={p.CurrentUiState}");
    p.Assert(Keyboard.current.fKey.isPressed, "モード外で実F再押下を観測");
    p.Assert(Keyboard.current.fKey.isPressed, "閉じる操作もF保持を維持");
    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 5f);
    await AimAtCell(p, b);
    // 進捗描画とサーバー削除の両方を確認する
    // Verify both rendered progress and server removal
    await p.Until(() => progress.IsShown && progress.CurrentProgress > 0f && progress.CurrentProgress < 1f, 5f, "再入場後の新しい保持で進捗が開始");
    p.Note($"再入場: rawF={Keyboard.current.fKey.isPressed} progress={progress.CurrentProgress:F3} focus={WebUiInputExclusivity.IsTextInputFocused}");
    p.Assert(p.GetBlock(b) != null, "進捗途中ではBを残す");
    var dom = await PlaytestUiElementOps.FindUi("progress-bar");
    p.Note($"進捗DOM: found={dom.IsFound} visible={dom.Visible} hit={dom.Clickable}");
    p.Assert(dom.IsFound && dom.Visible, "表示専用の進捗DOMが可視");
    await p.Screenshot("03-reentry-progress");
    await p.Until(() => p.GetBlock(b) == null, 15f, "Fを離さずBの撤去まで完了");
    SemanticInput.KeyUp(Key.F);
    await p.WaitSeconds(0.5f);
    await p.Screenshot("04-deleted");
});
