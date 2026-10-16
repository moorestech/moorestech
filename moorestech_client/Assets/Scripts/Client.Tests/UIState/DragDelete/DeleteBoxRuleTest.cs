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

    }
}
