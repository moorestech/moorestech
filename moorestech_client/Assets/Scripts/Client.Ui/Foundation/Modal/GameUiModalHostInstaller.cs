using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using VContainer.Unity;

namespace Client.Ui.Foundation.Modal
{
    /// <summary>
    /// Modal 層へ宿主を置く入口
    /// Entry point that mounts the host in the Modal layer
    /// </summary>
    public sealed class GameUiModalHostInstaller : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly UiModalHost _host;

        public GameUiModalHostInstaller(GameUiRoot gameUiRoot, UiModalHost host)
        {
            _gameUiRoot = gameUiRoot;
            _host = host;
        }

        public void Initialize()
        {
            _host.Mount(_gameUiRoot.GetLayer(UiLayer.Modal, UiFamily.Viewport));
        }
    }
}
