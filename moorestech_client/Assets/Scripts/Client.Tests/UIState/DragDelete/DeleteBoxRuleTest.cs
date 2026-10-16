using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteBoxRuleTest
    {
        [Test]
        public void 点はminの面を含みmaxセルの外側面を含まない()
        {
            var min = new Vector3Int(0, 0, 0);
            var max = new Vector3Int(1, 1, 1);
            Assert.IsTrue(DeleteBoxRule.IsPointInBox(new Vector3(0f, 0f, 0f), min, max));
            Assert.IsTrue(DeleteBoxRule.IsPointInBox(new Vector3(1.99f, 1.5f, 0.5f), min, max));
            Assert.IsFalse(DeleteBoxRule.IsPointInBox(new Vector3(2f, 0.5f, 0.5f), min, max));
            Assert.IsFalse(DeleteBoxRule.IsPointInBox(new Vector3(-0.01f, 0.5f, 0.5f), min, max));
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
        }
    }
}
