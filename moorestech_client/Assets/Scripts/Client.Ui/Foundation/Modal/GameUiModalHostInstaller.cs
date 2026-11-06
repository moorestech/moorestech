using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using VContainer.Unity;

namespace Client.Ui.Foundation.Modal
{
    /// <summary>
    /// GameUi の Modal 層（Viewport の族。Web の Portal は --ui-scale の外）に宿主を置く（DI の入口）
    /// Places the host in the GameUi Modal layer (Viewport family; the Web portal sits outside --ui-scale) (DI entry)
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
