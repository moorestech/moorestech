using System;
using System.Linq;
using Client.Playtest.Input;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.Playtest.Input
{
    public sealed class PlaytestInputScopeTest
    {
        [TestCase(false)][TestCase(true)]
        public void DedicatedDevicesRestoreAfterSuccessOrFailure(bool fail)
        {
            var original = InputSystem.settings;
            var originalDevices = InputSystem.devices.Where(d => d is Keyboard || d is Mouse).ToArray();
            var enabled = originalDevices.Select(d => d.enabled).ToArray();
            var dirty = UnityEditor.EditorUtility.IsDirty(original);
            if (fail) Assert.Throws<OperationCanceledException>(ExerciseScope);
            else Assert.DoesNotThrow(ExerciseScope);
            Assert.That(InputSystem.settings, Is.SameAs(original));
            CollectionAssert.AreEqual(enabled, originalDevices.Select(d => d.enabled).ToArray());
            Assert.That(InputSystem.devices.Any(d => d.name == "PlaytestKeyboard" || d.name == "PlaytestMouse"), Is.False);
            Assert.That(UnityEditor.EditorUtility.IsDirty(original), Is.EqualTo(dirty));
            #region Internal
            void ExerciseScope()
            {
                using var scope = new PlaytestInputScope();
                Assert.That(InputSystem.settings, Is.Not.SameAs(original));
                Assert.That(Keyboard.current.name, Is.EqualTo("PlaytestKeyboard"));
                Assert.That(Keyboard.current.enabled && Mouse.current.enabled, Is.True);
                Assert.That(originalDevices.All(d => !d.enabled), Is.True);
                if (fail) throw new OperationCanceledException();
            }
            #endregion
        }
    }
}
