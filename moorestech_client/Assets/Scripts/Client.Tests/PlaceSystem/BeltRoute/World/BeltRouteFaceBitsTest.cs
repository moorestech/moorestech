using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute.World
{
    // Markが辺の高さと2セルの両方へ面ビットを立てることを確認
    // Checks Mark raises face bits at the edge height on both cells
    public class BeltRouteFaceBitsTest
    {
        private static readonly Vector3Int Cell = new(5, 7, 9);

        [TestCase(BlockDirection.North, 0)]
        [TestCase(BlockDirection.East, 0)]
        [TestCase(BlockDirection.South, 0)]
        [TestCase(BlockDirection.West, 0)]
        [TestCase(BlockDirection.North, 1)]
        [TestCase(BlockDirection.East, 1)]
        [TestCase(BlockDirection.South, 1)]
        [TestCase(BlockDirection.West, 1)]
        public void EdgeOnAFaceRaisesTheBitOnBothTouchingCells(BlockDirection face, int heightOffset)
        {
            var bits = new Dictionary<Vector3Int, int>();
            BeltRouteFaceBits.Mark(BeltRouteEdge.FromFace(Cell, face, Cell.y + heightOffset), bits);

            var neighbour = Cell + BeltRouteDirection.Forward(face);
            Assert.AreEqual(BeltRouteFaceBits.FaceBit(face, heightOffset), bits[Cell] & BeltRouteFaceBits.FaceBit(face, heightOffset));
            Assert.AreEqual(BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(face), heightOffset), bits[neighbour] & BeltRouteFaceBits.FaceBit(BeltRouteDirection.Opposite(face), heightOffset));
            // 辺と違う段にはビットを立てない
            // No bit on levels differing from the edge
            var otherOffset = 1 - heightOffset;
            Assert.AreEqual(0, bits[Cell] & BeltRouteFaceBits.FaceBit(face, otherOffset));
        }
    }
}
