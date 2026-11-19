using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest.Input;
using Client.Playtest.UiElements.GameUi;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui.BuildMenu
{
    /// <summary>
    /// 実ゲームで、ビルドメニュー表示中・フォーカス無しの Tab が検索欄へ吸われずインベントリを開くことを確かめる
    /// Verifies in the real game that Tab opens the inventory instead of being swallowed by the search field while the build menu is shown and nothing is focused
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class BuildMenuTabOpensInventoryTest
    {
        private const int InventoryWaitFrameLimit = 30;

        [UnityTest]
        public IEnumerator ビルドメニューでフォーカスが無いときのTabは検索欄へ吸われずインベントリを開く()
        {
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
                await UniTask.DelayFrame(5);
                LogAssert.ignoreFailingMessages = false;
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                var panel = GameUiRoot.Instance.GetRootElement().panel;

                // 背面の Editor でも注入が届く設定にしてから、ビルドメニューを開く
                // Let injection reach a background Editor, then open the build menu
                using var backgroundInput = EditorBackgroundInputScope.Open();
                await UiStateTransitionWait.TransitionTo(uiStateControl, UIStateEnum.BuildMenu);
                await UniTask.DelayFrame(3);

                // 検索欄はメニューが閉じていてもパネルに載っているので、自分と祖先がすべて表示中であることを前提として確かめる
                // The search field stays in the panel even while the menu is closed, so assert it and every ancestor are displayed
                Assert.AreEqual(UIStateEnum.BuildMenu, uiStateControl.CurrentState, "前提: ビルドメニューの状態になっている");
                var search = panel.visualTree.Q<TextField>("build-menu-search");
                Assert.IsNotNull(search, "前提: ビルドメニューの検索欄がパネルに載っている");
                Assert.IsTrue(GameUiElementQuery.IsVisible(search), "前提: 検索欄が表示中");
                Assert.IsNull(panel.focusController.focusedElement, "前提: パネル内にフォーカスが無い");
                Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "前提: 入力中の旗が立っていない");

                // フォーカスが無いときに PanelEventHandler が Tab で送るのと同じ選択移動（宛先はパネルの根）を送る
                // Send the same navigation move PanelEventHandler sends for Tab with nothing focused (aimed at the panel root)
                using (var tabMove = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next))
                {
                    tabMove.target = panel.visualTree;
                    panel.visualTree.SendEvent(tabMove);
                }
                await UniTask.DelayFrame(2);
                var focusedAfterMove = panel.focusController.focusedElement as VisualElement;
                Assert.IsNull(focusedAfterMove, $"Tab 由来の選択移動でフォーカスが移った: {focusedAfterMove?.GetType().Name} '{focusedAfterMove?.name}'");
                Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "Tab 由来の選択移動で入力中の旗が立った（検索欄へ吸われた）");

                // ゲームが読む InputSystem の Tab でインベントリが開く
                // The InputSystem Tab the game reads opens the inventory
                await SemanticInput.TapKey(Key.Tab);
                for (var i = 0; i < InventoryWaitFrameLimit && uiStateControl.CurrentState != UIStateEnum.PlayerInventory; i++) await UniTask.Yield();
                Assert.AreEqual(UIStateEnum.PlayerInventory, uiStateControl.CurrentState, "Tab でインベントリが開いていない");
            }

            #endregion
        }
    }
}
