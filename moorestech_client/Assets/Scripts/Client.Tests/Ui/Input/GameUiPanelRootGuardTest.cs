using Client.Input;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Input
{
    /// <summary>
    /// フォーカスが無い間にパネルの根へ届く選択移動が入力欄へフォーカスを移さず、要素宛ての選択移動は通すことを実パネルで確かめる（L1）
    /// Verifies on a real panel that a navigation move reaching the panel root with nothing focused never focuses a text field, while element-aimed moves pass through (L1)
    /// </summary>
    public class GameUiPanelRootGuardTest : UiTestHost
    {
        private VisualElement _belowPanelRoot;
        private TextField _textField;
        private int _movesBelowPanelRoot;

        [SetUp]
        public void AttachRulesGuardAndTextField()
        {
            // パネルの根のすぐ下に、打ち消しを通り抜けた選択移動を数える器を置く
            // Right under the panel root, place a container that counts navigation moves that got past the guard
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
            GameUiInputRules.Attach(rulesRoot);
            GameUiInputRules.AttachPanelRootGuard(rootVisualElement.panel.visualTree);

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
