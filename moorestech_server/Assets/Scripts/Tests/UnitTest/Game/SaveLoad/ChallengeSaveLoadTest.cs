using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Challenge;
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
        private readonly List<Guid> _initialChallenge = new()
        {
            Guid.Parse("00000000-0000-0000-4567-000000000001"),
            Guid.Parse("00000000-0000-0000-4567-000000000002"),
            Guid.Parse("00000000-0000-0000-4567-000000000003")
        };
        
        [Test]
        public void NonCompletedChallengeSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            
            // 初期チャレンジを設定
            // Set initial challenges
            challengeDatastore.InitializeCurrentChallenges();
            
            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            var challengeInfo = challengeDatastore.CurrentChallengeInfo;
            Assert.AreEqual(_initialChallenge.Count ,challengeInfo.CurrentChallenges.Count);
            
            foreach (var currentChallenge in challengeInfo.CurrentChallenges)
            {
                var index = _initialChallenge.FindIndex(g => g == currentChallenge.ChallengeMasterElement.ChallengeGuid);
                Assert.IsTrue(index != -1);
            }
            
            
            // なにもクリアしていない状態でセーブ
            // Save without clearing anything
            var saveJson = assembleSaveJsonText.AssembleSaveJson();
            
            // ロード
            // load
            (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            (serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            
            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            challengeInfo = challengeDatastore.CurrentChallengeInfo;
            Assert.AreEqual(_initialChallenge.Count,challengeInfo.CurrentChallenges.Count);
            foreach (var currentChallenge in challengeInfo.CurrentChallenges)
            {
                var index = _initialChallenge.FindIndex(g => g == currentChallenge.ChallengeMasterElement.ChallengeGuid);
                Assert.IsTrue(index != -1);
            }
            // 何もクリアしていないことを確認
            // Check that nothing is cleared
            Assert.AreEqual(0,challengeInfo.CompletedChallenges.Count);
        }
        
        [Test]
        public void CompletedChallengeSaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            
            // 初期チャレンジを設定
            // Set initial challenges
            challengeDatastore.InitializeCurrentChallenges();
            
            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            var initialChallenge = _initialChallenge.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
            var challengeInfo = challengeDatastore.CurrentChallengeInfo;
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
            Assert.AreEqual(1, challengeInfo.CompletedChallenges.Count);
            var currentChallengeCount = challengeInfo.CurrentChallenges.Count;
            
            // セーブ
            // Save
            var saveJson = assembleSaveJsonText.AssembleSaveJson();
            
            // ロード
            // load
            (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            (serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            
            // チャレンジがクリアされていることを確認する
            // Check that the challenge is cleared
            var loadedChallengeInfo = challengeDatastore.CurrentChallengeInfo;
            Assert.AreEqual(1, loadedChallengeInfo.CompletedChallenges.Count);
            var challengeGuid = new Guid("00000000-0000-0000-4567-000000000001");
            Assert.AreEqual(challengeGuid, loadedChallengeInfo.CompletedChallenges[0].ChallengeGuid);
            
            Assert.AreEqual(currentChallengeCount, loadedChallengeInfo.CurrentChallenges.Count);
            for (int i = 0; i < loadedChallengeInfo.CompletedChallenges.Count; i++)
            {
                Assert.AreEqual(challengeInfo.CompletedChallenges[i].ChallengeGuid, loadedChallengeInfo.CompletedChallenges[i].ChallengeGuid);
            }
        }
        
        [Test]
        public void CurrentChallengeSaveLoadTest()
        {
            // AI生成コード
            // AI generated code
            
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();

            // 初期チャレンジを設定
            // Set initial challenges
            challengeDatastore.InitializeCurrentChallenges();

            // 初期チャレンジが正しく設定されていることを確認する
            // Check that the initial challenge is set correctly
            var initialChallenge = _initialChallenge.Select(MasterHolder.ChallengeMaster.GetChallenge).ToList();
            var challengeInfo = challengeDatastore.CurrentChallengeInfo;
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
            (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            (serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            // ロードされたチャレンジ情報を取得
            // Get the loaded challenge information
            var loadedChallengeInfo = challengeDatastore.CurrentChallengeInfo;

            // クリア済みのチャレンジが1つ存在することを確認
            Assert.AreEqual(1, loadedChallengeInfo.CompletedChallenges.Count);

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