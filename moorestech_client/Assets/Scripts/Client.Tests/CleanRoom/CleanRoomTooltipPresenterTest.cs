using System.Collections.Generic;
using Client.Game.InGame.CleanRoom;
using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.CleanRoom
{
    public class CleanRoomTooltipPresenterTest
    {
        [Test]
        public void ShowsWhenFreeAndYieldsToOtherOwnersTest()
        {
            var tooltip = new MouseCursorTooltipState();
            var presenter = new CleanRoomTooltipPresenter(tooltip);
            var lines = new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomDetecting) };

            // 誰も出していなければ自分の行を出す
            // Show our lines when nobody else is showing
            presenter.Present(lines);
            Assert.IsTrue(tooltip.GetPresentation().Visible);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.CleanRoomDetecting.Key, tooltip.GetPresentation().Lines[0].Key.Key);
            var ourOwner = tooltip.CurrentOwner;

            // 他の書き手が出している間は上書きしない
            // Do not overwrite while another writer is showing
            var other = new TooltipOwner();
            tooltip.Show(other, LocalizationKeys.Ui.Tooltip.PlaceTooFar);
            presenter.Present(lines);
            Assert.AreSame(other, tooltip.CurrentOwner);

            // 自分の Hide は他者の表示を消さない
            // Our Hide never clears someone else's tooltip
            presenter.Hide();
            Assert.IsTrue(tooltip.GetPresentation().Visible);

            // 他者が消えたら再び出す
            // Show again once the other writer has hidden
            tooltip.Hide(other);
            presenter.Present(lines);
            Assert.IsNotNull(tooltip.CurrentOwner);
            Assert.AreNotSame(other, tooltip.CurrentOwner);
            Assert.AreSame(ourOwner, tooltip.CurrentOwner);
        }

        [Test]
        public void SameContentInNewInstancesIsShownOnlyOnceTest()
        {
            var tooltip = new ShowCountingTooltip();
            var presenter = new CleanRoomTooltipPresenter(tooltip);

            // 毎フレーム別インスタンスの同じ内容（キーと TextParams が同じ）を渡しても、Show は最初の1回だけ
            // Handing the same content in new instances every frame (same keys and TextParams) shows only the first time
            for (var frame = 0; frame < 5; frame++) presenter.Present(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "A" }) });
            Assert.AreEqual(1, tooltip.ShowCount);

            // TextParams が変われば出し直す
            // A changed TextParams shows again
            presenter.Present(new[] { new TooltipLine(LocalizationKeys.Ui.Tooltip.CleanRoomClass, new[] { "B" }) });
            Assert.AreEqual(2, tooltip.ShowCount);
        }

        // 本物の状態に委ねつつ、行の Show 回数だけを数える
        // Delegates to the real state while counting only line Shows
        private sealed class ShowCountingTooltip : IMouseCursorTooltip
        {
            private readonly MouseCursorTooltipState _state = new();

            public System.IObservable<TooltipPresentation> OnPresentationChanged => _state.OnPresentationChanged;

            public int ShowCount { get; private set; }
            public TooltipOwner CurrentOwner => _state.CurrentOwner;

            public TooltipPresentation GetPresentation()
            {
                return _state.GetPresentation();
            }

            public void Hide(TooltipOwner owner)
            {
                _state.Hide(owner);
            }

            public void Show(TooltipOwner owner, LocalizationKey key)
            {
                _state.Show(owner, key);
            }

            public void Show(TooltipOwner owner, LocalizationKey key, IReadOnlyList<string> textParams)
            {
                _state.Show(owner, key, textParams);
            }

            public void Show(TooltipOwner owner, IReadOnlyList<TooltipLine> lines)
            {
                ShowCount++;
                _state.Show(owner, lines);
            }
        }
    }
}
