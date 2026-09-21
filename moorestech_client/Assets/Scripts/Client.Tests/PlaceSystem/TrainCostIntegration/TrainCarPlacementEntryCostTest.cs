using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar;
using Client.Game.InGame.Train.View.Object.Material;
using Client.Game.InGame.Train.View.Object.Pose;
using Client.Tests.Common;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.TrainCostIntegration
{
    public class TrainCarPlacementEntryCostTest : TrainPlacementEntryFixture
    {
        private static readonly Guid CarGuid = Guid.Parse("dc82cf3f-709d-49eb-bdb2-67ffcaff561b");
        private TrainCarPreviewController _preview;
        private TrainCarMaterialController _material;

        [TestCase(TrainCarPlacementMode.CreateNewTrainUnit)]
        [TestCase(TrainCarPlacementMode.AttachToExistingTrainUnit)]
        public void 素材不足のクリックは不可色と不足行を出して要求を送らない(TrainCarPlacementMode mode)
        {
            var hit = BuildHit(mode);
            PreparePreview();
            var system = new TrainCarPlaceSystem(new FixedPlacementDetector(hit), _preview, null, null, Inventory);
            var feedback = new PlacementFeedback();
            system.Enable();
            ClickRelease();

            // helperではなく選択ターゲットを受ける公開入口を駆動する
            // Drive the public selected-target entry point rather than the cost helper
            system.ManualUpdate(new PlaceSystemUpdateContext(new TrainCarPlacementTarget(CarGuid), true, feedback));

            Assert.IsTrue(_preview.gameObject.activeSelf, "実プレビューの姿勢解決まで進んでいない");
            Assert.AreEqual(TrainCarVisualMaterialMode.PlacementPreviewNotPlaceable, TestReflection.GetField<TrainCarVisualMaterialMode>(_material, "_baseMaterialMode"));
            Assert.AreEqual(2, feedback.Lines.Count, "車両の不足素材2種類が表示されない");
            AssertNoRequest();
        }

        public override void TearDown()
        {
            // EditModeではfixture側が実オブジェクトを即時破棄する
            // Let the fixture destroy the real objects immediately in EditMode
            if (_preview != null) TestReflection.SetField(_preview, "_previewObject", null);
            _material?.DestroyRuntimeMaterials();
            base.TearDown();
        }

        private void PreparePreview()
        {
            _preview = CreateObject("CarPreview").AddComponent<TrainCarPreviewController>();
            var model = CreateObject("CarModel");
            var poseUpdater = model.AddComponent<TrainCarRailPositionVisualPoseUpdater>();
            _material = new TrainCarMaterialController(model);

            // Addressableロード済みと同じ状態へ配線し、本番ShowPreviewを通す
            // Wire the already-loaded state and run the production ShowPreview implementation
            TestReflection.SetField(_preview, "_previewObject", model);
            TestReflection.SetField(_preview, "_currentTrainCarGuid", CarGuid);
            TestReflection.SetField(_preview, "_poseUpdater", poseUpdater);
            TestReflection.SetField(_preview, "_materialController", _material);
        }

        private static TrainCarPlacementHit BuildHit(TrainCarPlacementMode mode)
        {
            var head = new PlacementTestRailNode(0, new Vector3(0, 0, 20));
            var rear = new PlacementTestRailNode(1, Vector3.zero);
            var length = TrainLengthConverter.ToRailUnits(20);
            rear.ConnectTo(head, length);
            var position = new RailPosition(new List<IRailNode> { head, rear }, length, 0);
            return new TrainCarPlacementHit(Vector3.zero, position, Array.Empty<TrainUnitInstanceId>(), mode,
                new TrainUnitInstanceId(Guid.NewGuid()), true, TrainCarAttachTargetEndpoint.Head, TrainCarPlacementBlockReason.None);
        }

        private sealed class FixedPlacementDetector : ITrainCarPlacementDetector
        {
            private readonly TrainCarPlacementHit _hit;

            internal FixedPlacementDetector(TrainCarPlacementHit hit)
            {
                _hit = hit;
            }

            public bool TryDetect(Guid trainCarGuid, out TrainCarPlacementHit hit)
            {
                hit = _hit;
                return true;
            }

            public void AdvanceSelection() { }
            public void ResetSelection() { }
        }
    }
}
