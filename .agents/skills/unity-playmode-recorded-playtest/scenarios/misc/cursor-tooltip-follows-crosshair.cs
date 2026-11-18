// メニューから自由行動へ戻る遷移でカーソルツールチップがクロスヘア基準に出ることを検証する
// Verifies the cursor tooltip anchors on the crosshair when returning from a menu to free play
//
// UITK ではロック中のツールチップをポインタではなく画面中央に置くので、隅へ注入しても中央近くに居続けることを主張する。
// UITK places the locked tooltip at the screen centre rather than the pointer, so the scenario asserts it stays
// near the centre even after the cursor is injected into a corner.
using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.UiElements;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

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
    await p.Until(() => mapObjectDatastore.IsNearFieldInstantiated.Value, 180f, "mapObject近傍生成が完了する");

    var pebble = mapObjectDatastore.SearchNearestMapObject(new HashSet<Guid> { pebbleMapObject }, p.PlayerPosition);
    p.Assert(pebble != null, "最寄りの小石mapObjectを解決できる");
    var pebbleCollider = pebble.GetComponentInChildren<Collider>(true);
    p.Assert(pebbleCollider != null, "小石に照準用Colliderがある");
    await p.Until(() => Camera.main != null, 10f, "MainCameraの起動");

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
        focused = Tooltip().GetPresentation().Visible;
        if (focused) break;
    }

    p.Assert(focused, "小石へのフォーカスでツールチップ表示がUnity側から発火した");

    // 注入カーソルを画面隅へ寄せ、ロック中なのでツールチップが中央に居続けることを確かめる
    // Park the injected cursor in a corner and confirm the tooltip stays centred while locked
    var corner = new Vector2(Screen.width - 40f, 40f);
    var screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
    p.Note($"注入カーソルを画面隅{corner}へ寄せる");
    SemanticInput.MouseMoveTo(corner);
    await p.WaitSeconds(1.5f);
    p.Assert(await IsTooltipNearAsync(screenCenter, "クロスヘア"), "ロック中は隅へ注入してもツールチップが中央近くに居続ける");

    // インベントリを開閉してGameplayへ戻す（ここでカーソルがロックされる）
    // Open and close the inventory to return to Gameplay, which is where the cursor gets locked
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.PlayerInventory, 15f);
    await p.PressKey(Key.Tab);
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    p.WarpPlayer(standPosition);
    await p.Until(() => Tooltip().GetPresentation().Visible, 20f, "自由行動復帰後にツールチップが再表示される");

    // ツールチップ要素の再配置は遅れて届くことがあるため、クロスヘアへ寄るまで数秒ポーリングする
    // The tooltip element may reposition a little later, so poll for a few seconds until it settles on the crosshair
    var nearCrosshair = false;
    for (var attempt = 0; attempt < 10 && !nearCrosshair; attempt++)
    {
        nearCrosshair = await IsTooltipNearAsync(screenCenter, "クロスヘア");
        if (!nearCrosshair) await p.WaitSeconds(0.5f);
    }

    p.Assert(nearCrosshair, "ツールチップがクロスヘア近傍（200px以内）に出る");
    await p.Screenshot("01-tooltip-near-crosshair");

    #region Internal

    // 表示状態の正本はDI登録されたIMouseCursorTooltip（uGUIのMouseCursorTooltipは書き込まれない残骸）
    // The authoritative presentation lives on the DI-registered IMouseCursorTooltip; the uGUI MouseCursorTooltip is an unwritten leftover
    IMouseCursorTooltip Tooltip()
    {
        return ClientDIContext.DIContainer.DIContainerResolver.Resolve<IMouseCursorTooltip>();
    }

    async UniTask<bool> IsTooltipNearAsync(Vector2 expectedScreenPoint, string expectedLabel)
    {
        var tooltip = await PollUntilQueryAsync("cursor-tooltip", 20);
        if (!tooltip.IsFound)
        {
            p.Note($"ツールチップ要素が見つからない（期待位置={expectedLabel}{expectedScreenPoint}）");
            return false;
        }

        // 統合ロケータが返す画面矩形の中心で位置を比較する。
        // Compare position using the center of the locator's screen rectangle.
        var tooltipScreenPoint = tooltip.ScreenRect.center;

        var distance = Vector2.Distance(tooltipScreenPoint, expectedScreenPoint);
        p.Note($"ツールチップ中心={tooltipScreenPoint} 期待={expectedLabel}{expectedScreenPoint} 距離={distance:F1}px 文字='{tooltip.Text}'");
        return distance < 200f;
    }

    async UniTask<UiElementSnapshot> PollUntilQueryAsync(string testid, int seconds)
    {
        var result = await PlaytestUiElementOps.FindUi(testid);
        for (var i = 0; i < seconds && !result.IsFound; i++)
        {
            await p.WaitSeconds(1f);
            result = await PlaytestUiElementOps.FindUi(testid);
        }

        return result;
    }

    #endregion
});
