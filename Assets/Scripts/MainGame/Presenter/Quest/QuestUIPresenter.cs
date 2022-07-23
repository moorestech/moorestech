using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.UnityView.UI.Quest;
using MainGame.UnityView.UI.UIState;
using Server.Protocol.PacketResponse;
using VContainer.Unity;

namespace MainGame.Presenter.Quest
{
    public class QuestUIPresenter : IInitializable
    {
        public QuestUIPresenter(QuestUI questUI,RequestQuestProgressProtocol requestQuestProgressProtocol,ReceiveQuestDataEvent receiveQuestDataEvent,UIStateControl uiStateControl,SendEarnQuestRewardProtocol sendEarnQuestReward)
        {
            // UIステートがクエストになったら必要なデータのリクエストをする
            uiStateControl.OnStateChanged += uiState =>
            {
                if (uiState == UIStateEnum.QuestViewer)
                {
                    requestQuestProgressProtocol.Send();
                }
            };
            
            // クエストのリワード取得が押されたらそのパケットを送る
            questUI.OnGetReward += questId =>
            {
                sendEarnQuestReward.Send(questId);
                //更新するためにリクエストを送る
                requestQuestProgressProtocol.Send();
            };
            
            
            // クエストのデータが送られてきたらUIに返す
            receiveQuestDataEvent.OnReceiveQuestProgress += progress =>
            {
                questUI.SetQuestProgress(progress.QuestProgress);
            };
        }

        public void Initialize() { }
    }
}