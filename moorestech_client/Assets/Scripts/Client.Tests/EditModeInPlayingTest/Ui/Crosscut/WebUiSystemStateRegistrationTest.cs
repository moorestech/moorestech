using System.Collections;
using Client.Game.InGame.Context;
using Client.Input;
using Client.Ui.State.Recipe.Catalog;
using Client.Ui.State.Routing;
using Client.Ui.State.Train.Platform.Edit;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.Crosscut
{
    /// <summary>
    /// Web の系の起動で、Web ホストが呼ぶ State のインスタンス 2 型が DI から解決できることを確かめる（CEF・hub の有無に依らない。D-36）
    /// Verifies a Web boot can resolve the two State instance types the Web host calls (independent of CEF or the hub; D-36)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class WebUiSystemStateRegistrationTest : UiSystemPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Webの系でWebホストが呼ぶStateのインスタンスが解決できる()
        {
            SelectUiSystemBeforePlayMode(UiSystem.Web);
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();
                Assert.AreEqual(UiSystem.Web, UiInputExclusivity.ActiveUiSystem, "起動の系が Web でない");

                // 登録漏れは VContainer の例外でここが赤になる
                // A missing registration turns this red with a VContainer exception
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                Assert.IsNotNull(resolver.Resolve<RecipeViewerItemCatalog>(), "RecipeViewerItemCatalog が解決できない");
                Assert.IsNotNull(resolver.Resolve<ITrainBlockCommands>(), "ITrainBlockCommands が解決できない");
                Assert.IsNotNull(resolver.Resolve<IUiStateTransitionRequester>(), "IUiStateTransitionRequester が解決できない");
            }

            #endregion
        }
    }
}
