using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block.GearRotation;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Block.Interface.State;
using Game.Gear.Common;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class GearRotationUpdateListTest
    {
        private readonly List<GameObject> _owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _owned) Object.DestroyImmediate(go);
            _owned.Clear();
        }

        [Test]
        public void 一覧に残った歯車だけが回り途中を外しても残りは回り続ける()
        {
            // 真ん中を外すと末尾が入れ替わるので、入れ替わった歯車も回り続けることを確かめる
            // Removing the middle swaps in the last one; verify the swapped gear keeps rotating too
            var (first, firstPart) = CreateGear(60f);
            var (middle, middlePart) = CreateGear(60f);
            var (last, lastPart) = CreateGear(60f);
            GearRotationUpdateList.Add(first);
            GearRotationUpdateList.Add(middle);
            GearRotationUpdateList.Add(last);
            GearRotationUpdateList.Remove(middle);

            var before = new[] { firstPart.localRotation, middlePart.localRotation, lastPart.localRotation };
            GearRotationUpdateList.AdvanceAll(1f / 60f);
            GearRotationUpdateList.Remove(first);
            GearRotationUpdateList.Remove(last);

            Assert.Greater(Quaternion.Angle(before[0], firstPart.localRotation), 0.1f);
            Assert.AreEqual(0f, Quaternion.Angle(before[1], middlePart.localRotation), 0.001f);
            Assert.Greater(Quaternion.Angle(before[2], lastPart.localRotation), 0.1f);
        }

        [Test]
        public void 二重に追加しても1回分しか回らない()
        {
            var (processor, part) = CreateGear(60f);
            GearRotationUpdateList.Add(processor);
            GearRotationUpdateList.Add(processor);

            var before = part.localRotation;
            GearRotationUpdateList.AdvanceAll(1f / 60f);
            GearRotationUpdateList.Remove(processor);

            // 60RPMを1/60秒で6度。二重に回れば12度になる
            // 60 RPM over 1/60 s is 6 degrees; a double rotation would be 12
            Assert.AreEqual(6f, Quaternion.Angle(before, part.localRotation), 0.01f);
        }

        [Test]
        public void RPM0の歯車はTransformに書かない()
        {
            var (processor, part) = CreateGear(0f);
            part.hasChanged = false;

            processor.AdvanceRotation(1f / 60f);

            Assert.IsFalse(part.hasChanged);
        }

        private (GearStateChangeProcessor processor, Transform part) CreateGear(float rpm)
        {
            var root = new GameObject("gear");
            _owned.Add(root);
            var part = new GameObject("part").transform;
            part.SetParent(root.transform, false);
            var processor = root.AddComponent<GearStateChangeProcessor>();
            typeof(GearStateChangeProcessor).GetField("rotationInfos", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(processor, new List<RotationInfo> { new TransformRotationInfo(RotationAxis.Z, part, 1f, false, GearRotationDirectionMode.AlwaysForward) });

            // 受信と同じ経路で状態を渡す
            // Hand over the state through the same path as reception
            var detail = new GearStateDetail(true, rpm, 0f, GearRole.Consumer);
            var state = new BlockState(new Dictionary<string, byte[]> { [GearStateDetail.BlockStateDetailKey] = MessagePackSerializer.Serialize(detail) });
            processor.OnChangeState(new BlockStateMessagePack(state, Vector3Int.zero));
            return (processor, part);
        }
    }
}
