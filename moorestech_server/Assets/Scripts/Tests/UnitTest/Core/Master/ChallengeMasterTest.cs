using System;
using System.Linq;
using Core.Master;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Master
{
    public class ChallengeMasterTest
    {
        private ChallengeMaster _challengeMaster;
        
        [SetUp]
        public void Setup()
        {
            // カテゴリ構造のテストデータを使用してChallengeMasterを初期化
            var jsonText = @"{
                ""categories"": [
                    {
                        ""categoryGuid"": ""00000000-0000-0000-9999-000000000001"",
                        ""categoryName"": ""基本チュートリアル"",
                        ""categoryDescription"": ""ゲームの基本的な操作を学ぶ"",
                        ""displayOrder"": 1,
                        ""initialUnlocked"": true,
                        ""prevCategoryGuids"": [],
                        ""challenges"": [
                            {
                                ""challengeGuid"": ""00000000-0000-0000-4567-000000000001"",
                                ""title"": ""最初のアイテムを作る"",
                                ""summary"": ""アイテムを作成する"",
                                ""taskCompletionType"": ""createItem"",
                                ""taskParam"": {
                                    ""itemGuid"": ""00000000-0000-0000-1234-000000000001""
                                },
                                ""prevChallengeGuids"": [],
                                ""initialUnlocked"": true,
                                ""unlockAllPreviousChallengeComplete"": true,
                                ""clearedActions"": [],
                                ""startedActions"": []
                            },
                            {
                                ""challengeGuid"": ""00000000-0000-0000-4567-000000000002"",
                                ""title"": ""インベントリチャレンジ"",
                                ""summary"": ""アイテムを集める"",
                                ""taskCompletionType"": ""inInventoryItem"",
                                ""taskParam"": {
                                    ""itemGuid"": ""00000000-0000-0000-1234-000000000002"",
                                    ""itemCount"": 5
                                },
                                ""prevChallengeGuids"": [""00000000-0000-0000-4567-000000000001""],
                                ""initialUnlocked"": false,
                                ""unlockAllPreviousChallengeComplete"": true,
                                ""clearedActions"": [],
                                ""startedActions"": []
                            }
                        ]
                    },
                    {
                        ""categoryGuid"": ""00000000-0000-0000-9999-000000000002"",
                        ""categoryName"": ""建築チャレンジ"",
                        ""categoryDescription"": ""建築スキルを学ぶ"",
                        ""displayOrder"": 2,
                        ""initialUnlocked"": false,
                        ""prevCategoryGuids"": [""00000000-0000-0000-9999-000000000001""],
                        ""challenges"": [
                            {
                                ""challengeGuid"": ""00000000-0000-0000-4567-000000000003"",
                                ""title"": ""最初のブロックを設置"",
                                ""summary"": ""ブロックを設置する"",
                                ""taskCompletionType"": ""blockPlace"",
                                ""taskParam"": {
                                    ""blockGuid"": ""00000000-0000-0000-0000-000000000001"",
                                    ""itemCount"": 1
                                },
                                ""prevChallengeGuids"": [],
                                ""initialUnlocked"": true,
                                ""unlockAllPreviousChallengeComplete"": true,
                                ""clearedActions"": [
                                    {
                                        ""challengeActionType"": ""unlockChallengeCategory"",
                                        ""challengeActionParam"": {
                                            ""unlockCategoryGuid"": ""00000000-0000-0000-9999-000000000003""
                                        }
                                    }
                                ],
                                ""startedActions"": []
                            }
                        ]
                    },
                    {
                        ""categoryGuid"": ""00000000-0000-0000-9999-000000000003"",
                        ""categoryName"": ""上級チャレンジ"",
                        ""categoryDescription"": ""高度なチャレンジ"",
                        ""displayOrder"": 3,
                        ""initialUnlocked"": false,
                        ""prevCategoryGuids"": [""00000000-0000-0000-9999-000000000002""],
                        ""challenges"": []
                    }
                ]
            }";
            
            // NewtonsoftのJTokenの代わりに、既存のプロジェクトで使用されている方法を使用
            // 実際の実装では、適切なJSON読み込み方法を使用する必要がある
            // _challengeMaster = new ChallengeMaster(jsonData);
        }
        
        [Test]
        public void LoadCategories_カテゴリが正しく読み込まれる()
        {
            // カテゴリ数の確認
            Assert.AreEqual(3, _challengeMaster.GetAllCategories().Count);
            
            // カテゴリ名の確認
            var categories = _challengeMaster.GetAllCategories();
            Assert.IsTrue(categories.Any(c => c.CategoryName == "基本チュートリアル"));
            Assert.IsTrue(categories.Any(c => c.CategoryName == "建築チャレンジ"));
            Assert.IsTrue(categories.Any(c => c.CategoryName == "上級チャレンジ"));
        }
        
        [Test]
        public void GetChallengesByCategory_カテゴリ内のチャレンジが正しく取得できる()
        {
            var categoryGuid = new Guid("00000000-0000-0000-9999-000000000001");
            var challenges = _challengeMaster.GetChallengesByCategory(categoryGuid);
            
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
            var category = _challengeMaster.GetCategory(categoryGuid);
            
            Assert.IsNotNull(category);
            Assert.AreEqual("基本チュートリアル", category.CategoryName);
            Assert.AreEqual(1, category.DisplayOrder);
            Assert.IsTrue(category.InitialUnlocked);
        }
        
        [Test]
        public void GetInitialUnlockedCategories_初期アンロックカテゴリが正しく取得できる()
        {
            var initialCategories = _challengeMaster.GetInitialUnlockedCategories();
            
            // 基本チュートリアルのみが初期アンロック
            Assert.AreEqual(1, initialCategories.Count);
            Assert.AreEqual("基本チュートリアル", initialCategories[0].CategoryName);
        }
        
        [Test]
        public void GetChallenge_既存の実装との互換性を確認()
        {
            // 既存のGetChallengeメソッドが正しく動作することを確認
            var challengeGuid = new Guid("00000000-0000-0000-4567-000000000001");
            var challenge = _challengeMaster.GetChallenge(challengeGuid);
            
            Assert.IsNotNull(challenge);
            Assert.AreEqual("最初のアイテムを作る", challenge.Title);
            Assert.AreEqual("createItem", challenge.TaskCompletionType);
        }
        
        [Test]
        public void GetNextChallenges_カテゴリ内でのnextチャレンジが正しく取得できる()
        {
            // チャレンジ1の次のチャレンジを取得
            var challenge1Guid = new Guid("00000000-0000-0000-4567-000000000001");
            var nextChallenges = _challengeMaster.GetNextChallenges(challenge1Guid);
            
            // チャレンジ2が次のチャレンジとして設定されている
            Assert.AreEqual(1, nextChallenges.Count);
            Assert.AreEqual(new Guid("00000000-0000-0000-4567-000000000002"), nextChallenges[0].ChallengeGuid);
        }
        
        [Test]
        public void InitialChallenge_カテゴリ構造でも初期チャレンジが正しく取得できる()
        {
            // 初期チャレンジは前提チャレンジがないもの
            var initialChallenges = _challengeMaster.InitialChallenge;
            
            // チャレンジ1とチャレンジ3が初期チャレンジ（それぞれのカテゴリ内で）
            Assert.AreEqual(2, initialChallenges.Count);
            Assert.IsTrue(initialChallenges.Contains(new Guid("00000000-0000-0000-4567-000000000001")));
            Assert.IsTrue(initialChallenges.Contains(new Guid("00000000-0000-0000-4567-000000000003")));
        }
        
        [Test]
        public void GetCategoryOfChallenge_チャレンジが属するカテゴリを取得できる()
        {
            var challengeGuid = new Guid("00000000-0000-0000-4567-000000000001");
            var category = _challengeMaster.GetCategoryOfChallenge(challengeGuid);
            
            Assert.IsNotNull(category);
            Assert.AreEqual("基本チュートリアル", category.CategoryName);
        }
        
        [Test]
        public void ValidateChallengeReferences_カテゴリ間のチャレンジ参照がないことを確認()
        {
            // カテゴリを跨いだprevChallengeGuidsの参照がないことを検証
            var isValid = _challengeMaster.ValidateChallengeReferences();
            
            // このテストデータでは全てのチャレンジ参照が同一カテゴリ内なのでtrue
            Assert.IsTrue(isValid);
        }
        
        [Test]
        public void GetCategoriesDependingOn_カテゴリの依存関係が正しく取得できる()
        {
            var categoryGuid = new Guid("00000000-0000-0000-9999-000000000001");
            var dependentCategories = _challengeMaster.GetCategoriesDependingOn(categoryGuid);
            
            // 建築チャレンジが基本チュートリアルに依存している
            Assert.AreEqual(1, dependentCategories.Count);
            Assert.AreEqual("建築チャレンジ", dependentCategories[0].CategoryName);
        }
        
        [Test]
        public void EmptyCategory_空のカテゴリも正しく処理される()
        {
            var categoryGuid = new Guid("00000000-0000-0000-9999-000000000003");
            var challenges = _challengeMaster.GetChallengesByCategory(categoryGuid);
            
            // 上級チャレンジカテゴリは空
            Assert.AreEqual(0, challenges.Count);
        }
    }
}