using System.Threading;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Panel.Lifetime
{
    /// <summary>
    /// 機械の画面が出ている間だけ生きる取り消しトークンを配る
    /// Hands out a cancellation token that lives only while the machine screen is shown
    /// </summary>
    /// <remarks>
    /// この画面から開くモーダルなどに渡すと、画面が閉じたとき一緒に閉じる
    /// Passed to things opened from this screen (such as modals), it closes them together with the screen
    /// </remarks>
    public sealed class BlockInventoryScreenLifetime : IInitializable
    {
        private readonly UiRoutingState _routing;
        private readonly IBlockInventoryPanelSource _panel;
        private CancellationTokenSource _shown;

        // 画面が出ていない間は取り消し済みのトークンを返す
        // Returns an already-cancelled token while the screen is not shown
        public CancellationToken ShownToken => _shown.Token;

        public BlockInventoryScreenLifetime(UiRoutingState routing, IBlockInventoryPanelSource panel)
        {
            _routing = routing;
            _panel = panel;

            // 起動時は画面が出ていないので、終わった寿命から始める
            // The screen is hidden at startup, so begin with an ended lifetime
            _shown = new CancellationTokenSource();
            _shown.Cancel();
        }

        public void Initialize()
        {
            // 出し分けは機械の画面本体と同じ判定を通す（閉じ条件が変わっても一緒に変わる）
            // Use the same visibility rule as the machine screen itself, so a changed close rule changes both
            _routing.Current.CombineLatest(_panel.Current, BlockInventoryScreenVisibility.IsShown).DistinctUntilChanged().Subscribe(SetShown);
        }

        private void SetShown(bool shown)
        {
            if (shown)
            {
                // 出るたびに新しい寿命を始める（前例 TrainTimetableFetcher.RestartCancellation）
                // Start a fresh lifetime on each show (precedent TrainTimetableFetcher.RestartCancellation)
                _shown.Dispose();
                _shown = new CancellationTokenSource();
                return;
            }

            // 隠れたら寿命を終える。取り消し済みへの再取り消しは何もしない
            // End the lifetime on hide; cancelling an already-cancelled source does nothing
            _shown.Cancel();
        }
    }
}
