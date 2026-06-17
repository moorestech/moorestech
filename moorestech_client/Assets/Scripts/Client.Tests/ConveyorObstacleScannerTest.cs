using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class ConveyorObstacleScannerTest
    {
        private static readonly List<Vector3Int> FlatPath = new()
        {
            new Vector3Int(0, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(2, 0, 0),
        };

        [Test]
        public void NoObstacle_ReturnsBaseY()
        {
            var bounds = new ConveyorObstacleScanner().ComputeLowerBounds(FlatPath, _ => false);
            Assert.AreEqual(new[] { 0, 0, 0 }, bounds);
        }

        [Test]
        public void Height1Obstacle_RaisesByOne()
        {
            var occupied = new HashSet<Vector3Int> { new(1, 0, 0) };
            var bounds = new ConveyorObstacleScanner().ComputeLowerBounds(FlatPath, occupied.Contains);
            Assert.AreEqual(new[] { 0, 1, 0 }, bounds);
        }

        [Test]
        public void Height2Obstacle_RaisesByTwo()
        {
            var occupied = new HashSet<Vector3Int> { new(1, 0, 0), new(1, 1, 0) };
            var bounds = new ConveyorObstacleScanner().ComputeLowerBounds(FlatPath, occupied.Contains);
            Assert.AreEqual(new[] { 0, 2, 0 }, bounds);
        }

        [Test]
        public void FloatingBlockAboveFreeBase_DoesNotRaise()
        {
            var occupied = new HashSet<Vector3Int> { new(1, 2, 0) };
            var bounds = new ConveyorObstacleScanner().ComputeLowerBounds(FlatPath, occupied.Contains);
            Assert.AreEqual(new[] { 0, 0, 0 }, bounds);
        }
    }
}
