using System;
using System.Collections.Generic;
using Client.Ui.State.Challenge;
using Client.Network.API;
using Client.Tests.Inventory;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Client.Tests.WebUi.Challenge
{
    public class ChallengeProgressStateTest
    {
        [Test]
        public void 完了イベントでカテゴリを差し替えてから完了GUIDを流す()
        {
            var vanillaApiEvent = new CapturingVanillaApiEvent();
            var initialCategories = new List<ChallengeCategoryResponse>();
            var state = new ChallengeProgressState(vanillaApiEvent, CreateHandshake(initialCategories));
            state.Initialize();

            // 通知時点で差替済みを購読内で見る
            // Observe replaced categories inside the subscription
            var completedGuid = Guid.NewGuid();
            Guid? received = null;
            var replacedBeforeNotify = false;
            using var subscription = state.OnChallengeCompleted.Subscribe(guid =>
            {
                received = guid;
                replacedBeforeNotify = !ReferenceEquals(state.Categories, initialCategories);
            });

#pragma warning disable CS0618
            var message = new CompletedChallengeEventMessagePack
            {
                CompletedChallengeGuidStr = completedGuid.ToString(),
                NextChallengeGuidsStr = new List<string>(),
                PlayedSkitIds = new List<string>(),
                ChallengeCategories = new List<ChallengeCategoryMessagePack>(),
            };
#pragma warning restore CS0618
            vanillaApiEvent.Dispatch(CompletedChallengeEventPacket.EventTag, MessagePackSerializer.Serialize(message));

            Assert.AreEqual(completedGuid, received);
            Assert.IsTrue(replacedBeforeNotify, "完了GUIDを流す前にカテゴリが差し替わっていない");
            Assert.AreEqual(0, state.Categories.Count);
        }

        // チャレンジ以外は空で組む(前例UIStateTestFixtureBase)
        // Non-challenge handshake parts stay empty (precedent: UIStateTestFixtureBase)
        private static InitialHandshakeResponse CreateHandshake(List<ChallengeCategoryResponse> challenges)
        {
#pragma warning disable CS0618
            var initialHandshake = new global::Server.Protocol.PacketResponse.InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack
            {
                Accepted = new global::Server.Protocol.PacketResponse.Handshake.HandshakeAcceptedMessagePack
                {
                    PlayerPos = new Vector3MessagePack(Vector3.zero),
                },
            };
#pragma warning restore CS0618
            return new InitialHandshakeResponse(initialHandshake, (null, null, null, challenges, null, null, null, null));
        }
    }
}
