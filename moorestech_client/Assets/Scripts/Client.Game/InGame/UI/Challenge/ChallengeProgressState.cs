using System;
using System.Collections.Generic;
using Client.Network.API;
using Core.Master;
using MessagePack;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Challenge
{
    /// <summary>
    /// 全カテゴリを持ち完了を通知
    /// Holds every category and notifies completions
    /// </summary>
    public sealed class ChallengeProgressState : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly Subject<Guid> _onChallengeCompleted = new();

        public IReadOnlyList<ChallengeCategoryResponse> Categories { get; private set; }
        public IObservable<Guid> OnChallengeCompleted => _onChallengeCompleted;

        public ChallengeProgressState(IVanillaApiEvent vanillaApiEvent, InitialHandshakeResponse initial)
        {
            _vanillaApiEvent = vanillaApiEvent;
            Categories = initial.Challenges;
        }

        public void Initialize()
        {
            // 完了購読で全量置換
            // Replace the state on completion
            _vanillaApiEvent.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);

            #region Internal

            void OnCompletedChallenge(byte[] packet)
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
                Categories = categories;
                _onChallengeCompleted.OnNext(message.CompletedChallengeGuid);
            }

            #endregion
        }
    }
}
