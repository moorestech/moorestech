using MainGame.Control.UI.UIState;

namespace MainGame.UnityView.UI.UIState
{
    public interface IUIState
    {
        public bool IsNext();
        public UIStateEnum GetNext();
        public void OnEnter(UIStateEnum lastStateEnum);
        public void OnExit();
    }
}