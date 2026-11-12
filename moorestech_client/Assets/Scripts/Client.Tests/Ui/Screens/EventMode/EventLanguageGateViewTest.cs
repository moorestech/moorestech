using System.Linq;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.EventMode;
using Client.Ui.State.EventMode;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.EventMode
{
    /// <summary>
    /// 言語ゲートの見た目と層（L1）
    /// Language-gate look and layer (L1)
    /// </summary>
    public class EventLanguageGateViewTest : EventLanguageGateTestHost
    {
        /// <web-test file="src/features/eventLanguageGate/EventLanguageGate.test.ts">見せるなら全画面ゲート共有の不透明面とz層に固定testIdで描く</web-test>
        [Test]
        public void 待機中は最前面の全画面ゲートの層に不透明の黒い面を固定のtestidで敷く()
        {
            var view = MountView(new EventLanguageGate(true));

            // 層は最前面で、面は層いっぱいの不透明の黒（--full-screen-gate-face）
            // The layer is frontmost and the face is an opaque black filling it (--full-screen-gate-face)
            Assert.AreEqual(EventLanguageGateView.RootTestId, view.name);
            Assert.AreEqual(rootVisualElement.childCount - 1, rootVisualElement.IndexOf(GateLayer.parent), "FullScreenGate is the frontmost layer");
            var face = view.resolvedStyle.backgroundColor;
            Assert.AreEqual(0f, face.r, 0.001f);
            Assert.AreEqual(0f, face.g, 0.001f);
            Assert.AreEqual(0f, face.b, 0.001f);
            Assert.AreEqual(1f, face.a, 0.001f);
            Assert.AreEqual(GateLayer.worldBound.width, view.worldBound.width, 0.5f);
            Assert.AreEqual(GateLayer.worldBound.height, view.worldBound.height, 0.5f);
            Assert.IsNotNull(GateLayer.Q(EventLanguageGateView.TitleTestId));

            // 面は押下を受け、下へ通さない
            // The face takes presses and lets none through
            Assert.AreSame(view, rootVisualElement.panel.Pick(new Vector2(view.worldBound.xMin + 4f, view.worldBound.yMin + 4f)));
        }

        // Web はゲートの中身に --ui-scale を掛けない。倍率 1.5 でも実画面の寸法のまま（A-41）
        // The Web never applies --ui-scale inside the gate; at scale 1.5 the contents keep their real-screen size (A-41)
        [Test]
        public void 倍率が掛かっても中身は実画面のpxのままボタンは高さ60で画面の中央に並ぶ()
        {
            MountView(new EventLanguageGate(true));
            var options = Options();
            var title = GateLayer.Q(EventLanguageGateView.TitleTestId);

            // ボタンは xl（高さ 60）で横に並び、間は Mantine lg の 20px
            // Buttons are xl (60 high) in a row with Mantine lg 20px between them
            foreach (var option in options) Assert.AreEqual(60f, option.worldBound.height * StageScale, 0.5f);
            for (var i = 1; i < options.Count; i++) Assert.AreEqual(20f, (options[i].worldBound.xMin - options[i - 1].worldBound.xMax) * StageScale, 0.5f);

            // 見出しは 34px・行 44.2px で、本体との間は Mantine xl の 32px
            // The heading is 34px on a 44.2px row, 32px (Mantine xl) above the body
            Assert.AreEqual(44.2f, title.worldBound.height * StageScale, 0.5f);
            Assert.AreEqual(32f, (options[0].worldBound.yMin - title.worldBound.yMax) * StageScale, 0.5f);

            // ボタンは押せる。見出しからボタンの下端までの塊が画面の中央に来る（結末の 1 行は持たない。D-6）
            // The buttons are pressable; the heading-to-buttons block sits at the screen centre (no outcome line; D-6)
            Assert.IsTrue(options.All(option => option.enabledSelf));
            Assert.AreEqual(GateLayer.worldBound.center.y, (title.worldBound.yMin + options[0].worldBound.yMax) / 2f, 0.5f);
            Assert.AreEqual(GateLayer.worldBound.center.x, (options[0].worldBound.xMin + options[^1].worldBound.xMax) / 2f, 0.5f);
            Assert.AreEqual(GateLayer.worldBound.center.x, title.worldBound.center.x, 0.5f);
            Assert.IsTrue(title.ClassListContains(UiTypographyClass.TitleOrder1));
            Assert.LessOrEqual(title.worldBound.width * StageScale, 900.5f, "--full-screen-gate-text-width");
        }

        /// <web-test file="src/features/eventLanguageGate/EventLanguageGate.test.ts">待機中は母国語表記のボタンと英語固定の見出しを描く</web-test>
        [Test]
        public void 待機中は言語の一覧の順に母国語表記のボタンを並べ英語固定の見出しを出す()
        {
            OpenWaitingGate();
            var options = Options();

            // 並びと文言は生成済みの言語の一覧そのもの（Web の mock は 3 件、本物は 4 件）
            // Order and labels are the generated language list itself (the Web mock has 3, the real list 4)
            CollectionAssert.AreEqual(LanguageCatalog.Languages.Select(language => "event-language-gate-option-" + language.Code), options.Select(option => option.name));
            CollectionAssert.AreEqual(LanguageCatalog.Languages.Select(language => language.DisplayName), options.Select(option => ReadPlainText(option.Q<LocalizedLabel>())));
            Assert.AreEqual("日本語", ReadPlainText(Option("japanese").Q<LocalizedLabel>()), "native-name label");
            Assert.AreEqual("Select Language", ReadPlainText(GateLayer.Q<Label>(EventLanguageGateView.TitleTestId)));
            Assert.IsTrue(options.All(option => option.enabledSelf));
        }

        /// <web-test file="src/features/eventLanguageGate/EventLanguageGate.test.ts">見せないなら何も描かず一覧も購読しない</web-test>
        [Test]
        public void 待機しないゲートは何も描かない()
        {
            new EventLanguageGatePresenter(new EventLanguageGate(false), GateLayer, Scale(), LanguageCatalog.Languages).Initialize();
            simulate.FrameUpdate();

            Assert.AreEqual(0, GateLayer.childCount);
        }
    }
}
