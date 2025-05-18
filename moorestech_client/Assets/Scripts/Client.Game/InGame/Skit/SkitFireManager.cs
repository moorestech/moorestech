using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.Skit;
using Client.Network.API;
using Core.Master;
using Cysharp.Threading.Tasks;
using MessagePack;
using Mooresmaster.Model.ChallengeActionModule;
using Mooresmaster.Model.ChallengesModule;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Skit
{
    public class SkitFireManager : IPostInitializable
    {
        public List<string> PlayedSkitIds { get; private set; } = new();
        private readonly SkitManager _skitManager;
        private InitialHandshakeResponse _initialHandshakeResponse;
        
        public SkitFireManager(SkitManager skitManager, InitialHandshakeResponse initialHandshakeResponse)
        {
            _skitManager = skitManager;
            _initialHandshakeResponse = initialHandshakeResponse;
            ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
        }
        
        public void PostInitialize()
        {
            foreach (var challenge in _initialHandshakeResponse.Challenge.CurrentChallenges)
            {
                PlaySkit(challenge);
            }
        }
        
        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(packet);
            
            PlayedSkitIds = message.PlayedSkitIds;
            
            var nextChallenges = message.NextChallengeGuids.Select(c => MasterHolder.ChallengeMaster.GetChallenge(c));
            foreach (var challenge in nextChallenges)
            {
                PlaySkit(challenge);
            }
        }
        
        private void PlaySkit(ChallengeMasterElement challenge)
        {
            foreach (var action in challenge.StartedActions.items)
            {
                if (action.ChallengeActionType != ChallengeActionElement.ChallengeActionTypeConst.playSkit) continue;
                
                var param = (PlaySkitChallengeActionParam)action.ChallengeActionParam;
                if (_skitManager.IsPlayingSkit)
                {
                    Debug.LogError($"複数同時にスキットを再生することは出ません。ID:{challenge.ChallengeGuid} タイトル:{challenge.Title}");
                }
                else if (PlayedSkitIds.Contains(param.SkitAddressablePath))
                {
                    Debug.LogError($"スキットはすでに再生されています。ID:{challenge.ChallengeGuid} タイトル:{challenge.Title}");
                }
                else
                {
                    _skitManager.StartSkit(param.SkitAddressablePath).Forget();
                }
            }
        }
    }
}