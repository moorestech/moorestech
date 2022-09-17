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

        public bool IsNext()
        {
            return InputManager.UI.CloseUI.GetKey || InputManager.UI.OpenInventory.GetKey || InputManager.UI.QuestUI.GetKey;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKey || InputManager.UI.QuestUI.GetKey)
            {
                return UIStateEnum.GameScreen;
            }
            if (InputManager.UI.OpenInventory.GetKey)
            {
                return UIStateEnum.PlayerInventory;
            }

            return UIStateEnum.QuestViewer;
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