// メニューから自由行動へ戻る遷移でカーソルツールチップがクロスヘア基準に出ることを検証する
// Verifies the cursor tooltip anchors on the crosshair when returning from a menu to free play
//
// 開幕スキット直後を再現しない理由: スキットの遷移演出中はWeb UIが丸ごとunmountされ、
// 再mount時にReactのpointer状態が初期値へ戻るため、ワープ由来のmousemoveがDOMに残らない。
// Why not the post-skit moment: the whole Web UI unmounts during the skit transition and remounts
// with React's pointer state reset, so the warp's mousemove leaves no observable trace in the DOM.
using System;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.WebUi;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

var pebbleMapObject = new Guid("c74efe49-52f3-403b-9c9a-b39eb1c85fce"); // 小石（miningType=PickUp）
var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("cursor-tooltip-follows-crosshair", options, async p =>
{
    p.Note("開幕スキットを飛ばして自由行動へ入る");
    await p.SkipOpeningSkit();

    // 小石へ照準してPickUpのツールチップ（左クリックで取得）を出す
    // Aim at a pebble to raise the PickUp tooltip ("Left-click to pick up")
    var mapObjectDatastore = UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>();
    p.Assert(mapObjectDatastore != null, "MapObjectGameObjectDatastoreが起動した");
    await p.Until(() => mapObjectDatastore.WaitForInitialApplyAsync().Status.IsCompletedSuccessfully(), 180f, "mapObject生成ループが完走する");

    var pebble = mapObjectDatastore.SearchNearestMapObject(pebbleMapObject, p.PlayerPosition);
    p.Assert(pebble != null, "最寄りの小石mapObjectを解決できる");
    var pebbleCollider = pebble.GetComponentInChildren<Collider>(true);
    p.Assert(pebbleCollider != null, "小石に照準用Colliderがある");
    await p.Until(() => UnityEngine.Object.FindFirstObjectByType<MiningController>() != null && Camera.main != null, 10f, "採掘ControllerとMainCameraの起動");

    // 照準はロック中ScreenCenter固定のため、カーソルを動かさず立ち位置だけで小石を捉える
    // The aim source is fixed to ScreenCenter while locked, so catch the pebble by standing position alone
    var standPosition = Vector3.zero;
    var focused = false;
    foreach (var standDistance in new[] { 1.2f, 1.6f, 2.0f, 2.6f })
    {
        var cameraForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        if (cameraForward.sqrMagnitude < 0.1f) cameraForward = Vector3.forward;
        p.Note($"小石の正面{standDistance}m地点へワープして照準する");
        standPosition = pebbleCollider.bounds.center - cameraForward * standDistance + Vector3.up * 0.5f;
        p.WarpPlayer(standPosition);
        await p.WaitSeconds(1.5f);
        focused = MouseCursorTooltip.Instance.GetPresentation().Visible;
        if (focused) break;
    }

    p.Assert(focused, "小石へのフォーカスでツールチップ表示がUnity側から発火した");

    // インベントリを開いて自由カーソルにし、カーソルを画面隅へ寄せて不具合条件を作る
    // Open the inventory to free the cursor, then park it in a screen corner to build the bug's precondition
    var corner = new Vector2(Screen.width - 40f, 40f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
    p.Note($"自由カーソル中に注入カーソルを画面隅{corner}へ寄せる（不具合の再現条件）");
    SemanticInput.MouseMoveTo(corner);
    await p.WaitSeconds(1f);
    p.Assert(Vector2.Distance(SemanticInput.CurrentMousePosition(), corner) < 1f, "カーソルが画面隅へ移動した");

    // インベントリを閉じてGameplayへ戻す（ここでカーソルがロックされる）
    // Close the inventory to return to Gameplay, which is where the cursor gets locked
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    p.WarpPlayer(standPosition);
    await p.Until(() => MouseCursorTooltip.Instance.GetPresentation().Visible, 20f, "自由行動復帰後にツールチップが再表示される");

    // ツールチップDOMを取得し、矩形中心が画面中央付近にあることを確かめる
    // Fetch the tooltip DOM, then confirm its rect center sits near the screen center
    var tooltip = await PollUntilQueryAsync("cursor-tooltip", 20);
    p.Assert(tooltip.Found, "カーソルツールチップがWeb HUDに表示された");

    // ツールチップはpointer-events:noneでヒットテストを通らないため、矩形中心を直接ブラウザpxへ換算する
    // The tooltip is pointer-events:none and fails the hit test, so convert its rect center to browser px directly
    var tooltipBrowserPoint = new Vector2(
        (tooltip.X + tooltip.Width * 0.5f) * tooltip.DevicePixelRatio,
        (tooltip.Y + tooltip.Height * 0.5f) * tooltip.DevicePixelRatio);
    p.Assert(CefScreenMapper.TryBrowserToScreen(tooltipBrowserPoint, out var tooltipScreenPoint), "ツールチップDOM矩形をスクリーン座標へ変換できた");

    var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
    var distance = Vector2.Distance(tooltipScreenPoint, screenCenter);
    p.Note($"ツールチップ中心={tooltipScreenPoint} 画面中央={screenCenter} 隅={corner} 距離={distance:F1}px 文字='{tooltip.Text}'");
    p.Assert(distance < 200f, "ツールチップがクロスヘア近傍（200px以内）に出る");
    await p.Screenshot("01-tooltip-near-crosshair");

    #region Internal

    async UniTask<DomQueryResult> PollUntilQueryAsync(string testid, int seconds)
    {
        var result = await PlaytestDomQuery.Query(testid, 1f);
        for (var i = 0; i < seconds && !result.Found; i++)
        {
            await p.WaitSeconds(1f);
            result = await PlaytestDomQuery.Query(testid, 1f);
        }

        return result;
    }

    #endregion
});
