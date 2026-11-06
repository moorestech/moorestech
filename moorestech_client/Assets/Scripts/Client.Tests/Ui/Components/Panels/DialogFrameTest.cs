using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// DialogFrame の寸法を参照 3-modal（Web ModalHost の BP 名の画面）の実寸で固定する（L1。倍率 1 のパネル）
    /// Pins DialogFrame dimensions to the real sizes of reference 3-modal (the Web ModalHost blueprint-name dialog) (L1; panel at scale 1)
    /// </summary>
    public class DialogFrameTest : UiTestHost
    {
        [SetUp]
        public void LoadDictionary()
        {
            Localize.Initialize();
        }

        [Test]
        public void 幅は440で題の行は高さ60と左右16で本文は20空ける()
        {
            var frame = new DialogFrame("dialog-frame-test");
            frame.SetTitle(UiText.FromVerbatim("Blueprint name"));
            var body = new VisualElement { name = "dialog-frame-test-body" };
            body.style.height = 36;
            frame.Add(body);
            rootVisualElement.Add(frame);
            simulate.FrameUpdate();

            var header = frame.Q(className: "dialog-frame__header");
            var title = frame.Q<Label>("dialog-frame-test-title");
            Assert.AreEqual(440f, frame.worldBound.width, 0.1f, "--modal-size");
            Assert.AreEqual(60f, header.worldBound.height, 0.1f, "--dialog-frame-header-min-height");
            Assert.AreEqual(frame.worldBound.xMin + 16f, title.worldBound.xMin, 0.5f, "--mb-padding");
            Assert.AreEqual(frame.worldBound.xMin + 20f, body.worldBound.xMin, 0.5f, "--dialog-frame-body-padding");
            Assert.AreEqual(header.worldBound.yMax + 20f, body.worldBound.yMin, 0.5f);
            Assert.AreSame(body.hierarchy.parent, frame.contentContainer, "Add goes into the content");

            // 角丸で切る面が題の行と本文を持ち、外側の箱の直下にある（影は外側の箱に置けば切られない）
            // The clipping surface holds the title row and body directly under the outer box (a shadow on the outer box is not clipped)
            var surface = frame.Q(className: "dialog-frame__surface");
            Assert.AreSame(frame, surface.hierarchy.parent);
            Assert.IsTrue(surface.Contains(header) && surface.Contains(body));
        }

        [Test]
        public void 操作のボタンは本文の幅いっぱいに伸び題の無い枠は題の行を持たない()
        {
            var frame = new DialogFrame("dialog-frame-actions");
            frame.Add(new VisualElement { name = "dialog-frame-actions-body" });
            var save = new FormButton("dialog-frame-save", UiText.FromVerbatim("Save"), FormButtonVariant.Filled);
            frame.AddAction(save);
            rootVisualElement.Add(frame);
            simulate.FrameUpdate();

            Assert.IsNull(frame.Q(className: "dialog-frame__header"));
            Assert.IsTrue(save.ClassListContains("dialog-frame__action"));
            Assert.AreEqual(400f, save.worldBound.width, 0.5f, "440 - 20 x 2");
        }
    }
}
