using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.UnityView.UI.Quest;
using MainGame.UnityView.UI.UIState;
using VContainer.Unity;

namespace MainGame.Presenter.Quest
{
    public class QuestUIPresenter : IInitializable
    {
        public QuestUIPresenter(QuestUI questUI,RequestQuestProgressProtocol requestQuestProgressProtocol,QuestEvent questEvent,UIStateControl uiStateControl)
        {
            // UIステートがクエストになったら必要なデータのリクエストをする
            uiStateControl.OnStateChanged += uiState =>
            {
                if (uiState == UIStateEnum.QuestViewer)
                {
                    requestQuestProgressProtocol.Send();
                }
            };
            
            // クエストのデータが送られてきたらUIに返す
            questEvent.OnReciveQuestProgress += progress =>
            {
                questUI.SetQuestProgress(progress.QuestProgress);
            };
        }

        public void Initialize() { }
    }
}