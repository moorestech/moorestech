using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.UIObject;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState
{
    public class UIStateCameraInteractionOnEnterTest
    {
        [Test]
        public void GameScreenPushesGameplayInteractionOnEnter()
        {
            var applier = new FakePlayerCameraInteractionApplier();
            var state = new GameScreenState(null, null, null, null, applier);

            Assert.Catch(() => state.OnEnter(new UITransitContext(UIStateEnum.GameScreen)));

            CollectionAssert.AreEqual(new[] { "Cursor:False", "Rotatable:True" }, applier.Calls);
        }

        [Test]
        public void BuildMenuPushesCursorInteractionOnEnter()
        {
            var applier = new FakePlayerCameraInteractionApplier();
            var state = new BuildMenuState(null, applier);

            Assert.Catch(() => state.OnEnter(new UITransitContext(UIStateEnum.BuildMenu)));

            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, applier.Calls);
        }

        [Test]
        public void PlaceBlockPushesCursorInteractionOnEnter()
        {
            var applier = new FakePlayerCameraInteractionApplier();
            var state = new PlaceBlockState(null, null, null, null, applier);

            Assert.Catch(() => state.OnEnter(new UITransitContext(UIStateEnum.PlaceBlock)));

            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, applier.Calls);
        }

        [Test]
        public void DeleteObjectPushesCursorInteractionOnEnter()
        {
            var deleteObject = new GameObject("DeleteBar").AddComponent<DeleteBarObject>();
            var applier = new FakePlayerCameraInteractionApplier();
            var state = new DeleteObjectState(deleteObject, null, applier);

            Assert.Catch(() => state.OnEnter(new UITransitContext(UIStateEnum.DeleteBar)));

            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, applier.Calls);
            Object.DestroyImmediate(deleteObject.gameObject);
        }
    }
}
