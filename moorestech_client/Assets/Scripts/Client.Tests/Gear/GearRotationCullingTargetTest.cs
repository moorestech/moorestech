using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block.GearRotation;
using Game.Block.Interface.State;
using Game.Gear.Common;
using MessagePack;
using Server.Event.EventReceive;
using Client.Game.InGame.BlockSystem.StateProcessor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

            target.SetWithinRange(false);
            Assert.IsFalse(processors[0].enabled);
            Assert.IsFalse(processors[1].enabled);
            target.SetWithinRange(true);
            Assert.IsTrue(processors[0].enabled);
            Assert.IsTrue(processors[1].enabled);
            Object.DestroyImmediate(root);
        }
        [Test]
        public void MissingAnimatorWarnsAndAllowsRangeChanges()
        {
            var root = new GameObject("gear without animator");
            try
            {
                var processor = root.AddComponent<GearStateChangeProcessor>();
                typeof(GearStateChangeProcessor).GetField("rotationInfos", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(processor, new List<RotationInfo> { new AnimatorRotationInfo() });
                var target = new GearRotationCullingTarget(new[] { processor });

                // 未設定参照でも停止と復帰を継続する
                // Continue stopping and resuming even with an unassigned reference
                const string warning = "AnimatorRotationInfo: Animator is not assigned; rotation enable change was skipped.";
                LogAssert.Expect(LogType.Warning, warning);
                Assert.DoesNotThrow(() => target.SetWithinRange(false));
                Assert.IsFalse(processor.enabled);
                LogAssert.Expect(LogType.Warning, warning);
                Assert.DoesNotThrow(() => target.SetWithinRange(true));
                Assert.IsTrue(processor.enabled);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AnimatorStopsOutsideRangeAndResumesWithLatestReceivedRpm()
        {
            var root = new GameObject("animated gear");
            try
            {
                var processor = root.AddComponent<GearStateChangeProcessor>();
                var animator = root.AddComponent<Animator>();
                var unrelated = new GameObject("unrelated animator");
                unrelated.transform.SetParent(root.transform);
                var unrelatedAnimator = unrelated.AddComponent<Animator>();
                var rotation = new AnimatorRotationInfo();
                typeof(AnimatorRotationInfo).GetField("animator", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rotation, animator);
                typeof(GearStateChangeProcessor).GetField("rotationInfos", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(processor, new List<RotationInfo> { rotation });
                var target = new GearRotationCullingTarget(new[] { processor });
                Send(60);
                processor.Rotate(new GearStateDetail(true, 60f, 0f, GearRole.Consumer), 0);
                Assert.AreEqual(1f, animator.speed);

                // 停止中も受信し、復帰前に最新RPMを当てる
                // Receive while paused and apply the latest RPM before resuming
                target.SetWithinRange(false);
                target.SetWithinRange(false);
                Assert.IsFalse(animator.enabled);
                Assert.IsTrue(unrelatedAnimator.enabled);
                Send(0);
                target.SetWithinRange(true);
                Assert.IsTrue(animator.enabled);
                Assert.AreEqual(0f, animator.speed);
                target.SetWithinRange(false);
                Send(120);
                target.SetWithinRange(true);
                Assert.AreEqual(2f, animator.speed);

                #region Internal

                void Send(float rpm)
                {
                    var detail = new GearStateDetail(true, rpm, 0f, GearRole.Consumer);
                    var state = new BlockState(new Dictionary<string, byte[]> { [GearStateDetail.BlockStateDetailKey] = MessagePackSerializer.Serialize(detail) });
                    processor.OnChangeState(new BlockStateMessagePack(state, Vector3Int.zero));
                }

                #endregion
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
