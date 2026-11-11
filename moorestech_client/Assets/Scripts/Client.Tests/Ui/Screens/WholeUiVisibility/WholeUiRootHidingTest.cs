using System;
using System.Collections.Generic;
using Client.Input;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.WholeUiVisibility
{
    /// <summary>
    /// 全体非表示がルート 1 つの display で全層（BehindStage〜FullScreenGate）を隠し、戻すと同じ要素のまま出ることを確かめる（L1）
    /// Verifies that whole-UI hiding hides every layer (BehindStage to FullScreenGate) through the root's display, and shows the same elements again (L1)
    /// </summary>
    /// <remarks>
    /// GameUiRoot は UIDocument を持つ MonoBehaviour で L1 では組めないため、GameUiRoot.SetWholeUiVisible の中身 UiLayerStack.SetAllLayersVisible をテストの rootVisualElement に組んで試す（D-28）。
    /// 隠れた要素は自身の resolvedStyle.display が Flex のままなので、隠れたかはルートの display と Pick で見る。
    /// GameUiRoot is a MonoBehaviour owning a UIDocument and cannot be built in L1, so the test builds UiLayerStack.SetAllLayersVisible, the body of GameUiRoot.SetWholeUiVisible, on the test rootVisualElement (D-28).
    /// Hidden elements keep their own resolvedStyle.display as Flex, so hiddenness is read from the root's display and Pick.
    /// </remarks>
    public class WholeUiRootHidingTest : ScreenTestHost
    {
        private readonly List<VisualElement> _probes = new();
        private UiLayerStack _stack;

        [SetUp]
        public void PutProbeOnEveryLayer()
        {
            // 全層の両族に 100px 角の印を置く
            // Place a 100px probe on both families of every layer
            _probes.Clear();
            _stack = UiLayerStack.Build(rootVisualElement);
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                foreach (UiFamily family in Enum.GetValues(typeof(UiFamily)))
                {
                    var probe = new VisualElement { name = $"probe-{layer}-{family}" };
                    probe.style.position = Position.Absolute;
                    probe.style.left = 0;
                    probe.style.top = 0;
                    probe.style.width = 100;
                    probe.style.height = 100;
                    _stack.GetLayer(layer, family).Add(probe);
                    _probes.Add(probe);
                }
            }
            simulate.FrameUpdate();
        }

        [TearDown]
        public void ShowRootAndClearFocusFlag()
        {
            // 基底はルートのインラインの指定をテスト間で消さないので、隠したままにしない
            // The base keeps the root's inline style across tests, so never leave it hidden
            _stack.SetAllLayersVisible(true);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
        }

        /// <web-test file="e2e/tests/system/commonHud.spec.ts">ui.visibility=falseでPortalを含む全UIを退避し復帰する</web-test>
        /// <web-test file="e2e/tests/phaseC4.spec.ts">cutscene state withdraws every web UI layer</web-test>
        [Test]
        public void 隠すと全層の要素が拾えず戻すと同じ要素が前面から拾える()
        {
            // 最前面は最後の層の舞台（UiStageFrame は実画面の後に舞台を足す。族の enum 順とは逆）
            // The frontmost is the last layer's stage (UiStageFrame adds the stage after the viewport, opposite to the family enum order)
            var topmost = _stack.GetLayer(UiLayer.FullScreenGate, UiFamily.Stage)[0];
            Assert.AreSame(topmost, rootVisualElement.panel.Pick(new Vector2(50, 50)));

            _stack.SetAllLayersVisible(false);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, rootVisualElement.resolvedStyle.display);
            var picked = rootVisualElement.panel.Pick(new Vector2(50, 50));
            Assert.IsFalse(picked != null && rootVisualElement.Contains(picked), $"隠している間に {picked?.name} が拾えた");

            _stack.SetAllLayersVisible(true);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, rootVisualElement.resolvedStyle.display);
            Assert.AreSame(topmost, rootVisualElement.panel.Pick(new Vector2(50, 50)));
            foreach (var probe in _probes) Assert.IsNotNull(probe.panel, $"{probe.name} が階層から外れた");
        }

        [Test]
        public void 隠している間も層の中の要素の表示指定は変えない()
        {
            _stack.SetAllLayersVisible(false);
            simulate.FrameUpdate();
            foreach (var probe in _probes) Assert.AreEqual(DisplayStyle.Flex, probe.resolvedStyle.display, $"{probe.name} の display を書き換えた");
        }

        [Test]
        public void 隠す前に入力欄のフォーカスを外しキーの抑止を残さない()
        {
            // 入力規則を付けたモーダルの層の入力欄にフォーカスを置く（不具合報告の入力中にカットシーンへ入る形）
            // Focus a text field on the modal layer carrying the input rules (as when a cutscene starts while typing a bug report)
            var modal = _stack.GetLayer(UiLayer.Modal, UiFamily.Stage);
            GameUiInputRules.Attach(modal);
            var textField = new TextField { name = "hiding-text" };
            textField.style.width = 300;
            modal.Add(textField);
            simulate.FrameUpdate();
            textField.Focus();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);

            _stack.SetAllLayersVisible(false);

            Assert.IsNull(rootVisualElement.panel.focusController.focusedElement, "隠した後も入力欄がフォーカスを持っている");
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "隠している間もゲームのキーが抑止されたまま");
        }
    }
}
