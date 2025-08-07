using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Challenge;
using Game.Challenge.Task.Factory;
using Game.UnlockState;
using UnityEngine;
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
        
        private const string Category1Guid = "03ca4ded-3b2b-4e7f-bb6e-430f060c4ed1";
        private const string Category2Guid = "35330f9d-f44f-493d-a6bc-07ae6413d7c4";
        private const string Category2Challenge1Guid = "c2d84cfb-73cb-4ffa-99b5-9703c550f330";
        
        [Test]
        public void GetCompletedChallengeTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
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
        
        [Test]
        public void CategoryUnlockStartsFirstChallengeTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            
            // 初期チャレンジを設定
            challengeDatastore.InitializeCurrentChallenges();
            var currentChallengeInfo = challengeDatastore.CurrentChallengeInfo;
            
            var gameUnlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
            // カテゴリ1のチャレンジをクリアを確認
            Assert.AreEqual(1, gameUnlockState.ChallengeCategoryUnlockStateInfos.Values.Count(c => c.IsUnlocked));
            
            // 最初はCategory2のチャレンジは開始されていないことを確認
            var messagePack = new RequestChallengeMessagePack();
            var response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            var challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoMessagePack>(response.ToArray());
            
            // Category2のチャレンジが開始されていないことを確認
            var category2CurrentChallenges = challengeInfo.Categories
                .FirstOrDefault(c => c.ChallengeCategoryGuid == Guid.Parse(Category2Guid))
                ?.CurrentChallengeGuids ?? new List<Guid>();
            Assert.AreEqual(0, category2CurrentChallenges.Count, "Category2 should have no active challenges initially");
            
            // Category2がロックされていることを確認（IsUnlockedがfalse）
            var category2Info = challengeInfo.Categories.FirstOrDefault(c => c.ChallengeCategoryGuid == Guid.Parse(Category2Guid));
            Assert.NotNull(category2Info, "Category2 should exist");
            Assert.IsFalse(category2Info.IsUnlocked, "Category2 should be locked initially");
            
            // Challenge1〜4を順番にクリア（Challenge5の前提条件）
            var challenge1 = currentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == Guid.Parse(Challenge1Guid));
            var subject1 = (Subject<IChallengeTask>)challenge1.OnChallengeComplete;
            subject1.OnNext(challenge1);
            
            var challenge2 = currentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == Guid.Parse(Challenge2Guid));
            var subject2 = (Subject<IChallengeTask>)challenge2.OnChallengeComplete;
            subject2.OnNext(challenge2);
            
            var challenge3 = currentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == Guid.Parse(Challenge3Guid));
            var subject3 = (Subject<IChallengeTask>)challenge3.OnChallengeComplete;
            subject3.OnNext(challenge3);
            
            var challenge4 = currentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == Guid.Parse(Challenge4Guid));
            var subject4 = (Subject<IChallengeTask>)challenge4.OnChallengeComplete;
            subject4.OnNext(challenge4);
            
            // Challenge5が開始されていることを確認
            messagePack = new RequestChallengeMessagePack();
            response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoMessagePack>(response.ToArray());
            
            var allCurrentChallenges = challengeInfo.Categories.SelectMany(c => c.CurrentChallengeGuids).ToList();
            Assert.IsTrue(allCurrentChallenges.Contains(Guid.Parse(Challenge5Guid)), "Challenge5 should be started");
            
            // Challenge5をクリア（Category2をアンロック）
            var challenge5 = currentChallengeInfo.CurrentChallenges.First(c => c.ChallengeMasterElement.ChallengeGuid == Guid.Parse(Challenge5Guid));
            var subject5 = (Subject<IChallengeTask>)challenge5.OnChallengeComplete;
            subject5.OnNext(challenge5);
            
            // カテゴリ2のアンロック後、新しくアンロックされたカテゴリの初期チャレンジをチェック
            // カテゴリ1とカテゴリ2の両方アンロックされている
            Assert.AreEqual(2, gameUnlockState.ChallengeCategoryUnlockStateInfos.Values.Count(c => c.IsUnlocked));
            Debug.Log($"Checking unlocked categories after Challenge5 completion");
            
            // 最新のチャレンジ情報を取得
            messagePack = new RequestChallengeMessagePack();
            response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoMessagePack>(response.ToArray());
            
            // Category2がアンロックされていることを確認
            category2Info = challengeInfo.Categories.FirstOrDefault(c => c.ChallengeCategoryGuid == Guid.Parse(Category2Guid));
            Assert.NotNull(category2Info, "Category2 should exist");
            Assert.IsTrue(category2Info.IsUnlocked, "Category2 should be unlocked after completing Challenge5");
            
            // Category2の最初のチャレンジが開始されていることを確認
            category2CurrentChallenges = category2Info.CurrentChallengeGuids;
            
            // デバッグ情報を出力
            Debug.Log($"Category2 IsUnlocked: {category2Info.IsUnlocked}");
            Debug.Log($"Category2 Current Challenges Count: {category2CurrentChallenges.Count}");
            if (category2CurrentChallenges.Count > 0)
            {
                Debug.Log($"Category2 Current Challenge GUID: {category2CurrentChallenges[0]}");
            }
            
            // カテゴリ2の情報を追加で確認
            var allCategoriesInfo = challengeInfo.Categories;
            foreach (var cat in allCategoriesInfo)
            {
                Debug.Log($"Category {cat.ChallengeCategoryGuid}: IsUnlocked={cat.IsUnlocked}, CurrentChallenges={cat.CurrentChallengeGuids.Count}, CompletedChallenges={cat.CompletedChallengeGuids.Count}");
            }
            
            Assert.AreEqual(1, category2CurrentChallenges.Count, "Category2 should have 1 active challenge");
            Assert.AreEqual(Guid.Parse(Category2Challenge1Guid), category2CurrentChallenges[0], 
                "Category2's first challenge should be started");
            
            // Challenge1〜5がすべてクリアされていることを確認
            var allCompletedChallenges = challengeInfo.Categories.SelectMany(c => c.CompletedChallengeGuids).ToList();
            Assert.AreEqual(5, allCompletedChallenges.Count, "All 5 challenges should be completed");
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge1Guid)));
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge2Guid)));
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge3Guid)));
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge4Guid)));
            Assert.IsTrue(allCompletedChallenges.Contains(Guid.Parse(Challenge5Guid)));
        }
    }
}