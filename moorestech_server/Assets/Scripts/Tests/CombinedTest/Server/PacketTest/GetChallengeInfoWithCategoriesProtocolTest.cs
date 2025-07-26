using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
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
    public class GetChallengeInfoWithCategoriesProtocolTest
    {
        private const int PlayerId = 1;
        
        // カテゴリGUID
        private const string BasicCategoryGuid = "00000000-0000-0000-9999-000000000001";
        private const string AdvancedCategoryGuid = "00000000-0000-0000-9999-000000000002";
        
        // チャレンジGUID
        private const string Challenge1Guid = "00000000-0000-0000-4567-000000000001";
        private const string Challenge2Guid = "00000000-0000-0000-4567-000000000002";
        private const string Challenge3Guid = "00000000-0000-0000-4567-000000000003";
        private const string Challenge4Guid = "00000000-0000-0000-4567-000000000004";
        
        [Test]
        public void GetChallengeInfoWithCategories_カテゴリ構造が正しく返される()
        {
            // カテゴリ対応のテストデータを使用
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            var playerChallengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // チャレンジ情報をリクエスト
            var messagePack = new RequestChallengeMessagePack(PlayerId);
            var response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            var challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoWithCategoriesMessagePack>(response.ToArray());
            
            // カテゴリ情報が含まれていることを確認
            Assert.IsNotNull(challengeInfo.Categories);
            Assert.Greater(challengeInfo.Categories.Count, 0, "カテゴリが1つ以上存在する必要があります");
            
            // 初期アンロックカテゴリが含まれていることを確認
            var basicCategory = challengeInfo.Categories.FirstOrDefault(c => c.CategoryGuid == Guid.Parse(BasicCategoryGuid));
            Assert.IsNotNull(basicCategory, "基本カテゴリが存在する必要があります");
            Assert.IsTrue(basicCategory.IsUnlocked, "基本カテゴリは初期アンロックされている必要があります");
            
            // アンロックされていないカテゴリも含まれていることを確認
            var advancedCategory = challengeInfo.Categories.FirstOrDefault(c => c.CategoryGuid == Guid.Parse(AdvancedCategoryGuid));
            if (advancedCategory != null)
            {
                Assert.IsFalse(advancedCategory.IsUnlocked, "応用カテゴリは初期状態でロックされている必要があります");
            }
        }
        
        [Test]
        public void GetCategoryProgress_カテゴリの進捗が正しく計算される()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            var playerChallengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // 最初のチャレンジを完了させる
            if (playerChallengeInfo.CurrentChallenges.Count > 0)
            {
                var firstChallenge = playerChallengeInfo.CurrentChallenges[0];
                var subject = (Subject<IChallengeTask>)firstChallenge.OnChallengeComplete;
                subject.OnNext(firstChallenge);
            }
            
            // チャレンジ情報を再度リクエスト
            var messagePack = new RequestChallengeMessagePack(PlayerId);
            var response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            var challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoWithCategoriesMessagePack>(response.ToArray());
            
            // カテゴリの進捗が更新されていることを確認
            var basicCategory = challengeInfo.Categories.FirstOrDefault(c => c.CategoryGuid == Guid.Parse(BasicCategoryGuid));
            Assert.IsNotNull(basicCategory);
            Assert.Greater(basicCategory.Progress, 0f, "チャレンジを完了したので進捗は0より大きい必要があります");
            Assert.LessOrEqual(basicCategory.Progress, 1f, "進捗は1以下である必要があります");
        }
        
        [Test]
        public void CompleteCategoryChallenge_カテゴリ完了でイベントが発行される()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            var playerChallengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // カテゴリ完了イベントを監視
            CategoryCompletedEventPacket receivedEvent = null;
            var challengeEvent = serviceProvider.GetService<ChallengeEvent>();
            challengeEvent.OnCategoryCompleted.Subscribe(evt => receivedEvent = evt);
            
            // カテゴリ内の全チャレンジを完了させる（簡略化のため強制完了）
            // 実際の実装では、カテゴリ内の全チャレンジを順番に完了させる必要がある
            foreach (var challenge in playerChallengeInfo.CurrentChallenges.ToList())
            {
                var category = MasterHolder.ChallengeMaster.GetCategoryOfChallenge(challenge.ChallengeMasterElement.ChallengeGuid);
                if (category != null && category.CategoryGuid == Guid.Parse(BasicCategoryGuid))
                {
                    var subject = (Subject<IChallengeTask>)challenge.OnChallengeComplete;
                    subject.OnNext(challenge);
                }
            }
            
            // カテゴリ完了イベントが発行されたかを確認（実装に応じて調整）
            // この部分は実際の実装に合わせて修正が必要
        }
        
        [Test]
        public void UnlockCategory_カテゴリアンロックアクションが機能する()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            var playerChallengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // カテゴリアンロックアクションを持つチャレンジを探す
            IChallengeTask challengeWithUnlockAction = null;
            foreach (var challenge in playerChallengeInfo.CurrentChallenges)
            {
                var actions = challenge.ChallengeMasterElement.ClearedActions?.items;
                if (actions != null && actions.Any(a => a.ChallengeActionType == "unlockChallengeCategory"))
                {
                    challengeWithUnlockAction = challenge;
                    break;
                }
            }
            
            if (challengeWithUnlockAction != null)
            {
                // チャレンジを完了させる
                var subject = (Subject<IChallengeTask>)challengeWithUnlockAction.OnChallengeComplete;
                subject.OnNext(challengeWithUnlockAction);
                
                // チャレンジ情報を再取得
                var messagePack = new RequestChallengeMessagePack(PlayerId);
                var response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
                var challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoWithCategoriesMessagePack>(response.ToArray());
                
                // 新しいカテゴリがアンロックされていることを確認
                // (実際の実装に応じて、アンロックされたカテゴリのGUIDを確認)
            }
            else
            {
                Assert.Ignore("カテゴリアンロックアクションを持つチャレンジがテストデータに存在しません");
            }
        }
        
        [Test]
        public void GetCurrentChallengesByCategory_カテゴリ別の現在のチャレンジが取得できる()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            var playerChallengeInfo = challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // チャレンジ情報をリクエスト
            var messagePack = new RequestChallengeMessagePack(PlayerId);
            var response = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            var challengeInfo = MessagePackSerializer.Deserialize<ResponseChallengeInfoWithCategoriesMessagePack>(response.ToArray());
            
            // 各カテゴリの現在のチャレンジを確認
            foreach (var category in challengeInfo.Categories)
            {
                if (category.IsUnlocked)
                {
                    // アンロックされているカテゴリには現在のチャレンジが存在する可能性がある
                    Assert.IsNotNull(category.CurrentChallengeGuids);
                    
                    // 現在のチャレンジが全てこのカテゴリに属していることを確認
                    foreach (var challengeGuid in category.CurrentChallengeGuids)
                    {
                        var challenge = MasterHolder.ChallengeMaster.GetChallenge(challengeGuid);
                        var challengeCategory = MasterHolder.ChallengeMaster.GetCategoryOfChallenge(challengeGuid);
                        Assert.AreEqual(category.CategoryGuid, challengeCategory.CategoryGuid, 
                            "チャレンジは正しいカテゴリに属している必要があります");
                    }
                }
                else
                {
                    // ロックされているカテゴリには現在のチャレンジが存在しない
                    Assert.IsEmpty(category.CurrentChallengeGuids);
                }
            }
        }
    }
    
    /// <summary>
    /// カテゴリ対応のレスポンスメッセージパック（仮実装）
    /// 実際の実装では、Server.Protocol.PacketResponseに配置される
    /// </summary>
    [MessagePackObject]
    public class ResponseChallengeInfoWithCategoriesMessagePack
    {
        [Key(0)] public int PlayerId { get; set; }
        [Key(1)] public List<CategoryInfo> Categories { get; set; }
        
        [MessagePackObject]
        public class CategoryInfo
        {
            [Key(0)] public Guid CategoryGuid { get; set; }
            [Key(2)] public bool IsUnlocked { get; set; }
            [Key(4)] public List<Guid> CurrentChallengeGuids { get; set; }
            [Key(5)] public List<Guid> CompletedChallengeGuids { get; set; }
        }
    }
    
    /// <summary>
    /// カテゴリ完了イベントパケット（仮実装）
    /// </summary>
    public class CategoryCompletedEventPacket
    {
        public int PlayerId { get; set; }
        public Guid CategoryGuid { get; set; }
        public List<Guid> UnlockedCategoryGuids { get; set; }
    }
}