using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.Skit;
using Core.Master;
using Cysharp.Threading.Tasks;
using MessagePack;
using Mooresmaster.Model.ChallengeActionModule;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Skit
{
    public class SkitFireManager : IInitializable
    {
        private readonly SkitManager _skitManager;
        public List<string> PlayedSkitIds { get; private set; } = new();
        
        public SkitFireManager(SkitManager skitManager)
        {
            _skitManager = skitManager;
            ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
        }
        private void OnCompletedChallenge(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(packet);
            
            PlayedSkitIds = message.PlayedSkitIds;
            
            var nextChallenges = message.NextChallengeGuids.Select(c => MasterHolder.ChallengeMaster.GetChallenge(c));
            foreach (var challenge in nextChallenges)
            {
                foreach (var action in challenge.ClearedActions.items)
                {
                    if (action.ChallengeActionType != ChallengeActionElement.ChallengeActionTypeConst.unlockChallenge) continue;
                    
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
        
        public void Initialize() { }
    }
}