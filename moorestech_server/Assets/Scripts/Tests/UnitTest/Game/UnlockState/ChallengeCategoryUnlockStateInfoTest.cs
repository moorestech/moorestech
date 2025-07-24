using System;
using Game.UnlockState.States;
using NUnit.Framework;

namespace Tests.UnitTest.Game.UnlockState
{
    public class ChallengeCategoryUnlockStateInfoTest
    {
        private const string TestCategoryGuid = "00000000-0000-0000-9999-000000000001";
        
        [Test]
        public void Constructor_初期値が正しく設定される()
        {
            // 初期アンロック状態がtrueの場合
            var unlockedCategory = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                true
            );
            
            Assert.AreEqual(Guid.Parse(TestCategoryGuid), unlockedCategory.CategoryGuid);
            Assert.IsTrue(unlockedCategory.IsUnlocked);
            
            // 初期アンロック状態がfalseの場合
            var lockedCategory = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                false
            );
            
            Assert.AreEqual(Guid.Parse(TestCategoryGuid), lockedCategory.CategoryGuid);
            Assert.IsFalse(lockedCategory.IsUnlocked);
        }
        
        [Test]
        public void Unlock_ロック状態からアンロック状態に変更できる()
        {
            // 初期状態でロックされているカテゴリを作成
            var category = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                false
            );
            
            // ロック状態を確認
            Assert.IsFalse(category.IsUnlocked);
            
            // アンロック
            category.Unlock();
            
            // アンロック状態を確認
            Assert.IsTrue(category.IsUnlocked);
        }
        
        [Test]
        public void Unlock_既にアンロック済みでも問題なく動作する()
        {
            // 初期状態でアンロックされているカテゴリを作成
            var category = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                true
            );
            
            // アンロック状態を確認
            Assert.IsTrue(category.IsUnlocked);
            
            // 再度アンロック
            category.Unlock();
            
            // アンロック状態が維持されることを確認
            Assert.IsTrue(category.IsUnlocked);
        }
        
        [Test]
        public void GetProgress_カテゴリ内のチャレンジ進捗を正しく計算する()
        {
            // カテゴリの進捗計算機能をテスト
            var category = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                true
            );
            
            // 全チャレンジ数と完了チャレンジ数を設定してテスト
            var progress = category.GetProgress(10, 3); // 10個中3個完了
            Assert.AreEqual(0.3f, progress, 0.001f);
            
            // 全チャレンジ完了の場合
            progress = category.GetProgress(5, 5);
            Assert.AreEqual(1.0f, progress, 0.001f);
            
            // チャレンジが0個の場合（空のカテゴリ）
            progress = category.GetProgress(0, 0);
            Assert.AreEqual(1.0f, progress, 0.001f); // 空のカテゴリは100%完了扱い
        }
        
        [Test]
        public void JsonSerialization_シリアライズとデシリアライズが正しく動作する()
        {
            // アンロック済みカテゴリのシリアライズ
            var originalUnlocked = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                true
            );
            
            var jsonObject = new ChallengeCategoryUnlockStateInfoJsonObject(originalUnlocked);
            Assert.AreEqual(TestCategoryGuid, jsonObject.CategoryGuid);
            Assert.IsTrue(jsonObject.IsUnlocked);
            
            // デシリアライズ
            var deserializedUnlocked = new ChallengeCategoryUnlockStateInfo(jsonObject);
            Assert.AreEqual(originalUnlocked.CategoryGuid, deserializedUnlocked.CategoryGuid);
            Assert.AreEqual(originalUnlocked.IsUnlocked, deserializedUnlocked.IsUnlocked);
            
            // ロック状態のカテゴリのシリアライズ
            var originalLocked = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                false
            );
            
            jsonObject = new ChallengeCategoryUnlockStateInfoJsonObject(originalLocked);
            Assert.AreEqual(TestCategoryGuid, jsonObject.CategoryGuid);
            Assert.IsFalse(jsonObject.IsUnlocked);
            
            // デシリアライズ
            var deserializedLocked = new ChallengeCategoryUnlockStateInfo(jsonObject);
            Assert.AreEqual(originalLocked.CategoryGuid, deserializedLocked.CategoryGuid);
            Assert.AreEqual(originalLocked.IsUnlocked, deserializedLocked.IsUnlocked);
        }
        
        [Test]
        public void IsCompleted_カテゴリ完了状態を正しく判定する()
        {
            var category = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                true
            );
            
            // 未完了の場合
            Assert.IsFalse(category.IsCompleted(10, 3)); // 10個中3個完了
            
            // 完了の場合
            Assert.IsTrue(category.IsCompleted(10, 10)); // 全て完了
            
            // 空のカテゴリの場合
            Assert.IsTrue(category.IsCompleted(0, 0)); // 空のカテゴリは完了扱い
        }
        
        [Test]
        public void CanUnlockDependentCategories_依存カテゴリのアンロック可否を判定する()
        {
            var category = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                true
            );
            
            // 必要な完了率を設定（例: 80%）
            var requiredCompletionRate = 0.8f;
            
            // 完了率が不足している場合
            Assert.IsFalse(category.CanUnlockDependentCategories(10, 7, requiredCompletionRate)); // 70%完了
            
            // 完了率が十分な場合
            Assert.IsTrue(category.CanUnlockDependentCategories(10, 8, requiredCompletionRate)); // 80%完了
            Assert.IsTrue(category.CanUnlockDependentCategories(10, 10, requiredCompletionRate)); // 100%完了
        }
        
        [Test]
        public void GetRemainingChallengesCount_残りチャレンジ数を正しく計算する()
        {
            var category = new ChallengeCategoryUnlockStateInfo(
                Guid.Parse(TestCategoryGuid), 
                true
            );
            
            // 残りチャレンジ数の計算
            Assert.AreEqual(7, category.GetRemainingChallengesCount(10, 3)); // 10個中3個完了
            Assert.AreEqual(0, category.GetRemainingChallengesCount(5, 5)); // 全て完了
            Assert.AreEqual(0, category.GetRemainingChallengesCount(0, 0)); // 空のカテゴリ
        }
    }
    
    /// <summary>
    /// ChallengeCategoryUnlockStateInfoクラスの仮実装
    /// 実際の実装では、このクラスは別ファイルに配置される
    /// </summary>
    public class ChallengeCategoryUnlockStateInfo
    {
        public Guid CategoryGuid { get; }
        public bool IsUnlocked { get; private set; }
        
        public ChallengeCategoryUnlockStateInfo(Guid categoryGuid, bool initialUnlocked)
        {
            CategoryGuid = categoryGuid;
            IsUnlocked = initialUnlocked;
        }
        
        public ChallengeCategoryUnlockStateInfo(ChallengeCategoryUnlockStateInfoJsonObject jsonObject)
        {
            CategoryGuid = Guid.Parse(jsonObject.CategoryGuid);
            IsUnlocked = jsonObject.IsUnlocked;
        }
        
        public void Unlock()
        {
            IsUnlocked = true;
        }
        
        // カテゴリの進捗率を取得（0.0〜1.0）
        public float GetProgress(int totalChallenges, int completedChallenges)
        {
            if (totalChallenges == 0) return 1.0f; // 空のカテゴリは100%完了扱い
            return (float)completedChallenges / totalChallenges;
        }
        
        // カテゴリが完了しているかどうか
        public bool IsCompleted(int totalChallenges, int completedChallenges)
        {
            return GetProgress(totalChallenges, completedChallenges) >= 1.0f;
        }
        
        // 依存カテゴリをアンロック可能かどうか
        public bool CanUnlockDependentCategories(int totalChallenges, int completedChallenges, float requiredCompletionRate)
        {
            return GetProgress(totalChallenges, completedChallenges) >= requiredCompletionRate;
        }
        
        // 残りのチャレンジ数を取得
        public int GetRemainingChallengesCount(int totalChallenges, int completedChallenges)
        {
            return Math.Max(0, totalChallenges - completedChallenges);
        }
    }
    
    /// <summary>
    /// JSON シリアライズ用のクラス
    /// </summary>
    public class ChallengeCategoryUnlockStateInfoJsonObject
    {
        public string CategoryGuid { get; set; }
        public bool IsUnlocked { get; set; }
        
        public ChallengeCategoryUnlockStateInfoJsonObject(ChallengeCategoryUnlockStateInfo info)
        {
            CategoryGuid = info.CategoryGuid.ToString();
            IsUnlocked = info.IsUnlocked;
        }
        
        // JSONデシリアライズ用のパラメータなしコンストラクタ
        public ChallengeCategoryUnlockStateInfoJsonObject() { }
    }
}