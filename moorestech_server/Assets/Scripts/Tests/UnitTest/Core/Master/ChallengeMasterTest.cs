using System;
using System.Linq;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master
{
    public class ChallengeMasterTest
    {
        
        [Test]
        public void LoadCategories_カテゴリが正しく読み込まれる()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // カテゴリ数の確認
            Assert.AreEqual(3, MasterHolder.ChallengeMaster.GetAllCategories().Count);
            
            // カテゴリ名の確認
            var categories = MasterHolder.ChallengeMaster.GetAllCategories();
            Assert.IsTrue(categories.Any(c => c.CategoryName == "基本チュートリアル"));
            Assert.IsTrue(categories.Any(c => c.CategoryName == "建築チャレンジ"));
            Assert.IsTrue(categories.Any(c => c.CategoryName == "上級チャレンジ"));
        }
        
        [Test]
        public void GetChallengesByCategory_カテゴリ内のチャレンジが正しく取得できる()
        {
            var categoryGuid = new Guid("00000000-0000-0000-9999-000000000001");
            var challenges = MasterHolder.ChallengeMaster.GetChallengesByCategory(categoryGuid);
            
            // 基本チュートリアルカテゴリには2つのチャレンジがある
            Assert.AreEqual(2, challenges.Count);
            
            // チャレンジGUIDの確認
            Assert.IsTrue(challenges.Any(c => c.ChallengeGuid == new Guid("00000000-0000-0000-4567-000000000001")));
            Assert.IsTrue(challenges.Any(c => c.ChallengeGuid == new Guid("00000000-0000-0000-4567-000000000002")));
        }
        
        [Test]
        public void GetCategory_GUIDでカテゴリが正しく取得できる()
        {
            var categoryGuid = new Guid("00000000-0000-0000-9999-000000000001");
            var category = MasterHolder.ChallengeMaster.GetCategory(categoryGuid);
            
            Assert.IsNotNull(category);
            Assert.AreEqual("基本チュートリアル", category.CategoryName);
            Assert.AreEqual(1, category.DisplayOrder);
            Assert.IsTrue(category.InitialUnlocked);
        }
        
        [Test]
        public void GetInitialUnlockedCategories_初期アンロックカテゴリが正しく取得できる()
        {
            var initialCategories = MasterHolder.ChallengeMaster.GetInitialUnlockedCategories();
            
            // 基本チュートリアルのみが初期アンロック
            Assert.AreEqual(1, initialCategories.Count);
            Assert.AreEqual("基本チュートリアル", initialCategories[0].CategoryName);
        }
        
        [Test]
        public void GetChallenge_既存の実装との互換性を確認()
        {
            // 既存のGetChallengeメソッドが正しく動作することを確認
            var challengeGuid = new Guid("00000000-0000-0000-4567-000000000001");
            var challenge = MasterHolder.ChallengeMaster.GetChallenge(challengeGuid);
            
            Assert.IsNotNull(challenge);
            Assert.AreEqual("最初のアイテムを作る", challenge.Title);
            Assert.AreEqual("createItem", challenge.TaskCompletionType);
        }
        
        [Test]
        public void GetNextChallenges_カテゴリ内でのnextチャレンジが正しく取得できる()
        {
            // チャレンジ1の次のチャレンジを取得
            var challenge1Guid = new Guid("00000000-0000-0000-4567-000000000001");
            var nextChallenges = MasterHolder.ChallengeMaster.GetNextChallenges(challenge1Guid);
            
            // チャレンジ2が次のチャレンジとして設定されている
            Assert.AreEqual(1, nextChallenges.Count);
            Assert.AreEqual(new Guid("00000000-0000-0000-4567-000000000002"), nextChallenges[0].ChallengeGuid);
        }
        
        [Test]
        public void InitialChallenge_カテゴリ構造でも初期チャレンジが正しく取得できる()
        {
            // 初期チャレンジは前提チャレンジがないもの
            var initialChallenges = MasterHolder.ChallengeMaster.InitialChallenge;
            
            // チャレンジ1とチャレンジ3が初期チャレンジ（それぞれのカテゴリ内で）
            Assert.AreEqual(2, initialChallenges.Count);
            Assert.IsTrue(initialChallenges.Contains(new Guid("00000000-0000-0000-4567-000000000001")));
            Assert.IsTrue(initialChallenges.Contains(new Guid("00000000-0000-0000-4567-000000000003")));
        }
        
        [Test]
        public void GetCategoryOfChallenge_チャレンジが属するカテゴリを取得できる()
        {
            var challengeGuid = new Guid("00000000-0000-0000-4567-000000000001");
            var category = MasterHolder.ChallengeMaster.GetCategoryOfChallenge(challengeGuid);
            
            Assert.IsNotNull(category);
            Assert.AreEqual("基本チュートリアル", category.CategoryName);
        }
        
        [Test]
        public void ValidateChallengeReferences_カテゴリ間のチャレンジ参照がないことを確認()
        {
            // カテゴリを跨いだprevChallengeGuidsの参照がないことを検証
            var isValid = MasterHolder.ChallengeMaster.ValidateChallengeReferences();
            
            // このテストデータでは全てのチャレンジ参照が同一カテゴリ内なのでtrue
            Assert.IsTrue(isValid);
        }
        
        [Test]
        public void GetCategoriesDependingOn_カテゴリの依存関係が正しく取得できる()
        {
            var categoryGuid = new Guid("00000000-0000-0000-9999-000000000001");
            var dependentCategories = MasterHolder.ChallengeMaster.GetCategoriesDependingOn(categoryGuid);
            
            // 建築チャレンジが基本チュートリアルに依存している
            Assert.AreEqual(1, dependentCategories.Count);
            Assert.AreEqual("建築チャレンジ", dependentCategories[0].CategoryName);
        }
        
        [Test]
        public void EmptyCategory_空のカテゴリも正しく処理される()
        {
            var categoryGuid = new Guid("00000000-0000-0000-9999-000000000003");
            var challenges = MasterHolder.ChallengeMaster.GetChallengesByCategory(categoryGuid);
            
            // 上級チャレンジカテゴリは空
            Assert.AreEqual(0, challenges.Count);
        }
    }
}