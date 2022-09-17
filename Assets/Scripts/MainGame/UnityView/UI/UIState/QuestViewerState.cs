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
            return InputManager.Settings.UI.CloseUI.triggered || InputManager.Settings.UI.OpenInventory.triggered || InputManager.Settings.UI.QuestUI.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.Settings.UI.CloseUI.triggered || InputManager.Settings.UI.QuestUI.triggered)
            {
                return UIStateEnum.GameScreen;
            }
            if (InputManager.Settings.UI.OpenInventory.triggered)
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