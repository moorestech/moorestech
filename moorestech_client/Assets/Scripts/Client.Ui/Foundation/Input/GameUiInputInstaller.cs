using System;
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
            var root = _gameUiRoot.GetRootElement();

            // パネル未接続では Pick も入力も届かないので起動の段階で止める
            // Without an attached panel neither Pick nor input works, so stop at startup
            if (root.panel == null) throw new InvalidOperationException("[GameUiInput] GameUi root is not attached to a panel");
            GameUiInputRules.Attach(root);

            // フォーカスが無い間の Tab 由来の選択移動はパネルの根宛てに届くので、パネルの根にも打ち消しを付ける
            // A Tab-driven navigation move with nothing focused is aimed at the panel root, so attach the guard there too
            GameUiInputRules.AttachPanelRootGuard(root.panel.visualTree);
            UiInputExclusivity.SetGameUiPointerProbe(_pointerProbe);
        }

    }
}
