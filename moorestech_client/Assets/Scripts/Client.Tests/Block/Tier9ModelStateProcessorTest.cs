using System.Collections.Generic;
using System.Collections;
using UnityEngine.TestTools;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.Rocket;
using Client.Game.InGame.BlockSystem.StateProcessor.SpaceElevator;
using Client.Tests.Common;
using Core.Master;
using Game.Block.Interface.State;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Block
{
    public class Tier9ModelStateProcessorTest
    {
        private GameObject _root;
        [SetUp] public void Setup()
        {
            _root = new GameObject("tier9-model");
            _root.transform.localScale = new Vector3(2, 3, 4);
            _root.transform.rotation = Quaternion.Euler(15, 30, 20);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(_root);

        [Test]
        public void ElevatorReflectsConnectionAndReceivedProgress()
        {
            var cable = Child("cable");
            var climber = Child("climber").transform;
            var processor = _root.AddComponent<SpaceElevatorBaseStateChangeProcessor>();
            var bottom = new Vector3(1, 2, 3);
            var top = new Vector3(1, 12, 3);
            TestReflection.SetField(processor, "cable", cable);
            TestReflection.SetField(processor, "climber", climber);
            TestReflection.SetField(processor, "climberBottomLocalPosition", bottom);
            TestReflection.SetField(processor, "climberTopLocalPosition", top);
            processor.Initialize(_root.AddComponent<BlockGameObject>());
            Assert.IsFalse(cable.activeSelf);
            Assert.IsFalse(climber.gameObject.activeSelf);

            // 実状態の復号で下端・中点・上端を観測する
            // Observe bottom, midpoint and top after decoding real state messages
            foreach (var progress in new[] { 0f, 0.5f, 1f })
            {
                processor.OnChangeState(State(SpaceElevatorBaseStateDetail.BlockStateDetailKey,
                    new SpaceElevatorBaseStateDetail(true, 0, progress, SpaceElevatorBaseStatus.Climbing)));
                Assert.IsTrue(cable.activeSelf);
                Assert.IsTrue(climber.gameObject.activeSelf);
                Assert.That(Vector3.Distance(Vector3.Lerp(bottom, top, progress), climber.localPosition), Is.LessThan(0.0001f));
            }
            processor.OnChangeState(State(SpaceElevatorBaseStateDetail.BlockStateDetailKey,
                new SpaceElevatorBaseStateDetail(false, 0, 0, SpaceElevatorBaseStatus.ReadyToConnect)));
            Assert.IsFalse(cable.activeSelf);
            Assert.IsFalse(climber.gameObject.activeSelf);
            Assert.AreEqual(bottom, climber.localPosition);
        }

        [UnityTest]
        public IEnumerator RocketReflectsEveryPhaseAndRisesInWorldSpace()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var block = _root.AddComponent<BlockGameObject>();
            TestReflection.SetField(block, "<BlockMasterElement>k__BackingField", MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId));
            var body = Child("body").transform;
            body.localPosition = new Vector3(1, 2, 3);
            var rest = body.localPosition;
            var origin = body.position;
            var processor = _root.AddComponent<RocketLaunchPadStateChangeProcessor>();
            TestReflection.SetField(processor, "body", body);
            TestReflection.SetField(processor, "riseHeight", 80f);
            processor.Initialize(block);
            Assert.IsFalse(body.gameObject.activeSelf);

            // 各段階の可視性を受信状態から切り替える
            // Switch visibility through received state for every phase
            foreach (RocketLaunchPadPhase phase in System.Enum.GetValues(typeof(RocketLaunchPadPhase)))
            {
                Apply(phase, 0);
                Assert.AreEqual(phase is RocketLaunchPadPhase.AwaitingLaunchCommand or RocketLaunchPadPhase.ReadyToLaunch or RocketLaunchPadPhase.Launching,
                    body.gameObject.activeSelf, phase.ToString());
                Assert.That(Vector3.Distance(rest, body.localPosition), Is.LessThan(0.0001f));
            }
            Apply(RocketLaunchPadPhase.Idle, 0);
            // 外挿後の遅延通知でも下降させない
            // Exercise mid-launch initialization, delayed notifications after extrapolation and forward correction
            Apply(RocketLaunchPadPhase.Launching, 0.5f);
            var middle = body.position;
            yield return null;
            TestReflection.InvokePrivate(processor, "Update");
            var extrapolated = body.position;
            Assert.Greater(extrapolated.y, middle.y);
            Apply(RocketLaunchPadPhase.Launching, 0.5f);
            Assert.That(Vector3.Distance(extrapolated, body.position), Is.LessThan(0.0001f));
            Apply(RocketLaunchPadPhase.Launching, 0.4f);
            Assert.That(Vector3.Distance(extrapolated, body.position), Is.LessThan(0.0001f));
            Apply(RocketLaunchPadPhase.Launching, 0.9f);
            Assert.That(Vector3.Distance(origin + Vector3.up * (80f * 0.9f * 0.9f), body.position), Is.LessThan(0.0001f));
            Apply(RocketLaunchPadPhase.ReadyToLaunch, 1);
            Assert.That(Vector3.Distance(rest, body.localPosition), Is.LessThan(0.0001f));
            foreach (var progress in new[] { 0f, 0.5f, 1f })
            {
                Apply(RocketLaunchPadPhase.Launching, progress);
                Assert.That(Vector3.Distance(origin + Vector3.up * (80f * progress * progress), body.position), Is.LessThan(0.0001f));
            }
            Apply(RocketLaunchPadPhase.Idle, 0);
            Assert.AreEqual(rest, body.localPosition);
            Assert.IsFalse(body.gameObject.activeSelf);
            Apply(RocketLaunchPadPhase.Launching, 0);
            Assert.That(Vector3.Distance(origin, body.position), Is.LessThan(0.0001f));

            #region Internal
            void Apply(RocketLaunchPadPhase phase, float progress)
            {
                processor.OnChangeState(State(RocketLaunchPadStateDetail.BlockStateDetailKey,
                    new RocketLaunchPadStateDetail(phase, RocketLaunchPadWaitReason.None, progress)));
            }
            #endregion
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        private static BlockStateMessagePack State<T>(string key, T detail)
        {
            return new BlockStateMessagePack(new BlockState(new Dictionary<string, byte[]> { [key] = MessagePackSerializer.Serialize(detail) }), Vector3Int.zero);
        }
    }
}
