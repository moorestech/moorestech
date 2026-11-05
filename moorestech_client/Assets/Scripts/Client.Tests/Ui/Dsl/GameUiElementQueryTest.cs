using System.Linq;
using Client.Playtest.UiElements;
using Client.Playtest.UiElements.GameUi;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.TextField;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Dsl
{
    /// <summary>
    /// GameUi の照会（Find・SetText）を実パネルに置いた要素で確かめる（L1）
    /// Verifies the GameUi lookups (Find and SetText) against elements placed on a real panel (L1)
    /// </summary>
    public class GameUiElementQueryTest : UiTestHost
    {
        private const string Testid = "dsl-target";
        private static readonly Rect TargetPanelRect = new(100, 100, 200, 50);

        [Test]
        public void FindReturnsVisibleClickableLabelWithTextFlagsAndFlippedRect()
        {
            var label = Place(new Label("hello") { name = Testid });
            label.AddToClassList("dsl-label");
            label.AddToClassList("dsl-label--selected");
            simulate.FrameUpdate();

            var snapshot = Find();
            Assert.AreEqual(UiElementLookupStatus.Found, snapshot.Status);
            Assert.IsTrue(snapshot.Visible);
            Assert.IsTrue(snapshot.Clickable);
            Assert.AreEqual("hello", snapshot.Text);
            CollectionAssert.AreEquivalent(new[] { "dsl-label--selected" }, snapshot.StateFlags.ToArray());
            Assert.AreEqual(new Rect(100, 720 - 150, 200, 50), snapshot.ScreenRect);
        }

        [Test]
        public void FindReturnsNotFoundAndAmbiguous()
        {
            Assert.AreEqual(UiElementLookupStatus.NotFound, Find().Status);

            // 同じ name が 2 件あれば、どちらを押すか決められない
            // Two elements sharing the name cannot pick a target
            Place(new Label("a") { name = Testid });
            Place(new Label("b") { name = Testid });
            Assert.AreEqual(UiElementLookupStatus.Ambiguous, Find().Status);
        }

        [Test]
        public void CoveredElementIsVisibleButNotClickable()
        {
            Place(new Label("covered") { name = Testid });
            Place(new VisualElement { name = "dsl-cover", pickingMode = PickingMode.Position });

            var snapshot = Find();
            Assert.IsTrue(snapshot.Visible);
            Assert.IsFalse(snapshot.Clickable);
        }

        [Test]
        public void HiddenAncestorMakesElementInvisible()
        {
            var parent = Place(new VisualElement { name = "dsl-parent" });
            parent.Add(new Label("inside") { name = Testid });
            parent.style.display = DisplayStyle.None;
            simulate.FrameUpdate();

            var snapshot = Find();
            Assert.IsFalse(snapshot.Visible);
            Assert.IsFalse(snapshot.Clickable);
        }

        [Test]
        public void ContainerTextConcatenatesDescendantTexts()
        {
            var box = Place(new VisualElement { name = Testid });
            box.Add(new Label("left"));
            box.Add(new Label("right"));
            simulate.FrameUpdate();

            Assert.AreEqual("leftright", Find().Text);
        }

        [Test]
        public void SetTextAssignsValueAndRaisesOneChangeEvent()
        {
            var field = Place(new TextField { name = Testid });
            var changes = 0;
            field.RegisterValueChangedCallback(_ => changes++);

            var result = GameUiElementQuery.SetText(rootVisualElement, Testid, "日本語の本文");
            Assert.AreEqual(UiSetTextStatus.Applied, result.Status);
            Assert.AreEqual("日本語の本文", field.value);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void SetTextReachesGameTextFieldInnerField()
        {
            // GameTextField は testid を内側の TextField に付ける
            // GameTextField puts the testid on its inner TextField
            var gameTextField = new GameTextField(Testid, false);
            Place(gameTextField);

            Assert.AreEqual(UiSetTextStatus.Applied, GameUiElementQuery.SetText(rootVisualElement, Testid, "abc").Status);
            Assert.AreEqual("abc", gameTextField.Field.value);
        }

        [Test]
        public void SetTextRefusesReadOnlyNonFieldAndTruncatedValues()
        {
            var field = Place(new TextField { name = Testid, isReadOnly = true });
            Assert.AreEqual(UiSetTextStatus.NotInteractable, GameUiElementQuery.SetText(rootVisualElement, Testid, "x").Status);
            Assert.AreEqual(string.Empty, field.value);

            // 文字数上限で切られた値は要求と違うので ValueMismatch
            // A value cut by the length limit differs from the request, so it is ValueMismatch
            field.isReadOnly = false;
            field.maxLength = 3;
            var truncated = GameUiElementQuery.SetText(rootVisualElement, Testid, "abcdef");
            Assert.AreEqual(UiSetTextStatus.ValueMismatch, truncated.Status);
            Assert.AreEqual("abc", truncated.ActualValue);

            rootVisualElement.Clear();
            Place(new Label("not a field") { name = Testid });
            Assert.AreEqual(UiSetTextStatus.NotTextField, GameUiElementQuery.SetText(rootVisualElement, Testid, "x").Status);
        }

        private UiElementSnapshot Find()
        {
            return GameUiElementQuery.Find(rootVisualElement, Testid, new FlipOnlyScreenMapper());
        }

        // 要素を同じ矩形へ絶対配置してレイアウトを済ませる。後から置いたものが手前に重なる
        // Absolutely place the element on the same rect and lay out; later elements stack in front
        private T Place<T>(T element) where T : VisualElement
        {
            // テーマの余白を消して矩形を置いた値どおりにする
            // Drop the theme margins so the rect matches the placed values
            element.style.position = Position.Absolute;
            element.style.marginLeft = 0;
            element.style.marginTop = 0;
            element.style.left = TargetPanelRect.x;
            element.style.top = TargetPanelRect.y;
            element.style.width = TargetPanelRect.width;
            element.style.height = TargetPanelRect.height;
            rootVisualElement.Add(element);
            simulate.FrameUpdate();
            return element;
        }

        // 倍率 1 の画面を仮定し、y の反転だけを行う
        // Assumes a screen at scale 1 and only flips y
        private sealed class FlipOnlyScreenMapper : IGameUiScreenMapper
        {
            public Rect PanelToScreen(Rect panelRect)
            {
                return new Rect(panelRect.x, 720 - panelRect.yMax, panelRect.width, panelRect.height);
            }
        }
    }
}
