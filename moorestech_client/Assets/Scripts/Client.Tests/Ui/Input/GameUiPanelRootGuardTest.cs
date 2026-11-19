using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// 実パネルで検証（L1）：根宛てはフォーカス移動を抑止、要素宛ては通過
    /// On a real panel (L1), root-targeted moves preserve focus and element-targeted moves pass the guard.
    /// </summary>
    public class GameUiPanelRootGuardTest : UiTestHost
    {
        private VisualElement _belowPanelRoot;
        private TextField _textField;
        private int _movesBelowPanelRoot;

        [SetUp]
        public void AttachRulesGuardAndTextField()
        {
            // ガード通過件数を根の直下で測る
            // Count moves just below the root after they pass the guard.
            _movesBelowPanelRoot = 0;
            _belowPanelRoot = new VisualElement { name = "guard-below-panel-root", pickingMode = PickingMode.Ignore };
            _belowPanelRoot.StretchToParentSize();
            _belowPanelRoot.RegisterCallback<NavigationMoveEvent>(_ => _movesBelowPanelRoot++, TrickleDown.TrickleDown);
            rootVisualElement.Add(_belowPanelRoot);

            // 本番と同じ組み合わせ: 規則は UIDocument の根に当たる器へ、打ち消しはパネルの根へ付ける
            // Same pairing as production: the rules on a container standing in for the UIDocument root, the guard on the panel root
            var rulesRoot = new VisualElement { name = "guard-rules-root", pickingMode = PickingMode.Ignore };
            rulesRoot.StretchToParentSize();
            _belowPanelRoot.Add(rulesRoot);
            GameUiInputRules.AttachToGameUi(rulesRoot);

            _textField = new TextField { name = "guard-text" };
            _textField.style.width = 300;
            rulesRoot.Add(_textField);
            simulate.FrameUpdate();
        }

        [TestCase(NavigationMoveEvent.Direction.Next)]
        [TestCase(NavigationMoveEvent.Direction.Previous)]
        public void NavigationMoveAimedAtPanelRootWithNothingFocusedDoesNotFocusTextField(NavigationMoveEvent.Direction direction)
        {
            Assert.IsNull(panel.focusController.focusedElement);

            // PanelEventHandler.SendTabEvent と同じく、宛先をパネルの根にして送る
            // Aim the event at the panel root, exactly as PanelEventHandler.SendTabEvent does
            var panelRoot = rootVisualElement.panel.visualTree;
            using (var tab = NavigationMoveEvent.GetPooled(direction))
            {
                tab.target = panelRoot;
                panelRoot.SendEvent(tab);
            }
            simulate.FrameUpdate();

            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        [Test]
        public void NavigationMoveAimedAtFocusedElementPassesPanelRootAndIsCancelledByRules()
        {
            _textField.Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);

            // PanelEventHandler と同じく、宛先をフォーカス中の要素にして送る
            // Aim the event at the focused element, as PanelEventHandler does
            var focusedBefore = (VisualElement)panel.focusController.focusedElement;
            var movesAtTarget = 0;
            focusedBefore.RegisterCallback<NavigationMoveEvent>(_ => movesAtTarget++, TrickleDown.TrickleDown);
            using (var tab = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next))
            {
                tab.target = focusedBefore;
                rootVisualElement.panel.visualTree.SendEvent(tab);
            }
            simulate.FrameUpdate();

            // パネルの根は通し（下の器まで1件届く）、UIDocument の根の規則が打ち消すのでフォーカスは動かない
            // The panel root lets it through (one reaches the container below) and the root rules cancel it, so focus stays put
            Assert.AreEqual(1, _movesBelowPanelRoot);
            Assert.AreEqual(0, movesAtTarget);
            Assert.AreSame(focusedBefore, panel.focusController.focusedElement);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
        }
    }
}
