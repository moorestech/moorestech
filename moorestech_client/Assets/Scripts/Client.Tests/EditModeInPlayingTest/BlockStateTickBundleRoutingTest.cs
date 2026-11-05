using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.Block;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Gear.Common;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    ///     ブロック状態の束がクライアントで座標からブロックへ振り分けられることを確かめる（ADR 0091）。撤去して同じ座標に置き直した後も届くことまで見る
    ///     Verifies the client routes the block-state bundle to blocks by position (ADR 0091), including after removing and re-placing at the same position
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class BlockStateTickBundleRoutingTest
    {
        // テスト用modで60RPMを出し続けるジェネレーター
        // Generator that keeps producing 60 RPM in the test mod
        private const string GeneratorBlockName = "無限歯車ジェネレーター";
        private const float GeneratedRpm = 60f;
        // 待ちの上限。約15秒（900フレーム）
        // Upper bound for each wait, about 15 seconds (900 frames)
        private const int WaitFrameLimit = 900;

        [UnityTest]
        public IEnumerator 歯車ジェネレーターの状態が束で届き再設置後も届く()
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

                // 1回目の設置で歯車の状態が届く
                // The gear state arrives for the first placement
                var pos = new Vector3Int(3, 0, 3);
                await PlaceGeneratorAndAssertStateArrives(pos, "1回目");

                // 撤去してクライアントから消えるのを待ち、同じ座標に置き直しても届く
                // Remove, wait until the client drops it, then re-place at the same position and expect the state again
                Assert.IsTrue(RemoveBlock(pos), "ジェネレーターを撤去できるべき");
                for (var i = 0; i < WaitFrameLimit && ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out _); i++) await UniTask.Yield();
                Assert.IsFalse(ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(pos, out _), "クライアントからジェネレーターが消えるべき");
                await PlaceGeneratorAndAssertStateArrives(pos, "再設置後");
            }

            async UniTask PlaceGeneratorAndAssertStateArrives(Vector3Int pos, string label)
            {
                // 新しいBlockGameObjectは状態を持たず、ApplyBlockState経由でのみ状態が入る
                // A fresh BlockGameObject holds no state; it only gets one through ApplyBlockState
                PlaceBlock(GeneratorBlockName, pos, BlockDirection.East);
                var view = await WaitBlockGameObjectSpawn(pos);
                for (var i = 0; i < WaitFrameLimit && !HasGeneratedRpm(view); i++) await UniTask.Yield();
                Assert.IsTrue(HasGeneratedRpm(view), $"{label} 回転数{GeneratedRpm}の歯車状態が束で届くべき");
            }

            bool HasGeneratedRpm(BlockGameObject view)
            {
                var state = view.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
                return state != null && Mathf.Approximately(state.CurrentRpm, GeneratedRpm);
            }

            #endregion
        }
    }
}
