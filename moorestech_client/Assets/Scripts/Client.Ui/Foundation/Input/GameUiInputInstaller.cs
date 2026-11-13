using System;
using Client.Input;
using Client.Ui.Foundation.Root;
using VContainer.Unity;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// GameUi のルートへ入力規則を付け、Pick の probe を入力排他へ登録する
    /// Attaches the input rules to the GameUi root and registers the Pick probe with the input exclusivity
    /// </summary>
    public class GameUiInputInstaller : IInitializable, IDisposable
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
            var root = _gameUiRoot.GetRootElement();

            // パネル未接続では Pick も入力も届かないので起動の段階で止める
            // Without an attached panel neither Pick nor input works, so stop at startup
            if (root.panel == null) throw new InvalidOperationException("[GameUiInput] GameUi root is not attached to a panel");
            GameUiInputRules.Attach(root);
            UiInputExclusivity.SetGameUiPointerProbe(_pointerProbe);
        }

        public void Dispose()
        {
            UiInputExclusivity.SetGameUiPointerProbe(null);
        }
    }
}
