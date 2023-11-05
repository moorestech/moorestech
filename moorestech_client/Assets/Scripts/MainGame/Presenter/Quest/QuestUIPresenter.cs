using Cysharp.Threading.Tasks;
using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.UnityView.UI.Quest;
using MainGame.UnityView.UI.UIState;
using VContainer.Unity;

namespace MainGame.Presenter.Quest
{
    /// <summary>
    ///     ネットワークから来たクエストのデータを受け取り、UIに反映するクラス
    /// </summary>
    public class QuestUIPresenter : IInitializable
    {
        private readonly RequestQuestProgressProtocol _requestQuestProgressProtocol;

        public QuestUIPresenter(QuestUI questUI, RequestQuestProgressProtocol requestQuestProgressProtocol, ReceiveQuestDataEvent receiveQuestDataEvent, UIStateControl uiStateControl, SendEarnQuestRewardProtocol sendEarnQuestReward)
        {
            _requestQuestProgressProtocol = requestQuestProgressProtocol;
            // UIステートがクエストになったら必要なデータのリクエストをする
            uiStateControl.OnStateChanged += uiState =>
            {
                if (uiState == UIStateEnum.QuestViewer) requestQuestProgressProtocol.Send();
            };

            // クエストのリワード取得が押されたらそのパケットを送る
            questUI.OnGetReward += questId =>
            {
                sendEarnQuestReward.Send(questId);
                SendRequestQuestProgress().Forget();
            };


            // クエストのデータが送られてきたらUIに返す
            receiveQuestDataEvent.OnReceiveQuestProgress += progress => { questUI.SetQuestProgress(progress.QuestProgress); };
        }

        public void Initialize()
        {
        }

        /// <summary>
        ///     報酬受け取りした後すぐに要求することはできないので0.1秒待機する
        /// </summary>
        private async UniTask SendRequestQuestProgress()
        {
            await UniTask.Delay(100);
            //更新するためにリクエストを送る
            _requestQuestProgressProtocol.Send();
        }
    }
}