using Client.Input;
using Client.Tests.Ui.State.Recipe;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Screen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Recipe.RecipeScreenFixtures;

namespace Client.Tests.Ui.Screens.Recipe.Viewer
{
    /// <summary>
    /// 入力規則の下で、クリックで乗ったフォーカスと Enter の長押しを確かめる（L1）
    /// Verifies click-acquired focus and the Enter hold under the input rules (L1)
    /// </summary>
    public class CraftHoldKeyTest : ScreenTestHost
    {
        private RecordingRecipeCommands _commands;
        private RecipeItemSelection _selection;
        private VisualElement _rulesRoot;

        [SetUp]
        public void SetUpRules()
        {
            // 規則は基底のルートでなくテストごとの器に付け、基底の Clear で一緒に捨てる（前例 GameUiInputRulesTest）
            // Attach the rules to a per-test container, not the base root, so the base Clear discards them (precedent GameUiInputRulesTest)
            _commands = new RecordingRecipeCommands();
            _selection = new RecipeItemSelection();
            _rulesRoot = new VisualElement { name = "rules-root", pickingMode = PickingMode.Ignore };
            _rulesRoot.style.position = Position.Absolute;
            _rulesRoot.style.left = 0;
            _rulesRoot.style.top = 0;
            _rulesRoot.style.right = 0;
            _rulesRoot.style.bottom = 0;
            rootVisualElement.Add(_rulesRoot);
            GameUiInputRules.Attach(_rulesRoot);
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
        }

        /// <web-test file="src/features/recipe/views/CraftRecipeEntry.test.ts">SpaceではクラフトボタンのonKeyDownが何もせずEnterでは長押しを始める</web-test>
        [Test]
        public void クリックで乗ったフォーカスが規則の下でも残りSpaceでは何もせずEnterを保持する間クラフトが続く()
        {
            var guid = RecipeFixtures.RecipeGuid(1);
            var view = MountInRules(new CraftRecipeEntryView(CraftVm(guid, 0.4, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
            var button = view.Q<RecipeActionButton>();

            // クリックでフォーカスが乗り（Web のボタンもクリックで乗る）、規則の次のスケジュールでも外れない
            // A click focuses the button (as in Web) and the rules' next scheduled pass does not blur it
            simulate.Click(button.worldBound.center);
            for (var i = 0; i < 3; i++) simulate.FrameUpdate();
            Assert.AreSame(button, panel.focusController.focusedElement);

            SendKeyDown(button, KeyCode.Space);
            for (var i = 0; i < 6; i++) simulate.FrameUpdateMs(100);
            SendKeyUp(button, KeyCode.Space);
            Assert.IsEmpty(_commands.Crafted);

            // 押しっぱなしのリピートで始め直さず、1.0 秒の保持で 0.4・0.8 の 2 回
            // Key repeat does not restart; holding 1.0 s crafts twice (0.4 and 0.8)
            SendKeyDown(button, KeyCode.Return);
            SendKeyDown(button, KeyCode.Return);
            for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);
            CollectionAssert.AreEqual(new[] { guid, guid }, _commands.Crafted);
            Assert.AreSame(button, panel.focusController.focusedElement);
            SendKeyUp(button, KeyCode.Return);
            for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);
            Assert.AreEqual(2, _commands.Crafted.Count);
        }

        [Test]
        public void Tabのナビゲーションは打ち消されフォーカスは動かず長押しも切れない()
        {
            var guid = RecipeFixtures.RecipeGuid(1);
            var view = MountInRules(new CraftRecipeEntryView(CraftVm(guid, 0.4, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
            var button = view.Q<RecipeActionButton>();
            simulate.Click(button.worldBound.center);
            SendKeyDown(button, KeyCode.Return);
            using (var tab = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next)) _rulesRoot.SendEvent(tab);
            for (var i = 0; i < 6; i++) simulate.FrameUpdateMs(100);

            // ゲームの Tab（インベントリの開閉）は InputManager が受ける。UITK のフォーカス移動はしない
            // The game's Tab (inventory toggle) goes to InputManager; UITK never moves focus
            Assert.AreSame(button, panel.focusController.focusedElement);
            Assert.AreEqual(1, _commands.Crafted.Count);
        }

        [Test]
        public void Enterで長押し中にフォーカスが外れると止まる()
        {
            var view = MountInRules(new CraftRecipeEntryView(CraftVm(RecipeFixtures.RecipeGuid(1), 1, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
            var button = view.Q<RecipeActionButton>();
            var emptySpot = new Vector2(1200, 700);
            simulate.Click(button.worldBound.center);

            // ポインタを先に外へ出し、止まる理由をフォーカス外れだけにする（ボタンから外れても止まるため）
            // Move the pointer out first so only the blur can stop the hold (leaving the button stops it too)
            simulate.MouseMove(emptySpot);
            SendKeyDown(button, KeyCode.Return);
            for (var i = 0; i < 5; i++) simulate.FrameUpdateMs(100);
            Assert.AreSame(button, panel.focusController.focusedElement);

            // 空き領域のクリックでフォーカスが外れ、Enter の保持は解ける
            // Clicking an empty spot drops the focus and releases the Enter hold
            simulate.Click(emptySpot);
            Assert.IsNull(panel.focusController.focusedElement);
            for (var i = 0; i < 20; i++) simulate.FrameUpdateMs(100);
            Assert.IsEmpty(_commands.Crafted);
        }

        private T MountInRules<T>(T view) where T : VisualElement
        {
            _rulesRoot.Add(view);
            simulate.FrameUpdate();
            return view;
        }

        private static void SendKeyDown(VisualElement target, KeyCode keyCode)
        {
            using var evt = KeyDownEvent.GetPooled('\0', keyCode, EventModifiers.None);
            target.SendEvent(evt);
        }

        private static void SendKeyUp(VisualElement target, KeyCode keyCode)
        {
            using var evt = KeyUpEvent.GetPooled('\0', keyCode, EventModifiers.None);
            target.SendEvent(evt);
        }
    }
}
