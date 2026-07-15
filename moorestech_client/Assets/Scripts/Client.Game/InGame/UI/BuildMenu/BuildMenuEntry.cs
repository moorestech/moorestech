using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Mod.Texture;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// ビルドメニュー1エントリ（設置ターゲット＋表示情報）
    /// One build-menu entry: a placement target plus its display info
    /// </summary>
    public readonly struct BuildMenuEntry
    {
        public readonly IPlacementTarget Target;

        // アイコン無し（BP等）はnullでテキスト表示になる
        // Null icon (e.g. blueprints) renders as a text-only slot
        public readonly ItemViewData IconView;
        public readonly string ToolTipText;

        public BuildMenuEntry(IPlacementTarget target, ItemViewData iconView, string toolTipText)
        {
            Target = target;
            IconView = iconView;
            ToolTipText = toolTipText;
        }
    }
}
