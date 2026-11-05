using Client.Ui.Screens.Toast;
using Client.Ui.State.Toast;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Toast
{
    /// <summary>
    /// トーストが積まれ 3 秒後に消え、種別を保ち、入力を奪わないことを確かめる（L1。Web の toastStore.test.ts）
    /// Verifies toasts stack, disappear after 3 seconds, keep their variant and never take input (L1; Web toastStore.test.ts)
    /// </summary>
    public class ToastHostViewTest : ScreenTestHost
    {
        private ToastHostView _host;

        [SetUp]
        public void MountHost()
        {
            _host = Mount(new ToastHostView(Catalog.Toast));
        }

        [Test]
        public void トーストは積まれ3秒後に消える()
        {
            _host.Push(new GameUiToast("saved", GameUiToastVariant.Info));
            simulate.FrameUpdateMs(0);
            Assert.AreEqual(1, _host.childCount);
            Assert.AreEqual("saved", _host.Q<Label>(className: "toast-item__text").text);

            // 表示時間の途中ではまだ残り、薄れきったあとに外れる
            // Still present midway through the display time, removed once fully faded
            simulate.FrameUpdateMs(2500);
            Assert.AreEqual(1, _host.childCount);
            simulate.FrameUpdateMs(1000);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(300);
            Assert.AreEqual(0, _host.childCount);
        }

        [Test]
        public void 出現は透明から濃くなり消える前に薄れる()
        {
            _host.Push(new GameUiToast("fade", GameUiToastVariant.Info));
            var item = _host[0];
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(75);
            Assert.That(item.resolvedStyle.opacity, Is.GreaterThan(0f).And.LessThan(1f));
            simulate.FrameUpdateMs(200);
            Assert.AreEqual(1f, item.resolvedStyle.opacity, 0.0001f);

            simulate.FrameUpdateMs(2800);
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(75);
            Assert.That(item.resolvedStyle.opacity, Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void variantを保持し複数は縦に積まれる()
        {
            _host.Push(new GameUiToast("a", GameUiToastVariant.Info));
            _host.Push(new GameUiToast("b", GameUiToastVariant.Error));
            simulate.FrameUpdateMs(0);
            Assert.AreEqual(2, _host.childCount);
            Assert.IsTrue(_host[0].ClassListContains("toast-item--info"));
            Assert.IsTrue(_host[1].ClassListContains("toast-item--error"));
            Assert.Less(_host[0].worldBound.y, _host[1].worldBound.y);
        }

        [Test]
        public void ホストと各トーストは入力を奪わない()
        {
            _host.Push(new GameUiToast("x", GameUiToastVariant.Info));
            simulate.FrameUpdateMs(0);
            Assert.AreEqual(PickingMode.Ignore, _host.pickingMode);
            Assert.AreEqual(PickingMode.Ignore, _host[0].pickingMode);
        }

        [Test]
        public void 右下に寄る()
        {
            _host.Push(new GameUiToast("pos", GameUiToastVariant.Info));
            simulate.FrameUpdateMs(0);
            var root = rootVisualElement.worldBound;
            Assert.Greater(_host.worldBound.xMax, root.xMax - 40f);
            Assert.Greater(_host.worldBound.yMax, root.yMax - 40f);
        }
    }
}
