using Client.Input;
using Client.Ui.Foundation.Root;
using VContainer.Unity;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// GameUiとパネルの根に入力規則を登録し、入力排他にPickプローブを登録
    /// Registers input rules on the GameUi and panel roots and the Pick probe with input exclusivity.
    /// </summary>
    public class GameUiInputInstaller : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly GameUiPointerProbe _pointerProbe;

        public GameUiInputInstaller(GameUiRoot gameUiRoot, GameUiPointerProbe pointerProbe)
        {
            _gameUiRoot = gameUiRoot;
            _pointerProbe = pointerProbe;
        }

        public void Initialize()
        {
            GameUiInputRules.AttachToGameUi(_gameUiRoot.GetRootElement());
            UiInputExclusivity.SetGameUiPointerProbe(_pointerProbe);
        }

    }
}
