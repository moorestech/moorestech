using Mooresmaster.Localization.Generated;
using System.Linq;
using Client.Localization;
using Client.Skit.UI;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Challenge.CurrentHud;
using Client.Ui.State.Challenge;
using Client.Ui.State.SkitPresence;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Challenge
{
    /// <summary>
    /// 進行中チャレンジ HUD が完了の知らせ・blocking スキット・背景スキット・言語の切り替えに従うことを確かめる（L1。Web の challenge.spec）
    /// Verifies the current challenge HUD follows completion notices, blocking skits and language changes (L1; Web challenge.spec)
    /// </summary>
    public class CurrentChallengeHudStateTest : ScreenTestHost
    {
        private CurrentChallengeHudView _view;

        [SetUp]
        public void MountView()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            _view = new CurrentChallengeHudView();
            stack.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(_view);
            simulate.FrameUpdate();
        }

        /// <web-test file="e2e/tests/challenge.spec.ts">challenge.current完了eventで進行HUDを更新する</web-test>
        [Test]
        public void 完了の知らせで進行HUDの目標を差し替える()
        {
            _view.Render(Model(false, "石を採掘する"));
            CollectionAssert.AreEqual(new[] { "石を採掘する" }, ChallengeHudTestText.Objectives(_view));

            // 完了で次の目標の一覧が来ると、前の目標は残らない
            // When completion brings the next objective list, the previous objectives do not remain
            _view.Render(Model(false, "石器をクラフトする", "木を伐採して拠点へ運ぶ"));
            CollectionAssert.AreEqual(new[] { "石器をクラフトする", "木を伐採して拠点へ運ぶ" }, ChallengeHudTestText.Objectives(_view));

            // 全部終えて空になったら描かない
            // Hidden once everything is done and the list is empty
            _view.Render(Model(false));
            Assert.IsFalse(IsShown(_view));
        }

        /// <web-test file="e2e/tests/challenge.spec.ts">blockingスキット中だけ進行中チャレンジを隠す</web-test>
        [Test]
        public void blockingスキット中だけ進行中チャレンジを隠す()
        {
            _view.Render(Model(true, "石を採掘する"));
            Assert.IsFalse(IsShown(_view));
            _view.Render(Model(false, "石を採掘する"));
            Assert.IsTrue(IsShown(_view));
        }

        /// <web-test file="e2e/tests/challenge.spec.ts">言語辞書世代の更新だけで進行中チャレンジを再解決する</web-test>
        [Test]
        public void 言語の切り替えだけで進行中チャレンジを引き直す()
        {
            // 辞書のある鍵の文で確かめる（チャレンジ題の鍵はマスタの読み込みが要るので、題の代わりに UI の鍵を使う）
            // Check with a key present in the dictionary (challenge title keys need loaded masters, so a UI key stands in)
            var key = LocalizationKeys.Ui.TrainHud.Title;
            _view.Render(CurrentChallengeHudViewModel.Create(new[] { new UiText(key) }, false));
            simulate.FrameUpdate();
            Assert.AreEqual(Localize.Get(key), ChallengeHudTestText.Objectives(_view).Single());

            // Render を呼ばずに言語だけ変えると、題と目標が引き直される
            // Changing only the language, without Render, re-resolves the title and objectives
            Localize.TrySetChosenLanguage("japanese");
            simulate.FrameUpdate();
            Assert.AreEqual(Localize.Get(key), ChallengeHudTestText.Objectives(_view).Single());
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.Challenge.CurrentTitle), _view.Q<Label>("challenge-hud-label").text);
            Assert.AreNotEqual("Riding Train", ChallengeHudTestText.Objectives(_view).Single());
        }

        /// <web-test file="e2e/tests/challenge.spec.ts">背景スキット中は進行中チャレンジの描画契約を維持する</web-test>
        [Test]
        public void 背景スキットはblockingに数えず進行中チャレンジHUDを隠さない()
        {
            var store = new SkitPresentationStateStore();
            var presence = new BlockingSkitPresence(store);
            store.BeginBackground();
            store.SetBackgroundText("Moore", "Hello");
            Assert.IsFalse(presence.IsBlocking.Value);

            // 背景スキット中でも HUD の出し分けは変わらない
            // The HUD stays shown during a background skit
            var model = CurrentChallengeHudViewModel.Create(new[] { UiText.FromVerbatim("石を採掘する") }, presence.IsBlocking.Value);
            Assert.IsTrue(model.Shown);
        }

        private static CurrentChallengeHudViewModel Model(bool blockingSkitActive, params string[] objectives)
        {
            return CurrentChallengeHudViewModel.Create(objectives.Select(UiText.FromVerbatim).ToArray(), blockingSkitActive);
        }
    }
}
