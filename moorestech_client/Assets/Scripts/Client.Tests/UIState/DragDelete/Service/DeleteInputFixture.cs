using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.Train.View.Object.Material;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using Client.Tests.BuildUndo;
using Client.Tests.Common;
using Game.Train.Unit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Client.Tests.UIState.DragDelete.Service
{
    public abstract class DeleteInputFixture : UIStateTestFixtureBase
    {
        protected DeleteObjectService Service;
        protected DeleteSelection Selection;
        protected DeleteBoxInput Box;
        protected DeleteBoxTargetResolver Resolver;
        protected ProgressBarState Progress;
        protected MouseCursorTooltipState Tooltip;
        protected Camera Camera;
        protected readonly List<HoverDenyReasonTarget> Targets = new();
        private readonly List<GameObject> _previousCameras = new();
        private TrainRailObjectManager _previousRails;
        private TrainCarObjectDatastore _cars;
        private long _nextCar;

        public override void Setup()
        {
            base.Setup();
            TestReflection.ResetInputManagerCache();
            Client.Input.WebUiInputExclusivity.SetState(false, false);
            foreach (var previous in GameObject.FindGameObjectsWithTag("MainCamera"))
            {
                _previousCameras.Add(previous);
                previous.tag = "Untagged";
            }
            // 遠方の専用照準と地面で既存シーンから隔離する
            // Isolate from the existing scene with a distant aim and ground
            Camera = CreateComponent<Camera>("DeleteInputCamera");
            Camera.tag = "MainCamera";
            var center = Client.Input.ScreenCenter.GetPosition();
            Camera.pixelRect = new Rect(0, 0, center.x * 2, center.y * 2);
            Camera.transform.position = new Vector3(10000.5f, 10, 10000.5f);
            Camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var ground = CreateComponent<GroundGameObject>("DeleteInputGround");
            ground.transform.position = new Vector3(10000, -0.5f, 10000);
            ground.gameObject.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100);
            _previousRails = TrainRailObjectManager.Instance;
            TestReflection.SetStaticProperty(typeof(TrainRailObjectManager), nameof(TrainRailObjectManager.Instance), CreateComponent<TrainRailObjectManager>("DeleteInputRails"));
            _cars = CreateComponent<TrainCarObjectDatastore>("DeleteInputCars");
            Resolver = new DeleteBoxTargetResolver(CreateComponent<BlockGameObjectDataStore>("DeleteInputBlocks"), _cars);
            Progress = new ProgressBarState();
            Tooltip = new MouseCursorTooltipState();
            Service = new DeleteObjectService(new BuildOperationHistory(), Tooltip, new FakeRemovalRestoreSender(), Progress, Camera, new PlacementHeightOffset(), Resolver);
            Selection = TestReflection.GetField<DeleteSelection>(Service, "_selection");
            Box = TestReflection.GetField<DeleteBoxInput>(Service, "_box");
            // EditModeでは遅延Destroyを使えないため表示オブジェクトだけ事前配置する
            // Pre-place only visual objects because delayed Destroy is unavailable in EditMode
            foreach (var field in new[] { "_addView", "_removeView" })
            {
                var view = TestReflection.GetField<DeleteBoxIntentView>(Box, field);
                var visualizer = (Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox.CellRangeBoxVisualizer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox.CellRangeBoxVisualizer));
                foreach (var marker in new[] { "_startMarker", "_endMarker", "_rangeBox" }) TestReflection.SetField(visualizer, marker, CreateObject(field + marker, false));
                TestReflection.SetField(view, "_visualizer", visualizer);
            }
            Physics.SyncTransforms();
            Frame(false, System.Array.Empty<Key>());
        }

        public override void TearDown()
        {
            Box.Clear();
            TestReflection.SetStaticProperty(typeof(TrainRailObjectManager), nameof(TrainRailObjectManager.Instance), _previousRails);
            TestReflection.ResetInputManagerCache();
            Targets.Clear();
            base.TearDown();
            foreach (var previous in _previousCameras) previous.tag = "MainCamera";
            _previousCameras.Clear();
        }

        protected HoverDenyReasonTarget CreateTarget()
        {
            var target = CreateComponent<HoverDenyReasonTarget>("DeleteInputTarget");
            target.Target.Removable = true;
            target.gameObject.layer = LayerConst.BlockLayer;
            target.gameObject.AddComponent<BoxCollider>();
            target.transform.position = new Vector3(20000 + Targets.Count * 2, 10000, 20000);
            Targets.Add(target);
            return target;
        }

        protected void AimAt(HoverDenyReasonTarget target)
        {
            foreach (var item in Targets) item.transform.position = new Vector3(20000, 10000, 20000);
            target.transform.position = Camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint()).GetPoint(5);
            Physics.SyncTransforms();
        }

        protected TrainCarEntityChildrenObject CreateCar()
        {
            var car = CreateComponent<TrainCarEntityObject>("DeleteInputCar");
            car.transform.position = new Vector3(10000.5f, 0, 10000.5f);
            var id = new TrainCarInstanceId(++_nextCar);
            typeof(TrainCarEntityObject).GetProperty(nameof(TrainCarEntityObject.TrainCarInstanceId)).SetValue(car, id);
            var child = car.gameObject.AddComponent<TrainCarEntityChildrenObject>();
            child.Initialize(car, new TrainCarMaterialController(car.gameObject));
            TestReflection.GetField<Dictionary<TrainCarInstanceId, TrainCarEntityObject>>(_cars, "_entities").Add(id, car);
            return child;
        }

        protected void Frame(bool leftHeld, Key[] keys)
        {
            InputSystem.QueueStateEvent(KeyboardDevice, new KeyboardState(keys));
            InputSystem.QueueStateEvent(MouseDevice, new MouseState { buttons = (ushort)(leftHeld ? 1 : 0), position = new Vector2(Screen.width / 2f, Screen.height / 2f) });
            // Dynamic更新で本番の押下・解放フラグをそのまま読む
            // Read production press and release flags through a dynamic input update
            typeof(InputSystem).GetMethod("Update", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Dynamic });
        }
    }
}
