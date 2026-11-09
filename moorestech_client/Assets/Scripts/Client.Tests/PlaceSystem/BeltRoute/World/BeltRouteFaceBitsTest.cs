using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute.World
{
    // 辺から面ビットを立てる1本（BeltRouteFaceBits.Mark）が、辺の高さ h ↔ セル c のビット(face, k) は h == c.y + k、かつ辺に接する2セルの両方、になることを確かめる
    // Checks the single face-bit raiser (BeltRouteFaceBits.Mark): an edge at height h sets bit (face, k) on cell c iff h == c.y + k, on both cells touching the edge
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
            // 辺の高さと違う段にはこの面のビットは立たない
            // No bit for this face on the level that does not match the edge height
            var otherOffset = 1 - heightOffset;
            Assert.AreEqual(0, bits[Cell] & BeltRouteFaceBits.FaceBit(face, otherOffset));
        }
    }
}
