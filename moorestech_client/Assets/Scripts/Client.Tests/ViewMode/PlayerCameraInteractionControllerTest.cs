using System.Reflection;
using Client.Game.InGame.Control;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.ViewMode
{
    public class PlayerCameraInteractionControllerTest : InputTestFixture
    {
        private GameObject _cameraObject;
        private InGameCameraController _cameraController;
        private PlayerCameraInteractionController _interactionController;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _cameraObject = new GameObject("InGameCamera");
            _cameraController = _cameraObject.AddComponent<InGameCameraController>();
            _interactionController = new PlayerCameraInteractionController(_cameraController);
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Object.DestroyImmediate(_cameraObject);
            base.TearDown();
        }

        [Test]
        public void GameplayLocksCursorAndEnablesCameraRotation()
        {
            _interactionController.EnterGameplay();

            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
            Assert.IsTrue(IsCameraControllable());
        }

        [Test]
        public void CursorInteractionShowsCursorAndStopsCameraRotation()
        {
            _interactionController.EnterGameplay();
            _interactionController.EnterCursorInteraction();

            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsFalse(IsCameraControllable());
        }

        [Test]
        public void RightDragDownAndUpSwitchCursorAndCameraRotation()
        {
            _interactionController.EnterCursorInteraction();
            Press(_mouse.rightButton);
            _interactionController.UpdateRightDrag();

            Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState);
            Assert.IsTrue(IsCameraControllable());

            Release(_mouse.rightButton);
            _interactionController.UpdateRightDrag();

            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsFalse(IsCameraControllable());
        }

        [Test]
        public void ExitRestoresCursorInteractionAfterMissedMouseUp()
        {
            Press(_mouse.rightButton);
            _interactionController.UpdateRightDrag();
            _interactionController.ExitCursorInteraction();

            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
            Assert.IsFalse(IsCameraControllable());
        }

        private bool IsCameraControllable()
        {
            var field = typeof(InGameCameraController).GetField("_isControllable", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)field.GetValue(_cameraController);
        }
    }
}
