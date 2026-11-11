using System;
using Client.Game.Common;
using Client.Game.InGame.UI.UIState;
using UniRx;

namespace Client.Ui.State.WholeUiVisibility
{
    /// <summary>
    /// ゲームの Ctrl+U（UIRoot）とカットシーン（GameStateController）を、全体非表示の源として読む（Web の ui.visibility と game_state）
    /// Reads the game's Ctrl+U (UIRoot) and cutscene (GameStateController) as the whole-UI hiding sources (Web ui.visibility and game_state)
    /// </summary>
    public sealed class GameWholeUiVisibilitySource : IWholeUiVisibilitySource
    {
        private readonly UIRoot _uiRoot;

        public GameWholeUiVisibilitySource(UIRoot uiRoot)
        {
            _uiRoot = uiRoot;
        }

        public IObservable<bool> ObserveUiRootVisible()
        {
            return _uiRoot.OnVisibilityChanged;
        }

        // 状態の通知は変化だけなので、購読の時点の状態を先に流す
        // The state stream carries changes only, so emit the state at subscription first
        public IObservable<bool> ObserveCutScenePlaying()
        {
            return Observable.Defer(() => GameStateController.OnStateChanged.Select(IsCutScene).StartWith(IsCutScene(GameStateController.CurrentState)));

            #region Internal

            static bool IsCutScene(GameStateType state)
            {
                return state == GameStateType.CutScene;
            }

            #endregion
        }
    }
}
