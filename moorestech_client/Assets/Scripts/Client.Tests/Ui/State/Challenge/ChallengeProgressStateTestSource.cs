using System;
using System.Collections.Generic;
using System.Linq;
using Client.Network.API;
using Client.Tests.Inventory;
using Client.Ui.State.Challenge;
using Core.Master;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// テスト mod の 2 カテゴリで State を組み完了を流す（前例 ChallengeProgressStateTest）
    /// Build State from two test mod categories and dispatch completion (precedent ChallengeProgressStateTest)
    /// </summary>
    internal sealed class ChallengeProgressStateTestSource
    {
        public static readonly Guid Category1 = Guid.Parse("03ca4ded-3b2b-4e7f-bb6e-430f060c4ed1");
        public static readonly Guid Category2 = Guid.Parse("35330f9d-f44f-493d-a6bc-07ae6413d7c4");
        public static readonly Guid Test1 = Guid.Parse("00000000-0000-0000-4567-000000000001");
        public static readonly Guid Test2 = Guid.Parse("00000000-0000-0000-4567-000000000002");
        public static readonly Guid Category2First = Guid.Parse("c2d84cfb-73cb-4ffa-99b5-9703c550f330");

        private readonly CapturingVanillaApiEvent _events = new();

        public ChallengeProgressState Progress { get; }

        public ChallengeProgressStateTestSource()
        {
            // 2 カテゴリとも解放済み
            // Both categories unlocked
            var initial = new List<ChallengeCategoryResponse>
            {
                Response(Category1, new[] { Test2 }, new[] { Test1 }),
                Response(Category2, new[] { Category2First }, Array.Empty<Guid>()),
            };
            Progress = new ChallengeProgressState(_events, Handshake(initial));
            Progress.Initialize();
        }

        // Test2 を完了にした全量を流す
        // Dispatch the full state with Test2 completed
        public void CompleteTest2()
        {
#pragma warning disable CS0618
            var message = new CompletedChallengeEventMessagePack
            {
                CompletedChallengeGuidStr = Test2.ToString(),
                NextChallengeGuidsStr = new List<string>(),
                PlayedSkitIds = new List<string>(),
                ChallengeCategories = new List<ChallengeCategoryMessagePack>
                {
                    Category(Category1, Array.Empty<Guid>(), new[] { Test1, Test2 }),
                    Category(Category2, new[] { Category2First }, Array.Empty<Guid>()),
                },
            };
#pragma warning restore CS0618
            _events.Dispatch(CompletedChallengeEventPacket.EventTag, MessagePackSerializer.Serialize(message));
        }

        private static ChallengeCategoryResponse Response(Guid category, Guid[] current, Guid[] completed)
        {
            var master = MasterHolder.ChallengeMaster;
            return new ChallengeCategoryResponse(master.GetChallengeCategory(category), true, current.Select(master.GetChallenge).ToList(), completed.Select(master.GetChallenge).ToList());
        }

        private static ChallengeCategoryMessagePack Category(Guid category, Guid[] current, Guid[] completed)
        {
            return new ChallengeCategoryMessagePack
            {
                ChallengeCategoryGuid = category,
                IsUnlocked = true,
                CurrentChallengeGuidsStr = current.Select(guid => guid.ToString()).ToList(),
                CompletedChallengeGuidsStr = completed.Select(guid => guid.ToString()).ToList(),
            };
        }

        // チャレンジ以外は空で組む（前例 ChallengeProgressStateTest.CreateHandshake）
        // Non-challenge parts stay empty (precedent ChallengeProgressStateTest.CreateHandshake)
        private static InitialHandshakeResponse Handshake(List<ChallengeCategoryResponse> challenges)
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
