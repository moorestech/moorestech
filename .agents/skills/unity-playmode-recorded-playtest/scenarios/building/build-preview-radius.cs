using Client.Game.InGame.Context;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Operations;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Client.Playtest.Input;
using UnityEngine.InputSystem;
using UnityEditor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using VContainer;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

// 1600ブロック並べて設置モードのフレーム時間を測る
// Lay out 1600 blocks and measure frame time in build mode
var options = new PlaytestRunOptions { Record = true };
// 録画前にフォーカスと単体マウス入力を確認する
// Verify focus and a single mouse input before recording
EditorWindow.GetWindow(System.Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
SemanticInput.EnsureDevices();
foreach (var device in InputSystem.devices)
    if (!device.enabled) InputSystem.EnableDevice(device);
SemanticInput.MouseMoveTo(new Vector2(400, 300));
await UniTask.DelayFrame(5);
if (!Application.isFocused || !Keyboard.current.enabled || !Mouse.current.enabled || Vector2.Distance(Mouse.current.position.ReadValue(), new Vector2(400, 300)) > 1f)
    throw new System.InvalidOperationException($"入力事前確認失敗: focus={Application.isFocused}, keyboard={Keyboard.current.enabled}, mouse={Mouse.current.enabled}, position={Mouse.current.position.ReadValue()}");
return PlaytestRunner.Run("build-preview-radius", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    await p.SkipOpeningSkit();

    // サーバー直置きなので足場外でもよい。プレイヤーは足場内に立つ
    // Server-direct placement needs no floor; the player stays on the floor
    for (var x = -40; x < 40; x += 2)
    {
        for (var z = -40; z < 40; z += 2)
            p.PlaceBlockDirect("木のチェスト", new Vector3Int(x, 32, z), BlockDirection.North);
        await UniTask.Yield();
    }
    await p.Until(() => ClientDIContext.BlockGameObjectDataStore.BlockGameObjectDictionary.Count >= 1600, 60f, "1600ブロックのクライアント反映");

    // 設置モードへ入り3秒間の平均フレーム時間を出す
    // Enter build mode and report the average frame time over 3 seconds
    // キー入力を介さず本番の遷移要求へ設置対象を渡す
    // Pass the placement target through the production transition request without keyboard input
    var blockId = PlaytestBlockOps.ResolveBlockId("木のチェスト");
    var target = new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid, null);
    var controller = ClientDIContext.DIContainer.DIContainerResolver.Resolve<PlaceSystemStateController>();
    controller.SetTarget(target, PlacementOrigin.NonHotbar);
    UnityEngine.Object.FindFirstObjectByType<UIStateControl>().RequestTransition(UIStateEnum.PlaceBlock);
    await p.Until(() => PlaytestUiOps.CurrentUiState() == UIStateEnum.PlaceBlock, 10f, "設置モードへ遷移");
    await p.WaitSeconds(0.6f);
    var frames = 0;
    var elapsed = 0f;
    while (elapsed < 3f)
    {
        await UniTask.Yield();
        elapsed += Time.unscaledDeltaTime;
        frames++;
    }
    p.Note($"avgFrameMs={elapsed * 1000f / frames:F2}");
    await p.Screenshot("01-build-mode-dense");

    // 非同期ロード後に内外の枠線を判定する
    // Judge nearby and distant outlines after asynchronous loading
    foreach (var probe in new[] { new Vector3Int(0, 32, 0), new Vector3Int(-30, 32, -30), new Vector3Int(0, 32, 28) })
        await p.Until(() => BoxAt(probe) != null, 30f, $"枠線ロード {probe}");
    p.Note("プレイヤー周囲だけ枠線が描画されることを確認");
    p.WarpPlayer(new Vector3(0, 33.5f, 0));
    await UniTask.DelayFrame(5);
    p.Assert(IsBoxRendered(new Vector3Int(0, 32, 0)), "足元のブロックは枠線あり");
    p.Assert(!IsBoxRendered(new Vector3Int(-30, 32, -30)), "約42m先のブロックは枠線なし");

    // 移動による表示範囲の切り替えを確かめる
    // Verify the visible range changes with player movement
    p.WarpPlayer(new Vector3(-24, 33.5f, -24));
    await UniTask.DelayFrame(5);
    p.Assert(IsBoxRendered(new Vector3Int(-30, 32, -30)), "移動後は約8.5m先に枠線あり");
    p.Assert(!IsBoxRendered(new Vector3Int(0, 32, 0)), "移動後は元の足元の枠線なし");
    await p.Screenshot("02-after-move");

    // 半径外でも面コライダー経由で設置する
    // Place through the face collider even outside the radius
    var farTarget = new Vector3Int(0, 32, 28);
    p.Note("半径外のチェスト上面へ照準して設置");
    p.WarpPlayer(new Vector3(0, 33.5f, 0));
    await UniTask.DelayFrame(5);
    p.Assert(!IsBoxRendered(farTarget), "照準先は半径外");
    await p.AimAt(new Vector3(0.5f, 33.1f, 28.5f));
    await p.ClickPlace();
    await p.Until(() => p.GetBlock(farTarget + Vector3Int.up) != null, 10f, "半径外ブロックの上へ設置できた");
    await p.WaitBlockGameObject(farTarget + Vector3Int.up);
    await p.Until(() => !ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(farTarget + Vector3Int.up).IsPlayingPlaceAnimation, 10f, "新規設置アニメーション完了");
    await p.Until(() => BoxAt(farTarget + Vector3Int.up) != null, 30f, "新規ブロックの枠線ロード");
    p.Assert(!IsBoxRendered(farTarget + Vector3Int.up), "半径外の新規ブロックも枠線なし");
    await p.Screenshot("03-place-on-far-block");

    #region Internal

    BlockPreviewBoundingBox BoxAt(Vector3Int pos) => ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos).GetComponentInChildren<BlockPreviewBoundingBox>(true);

    bool IsBoxRendered(Vector3Int pos)
    {
        var box = BoxAt(pos);
        return box.gameObject.activeInHierarchy && box.GetComponentInChildren<Renderer>(true).enabled;
    }

    #endregion
});
