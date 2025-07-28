using System;
using System.Linq;
using Game.Challenge;
using Game.Challenge.Task;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using static Server.Protocol.PacketResponse.GetChallengeInfoProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetChallengeInfoProtocolTest
    {
        private const string Challenge1Guid = "00000000-0000-0000-4567-000000000001";
        private const string Challenge2Guid = "00000000-0000-0000-4567-000000000002";
        private const string Challenge3Guid = "00000000-0000-0000-4567-000000000003";
        private const string Challenge4Guid = "00000000-0000-0000-4567-000000000004";
        private const string Challenge5Guid = "00000000-0000-0000-4567-000000000005";
        
        [Test]
        public void GetCompletedChallengeTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // チャレンジを無理やりクリアする
            // Forced to complete a challenge
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            
            // 初期チャレンジを設定
            challengeDatastore.InitializeCurrentChallenges();
            var currentChallengeInfo = challengeDatastore.CurrentChallengeInfo;
            
            // 最初は3つのチャレンジがあるはず（1、2、3）
            Assert.AreEqual(3, currentChallengeInfo.CurrentChallenges.Count);
            
            // チャレンジ1だけをクリア
            var challenge1 = currentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == Guid.Parse(Challenge1Guid));
            var subject1 = (Subject<IChallengeTask>)challenge1.OnChallengeComplete;
            subject1.OnNext(challenge1);
            
            // 現在のチャレンジ情報をリクエスト
            // Request current challenge information
            var messagePack = new RequestChallengeMessagePack();
            var response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            var challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoMessagePack>(response.ToArray());
            
            // 検証
            // Verification
            
            // Categories内のすべてのCompletedChallengeを集計
            var allCompletedChallenges = challengeInfo.Categories.SelectMany(c => c.CompletedChallengeGuids).ToList();
            Assert.AreEqual(1, allCompletedChallenges.Count);
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge1Guid)));
            
            // チャレンジ1がクリアされたので、チャレンジ4が開始されているはず
            // まだチャレンジ2と3が残っているので、合計で3つのCurrentChallengeがあるはず
            // Categories内のすべてのCurrentChallengeを集計
            var allCurrentChallenges = challengeInfo.Categories.SelectMany(c => c.CurrentChallengeGuids).ToList();
            Assert.AreEqual(3, allCurrentChallenges.Count);
            Assert.IsTrue(allCurrentChallenges.Contains(Guid.Parse(Challenge4Guid)));
            Assert.IsTrue(allCurrentChallenges.Contains(Guid.Parse(Challenge2Guid)));
            Assert.IsTrue(allCurrentChallenges.Contains(Guid.Parse(Challenge3Guid)));
                
                
            // 複数のチャレンジがクリアしたらチャレンジがスタートすることを検証する
            // Verify that the challenge starts when multiple challenges are cleared
            
            // チャレンジ2、3、4をクリア（チャレンジ5の前提条件を満たすため）
            var challenge2 = currentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == Guid.Parse(Challenge2Guid));
            var subject2 = (Subject<IChallengeTask>)challenge2.OnChallengeComplete;
            subject2.OnNext(challenge2);
            
            var challenge3 = currentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == Guid.Parse(Challenge3Guid));
            var subject3 = (Subject<IChallengeTask>)challenge3.OnChallengeComplete;
            subject3.OnNext(challenge3);
            
            var challenge4 = currentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == Guid.Parse(Challenge4Guid));
            var subject4 = (Subject<IChallengeTask>)challenge4.OnChallengeComplete;
            subject4.OnNext(challenge4);
            
            
            // 現在のチャレンジ情報をリクエスト
            // Request current challenge information
            messagePack = new RequestChallengeMessagePack();
            response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoMessagePack>(response.ToArray());
            
            // 検証
            // Verification
            
            // チャレンジ1、2、3、4がクリアされているはず
            allCompletedChallenges = challengeInfo.Categories.SelectMany(c => c.CompletedChallengeGuids).ToList();
            Assert.AreEqual(4, allCompletedChallenges.Count);
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge1Guid)));
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge2Guid)));
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge3Guid)));
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge4Guid)));
            
            // チャレンジ5だけが現在のチャレンジとして残っているはず
            allCurrentChallenges = challengeInfo.Categories.SelectMany(c => c.CurrentChallengeGuids).ToList();
            Assert.AreEqual(1, allCurrentChallenges.Count);
            Assert.IsTrue(allCurrentChallenges.Contains(Guid.Parse(Challenge5Guid)));
        }
    }
}