using Client.Game.InGame.Block.GearRotation;
using Client.Game.InGame.BlockSystem.StateProcessor;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class GearRotationCullingTargetTest
    {
        [Test]
        public void OutsideRangeDisablesAndInsideRangeRestoresEveryGearProcessor()
        {
            var root = new GameObject("gear");
            var processors = new[] { root.AddComponent<GearStateChangeProcessor>(), new GameObject("child").AddComponent<GearStateChangeProcessor>() };
            processors[1].transform.SetParent(root.transform);
            var target = new GearRotationCullingTarget(processors);

            // 範囲外で全停止し、範囲内で全再開する
            // Every processor stops outside the range and resumes inside it
            target.SetWithinRange(false);
            Assert.IsFalse(processors[0].enabled);
            Assert.IsFalse(processors[1].enabled);
            target.SetWithinRange(true);
            Assert.IsTrue(processors[0].enabled);
            Assert.IsTrue(processors[1].enabled);
            Object.DestroyImmediate(root);
        }
    }
}
