using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteBoxRuleTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void 各軸は下限面を含み上限セル外側面を含まない(int axis)
        {
            var min = new Vector3Int(0, 0, 0);
            var max = new Vector3Int(1, 1, 1);
            var point = new Vector3(0.5f, 0.5f, 0.5f);
            point[axis] = -0.01f;
            Assert.IsFalse(DeleteBoxRule.IsPointInBox(point, min, max));
            point[axis] = 0f;
            Assert.IsTrue(DeleteBoxRule.IsPointInBox(point, min, max));
            point[axis] = 1.99f;
            Assert.IsTrue(DeleteBoxRule.IsPointInBox(point, min, max));
            point[axis] = 2f;
            Assert.IsFalse(DeleteBoxRule.IsPointInBox(point, min, max));
        }

        [Test]
        public void 曲線は1点でも箱内なら入り全点が外なら入らない()
        {
            var min = new Vector3Int(0, 0, 0);
            var max = new Vector3Int(0, 0, 0);
            var crossing = new List<Vector3> { new(-5f, 0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(5f, 0.5f, 0.5f) };
            var outside = new List<Vector3> { new(-5f, 0.5f, 0.5f), new(5f, 0.5f, 0.5f) };
            Assert.IsTrue(DeleteBoxRule.IsAnyPointInBox(crossing, min, max));
            Assert.IsFalse(DeleteBoxRule.IsAnyPointInBox(outside, min, max));
            var outsideY = new List<Vector3> { new(0.5f, -1f, 0.5f), new(0.5f, 1f, 0.5f) };
            var outsideZ = new List<Vector3> { new(0.5f, 0.5f, -1f), new(0.5f, 0.5f, 1f) };
            Assert.IsFalse(DeleteBoxRule.IsAnyPointInBox(outsideY, min, max));
            Assert.IsFalse(DeleteBoxRule.IsAnyPointInBox(outsideZ, min, max));
        }
    }
}
