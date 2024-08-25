using System.Collections.Generic;
using Game.Challenge;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class ChallengeSaveLoadTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void NonCompletedChallengeSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            
            // そのプレイヤーIDのチャレンジを作成する
            // create a challenge for that player ID
            var challengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            var initialChallenge = (List<ChallengeInfo>)ServerContext.ChallengeConfig.InitialChallenges;
            Assert.AreEqual(initialChallenge.Count,challengeInfo.CurrentChallenges.Count);
            foreach (var currentChallenge in challengeInfo.CurrentChallenges)
            {
                var challenge = initialChallenge.Find(c => c.Id == currentChallenge.ChallengeElement.Id);
                Assert.IsNotNull(challenge);
            }
            
            
            // なにもクリアしていない状態でセーブ
            // Save without clearing anything
            var saveJson = assembleSaveJsonText.AssembleSaveJson();
            
            // ロード
            // load
            (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            (serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            
            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            challengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            Assert.AreEqual(initialChallenge.Count,challengeInfo.CurrentChallenges.Count);
            foreach (var currentChallenge in challengeInfo.CurrentChallenges)
            {
                var challenge = initialChallenge.Find(c => c.Id == currentChallenge.ChallengeElement.Id);
                Assert.IsNotNull(challenge);
            }
            // 何もクリアしていないことを確認
            // Check that nothing is cleared
            Assert.AreEqual(0,challengeInfo.CompletedChallengeGuids.Count);
        }
        
        [Test]
        public void CompletedChallengeSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            
            // そのプレイヤーIDのチャレンジを作成する
            // create a challenge for that player ID
            var challengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            var initialChallenge = (List<ChallengeInfo>)ServerContext.ChallengeConfig.InitialChallenges;
            foreach (var currentChallenge in challengeInfo.CurrentChallenges)
            {
                var challenge = initialChallenge.Find(c => c.Id == currentChallenge.ChallengeElement.Id);
                Assert.IsNotNull(challenge);
            }
            
            // クラフトのチャレンジをクリアする
            // Clear the craft challenge
            ChallengeCompletedEventTest.ClearCraftChallenge(packet, serviceProvider);
            
            // クラフトのチャレンジがクリアされたことを確認する
            // Check that the craft challenge is cleared
            Assert.AreEqual(1, challengeInfo.CompletedChallengeGuids.Count);
            var currentChallengeCount = challengeInfo.CurrentChallenges.Count;
            
            // セーブ
            // Save
            var saveJson = assembleSaveJsonText.AssembleSaveJson();
            
            // ロード
            // load
            (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            (serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            
            // チャレンジがクリアされていることを確認する
            // Check that the challenge is cleared
            var loadedChallengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            Assert.AreEqual(1, loadedChallengeInfo.CompletedChallengeGuids.Count);
            Assert.AreEqual(1000, loadedChallengeInfo.CompletedChallengeGuids[0]);
            
            Assert.AreEqual(currentChallengeCount, loadedChallengeInfo.CurrentChallenges.Count);
            for (int i = 0; i < loadedChallengeInfo.CompletedChallengeGuids.Count; i++)
            {
                Assert.AreEqual(challengeInfo.CompletedChallengeGuids[i], loadedChallengeInfo.CompletedChallengeGuids[i]);
            }
        }
    }
}