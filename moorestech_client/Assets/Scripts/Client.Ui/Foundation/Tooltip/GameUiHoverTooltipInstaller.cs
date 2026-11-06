using System;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
using VContainer.Unity;

namespace Client.Ui.Foundation.Tooltip
{
    /// <summary>
    /// GameUi の Tooltip 層に宿主を 1 つ置き、パネルの木の根で移動を受ける（DI の入口）
    /// Places one host in the GameUi Tooltip layer and receives moves at the panel tree root (DI entry)
    /// </summary>
    public sealed class GameUiHoverTooltipInstaller : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly HoverTooltipHost _host;

        public GameUiHoverTooltipInstaller(GameUiRoot gameUiRoot, IHoverTooltipFace face, StageScaleSource stageScale)
        {
            _gameUiRoot = gameUiRoot;
            _host = new HoverTooltipHost(face, stageScale);
        }

        public void Initialize()
        {
            // パネル未接続では移動を受けられないので起動の段階で止める
            // Without an attached panel no move arrives, so stop at startup
            var root = _gameUiRoot.GetRootElement();
            if (root.panel == null) throw new InvalidOperationException("[HoverTooltipHost] GameUi root is not attached to a panel");
            _host.Attach(root.panel.visualTree, _gameUiRoot.GetLayer(UiLayer.Tooltip, UiFamily.Viewport));
        }
    }
}
