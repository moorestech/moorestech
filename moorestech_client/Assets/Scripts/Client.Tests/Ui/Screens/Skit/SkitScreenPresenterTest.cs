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
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Skit
{
    /// <summary>
    /// Presenter が View を層へ載せてから最初の Render を呼び（途中から開いても本文を打ち直さない）、暗転を store に、Enter の可否を全体表示に追わせることを確かめる（L1）
    /// Verifies the presenter mounts the view on its layer before the first Render (no retyping when opened midway), the blackout follows the store and Enter follows whole-UI visibility (L1)
    /// </summary>
    public class SkitScreenPresenterTest : ScreenTestHost
    {
        private const string Body = "Blocking message";

        private GameObject _host;
        private SkitPresentationStateStore _store;
        private FakeWholeUiVisibilitySource _wholeUiSource;
        private RecordingSkitCommands _commands;

        [SetUp]
        public void SetUpStoreAndPresenter()
        {
            _host = new GameObject("skit-presenter");
            _store = new SkitPresentationStateStore();
            _wholeUiSource = new FakeWholeUiVisibilitySource();
            _commands = new RecordingSkitCommands();
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

        [Test]
        public void 全体非表示の間は会話窓のEnterで送らず戻すと送る()
        {
            // 全文が出た提示で組み、Ctrl+U 相当で全体を隠す
            // Build with a fully shown presentation and hide the whole UI as Ctrl+U does
            _store.BeginBlocking(new SkitActionContext());
            _store.PresentBlockingText("Moore", Body);
            Mount();
            var view = rootVisualElement.Q<SkitScreenView>();
            _wholeUiSource.SetUiRootVisible(false);

            LogAssert.Expect(LogType.Log, "[skit.text] Enter ignored: the whole UI is hidden");
            view.HandleEnterKey();
            Assert.AreEqual(0, _commands.Issued.Count, "全体非表示中の Enter で送った");

            _wholeUiSource.SetUiRootVisible(true);
            view.HandleEnterKey();
            Assert.AreEqual(1, _commands.Issued.Count, "全体表示へ戻した後の Enter で送らない");
        }

        private void Mount()
        {
            var routing = UiRoutingStateTestFactory.Create(_host, UIStateEnum.Story);
            var model = new SkitPresentationModel(_store);
            model.Initialize();
            var wholeUi = new WholeUiVisibilityModel(_wholeUiSource);
            wholeUi.Initialize();
            var root = GameUiRootTestFactory.Create(_host, rootVisualElement);
            new SkitScreenPresenter(root, routing, model, wholeUi, _commands, Toaster, new UiOsCursorRequestStack(new FakeUiOsCursorApplier())).Initialize();
            simulate.FrameUpdate();
        }
    }
}
