using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class QuestViewerState : IUIState
    {
        private readonly MoorestechInputSettings _input;
        private readonly QuestViewerObject _questViewerObject;

        public QuestViewerState(MoorestechInputSettings input, QuestViewerObject questViewerObject)
        {
            _input = input;
            _questViewerObject = questViewerObject;
        }

        public bool IsNext()
        {
            return _input.UI.CloseUI.triggered || _input.UI.OpenInventory.triggered || _input.UI.QuestUI.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (_input.UI.CloseUI.triggered || _input.UI.QuestUI.triggered)
            {
                return UIStateEnum.GameScreen;
            }
            if (_input.UI.OpenInventory.triggered)
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