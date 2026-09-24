using System.Collections;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Tests.EditModeInPlayingTest.Util;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.SaveLoad.Snapshot;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UniRx;
using UnityEngine.TestTools;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.BugReport
{
    [Category("CiShardClientPlay3")]
    public class BugReportRepeatSubmitTest
    {
        [UnityTest]
        public IEnumerator 同じポーズで二件送ると新しい記録を添えた箱が二つできる()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();
            UnityEditor.SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal
            async UniTask Body()
            {
                await EditModeInPlayingTestUtil.LoadMainGame();
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                ServerContext.GetService<WorldSnapshotRing>().Start(null, null, null);
                await UniTask.Delay(1000);
                var pauseMenu = resolver.Resolve<PauseMenuStateService>();
                var opened = 0;
                using var subscription = pauseMenu.OnPauseMenuOpened.Subscribe(_ => opened++);
                var session = await BugReportSubmitUtil.OpenPauseMenuAndWaitCapture(resolver);

                // 書き出しに先立って時刻を進め、再確保した記録が最初のEscapeと異なることを検出する
                // Advance before writing so fresh records can be distinguished from the first Escape moment
                await UniTask.Delay(1000);
                var before = BugReportSubmitUtil.ExistingBundles();
                pauseMenu.ShowPage(PauseMenuPage.BugReport);
                var first = await BugReportSubmitUtil.SubmitAndTakeNewBundle(resolver, "一件目", PlaytestReportKind.Bug, before);
                Assert.AreEqual(PauseMenuPage.Top, pauseMenu.CurrentPage.Value);
                Assert.AreEqual(UIStateEnum.PauseMenu, resolver.Resolve<UIStateControl>().CurrentState);
                await BugReportSubmitUtil.WaitCapture(session);

                // ポーズを開き直さず二件目を送り、二つの独立した完成済みバンドルを確かめる
                // Send again without reopening the pause and verify two independent completed bundles
                pauseMenu.ShowPage(PauseMenuPage.BugReport);
                var second = await BugReportSubmitUtil.SubmitAndTakeNewBundle(resolver, "二件目", PlaytestReportKind.Bug, BugReportSubmitUtil.ExistingBundles());
                Assert.AreNotEqual(first, second);
                Assert.AreEqual(2, BugReportSubmitUtil.ExistingBundles().Except(before).Count(path => path == first || path == second));
                Assert.AreEqual(1, opened);
                Assert.AreEqual(PauseMenuPage.Top, pauseMenu.CurrentPage.Value);
                Assert.AreEqual(UIStateEnum.PauseMenu, resolver.Resolve<UIStateControl>().CurrentState);

                var firstManifest = JObject.Parse(File.ReadAllText(Path.Combine(first, "manifest.json")));
                var secondManifest = JObject.Parse(File.ReadAllText(Path.Combine(second, "manifest.json")));
                BundleManifestContract.AssertPlanC(first, firstManifest);
                BundleManifestContract.AssertPlanC(second, secondManifest);
                Assert.AreEqual("一件目", (string)firstManifest["description"]);
                Assert.AreEqual("二件目", (string)secondManifest["description"]);
                Assert.Greater((ulong)secondManifest["reportTick"], (ulong)firstManifest["reportTick"]);
                Assert.GreaterOrEqual(((JArray)secondManifest["snapshotFiles"]).Count, 1);
                Directory.Delete(first, true);
                Directory.Delete(second, true);
            }
            #endregion
        }
    }
}
