// CEF(Web UI)描画ショーケース: ゲーム画面→インベントリ→リサーチツリーをCEF越しに表示して録画
// CEF (Web UI) showcase: game screen -> inventory -> research tree rendered through CEF, recorded
// run-scenario.sh から execute-dynamic-code に渡されるスニペット
// Snippet passed to execute-dynamic-code by run-scenario.sh
using Client.Playtest;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("cef-webui-showcase", options, async p =>
{
    // CEFネイティブプラグイン復旧後の初期状態。まずゲーム画面(CEF透明オーバーレイ稼働中)を撮る
    // Initial state after the CEF native plugin was restored; capture the game screen (CEF transparent overlay live)
    p.Note("CEF(Web UI)ホストが起動済み。ゲーム画面を撮影する");
    await p.WaitSeconds(2f);
    await p.Screenshot("01_game_screen");

    // Tabでインベントリを開く。Web UIモードではCEFがReactのインベントリ画面を描画する
    // Open inventory with Tab; in Web UI mode CEF renders the React inventory screen
    p.Note("Tabキーでインベントリ(CEF Web UI)を開く");
    await p.PressKey(Key.Tab);
    await p.WaitSeconds(2.5f);
    await p.Screenshot("02_inventory_cef");
    p.Assert(p.CurrentUiState == UIStateEnum.PlayerInventory, "インベントリ状態へ遷移した");

    // Tabで閉じてゲーム画面へ戻す
    // Close with Tab and return to the game screen
    p.Note("Tabでインベントリを閉じる");
    await p.PressKey(Key.Tab);
    await p.WaitSeconds(1.5f);

    // Rでリサーチツリーを開く。こちらもCEFがWeb UIで描画する
    // Open the research tree with R; CEF renders this as Web UI too
    p.Note("Rキーでリサーチツリー(CEF Web UI)を開く");
    await p.PressKey(Key.R);
    await p.WaitSeconds(2.5f);
    await p.Screenshot("03_research_cef");
    p.Assert(p.CurrentUiState == UIStateEnum.ResearchTree, "リサーチツリー状態へ遷移した");

    // 撮影完了
    // Capture complete
    p.Note("CEF Web UI 描画の確認完了");
    await p.WaitSeconds(1f);
    await p.Screenshot("04_final");
});
