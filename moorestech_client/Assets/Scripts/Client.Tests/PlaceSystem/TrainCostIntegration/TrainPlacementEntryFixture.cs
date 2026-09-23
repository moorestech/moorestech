using System.Reflection;
using Client.Game.InGame.BlockSystem;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Client.Localization;
using Client.Network.API;
using Client.Tests.Common;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.PlaceSystem.TrainCostIntegration
{
    public abstract class TrainPlacementEntryFixture : InputTestFixture
    {
        protected GameObject Root;
        protected Camera PlacementCamera;
        protected LocalPlayerInventory Inventory;
        private Mouse _mouse;
        private PlacementPacketCapture _packets;
        private PlayerSystemContainer _previousPlayer;
        private VanillaApi _previousApi;
        private ThirdPersonAimSource _previousAimSource;

        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
            _mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();
            _ = InputManager.Playable;
            InputSystem.Update();

            // プレイヤー距離判定を既存の実オブジェクトで成立させる
            // Satisfy player-distance checks through the existing runtime objects
            _previousPlayer = PlayerSystemContainer.Instance;
            _previousApi = ClientContext.VanillaApi;
            Root = new GameObject("TrainPlacementEntryTest");
            var player = CreateObject("Player");
            var grab = player.AddComponent<PlayerGrabItemManager>();
            var controller = player.AddComponent<PlayerObjectController>();
            TestReflection.SetField(controller, "animator", player.AddComponent<Animator>());
            var container = player.AddComponent<PlayerSystemContainer>();
            TestReflection.SetField(container, "playerGrabItemManager", grab);
            TestReflection.SetField(container, "playerObjectController", controller);
            TestReflection.InvokePrivate(container, "Awake");

            // 中央レイが地面へ届くよう実ColliderとCameraを置く
            // Place a real collider and camera so the center ray reaches terrain
            _previousAimSource = (ThirdPersonAimSource)typeof(AimPointProvider).GetField("_thirdPersonAimSource", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
            PlacementCamera = CreateObject("Camera").AddComponent<Camera>();
            PlacementCamera.transform.SetPositionAndRotation(new Vector3(0, 10, 0), Quaternion.Euler(90, 0, 0));
            var ground = CreateObject("Ground");
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100);
            ground.AddComponent<GroundGameObject>();
            Physics.SyncTransforms();

            Inventory = new LocalPlayerInventory();
            _packets = new PlacementPacketCapture();
            TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", _packets.Api);
        }

        public override void TearDown()
        {
            TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", _previousApi);
            TestReflection.SetStaticProperty(typeof(PlayerSystemContainer), "Instance", _previousPlayer);
            AimPointProvider.SetThirdPersonAimSource(_previousAimSource);
            _packets.Dispose();
            Object.DestroyImmediate(Root);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        protected GameObject CreateObject(string name)
        {
            var result = new GameObject(name);
            result.transform.SetParent(Root.transform);
            return result;
        }

        protected void ClickRelease()
        {
            Press(_mouse.leftButton);
            Release(_mouse.leftButton);
            Assert.IsTrue(InputManager.Playable.ScreenLeftClick.GetKeyUp, "クリック解放が入力へ届いていない");
            Assert.IsFalse(UiPointerHitTest.IsPointerOverAnyUi(), "UIによる送信停止で不足ガードが隠れている");
        }

        protected void ClickPress()
        {
            Press(_mouse.leftButton);
            Assert.IsTrue(InputManager.Playable.ScreenLeftClick.GetKeyDown, "クリック押下が入力へ届いていない");
            Assert.IsFalse(UiPointerHitTest.IsPointerOverAnyUi(), "UIによる送信停止で不足ガードが隠れている");
        }

        protected void AssertNoRequest()
        {
            _packets.AssertNoRequest();
        }
    }
}
