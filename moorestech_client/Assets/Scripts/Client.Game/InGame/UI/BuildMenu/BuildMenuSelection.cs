using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    ///     Webのビルドメニューで選ばれた設置ターゲットを、BuildMenuState が1回だけ消費するキュー
    ///     Holds the placement target chosen on the web build menu until BuildMenuState consumes it once
    /// </summary>
    public class BuildMenuSelection
    {
        private IPlacementTarget _selectedTarget;

        public void SetSelectedTarget(IPlacementTarget target)
        {
            _selectedTarget = target;
        }

        // 消費は一方通行。同じ選択が二度設置モードへ入らない
        // Consumption is one-way so the same selection never enters placement twice
        public bool TryConsumeSelectedTarget(out IPlacementTarget target)
        {
            target = _selectedTarget;
            _selectedTarget = null;
            return target != null;
        }

        // メニューを開き直したときに前回の未消費選択を捨てる
        // Discard a stale unconsumed selection when the menu is reopened
        public void Clear()
        {
            _selectedTarget = null;
        }
    }
}
