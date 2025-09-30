using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BackgroundSkit;
using Client.Game.InGame.Context;
using Client.Game.Skit;
using Client.Network.API;
using Common.Debug;
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
        private readonly BackgroundSkitManager _backgroundSkitManager;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;
        
        public SkitFireManager(SkitManager skitManager, InitialHandshakeResponse initialHandshakeResponse, BackgroundSkitManager backgroundSkitManager)
        {
            _skitManager = skitManager;
            _initialHandshakeResponse = initialHandshakeResponse;
            _backgroundSkitManager = backgroundSkitManager;
            PlayedSkitIds.AddRange(initialHandshakeResponse.PlayedSkitIds);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(SkitRegisterEventPacket.EventTag, OnSkitRegister);
        }
        
        public void PostInitialize()
        {
            var currentChallenges = _initialHandshakeResponse.Challenges.SelectMany(c => c.CurrentChallenges);
            foreach (var challenge in currentChallenges)
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
        
        private void OnSkitRegister(byte[] packet)
        {
            var message = MessagePackSerializer.Deserialize<SkitRegisterEventPacket.SkitRegisterEventMessagePack>(packet);
            PlayedSkitIds = message.PlayedSkitIds;
        }
        
        private void PlaySkit(ChallengeMasterElement challenge)
        {
            var isSkip = DebugParameters.GetValueOrDefaultBool(DebugConst.SkitPlaySettingsKey);
            if (isSkip)
            {
                return;
            }
            
            var skitActions = new List<PlaySkitChallengeActionParam>();
            foreach (var action in challenge.StartedActions.items)
            {
                if (action.ChallengeActionType != ChallengeActionElement.ChallengeActionTypeConst.playSkit) continue;
                
                var param = (PlaySkitChallengeActionParam)action.ChallengeActionParam;
                if (PlayedSkitIds.Contains(param.SkitAddressablePath)) continue;
                
                skitActions.Add(param);
            }
            
            SkitProcess(skitActions).Forget();
        }
        
        private async UniTask SkitProcess(List<PlaySkitChallengeActionParam> skitActions)
        {
            skitActions.Sort((a, b) => a.PlaySortPriority.CompareTo(b.PlaySortPriority));
            
            foreach (var action in skitActions)
            {
                // 現在スキットがプレイ中ならスキップする
                var isPlayedSkit = _skitManager.IsPlayingSkit || _backgroundSkitManager.IsPlayingSkit;
                if (isPlayedSkit) continue;
                
                var path = action.SkitAddressablePath;
                if (action.PlaySkitType == PlaySkitChallengeActionParam.PlaySkitTypeConst.normal)
                {
                    await _skitManager.StartSkit(path);
                }
                else if (action.PlaySkitType == PlaySkitChallengeActionParam.PlaySkitTypeConst.background)
                {
                    await _backgroundSkitManager.StartBackgroundSkit(path);
                }
                
                ClientContext.VanillaApi.SendOnly.RegisterPlayedSkit(path);
            }
        }
    }
}