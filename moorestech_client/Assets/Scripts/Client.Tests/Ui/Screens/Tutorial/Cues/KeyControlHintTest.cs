using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
using Client.Tests.Ui.State.Tutorial;
using Client.Ui.Components.Text;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Tutorial.KeyControlHint;
using Client.Ui.State.Tutorial.KeyControlHint;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Tutorial.Cues
{
    /// <summary>
    /// ホットバー上のキー操作ヒントの L1
    /// L1 for the key control hints above the hotbar
    /// </summary>
    public class KeyControlHintTest : ScreenTestHost
    {
        private FakeKeyControlHintModel _model;
        private KeyControlHintProjection _projection;
        private KeyControlHintView _view;

        [SetUp]
        public void MountHint()
        {
            _model = new FakeKeyControlHintModel();
            _projection = new KeyControlHintProjection();
            _view = KeyControlHintPresenter.Mount(rootVisualElement, _model.Current);
        }

        internal static TutorialKeyControlElementData Hint(string elementId, string keyName, string uiState)
        {
            return new TutorialKeyControlElementData { ElementId = elementId, TutorialGuid = TutorialOverlayProjectionTest.LabelGuid.ToString(), KeyName = keyName, UiState = uiState };
        }

        private List<ShortcutHintLabel> Hints()
        {
            return _view.Query<ShortcutHintLabel>("key-control-hint").Where(hint => IsShown(hint)).ToList();
        }

        /// <web-test file="src/features/tutorial/KeyControlHintHud.test.ts">uiStateが一致するヒントだけを、既存共有部品のkbd+本文の順で描く</web-test>
        [Test]
        public void 画面状態が一致するヒントだけをkbdと文の順で出す()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "GameScreen"), Hint("k2", "E", "PlayerInventory")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();

            var hint = Hints().Single();
            var keyCap = hint.Q<KeyCap>();
            Assert.AreEqual("Tab", keyCap.text);
            // Prefix は前半の文が空なので、見える子の先頭が kbd、その次が文
            // In Prefix the leading text is empty, so the first visible child is the kbd and the sentence follows
            var visible = hint.Children().Where(child => IsShown(child)).ToList();
            Assert.IsInstanceOf<KeyCap>(visible[0]);
            Assert.IsNotEmpty(((Label)visible[1]).text);
            Assert.IsTrue(hint.ClassListContains(UiTypographyClass.KeyHintText));
        }

        /// <web-test file="src/features/tutorial/KeyControlHintHud.test.ts">一致するヒントが無ければHUD自体を描かない</web-test>
        [Test]
        public void 一致するヒントが無ければHUDごと出さない()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "PlayerInventory")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view));
        }

        /// <web-test file="src/features/tutorial/KeyControlHintHud.test.ts">blockingスキット中は描かない</web-test>
        [Test]
        public void blockingのスキット中は出さない()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "GameScreen")), UIStateEnum.GameScreen, true));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_view));
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void キー操作ヒントは入力を奪わない()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "GameScreen")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();

            // 行と部品の内側（kbd・文）のどの中心を拾っても、ヒントの要素は当たらない（Web の pointer-events: none。D-36）
            // Picking at the center of the row or any part inside it (kbd, text) never hits a hint element (Web pointer-events: none; D-36)
            var parts = Hints().Single().Query<VisualElement>().Where(part => IsShown(part) && 0 < part.worldBound.width).ToList();
            Assert.IsNotEmpty(parts);
            foreach (var part in parts)
            {
                var picked = rootVisualElement.panel.Pick(part.worldBound.center);
                Assert.IsFalse(picked != null && (picked == _view || _view.Contains(picked)), $"{part.name} picked {picked?.name}");
            }
        }

        /// <web-test file="src/features/tutorial/KeyControlHintHud.test.ts">同時に一致するヒントが2件あれば2件とも描く</web-test>
        [Test]
        public void 同時に一致する2件は2件とも縦に積む()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "GameScreen"), Hint("k2", "Q", "GameScreen")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();

            var hints = Hints();
            Assert.AreEqual(2, hints.Count);
            Assert.Less(hints[0].worldBound.yMax, hints[1].worldBound.yMin + 0.5f);

            // 行は Web の行箱 30px と gap 6px の間隔で積む（字の自然な高さ 37px で広がらない。脈動の拡縮を含まない layout で測る）
            // Rows stack at the Web 30px line box plus the 6px gap (not widened by the 37px glyph; measured on layout, free of the pulse scale)
            Assert.AreEqual(30f, hints[0].layout.height, 0.5f);
            Assert.AreEqual(36f, hints[1].layout.y - hints[0].layout.y, 0.5f);
        }

        [Test]
        public void ヒントが減ると残った行は残ったヒントのキーを示す()
        {
            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(1, Hint("k1", "Tab", "GameScreen"), Hint("k2", "E", "GameScreen")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();
            Assert.AreEqual(2, Hints().Count);

            _model.Set(_projection.Project(TutorialOverlayProjectionTest.Data(2, Hint("k2", "E", "GameScreen")), UIStateEnum.GameScreen, false));
            simulate.FrameUpdate();

            var hints = Hints();
            Assert.AreEqual(1, hints.Count);
            Assert.AreEqual("E", hints[0].Q<KeyCap>().text);
        }
    }
}
