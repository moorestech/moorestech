using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial;
using Client.Network.API;
using Core.Master;
using MessagePack;
using Mooresmaster.Model.ChallengesModule;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeManager : MonoBehaviour
    {
        [Inject] private TutorialManager _tutorialManager;
        private List<ChallengeMasterElement> _initialCurrentChallenges;

        [Inject]
        public void Construct(InitialHandshakeResponse initialHandshakeResponse)
        {
            var currentChallenges = initialHandshakeResponse.Challenges.SelectMany(c => c.CurrentChallenges).ToList();
            _initialCurrentChallenges = currentChallenges;

            ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
        }

        /// <summary>
        ///     初期チャレンジのチュートリアルを適用する
        ///     Apply the tutorials for the initial challenges
        /// </summary>
        public void ApplyInitialTutorials()
        {
            _initialCurrentChallenges.ForEach(c => _tutorialManager.ApplyTutorial(c.ChallengeGuid));
        }


        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(packet);
            var nextChallenges = message.NextChallengeGuids.Select(c => MasterHolder.ChallengeMaster.GetChallenge(c)).ToList();

            // チュートリアルを完了
            _tutorialManager.CompleteChallenge(message.CompletedChallengeGuid);

            // 次のチャレンジのチュートリアルを適用
            ApplyNextTutorials(nextChallenges);

            #region Internal

            void ApplyNextTutorials(List<ChallengeMasterElement> nextList)
            {
                // チュートリアルの適用
                // Apply tutorial
                nextList.ForEach(id => _tutorialManager.ApplyTutorial(id.ChallengeGuid));
            }

            #endregion
        }
    }
}
