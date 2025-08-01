using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            
            // そのプレイヤーIDのチャレンジを作成する
            // create a challenge for that player ID
            var challengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            var initialChallenge = MasterHolder.ChallengeMaster.InitialChallenge.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
            Assert.AreEqual(initialChallenge.Count,challengeInfo.CurrentChallenges.Count);
            foreach (var currentChallenge in challengeInfo.CurrentChallenges)
            {
                var challenge = initialChallenge.Find(c => c.ChallengeGuid == currentChallenge.ChallengeMasterElement.ChallengeGuid);
                Assert.IsNotNull(challenge);
            }
            
            
            // なにもクリアしていない状態でセーブ
            // Save without clearing anything
            var saveJson = assembleSaveJsonText.AssembleSaveJson();
            
            // ロード
            // load
            (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            (serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            
            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            challengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            Assert.AreEqual(initialChallenge.Count,challengeInfo.CurrentChallenges.Count);
            foreach (var currentChallenge in challengeInfo.CurrentChallenges)
            {
                var challenge = initialChallenge.Find(c => c.ChallengeGuid == currentChallenge.ChallengeMasterElement.ChallengeGuid);
                Assert.IsNotNull(challenge);
            }
            // 何もクリアしていないことを確認
            // Check that nothing is cleared
            Assert.AreEqual(0,challengeInfo.CompletedChallengeGuids.Count);
        }
        
        [Test]
        public void CompletedChallengeSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            
            // そのプレイヤーIDのチャレンジを作成する
            // create a challenge for that player ID
            var challengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            var initialChallenge = MasterHolder.ChallengeMaster.InitialChallenge.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
            foreach (var currentChallenge in challengeInfo.CurrentChallenges)
            {
                var challenge = initialChallenge.Find(c => c.ChallengeGuid == currentChallenge.ChallengeMasterElement.ChallengeGuid);
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
            (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            (serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            
            // チャレンジがクリアされていることを確認する
            // Check that the challenge is cleared
            var loadedChallengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            Assert.AreEqual(1, loadedChallengeInfo.CompletedChallengeGuids.Count);
            var challengeGuid = new Guid("00000000-0000-0000-4567-000000000001");
            Assert.AreEqual(challengeGuid, loadedChallengeInfo.CompletedChallengeGuids[0]);
            
            Assert.AreEqual(currentChallengeCount, loadedChallengeInfo.CurrentChallenges.Count);
            for (int i = 0; i < loadedChallengeInfo.CompletedChallengeGuids.Count; i++)
            {
                Assert.AreEqual(challengeInfo.CompletedChallengeGuids[i], loadedChallengeInfo.CompletedChallengeGuids[i]);
            }
        }
        
        [Test]
        public void CurrentChallengeSaveLoadTest()
        {
            // AI生成コード
            // AI generated code
            
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();

            // そのプレイヤーIDのチャレンジを作成する
            // create a challenge for that player ID
            var challengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);

            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            var initialChallenge = MasterHolder.ChallengeMaster.InitialChallenge.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
            Assert.AreEqual(initialChallenge.Count, challengeInfo.CurrentChallenges.Count);
            foreach (var currentChallenge in challengeInfo.CurrentChallenges)
            {
                var challenge = initialChallenge.Find(c => c.ChallengeGuid == currentChallenge.ChallengeMasterElement.ChallengeGuid);
                Assert.IsNotNull(challenge);
            }

            // クラフトのチャレンジをクリアする
            // Clear the craft challenge
            ChallengeCompletedEventTest.ClearCraftChallenge(packet, serviceProvider);

            // 保存する直前の現在のチャレンジを取得
            // Get the current challenge just before saving
            var currentChallenges = challengeInfo.CurrentChallenges.ToList();

            // セーブ
            // Save
            var saveJson = assembleSaveJsonText.AssembleSaveJson();

            // ロード
            // load
            (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            (serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            // ロードされたチャレンジ情報を取得
            // Get the loaded challenge information
            var loadedChallengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);

            // クリア済みのチャレンジが1つ存在することを確認
            Assert.AreEqual(1, loadedChallengeInfo.CompletedChallengeGuids.Count);

            // 現在のチャレンジが正しくロードされていることを確認
            // Check that the current challenges are loaded correctly
            Assert.AreEqual(currentChallenges.Count, loadedChallengeInfo.CurrentChallenges.Count);
            for (int i = 0; i < currentChallenges.Count; i++)
            {
                var beforeChallenge = currentChallenges[i].ChallengeMasterElement.ChallengeGuid;
                var loadedChallenge = loadedChallengeInfo.CurrentChallenges[i].ChallengeMasterElement.ChallengeGuid;
                
                Assert.AreEqual(beforeChallenge, loadedChallenge);
            }
        }
    }
}