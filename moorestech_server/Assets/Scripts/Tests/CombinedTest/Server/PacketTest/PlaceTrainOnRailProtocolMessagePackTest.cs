using System;
using System.Linq;
using System.Reflection;
using Core.Master;
using MessagePack;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlaceTrainOnRailProtocolMessagePackTest
    {
        [Test]
        public void RequestMessagePack_HasExpectedStructure()
        {
            // MessagePack属性の存在確認
            // Verify presence of MessagePack attribute
            var attribute = typeof(PlaceTrainOnRailRequestMessagePack).GetCustomAttribute<MessagePackObjectAttribute>();
            Assert.IsNotNull(attribute, "RequestMessagePackに[MessagePackObject]が付きません");

            // プロパティのKey割り当て確認
            // Verify key assignments for properties
            var railSpecifierProperty = typeof(PlaceTrainOnRailRequestMessagePack).GetProperty("RailSpecifier");
            var hotBarSlotProperty = typeof(PlaceTrainOnRailRequestMessagePack).GetProperty("HotBarSlot");
            var playerIdProperty = typeof(PlaceTrainOnRailRequestMessagePack).GetProperty("PlayerId");

            AssertKey(railSpecifierProperty, 2);
            Assert.AreEqual(typeof(RailComponentSpecifier), railSpecifierProperty!.PropertyType);
            AssertKey(hotBarSlotProperty, 3);
            Assert.AreEqual(typeof(int), hotBarSlotProperty!.PropertyType);
            AssertKey(playerIdProperty, 4);
            Assert.AreEqual(typeof(int), playerIdProperty!.PropertyType);

            // コンストラクタの動作検証
            // Verify constructor behaviour
            var railSpecifier = RailComponentSpecifier.CreateRailSpecifier(Vector3Int.zero);
            const int hotBarSlot = 0;
            const int playerId = 77;
            var request = new PlaceTrainOnRailRequestMessagePack(railSpecifier, hotBarSlot, playerId);

            Assert.AreEqual(PlaceTrainCarOnRailProtocol.ProtocolTag, request.Tag);
            Assert.AreEqual(railSpecifier, request.RailSpecifier);
            Assert.AreEqual(hotBarSlot, request.HotBarSlot);
            Assert.AreEqual(playerId, request.PlayerId);

            #region Internal

            void AssertKey(PropertyInfo property, int expectedKey)
            {
                // Key属性と値をチェック
                // Check Key attribute and value
                var keyAttribute = property?.GetCustomAttribute<KeyAttribute>();
                Assert.IsNotNull(keyAttribute, $"{property?.Name} に [Key] がありません");
                Assert.AreEqual(expectedKey, keyAttribute!.IntKey);
            }

            #endregion
        }

        [Test]
        public void ResponseMessagePack_HasExpectedStructure()
        {
            // MessagePack属性の存在確認
            // Verify presence of MessagePack attribute
            var attribute = typeof(PlaceTrainOnRailResponseMessagePack).GetCustomAttribute<MessagePackObjectAttribute>();
            Assert.IsNotNull(attribute, "ResponseMessagePackに[MessagePackObject]が付きません");

            // プロパティのKey割り当て確認
            // Verify key assignments for properties
            AssertKey(typeof(PlaceTrainOnRailResponseMessagePack).GetProperty("IsSuccess"), 2);
            AssertKey(typeof(PlaceTrainOnRailResponseMessagePack).GetProperty("ErrorMessage"), 3);
            AssertKey(typeof(PlaceTrainOnRailResponseMessagePack).GetProperty("TrainIdStr"), 4);

            // 正常系レスポンスの確認
            // Verify successful response behaviour
            var trainId = Guid.NewGuid();
            var successResponse = new PlaceTrainOnRailResponseMessagePack(true, trainId, null);
            Assert.IsTrue(successResponse.IsSuccess);
            Assert.AreEqual(trainId.ToString(), successResponse.TrainIdStr);
            Assert.AreEqual(trainId, successResponse.TrainId);
            Assert.IsNull(successResponse.ErrorMessage);

            // エラー系レスポンスの確認
            // Verify error response behaviour
            const string errorMessage = "invalid rail";
            var errorResponse = new PlaceTrainOnRailResponseMessagePack(false, null, errorMessage);
            Assert.IsFalse(errorResponse.IsSuccess);
            Assert.AreEqual(errorMessage, errorResponse.ErrorMessage);
            Assert.IsNull(errorResponse.TrainIdStr);
            Assert.IsNull(errorResponse.TrainId);

            #region Internal

            void AssertKey(PropertyInfo property, int expectedKey)
            {
                // Key属性と値をチェック
                // Check Key attribute and value
                var keyAttribute = property?.GetCustomAttribute<KeyAttribute>();
                Assert.IsNotNull(keyAttribute, $"{property?.Name} に [Key] がありません");
                Assert.AreEqual(expectedKey, keyAttribute!.IntKey);
            }

            #endregion
        }
    }
}

