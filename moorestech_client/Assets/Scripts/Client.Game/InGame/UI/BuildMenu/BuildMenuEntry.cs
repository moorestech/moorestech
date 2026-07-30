// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
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
