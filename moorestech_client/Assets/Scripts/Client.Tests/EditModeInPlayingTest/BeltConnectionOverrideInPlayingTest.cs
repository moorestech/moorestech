using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Game.Context;
using NUnit.Framework;
using Server.Boot.Loop.PacketProcessing;
using UnityEditor;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    [Category("CiShardClientPlay3")]
    public class BeltConnectionOverrideInPlayingTest
    {
        [UnityTest]
        public IEnumerator LiveWorld_ReconnectsAfterUpperSourceRemovalAndReplacement()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await LoadMainGame();

                var entry = new BeltConnectionWorldTestEntry(
                    FindBlockIdByName("直進高速ベルトコンベア"),
                    FindBlockIdByName("上り高速ベルトコンベア"),
                    FindBlockIdByName("下り高速ベルトコンベア"));
                ServerContext.GetService<TickEndPacketQueue>().Enqueue(entry);
                var completed = await UniTask.WhenAny(
                    UniTask.WaitUntil(() => entry.IsCompleted),
                    UniTask.Delay(TimeSpan.FromSeconds(30)));
                Assert.AreEqual(0, completed, "server tick entry timed out");
                Assert.IsNull(entry.Failure, entry.Failure);

                // 全候補の選択・切替・復帰を照合。
                // Check selection, switching, and restoration for all candidates.
                Assert.AreEqual(BeltConnectionWorldTestEntry.AllBlocks, entry.InitiallyPresent);
                Assert.AreEqual(BeltConnectionWorldTestEntry.UpperToUpper, entry.InitiallyConnected);
                Assert.AreEqual(BeltConnectionWorldTestEntry.WithoutUpperSource, entry.AfterRemovalPresent);
                Assert.AreEqual(BeltConnectionWorldTestEntry.LowerToUpper, entry.AfterRemovalConnected);
                Assert.AreEqual(BeltConnectionWorldTestEntry.AllBlocks, entry.AfterReplacementPresent);
                Assert.AreEqual(BeltConnectionWorldTestEntry.UpperToUpper, entry.AfterReplacementConnected);
            }

            #endregion
        }
    }
}
