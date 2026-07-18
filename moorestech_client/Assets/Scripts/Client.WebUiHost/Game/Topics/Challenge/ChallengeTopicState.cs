using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Network.API;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Core.Master;
using MessagePack;
using Server.Event.EventReceive;

namespace Client.WebUiHost.Game.Topics
{
    public class ChallengeTopicState
    {
        private readonly WebSocketHub _hub;
        private List<ChallengeCategoryResponse> _categories;

        public ChallengeTopicState(WebSocketHub hub, InitialHandshakeResponse initial)
        {
            _hub = hub;
            _categories = initial.Challenges;

            // 既存の完了イベントを購読し、両Topicを同じ全量状態から更新する
            // Subscribe to the existing completion event and update both topics from one full state
            ClientContext.VanillaApi.Event.SubscribeEventResponse(
                CompletedChallengeEventPacket.EventTag,
                OnCompletedChallenge);
        }

        public string BuildTreeJson()
        {
            return WebUiJson.Serialize(ChallengeDtoBuilder.BuildTree(_categories));
        }

        public string BuildCurrentJson(string completedGuid)
        {
            return WebUiJson.Serialize(ChallengeDtoBuilder.BuildCurrent(_categories, completedGuid));
        }

        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(packet);
            var categories = new List<ChallengeCategoryResponse>();
            foreach (var category in message.ChallengeCategories)
            {
                var master = MasterHolder.ChallengeMaster.GetChallengeCategory(category.ChallengeCategoryGuid);
                var current = category.CurrentChallengeGuids.ConvertAll(MasterHolder.ChallengeMaster.GetChallenge);
                var completed = category.CompletedChallengeGuids.ConvertAll(MasterHolder.ChallengeMaster.GetChallenge);
                categories.Add(new ChallengeCategoryResponse(master, category.IsUnlocked, current, completed));
            }
            _categories = categories;

            // 完了GUIDはeventだけへ載せ、snapshotではnullに戻す
            // Include the completed GUID only in the event; snapshots return null
            _hub.Publish(ChallengeTreeTopic.TopicName, BuildTreeJson());
            _hub.Publish(ChallengeCurrentTopic.TopicName, BuildCurrentJson(message.CompletedChallengeGuid.ToString()));
        }
    }
}
