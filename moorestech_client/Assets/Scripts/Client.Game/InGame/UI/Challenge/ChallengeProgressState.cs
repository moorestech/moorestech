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
    /// チャレンジの全カテゴリ（handshakeの初期値と完了イベントの全量）を持ち、完了を通知する
    /// Holds every challenge category (the handshake's initial value and each completion event's full state) and notifies completions
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
            // 既存の完了イベントを購読し、全量状態で置き換える
            // Subscribe to the existing completion event and replace the state with its full snapshot
            _vanillaApiEvent.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnCompletedChallenge);
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
            Categories = categories;
            _onChallengeCompleted.OnNext(message.CompletedChallengeGuid);
        }
    }
}
