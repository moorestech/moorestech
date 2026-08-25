using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// Invalidプレビューが常に無効かつ設置不可であることのテスト
    /// Tests that the Invalid preview is always invalid and never placeable
    /// </summary>
    public class TrainRailConnectPreviewDataInvalidTest
    {
        [Test]
        // Invalidは無効かつ設置不可で、原点に橋脚プレビューが出ないことを保証する
        // Invalid must be invalid and not placeable, so no pier preview appears at the origin
        public void Invalidは無効かつ設置不可である()
        {
            var invalid = TrainRailConnectPreviewData.Invalid;

            Assert.IsFalse(invalid.IsValid);
            Assert.IsFalse(invalid.IsPlaceable);
            Assert.IsFalse(invalid.IsCurvePlaceable);
            Assert.AreNotEqual(RailConnectionEditProtocol.RailConnectionEditFailureReason.None, invalid.FailureReason);
            Assert.AreEqual(Guid.Empty, invalid.RailTypeGuid);
            Assert.AreEqual(Vector3.zero, invalid.StartPoint);
        }
    }
}
