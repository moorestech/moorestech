// メニューから自由行動へ戻る遷移でカーソルツールチップがクロスヘア基準に出ることを検証する
// Verifies the cursor tooltip anchors on the crosshair when returning from a menu to free play
//
// 再現前提は「Web側のpointerがクロスヘア以外の場所に居る」こと。注入カーソルを隅へ動かし、
// ツールチップDOMがその隅へ追従したことを確かめてから、メニュー往復でロック遷移を踏む。
// 開幕スキット直後を契機にしない理由: スキット中はWeb UIが丸ごとunmountされ、pointerが初期値(0,0)のまま
// 残るため、隅へ退避させた前提そのものをDOMで確認できない（この経路の実測は task-5-report.md 参照）。
// The precondition is that the Web-side pointer sits somewhere other than the crosshair: the injected cursor
// is parked in a corner and the tooltip DOM is confirmed to have followed it there, before the menu round
// trip triggers the lock transition.
// Why not the post-skit moment: the Web UI unmounts during the skit, leaving the pointer at its initial
// (0,0), so the corner precondition itself cannot be confirmed in the DOM (measurements in task-5-report.md).
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
    // ワープはカメラの向きを変えないため、正面ベクトルは試行ごとに変わらない
    // Warping never rotates the camera, so the forward vector is identical across the attempts
    var cameraForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
    if (cameraForward.sqrMagnitude < 0.1f) cameraForward = Vector3.forward;
    var standPosition = Vector3.zero;
    var focused = false;
    foreach (var standDistance in new[] { 1.2f, 1.6f, 2.0f, 2.6f })
    {
        p.Note($"小石の正面{standDistance}m地点へワープして照準する");
        standPosition = pebbleCollider.bounds.center - cameraForward * standDistance + Vector3.up * 0.5f;
        p.WarpPlayer(standPosition);
        await p.WaitSeconds(1.5f);
        focused = MouseCursorTooltip.Instance.GetPresentation().Visible;
        if (focused) break;
    }

    p.Assert(focused, "小石へのフォーカスでツールチップ表示がUnity側から発火した");

    // 注入カーソルを画面隅へ寄せ、ツールチップDOMが隅へ追従したことで前提の到達を確かめる
    // Park the injected cursor in a screen corner and confirm the precondition landed via the tooltip DOM
    var corner = new Vector2(Screen.width - 40f, 40f);
    p.Note($"注入カーソルを画面隅{corner}へ寄せる（不具合の再現前提）");
    SemanticInput.MouseMoveTo(corner);
    await p.WaitSeconds(1.5f);
    p.Assert(await IsTooltipNearAsync(corner, "隅へ退避したカーソル"), "退避先の隅にツールチップが追従した（前提がWeb側へ届いた）");

    // インベントリを開閉してGameplayへ戻す（ここでカーソルがロックされる）
    // Open and close the inventory to return to Gameplay, which is where the cursor gets locked
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    p.WarpPlayer(standPosition);
    await p.Until(() => MouseCursorTooltip.Instance.GetPresentation().Visible, 20f, "自由行動復帰後にツールチップが再表示される");

    var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
    p.Assert(await IsTooltipNearAsync(screenCenter, "クロスヘア"), "ツールチップがクロスヘア近傍（200px以内）に出る");
    await p.Screenshot("01-tooltip-near-crosshair");

    #region Internal

    async UniTask<bool> IsTooltipNearAsync(Vector2 expectedScreenPoint, string expectedLabel)
    {
        var tooltip = await PollUntilQueryAsync("cursor-tooltip", 20);
        if (!tooltip.Found)
        {
            p.Note($"ツールチップDOMが見つからない（期待位置={expectedLabel}{expectedScreenPoint}）");
            return false;
        }

        // ツールチップはpointer-events:noneでヒットテストを通らないため、矩形中心を直接ブラウザpxへ換算する
        // The tooltip is pointer-events:none and fails the hit test, so convert its rect center to browser px directly
        var tooltipBrowserPoint = new Vector2(
            (tooltip.X + tooltip.Width * 0.5f) * tooltip.DevicePixelRatio,
            (tooltip.Y + tooltip.Height * 0.5f) * tooltip.DevicePixelRatio);
        if (!CefScreenMapper.TryBrowserToScreen(tooltipBrowserPoint, out var tooltipScreenPoint))
        {
            p.Note($"ツールチップDOM矩形をスクリーン座標へ変換できない（css={tooltip.X},{tooltip.Y}）");
            return false;
        }

        var distance = Vector2.Distance(tooltipScreenPoint, expectedScreenPoint);
        p.Note($"ツールチップ中心={tooltipScreenPoint} 期待={expectedLabel}{expectedScreenPoint} 距離={distance:F1}px 文字='{tooltip.Text}'");
        return distance < 200f;
    }

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
