using Client.Input;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls.RecipeAction
{
    /// <summary>
    /// RecipeActionButton の L1 の共通の器（入力の規則・辞書・置き方）
    /// Shared L1 fixture for RecipeActionButton (input rules, dictionary, placement)
    /// </summary>
    public abstract class RecipeActionButtonFixture : UiTestHost
    {
        protected const string TestId = "recipe-action-button-test";

        protected VisualElement RulesRoot { get; private set; }

        [SetUp]
        public void AttachRules()
        {
            // テストごとの器に入力の規則を付け、文言の辞書を先に読む（FormButtonTest と同じ形）
            // Attach input rules to a per-test container and load the dictionary first (same shape as FormButtonTest)
            RulesRoot = new VisualElement { name = "rules-root" };
            RulesRoot.style.position = Position.Absolute;
            RulesRoot.style.left = 0;
            RulesRoot.style.top = 0;
            RulesRoot.style.right = 0;
            RulesRoot.style.bottom = 0;
            RulesRoot.style.alignItems = Align.FlexStart;
            rootVisualElement.Add(RulesRoot);
            GameUiInputRules.Attach(RulesRoot);
            Localize.Initialize();
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        protected RecipeActionButton AddButton(VisualElement parent, RecipeActionButtonVariant variant)
        {
            var button = new RecipeActionButton(TestId, new UiText(LocalizationKeys.Ui.Recipe.CraftButtonLabel), variant);
            parent.Add(button);
            simulate.FrameUpdate();
            return button;
        }
    }
}
