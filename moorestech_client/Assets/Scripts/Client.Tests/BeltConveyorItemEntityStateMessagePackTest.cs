using System;
using Core.Master;
using Game.Entity.Interface;
using MessagePack;
using NUnit.Framework;

namespace Client.Tests
{
    /// <summary>
    /// BeltConveyorItemEntityStateMessagePackのシリアライズ/デシリアライズをテスト
    /// Test serialization/deserialization of BeltConveyorItemEntityStateMessagePack
    /// </summary>
    public class BeltConveyorItemEntityStateMessagePackTest
    {
        /// <summary>
        /// Guid付きでシリアライズ/デシリアライズが往復できることをテスト
        /// Test that serialization/deserialization round-trip works with Guid
        /// </summary>
        [Test]
        public void RoundTripWithGuidTest()
        {
            // テストデータを作成
            // Create test data
            var sourceGuid = Guid.NewGuid();
            var goalGuid = Guid.NewGuid();
            var original = new BeltConveyorItemEntityStateMessagePack
            {
                ItemId = 123,
                Count = 5,
                SourceConnectorGuid = sourceGuid,
                GoalConnectorGuid = goalGuid
            };

            // シリアライズ
            // Serialize
            var bytes = MessagePackSerializer.Serialize(original);

            // デシリアライズ
            // Deserialize
            var deserialized = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(bytes);

            // 検証
            // Verify
            Assert.AreEqual(original.ItemId, deserialized.ItemId);
            Assert.AreEqual(original.Count, deserialized.Count);
            Assert.IsNotNull(deserialized.SourceConnectorGuid, "SourceConnectorGuidはnullであるべきではない / SourceConnectorGuid should not be null");
            Assert.IsNotNull(deserialized.GoalConnectorGuid, "GoalConnectorGuidはnullであるべきではない / GoalConnectorGuid should not be null");
            Assert.AreEqual(sourceGuid, deserialized.SourceConnectorGuid.Value, "SourceConnectorGuidが一致すべき / SourceConnectorGuid should match");
            Assert.AreEqual(goalGuid, deserialized.GoalConnectorGuid.Value, "GoalConnectorGuidが一致すべき / GoalConnectorGuid should match");
        }

        /// <summary>
        /// nullのGuidでシリアライズ/デシリアライズが往復できることをテスト
        /// Test that serialization/deserialization round-trip works with null Guid
        /// </summary>
        [Test]
        public void RoundTripWithNullGuidTest()
        {
            // テストデータを作成（Guidはnull）
            // Create test data (Guid is null)
            var original = new BeltConveyorItemEntityStateMessagePack
            {
                ItemId = 456,
                Count = 10,
                SourceConnectorGuid = null,
                GoalConnectorGuid = null
            };

            // シリアライズ
            // Serialize
            var bytes = MessagePackSerializer.Serialize(original);

            // デシリアライズ
            // Deserialize
            var deserialized = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(bytes);

            // 検証
            // Verify
            Assert.AreEqual(original.ItemId, deserialized.ItemId);
            Assert.AreEqual(original.Count, deserialized.Count);
            Assert.IsNull(deserialized.SourceConnectorGuid, "SourceConnectorGuidはnullであるべき / SourceConnectorGuid should be null");
            Assert.IsNull(deserialized.GoalConnectorGuid, "GoalConnectorGuidはnullであるべき / GoalConnectorGuid should be null");
        }

        /// <summary>
        /// 片方のGuidだけがnullの場合のテスト
        /// Test when only one Guid is null
        /// </summary>
        [Test]
        public void RoundTripWithPartialNullGuidTest()
        {
            // SourceGuidのみ設定
            // Only SourceGuid is set
            var sourceGuid = Guid.NewGuid();
            var original = new BeltConveyorItemEntityStateMessagePack
            {
                ItemId = 789,
                Count = 1,
                SourceConnectorGuid = sourceGuid,
                GoalConnectorGuid = null
            };

            // シリアライズ
            // Serialize
            var bytes = MessagePackSerializer.Serialize(original);

            // デシリアライズ
            // Deserialize
            var deserialized = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(bytes);

            // 検証
            // Verify
            Assert.AreEqual(original.ItemId, deserialized.ItemId);
            Assert.AreEqual(original.Count, deserialized.Count);
            Assert.IsNotNull(deserialized.SourceConnectorGuid, "SourceConnectorGuidはnullであるべきではない / SourceConnectorGuid should not be null");
            Assert.AreEqual(sourceGuid, deserialized.SourceConnectorGuid.Value);
            Assert.IsNull(deserialized.GoalConnectorGuid, "GoalConnectorGuidはnullであるべき / GoalConnectorGuid should be null");
        }

        /// <summary>
        /// コンストラクタを使用した場合のシリアライズ/デシリアライズをテスト
        /// Test serialization/deserialization when using constructor
        /// </summary>
        [Test]
        public void RoundTripWithConstructorTest()
        {
            // コンストラクタを使用してテストデータを作成
            // Create test data using constructor
            var sourceGuid = Guid.NewGuid();
            var goalGuid = Guid.NewGuid();
            var original = new BeltConveyorItemEntityStateMessagePack(
                new ItemId(100),
                3,
                sourceGuid,
                goalGuid
            );

            // シリアライズ
            // Serialize
            var bytes = MessagePackSerializer.Serialize(original);

            // デシリアライズ
            // Deserialize
            var deserialized = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(bytes);

            // 検証
            // Verify
            Assert.AreEqual(100, deserialized.ItemId);
            Assert.AreEqual(3, deserialized.Count);
            Assert.IsNotNull(deserialized.SourceConnectorGuid);
            Assert.IsNotNull(deserialized.GoalConnectorGuid);
            Assert.AreEqual(sourceGuid, deserialized.SourceConnectorGuid.Value);
            Assert.AreEqual(goalGuid, deserialized.GoalConnectorGuid.Value);
        }
    }
}
