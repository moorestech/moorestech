using System;
using System.Linq;
using Game.Challenge;
using Game.Challenge.Task;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetChallengeInfoProtocolTest
    {
        private const int PlayerId = 1;
        private const string Challenge1Guid = "";
        private const string Challenge2Guid = "";
        private const string Challenge3Guid = "";
        private const string Challenge4Guid = "";
        private const string Challenge5Guid = "";
        
        [Test]
        public void GetCompletedChallengeTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // チャレンジを無理やりクリアする
            // Forced to complete a challenge
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            var playerChallengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            foreach (var challenge in playerChallengeInfo.CurrentChallenges.ToList())
            {
                var subject = (Subject<IChallengeTask>)challenge.OnChallengeComplete;
                subject.OnNext(challenge); // 無理やりクリア
            }
            
            // 現在のチャレンジ情報をリクエスト
            // Request current challenge information
            var messagePack = new RequestChallengeMessagePack(PlayerId);
            var response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            var challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoMessagePack>(response.ToArray());
            
            // 検証
            // Verification
            Assert.AreEqual(PlayerId, challengeInfo.PlayerId);
            
            Assert.AreEqual(3, challengeInfo.CompletedChallengeGuids.Count);
            Assert.IsTrue(challengeInfo.CompletedChallengeGuids.Contains(Guid.Parse(Challenge1Guid)));
            Assert.IsTrue(challengeInfo.CompletedChallengeGuids.Contains(Guid.Parse(Challenge2Guid)));
            Assert.IsTrue(challengeInfo.CompletedChallengeGuids.Contains(Guid.Parse(Challenge3Guid)));
            
            Assert.AreEqual(1, challengeInfo.CompletedChallengeGuids.Count);
            Assert.IsTrue(challengeInfo.CompletedChallengeGuids.Contains(Guid.Parse(Challenge4Guid)));
        }
    }
}