using System;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.Recipe.Input
{
    /// <summary>
    /// Esc（CloseUI）で画面が変わったら選択を外す（Web RecipeSelectionKeyHandler のゲーム層の Escape の写し）
    /// Clears the selection when Esc (CloseUI) changes the screen (port of the Web RecipeSelectionKeyHandler game-layer Escape)
    /// </summary>
    public sealed class RecipeSelectionCloseReset : IInitializable, IDisposable
    {
        private readonly UiRoutingState _routing;
        private readonly IUiCloseKeyProbe _closeKey;
        private readonly RecipeItemSelection _selection;
        private IDisposable _subscription;

        public RecipeSelectionCloseReset(UiRoutingState routing, IUiCloseKeyProbe closeKey, RecipeItemSelection selection)
        {
            _routing = routing;
            _closeKey = closeKey;
            _selection = selection;
        }

        public void Initialize()
        {
            // UITK のキーイベントは届かないので、遷移と同じフレームの CloseUI で読む（A-83 と同じく InputManager を正にする）
            // UITK key events never arrive, so read CloseUI in the same frame as the transition (InputManager is authoritative, as in A-83)
            _subscription = _routing.Current.Skip(1).Subscribe(_ => OnScreenChanged());

            #region Internal

            void OnScreenChanged()
            {
                if (!_closeKey.WasPressedThisFrame()) return; // silent-ok: Esc 以外の遷移（Tab 等）は選択を残す仕様 / RecipeSelectionCloseResetTest の CloseUIなし の件が見る
                _selection.Clear();
            }

            #endregion
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
