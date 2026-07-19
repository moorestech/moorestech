// 無料設置デバッグ検証: FreeBlockPlacementをONにすると、未解放かつ建設コストを1つも持たないブロックでも
// ビルドメニューUI経路で設置できることをE2Eで確認する。修正前はサーバーのunlock/costゲートで無言拒否されていた
// Free-placement debug verification: with FreeBlockPlacement ON, a locked block with zero required items in
// inventory can still be placed via the build-menu UI route. Before the fix the server's unlock/cost gates silently rejected it
using Client.Playtest;
using Client.Playtest.Operations;
using Common.Debug;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.UnlockState;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("free-placement-locked-block", options, async p =>
{
    await p.SetupFlatGround();
    p.WarpPlayer(new Vector3(4f, 33.5f, 5f));

    // 無料設置デバッグをON（クライアントのビルドメニュー全表示＆サーバーの強制設置を有効化）
    // Turn on the free-placement debug toggle (enables client's full build menu + server's forced placement)
    DebugParameters.SaveBool(DebugParameterKeys.FreeBlockPlacement, true);
    p.Note("無料設置デバッグをONにした");

    // 対象は未解放・建設コスト持ちの非電気ブロック。前提が「未解放かつ在庫ゼロ」であることを設置前に固める
    // Target is a locked, cost-bearing, non-electric block. Pin the premise (locked + zero stock) before placing
    const string blockName = "鉄のコンベアチェスト";
    var blockGuid = System.Guid.Parse("ab1a47dd-94fe-4e62-a005-a8318fc304a9");
    var unlockController = p.ServerService<IGameUnlockStateDataController>();
    var isUnlockedBefore = unlockController.BlockUnlockStateInfos[blockGuid].IsUnlocked;
    p.Assert(!isUnlockedBefore, $"前提: {blockName} は未解放である");
    p.Note($"{blockName} は未解放・建設コスト未所持のまま設置を試みる");

    // ビルドメニューUI経路で設置（未解放なので全表示モードでのみメニューに出る）。反映Until内蔵
    // Place via the build-menu UI route (only visible thanks to show-all mode). Placement wait is built in
    var placePos = new Vector3Int(4, 32, 5);
    await p.PlaceBlockViaUi(blockName, placePos, BlockDirection.North);
    await p.ExitToGameScreen();

    // 設置が実際に成立したことを検証（修正前はここでブロックがnullのまま）
    // Verify placement actually took effect (before the fix the block stayed null here)
    var placed = p.GetBlock(placePos);
    p.Assert(placed != null, "未解放ブロックが無料設置で設置された");
    p.Assert(placed != null && placed.BlockId == MasterHolder.BlockMaster.GetBlockId(blockGuid), "設置されたのは対象ブロックである");

    // 建設コスト素材を1つも渡していないので在庫は空のまま＝コスト非消費の証拠
    // No required items were ever granted, so stock stays empty = proof cost was not consumed
    await p.WaitBlockGameObject(placePos);
    await p.Screenshot("01-locked-block-placed-free");
    p.Note("未解放ブロックがコスト無しで設置された。無料設置デバッグは機能している");
});
