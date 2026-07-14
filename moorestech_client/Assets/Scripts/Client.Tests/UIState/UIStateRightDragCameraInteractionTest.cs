using System.Runtime.Serialization;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Game.Skit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState
{
    public class UIStateRightDragCameraInteractionTest : InputTestFixture
    {
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [Test]
        public void PlaceBlockHandlesRightDragAndRestoresOnExit()
        {
            var applier = new FakePlayerCameraInteractionApplier();
            var skitManager = (SkitManager)FormatterServices.GetUninitializedObject(typeof(SkitManager));
            var state = new PlaceBlockState(skitManager, null, null, null, applier);

            Press(_mouse.rightButton);
            Assert.Catch(() => state.GetNextUpdate());
            CollectionAssert.AreEqual(new[] { "Cursor:False", "Rotatable:True" }, applier.Calls);

            applier.Calls.Clear();
            Release(_mouse.rightButton);
            Assert.Catch(() => state.GetNextUpdate());
            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, applier.Calls);

            applier.Calls.Clear();
            Assert.Throws<System.NullReferenceException>(() => state.OnExit());
            CollectionAssert.AreEqual(new[] { "Rotatable:False" }, applier.Calls);
        }

        [Test]
        public void DeleteObjectHandlesRightDragAndRestoresOnExit()
        {
            var deleteObject = new GameObject("DeleteBar").AddComponent<DeleteBarObject>();
            var applier = new FakePlayerCameraInteractionApplier();
            var state = new DeleteObjectState(deleteObject, null, applier);

            Press(_mouse.rightButton);
            state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { "Cursor:False", "Rotatable:True" }, applier.Calls);

            applier.Calls.Clear();
            Release(_mouse.rightButton);
            state.GetNextUpdate();
            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, applier.Calls);

            applier.Calls.Clear();
            Assert.Throws<System.NullReferenceException>(() => state.OnExit());
            CollectionAssert.AreEqual(new[] { "Rotatable:False" }, applier.Calls);
            Object.DestroyImmediate(deleteObject.gameObject);
        }
    }
}
