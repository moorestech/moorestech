using System;
using System.Collections;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Fluid;
using NUnit.Framework;
using UniRx;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    ///     ブロック状態の束がクライアントで座標からブロックへ振り分けられることを確かめる（ADR 0088）。撤去して同じ座標に置き直した後も届くことまで見る
    ///     Verifies the client routes the block-state bundle to blocks by position (ADR 0088), including after removing and re-placing at the same position
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class BlockStateTickBundleRoutingTest
    {
        private const string PipeBlockName = "テスト用パイプ";
        private static readonly Guid MixedFluidGuid = new("00000000-0000-0000-0000-000000000001");
        // 待ちの上限。約15秒（900フレーム）
        // Upper bound for each wait, about 15 seconds (900 frames)
        private const int WaitFrameLimit = 900;

        [UnityTest]
        public IEnumerator パイプの水位が束で届き再設置後も届く()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
#if UNITY_EDITOR
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
#endif

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                // 1回目の設置で水位が届く
                // The fluid amount arrives for the first placement
                var pos = new Vector3Int(3, 0, 3);
                await PlacePipeAndAssertAmountArrives(pos, 100f, "1回目");

                // 撤去してクライアントから消えるのを待ち、同じ座標に置き直しても届く
                // Remove, wait until the client drops it, then re-place at the same position and expect the state again
                Assert.IsTrue(RemoveBlock(pos), "パイプを撤去できるべき");
                for (var i = 0; i < WaitFrameLimit && ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out _); i++) await UniTask.Yield();
                Assert.IsFalse(ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out _), "クライアントからパイプが消えるべき");
                await PlacePipeAndAssertAmountArrives(pos, 50f, "再設置後");
            }

            async UniTask PlacePipeAndAssertAmountArrives(Vector3Int pos, float amount, string label)
            {
                var serverBlock = PlaceBlock(PipeBlockName, pos, BlockDirection.East);
                var view = await WaitBlockGameObjectSpawn(pos);
                for (var i = 0; i < WaitFrameLimit && view.IsPlayingPlaceAnimation; i++) await UniTask.Yield();
                Assert.IsFalse(view.IsPlayingPlaceAnimation, $"{label} 設置アニメが終わるべき");

                var changedCount = 0;
                using var changedSubscription = view.OnBlockStateChanged.Subscribe(_ => changedCount++);
                var processor = view.GetComponentInChildren<FluidPipeStateChangeProcessor>();
                Assert.IsNotNull(processor, $"{label} パイプのプレハブに状態プロセッサがあるべき");

                // サーバー側のパイプへ直接液体を入れ、束で届いた水位を観測する
                // Put fluid straight into the server pipe and observe the amount delivered by the bundle
                var fluidId = MasterHolder.FluidMaster.GetFluidId(MixedFluidGuid);
                serverBlock.GetComponent<FluidPipeComponent>().Node.AddExternal(new FluidStack(amount, fluidId));
                for (var i = 0; i < WaitFrameLimit && !HasAmount(processor, amount); i++) await UniTask.Yield();
                Assert.IsTrue(HasAmount(processor, amount), $"{label} 水位{amount}が届くべき");
                Assert.Greater(changedCount, 0, $"{label} OnBlockStateChangedが発火するべき");
            }

            bool HasAmount(FluidPipeStateChangeProcessor processor, float amount)
            {
                return processor.DebugCurrentFluidPipeState != null && Mathf.Approximately(processor.DebugCurrentFluidPipeState.Amount, amount);
            }

            #endregion
        }
    }
}
