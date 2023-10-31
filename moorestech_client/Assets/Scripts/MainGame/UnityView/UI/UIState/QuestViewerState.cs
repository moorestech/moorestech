using MainGame.UnityView.Control;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class QuestViewerState : IUIState
    {
        private readonly QuestViewerObject _questViewerObject;

        public QuestViewerState(QuestViewerObject questViewerObject)
        {
            _questViewerObject = questViewerObject;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.QuestUI.GetKeyDown)
            {
                return UIStateEnum.GameScreen;
            }
            if (InputManager.UI.OpenInventory.GetKeyDown)
            {
                return UIStateEnum.PlayerInventory;
            }

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _questViewerObject.gameObject.SetActive(true);
        }

        public void OnExit()
        {
            _questViewerObject.gameObject.SetActive(false);
        }
    }
}