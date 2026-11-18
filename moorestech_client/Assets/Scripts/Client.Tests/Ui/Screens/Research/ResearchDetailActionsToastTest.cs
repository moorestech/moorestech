using Client.Ui.Components.Controls;
using Client.Ui.Content;
using Client.Ui.Screens.Research.Parts;
using Game.Research;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究ボタンは操作を呼ぶだけで、結果のトーストは Commands が出す
    /// The research button only calls the operation; the commands toast the result
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
        public void 研究ボタンは研究完了を1回呼びViewはトーストを出さない()
        {
            // 結果のトーストは ResearchTreeCommands が出すので、View は呼ぶだけ
            // ResearchTreeCommands toasts the result, so the view only calls it
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
            var pane = new ResearchDetailPane(_commands, _content);
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
