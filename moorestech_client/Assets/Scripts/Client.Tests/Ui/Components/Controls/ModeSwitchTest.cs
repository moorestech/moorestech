using System;
using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls
{
    /// <summary>
    /// ModeSwitchを実パネルで確かめる（L1）
    /// Verifies ModeSwitch on a real panel (L1)
    /// </summary>
    public class ModeSwitchTest : UiTestHost
    {
        private const string SelectedClass = "mode-switch__option--selected";

        private readonly List<int> _selected = new();
        private IDisposable _subscription;

        [SetUp]
        public void LoadDictionaryAndClearSelected()
        {
            // LocalizedLabel が載った時点で辞書を引くので先に読み込む
            // LocalizedLabel looks up the dictionary on attach, so load it first
            Localize.Initialize();
            _selected.Clear();
        }

        [TearDown]
        public void DisposeSubscription()
        {
            _subscription?.Dispose();
        }

        [Test]
        public void ClickPublishesOptionValue()
        {
            var modeSwitch = AddSwitch(ModeSwitchOrientation.Horizontal);
            simulate.Click(Option(modeSwitch, "option-b").worldBound.center);
            CollectionAssert.AreEqual(new[] { 2 }, _selected);
        }

        [Test]
        public void SetSelectedMarksExactlyOneAndShowsItBrighter()
        {
            var modeSwitch = AddSwitch(ModeSwitchOrientation.Horizontal);
            modeSwitch.SetSelected(3);
            simulate.FrameUpdate();

            var selected = modeSwitch.Query<Button>(className: SelectedClass).ToList();
            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual("option-c", selected[0].name);

            // 選択肢の文字色が非選択と違う
            // Selected text color differs
            Assert.AreNotEqual(Option(modeSwitch, "option-a").resolvedStyle.color, selected[0].resolvedStyle.color);
        }

        [Test]
        public void UnknownValueAndSetSelectedNoneLeaveNothingSelected()
        {
            var modeSwitch = AddSwitch(ModeSwitchOrientation.Horizontal);
            modeSwitch.SetSelected(1);
            modeSwitch.SetSelected(99);
            Assert.AreEqual(0, modeSwitch.Query<Button>(className: SelectedClass).ToList().Count);

            modeSwitch.SetSelected(2);
            modeSwitch.SetSelectedNone();
            Assert.AreEqual(0, modeSwitch.Query<Button>(className: SelectedClass).ToList().Count);
        }

        [Test]
        public void DisabledSwitchDoesNotPublishAndStaysOpaque()
        {
            var modeSwitch = AddSwitch(ModeSwitchOrientation.Horizontal);
            modeSwitch.SetEnabledAll(false);
            simulate.FrameUpdate();
            simulate.Click(Option(modeSwitch, "option-a").worldBound.center);
            Assert.IsEmpty(_selected);
            Assert.IsTrue(modeSwitch.ClassListContains("mode-switch--disabled"));
            Assert.AreEqual(1f, Option(modeSwitch, "option-a").resolvedStyle.opacity);

            modeSwitch.SetEnabledAll(true);
            simulate.Click(Option(modeSwitch, "option-a").worldBound.center);
            CollectionAssert.AreEqual(new[] { 1 }, _selected);
        }

        [TestCase(ModeSwitchOrientation.Horizontal, "mode-switch--horizontal", FlexDirection.Row)]
        [TestCase(ModeSwitchOrientation.Vertical, "mode-switch--vertical", FlexDirection.Column)]
        public void OrientationSetsClassAndDirection(ModeSwitchOrientation orientation, string expectedClass, FlexDirection expectedDirection)
        {
            var modeSwitch = AddSwitch(orientation);
            Assert.IsTrue(modeSwitch.ClassListContains(expectedClass));
            Assert.AreEqual(expectedDirection, modeSwitch.resolvedStyle.flexDirection);
        }

        [Test]
        public void OptionsAreUnfocusableButtonsNamedByTestid()
        {
            var modeSwitch = AddSwitch(ModeSwitchOrientation.Horizontal);
            Assert.AreEqual("mode-switch-test", modeSwitch.name);
            var buttons = modeSwitch.Query<Button>().ToList();
            CollectionAssert.AreEqual(new[] { "option-a", "option-b", "option-c" }, buttons.Select(button => button.name));
            Assert.IsTrue(buttons.All(button => !button.focusable));
            Assert.AreEqual("300m", Option(modeSwitch, "option-c").text);
        }

        [Test]
        public void OptionNeedsExactlyOneLabelSource()
        {
            Assert.Throws<ArgumentException>(() => new ModeSwitchOption<int>("both", 1, LocalizationKeys.Ui.PauseMenu.Back, "300m"));
            Assert.Throws<ArgumentException>(() => new ModeSwitchOption<int>("neither", 1, null, null));
        }

        private ModeSwitch<int> AddSwitch(ModeSwitchOrientation orientation)
        {
            var options = new List<ModeSwitchOption<int>>
            {
                new("option-a", 1, LocalizationKeys.Ui.PauseMenu.Settings, null),
                new("option-b", 2, LocalizationKeys.Ui.PauseMenu.Back, null),
                new("option-c", 3, null, "300m"),
            };
            var modeSwitch = new ModeSwitch<int>("mode-switch-test", orientation, options);
            _subscription = modeSwitch.OnSelected.Subscribe(value => _selected.Add(value));
            rootVisualElement.Add(modeSwitch);
            simulate.FrameUpdate();
            return modeSwitch;
        }

        private static Button Option(VisualElement modeSwitch, string testid)
        {
            return modeSwitch.Q<Button>(testid);
        }
    }
}
