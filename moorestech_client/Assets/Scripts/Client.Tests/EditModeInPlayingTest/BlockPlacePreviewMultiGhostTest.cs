using System;
using System.Collections;
using Client.Game.InGame.BlockSystem.PlaceSystem.PreviewGhost;
using Client.Game.InGame.Tutorial;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    ///     テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    ///     ゴースト生成はClientContextのプレハブ生成を要するため、複数ゴーストの独立性は実機クライアント上で検証する
    ///     This test runs in EditMode but switches to PlayMode during execution.
    ///     Ghost creation needs ClientContext's prefab container, so per-guid ghost independence is verified on a running client.
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class BlockPlacePreviewMultiGhostTest
    {
        private const string FirstTutorialGuid = "aaaaaaaa-0000-0000-0000-000000000001";
        private const string SecondTutorialGuid = "aaaaaaaa-0000-0000-0000-000000000002";
        private static readonly Vector3Int FirstCell = new(20, 0, 20);
        private static readonly Vector3Int SecondCell = new(22, 0, 22);

        [UnityTest]
        public IEnumerator 複数ゴーストはtutorialGuidごとに独立し片方の解除で他方が残る()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);

            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                var manager = Object.FindFirstObjectByType<BlockPlacePreviewTutorialManager>(FindObjectsInactive.Include);
                Assert.IsNotNull(manager, "the scene has no BlockPlacePreviewTutorialManager");
                var shaftId = FindBlockId("シャフト");

                manager.SetTargetCell(shaftId, FirstCell, BlockDirection.North, FirstTutorialGuid);
                manager.SetTargetCell(shaftId, SecondCell, BlockDirection.North, SecondTutorialGuid);

                // ゴーストはAddressableの非同期ロード後に立つため、両方の着地を待つ
                // Ghosts appear after an async Addressable load, so wait until both have landed
                for (var i = 0; i < 300 && !(HasGhostAt(manager, FirstCell) && HasGhostAt(manager, SecondCell)); i++) await UniTask.Yield();
                Assert.IsTrue(HasGhostAt(manager, FirstCell), "the first guid's ghost was not shown");
                Assert.IsTrue(HasGhostAt(manager, SecondCell), "the second guid's ghost was not shown");

                // 片方の解除で他方のゴーストは残る。Destroyはフレーム末に反映されるので1フレーム進める
                // Clearing one guid leaves the other ghost; Destroy lands at frame end, so advance one frame
                manager.ClearTarget(FirstTutorialGuid);
                await UniTask.Yield();
                Assert.IsFalse(HasGhostAt(manager, FirstCell), "the cleared guid's ghost is still shown");
                Assert.IsTrue(HasGhostAt(manager, SecondCell), "clearing one guid removed the other guid's ghost");

                manager.ClearTarget(SecondTutorialGuid);
            }

            #endregion
        }

        private static bool HasGhostAt(BlockPlacePreviewTutorialManager manager, Vector3Int cell)
        {
            foreach (var ghost in manager.GetComponentsInChildren<PreviewGhostObject>(false))
            {
                if (Vector3Int.FloorToInt(ghost.transform.position) == cell) return true;
            }
            return false;
        }

        private static BlockId FindBlockId(string blockName)
        {
            foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                if (MasterHolder.BlockMaster.GetBlockMaster(blockId).Name == blockName) return blockId;
            }
            throw new InvalidOperationException($"block not found in the test mod: {blockName}");
        }
    }
}
