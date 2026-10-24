using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Tooltip
{
    /// <summary>
    ///     設置フィードバックと部屋のツールチップが共有する書き手の、同一内容スキップ・上書き後の出し直し・自分の分だけの Hide を固定する
    ///     Pins the shared writer's unchanged-content skip, re-show after an overwrite and own-only Hide used by placement feedback and the room tooltip
    /// </summary>
    public class OwnedTooltipLinesWriterTest
    {
        private MouseCursorTooltipState _tooltip;

        [SetUp]
        public void SetUp()
        {
            // 文言解決が実辞書を引くため初期化しておく（前例 PlacementFeedbackTooltipPresenterTest）
            // Initialize the real dictionary because text resolution reads from it, as PlacementFeedbackTooltipPresenterTest does
            Localize.Initialize();
            _tooltip = new MouseCursorTooltipState();
        }

        [Test]
        public void SameContentSkipsShowAndOverwriteShowsAgainTest()
        {
            var writer = new OwnedTooltipLinesWriter(_tooltip);
            writer.Show(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "A" }) });
            var shownLines = _tooltip.GetPresentation().Lines;

            // 別インスタンスでもキーと TextParams が同じなら出し直さない
            // A new instance with the same keys and TextParams is not shown again
            writer.Show(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "A" }) });
            Assert.AreSame(shownLines, _tooltip.GetPresentation().Lines);
            Assert.IsTrue(writer.TryShowWithoutReplacingOther(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "A" }) }));
            Assert.AreSame(shownLines, _tooltip.GetPresentation().Lines);

            // 他の書き手に上書きされたら、同じ内容でも出し直す
            // After another writer overwrites, the same content is shown again
            _tooltip.Show(new TooltipOwner(), LocalizationKeys.Ui.Tooltip.HoldToGet);
            Assert.IsFalse(writer.TryShowWithoutReplacingOther(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "A" }) }));
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.HoldToGet.Key, _tooltip.GetPresentation().Lines[0].Key.Key);
            writer.Show(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "A" }) });
            Assert.IsTrue(writer.TryShowWithoutReplacingOther(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "A" }) }));
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CleanRoomClass.Key, _tooltip.GetPresentation().Lines[0].Key.Key);
        }

        [Test]
        public void HideClearsOnlyOwnShowTest()
        {
            var writer = new OwnedTooltipLinesWriter(_tooltip);
            writer.Show(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomDetecting) });

            // 他の書き手が出している間の Hide は、その表示を消さない
            // A Hide while another writer is showing never clears that tooltip
            _tooltip.Show(new TooltipOwner(), LocalizationKeys.Ui.Tooltip.HoldToGet);
            writer.Hide();
            Assert.IsTrue(_tooltip.GetPresentation().Visible);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.HoldToGet.Key, _tooltip.GetPresentation().Lines[0].Key.Key);
        }
    }
}
