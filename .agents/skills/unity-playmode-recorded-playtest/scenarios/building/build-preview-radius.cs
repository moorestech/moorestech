using Client.Game.InGame.Context;
using Client.Playtest;
using Client.Playtest.Operations.Ui;
using Client.Game.InGame.UI.UIState;
using Client.Playtest.Operations;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using VContainer;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using UnityEngine;

// 1600ブロック並べて設置モードのフレーム時間を測る
// Lay out 1600 blocks and measure frame time in build mode
var options = new PlaytestRunOptions { Record = false };
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
});
