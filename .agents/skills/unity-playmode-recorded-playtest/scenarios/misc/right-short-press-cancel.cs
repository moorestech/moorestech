// 右短押しでの解除検証: カーソルロック中の右ドラッグでは抜けない / 右短押しで建築モードを抜ける / インベントリ→パネル外右短押しで閉じる
// Right short press cancel probe: a right drag under a locked cursor keeps build mode, a short press leaves it, and a short press outside the inventory panel closes it
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("right-short-press-cancel", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    // 開幕スキットはこのプレイテスト起動では再生されず(mode=none)Skipインテントが拒否されるため、GameScreen到達だけを待つ
    // The opening skit never starts on this playtest boot (mode=none) and rejects the skip intent, so wait only for GameScreen
    await p.WaitUiState(UIStateEnum.GameScreen, 30f);

    // 三人称の右ドラッグは押下と同時にカーソルロックへ切り替わり、その1フレームだけ注入した押下が実マウス状態で上書きされて
    // 離しと区別できなくなる（注入の限界であり製品挙動ではない）。ロック済みの一人称なら遷移が起きないためdelta累積を素通しで検証できる
    // A third-person right drag flips to a locked cursor on press, and for that one frame the injected button is overwritten by the real
    // mouse state, which is indistinguishable from a release (an injection limit, not product behaviour). First person is already locked,
    // so no transition occurs and the delta accumulation can be verified directly
    p.Note("一人称へ切り替えて建築モードへ入る");
    await p.PressKey(Key.V);
    await UniTask.DelayFrame(10);
    await p.Hotbar.AssignHotbar(0, "木のチェスト");
    await p.Hotbar.EnterBuildMode(0);
    await p.WaitUiState(UIStateEnum.PlaceBlock, 5f);
    p.Assert(Cursor.lockState == CursorLockMode.Locked, "一人称の建築モードはカーソルロック中");

    p.Note("カーソルロック中の右ドラッグでは建築モードに留まる");
    await p.RightDrag(new Vector2(60f, 0f));
    await UniTask.DelayFrame(5);
    p.Assert(p.CurrentUiState == UIStateEnum.PlaceBlock, "右ドラッグ後もPlaceBlock");
    await p.Screenshot("01-after-right-drag");

    p.Note("右短押しで建築モードを抜ける");
    await p.RightShortClick();
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "右短押しでGameScreen");
    await p.Screenshot("02-after-right-short-press");

    p.Note("インベントリをパネル外の右短押しで閉じる");
    await p.PressKey(Key.V);
    await UniTask.DelayFrame(10);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 5f);
    // 画面左上端はインベントリパネルの外
    // The top-left corner is outside the inventory panel
    SemanticInput.MouseMoveTo(new Vector2(8f, Screen.height - 8f));
    await UniTask.DelayFrame(3);
    await p.RightShortClick();
    await p.WaitUiState(UIStateEnum.GameScreen, 5f);
    p.Assert(p.CurrentUiState == UIStateEnum.GameScreen, "パネル外右短押しでインベントリが閉じる");
    await p.Screenshot("03-inventory-closed");

    // パネル上（スロット）の右クリックがUIを閉じないことの検証は、この環境でWeb UIのインベントリDOMが出ないため未実施
    // Verifying that a right click on a panel slot keeps the UI open is not run here: the Web UI inventory DOM never appears in this environment
    // 環境不具合は別issue moorestech-vq8w で追跡する
    // The environment defect is tracked separately as issue moorestech-vq8w
});
