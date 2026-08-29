using Client.Input;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.Input
{
    public class PlayableInteractInputTest : InputTestFixture
    {
        [Test]
        public void FキーがInteractとして読める()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var interact = InputManager.Playable.Interact;
            InputSystem.Update();
            Press(keyboard.fKey);
            InputSystem.Update();
            Assert.IsTrue(interact.GetKey);
            Assert.IsTrue(interact.GetKeyDown);
        }

        [Test]
        public void EキーがRideとして読める()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var ride = InputManager.Playable.Ride;
            InputSystem.Update();
            Press(keyboard.eKey);
            InputSystem.Update();
            Assert.IsTrue(ride.GetKeyDown);
        }
    }
}
