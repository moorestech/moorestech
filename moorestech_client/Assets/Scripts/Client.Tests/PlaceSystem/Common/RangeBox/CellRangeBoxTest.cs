using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common.RangeBox
{
    public class CellRangeBoxTest
    {
        [Test]
        public void Calcは対角2セルから成分ごとのminとmaxを返す()
        {
            var (min, max) = CellRangeBox.Calc(new Vector3Int(5, 2, -1), new Vector3Int(1, 4, 3));
            Assert.AreEqual(new Vector3Int(1, 2, -1), min);
            Assert.AreEqual(new Vector3Int(5, 4, 3), max);
        }

        [Test]
        public void ToWorldBoundsはセル境界を含む直方体になる()
        {
            var bounds = CellRangeBox.ToWorldBounds(new Vector3Int(1, 2, 3), new Vector3Int(2, 2, 5));
            Assert.AreEqual(new Vector3(2f, 2.5f, 4.5f), bounds.center);
            Assert.AreEqual(new Vector3(2f, 1f, 3f), bounds.size);
        }
    }
}
