using System.Text.RegularExpressions;
using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Screens.Research.Parts;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Game.Research;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究ボタンの結果は詳細ペインの View がトースト係へ渡す（L1。A-262）
    /// The detail pane view hands the research button's result to the toaster (L1; A-262)
    /// </summary>
    public class ResearchDetailActionsToastTest : ScreenTestHost
    {
        private Texture2D _texture;
        private GameContentPresentation _content;
        private RecordingResearchTreeCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = Kit.LoadMasterAndContent(_texture);
            _commands = new RecordingResearchTreeCommands();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void 研究が拒否されると研究失敗のトーストを1回出す()
        {
            _commands.SetCompleteResult(UiCommandRejected.Of(UiCommandRejectReason.ResearchFailed));
            MountPane();

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[UiCommandResultToaster\] research\.complete failed: research_failed$"));
            ClickResearchButton();

            CollectionAssert.AreEqual(new[] { $"complete:{Kit.Researchable}" }, _commands.Issued);
            Assert.AreEqual(1, ShownToasts.Count);
            Assert.AreEqual(GameUiToastVariant.Error, ShownToasts[0].Variant);
        }

        [Test]
        public void 研究が受理されるとトーストを出さない()
        {
            MountPane();

            ClickResearchButton();

            CollectionAssert.AreEqual(new[] { $"complete:{Kit.Researchable}" }, _commands.Issued);
            Assert.IsEmpty(ShownToasts);
        }

        #region Helpers

        private void MountPane()
        {
            // キーの無いラベルを載せないよう、描いてから載せる
            // Render before mounting so no keyless label is attached
            var pane = new ResearchDetailPane(_commands, _content, Toaster);
            pane.Render(Kit.Detail(ResearchNodeState.Researchable, 3, 0, Kit.NoUnlocks()));
            Mount(pane);
            simulate.FrameUpdate();
        }

        private void ClickResearchButton()
        {
            var button = Find<RecipeActionButton>(rootVisualElement, $"research-button-{Kit.Researchable}");
            var center = button.worldBound.center;
            simulate.MouseDown(center, MouseButton.LeftMouse);
            simulate.MouseUp(center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }

        #endregion
    }
}
