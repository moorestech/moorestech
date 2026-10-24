using System.Linq;
using Client.Game.InGame.CleanRoom;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.CleanRoom
{
    public class CleanRoomTooltipPresenterTest
    {
        [Test]
        public void ListsUnderOtherOwnersWithoutTakingOwnershipTest()
        {
            var tooltip = new MouseCursorTooltipState();
            var presenter = new CleanRoomTooltipPresenter(tooltip);
            var lines = new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomDetecting) };

            // 誰も出していなければ要約だけが出る
            // Only the summary shows when nobody else is showing
            presenter.Present(lines);
            CollectionAssert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.CleanRoomDetecting.Key }, Keys(tooltip));
            Assert.IsNull(tooltip.CurrentOwner);

            // 他の書き手が出すと、その行の下に要約が並び、主表示の所有者は他の書き手のまま
            // When another writer shows, the summary follows its lines and the main owner stays that writer
            var other = new TooltipOwner();
            tooltip.Show(other, LocalizationKeys.Ui.Tooltip.PlaceTooFar);
            CollectionAssert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.PlaceTooFar.Key, LocalizationKeys.Ui.Tooltip.CleanRoomDetecting.Key }, Keys(tooltip));
            Assert.AreSame(other, tooltip.CurrentOwner);

            // 自分の Hide は他者の行を残し、他者の Hide は要約を残す
            // Our Hide keeps the other's lines, and the other's Hide keeps the summary
            presenter.Hide();
            CollectionAssert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.PlaceTooFar.Key }, Keys(tooltip));
            presenter.Present(lines);
            tooltip.Hide(other);
            CollectionAssert.AreEqual(new[] { LocalizationKeys.Ui.Tooltip.CleanRoomDetecting.Key }, Keys(tooltip));

            // 両方が消えれば非表示になる
            // Hidden once both are gone
            presenter.Hide();
            Assert.IsFalse(tooltip.GetPresentation().Visible);
        }

        [Test]
        public void SameContentInNewInstancesNotifiesOnlyOnceTest()
        {
            var tooltip = new MouseCursorTooltipState();
            var presenter = new CleanRoomTooltipPresenter(tooltip);
            var notifyCount = 0;
            tooltip.OnPresentationChanged.Skip(1).Subscribe(_ => notifyCount++);

            // 照会ごとに別インスタンスの同じ内容（キーと TextParams が同じ）を渡しても、変化通知は最初の1回だけ
            // Handing the same content in new instances on every poll (same keys and TextParams) notifies only the first time
            for (var poll = 0; poll < 5; poll++) presenter.Present(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "A" }) });
            Assert.AreEqual(1, notifyCount);

            // TextParams が変われば通知する
            // A changed TextParams notifies again
            presenter.Present(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "B" }) });
            Assert.AreEqual(2, notifyCount);
        }

        private static string[] Keys(MouseCursorTooltipState tooltip)
        {
            return tooltip.GetPresentation().Lines.Select(line => line.Key.Key).ToArray();
        }
    }
}
