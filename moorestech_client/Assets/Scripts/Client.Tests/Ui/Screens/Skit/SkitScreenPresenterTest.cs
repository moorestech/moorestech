using System.Text.RegularExpressions;
using Client.Game.InGame.UI.UIState;
using Client.Skit.Skit;
using Client.Skit.UI;
using Client.Tests.Ui.Screens.Inventory.Support;
using Client.Tests.Ui.State.WholeUiVisibility;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Skit;
using Client.Ui.Screens.Skit.Parts.Window;
using Client.Ui.State.Skit;
using Client.Ui.State.WholeUiVisibility;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// Presenter が View を層へ載せてから最初の Render を呼び（途中から開いても本文を打ち直さない）、暗転を store に追わせることを確かめる（L1）
    /// Verifies the presenter mounts the view on its layer before the first Render (no retyping when opened midway) and the blackout follows the store (L1)
    /// </summary>
    public class SkitScreenPresenterTest : ScreenTestHost
    {
        private const string Body = "Blocking message";

        private GameObject _host;
        private SkitPresentationStateStore _store;

        [SetUp]
        public void SetUpStoreAndPresenter()
        {
            _host = new GameObject("skit-presenter");
            _store = new SkitPresentationStateStore();
        }

        [TearDown]
        public void DestroyHost()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void 本文が打ち出し中の提示で開いても最初の描画は全文を出す()
        {
            // 提示ありの状態で画面を組む
            // Build the screen with a presentation present
            _store.BeginBlocking(new SkitActionContext());
            _store.PresentBlockingText("Moore", Body);
            Mount();

            var text = Find<LocalizedLabel>(rootVisualElement, "skit-body").text;
            Assert.IsTrue(Regex.IsMatch(text, "^(<line-height=[^>]*>)?" + Regex.Escape(SkitRichText.TypewriterBody(Body, Body.Length)) + "$"), $"途中から開いて本文が打ち直された: {text}");
        }

        [Test]
        public void 暗転はstoreの変化に合わせて出入りする()
        {
            Mount();
            _store.BeginBlocking(new SkitActionContext());
            var transition = rootVisualElement.Q<SkitTransitionView>();
            Assert.IsNotNull(transition);
            Assert.AreEqual(DisplayStyle.None, transition.style.display.value);

            _store.SetTransitionVisible(true);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, transition.style.display.value);
        }

        private void Mount()
        {
            var routing = UiRoutingStateTestFactory.Create(_host, UIStateEnum.Story);
            var model = new SkitPresentationModel(_store);
            model.Initialize();
            var wholeUi = new WholeUiVisibilityModel(new FakeWholeUiVisibilitySource());
            wholeUi.Initialize();
            var root = GameUiRootTestFactory.Create(_host, rootVisualElement);
            new SkitScreenPresenter(root, routing, model, wholeUi, new RecordingSkitCommands(), Toaster, new UiOsCursorRequestStack(new FakeUiOsCursorApplier())).Initialize();
            simulate.FrameUpdate();
        }
    }
}
