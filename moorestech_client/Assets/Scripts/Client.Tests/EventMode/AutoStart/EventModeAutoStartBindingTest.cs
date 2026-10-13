using System.Reflection;
using Client.Starter;
using Client.Starter.EventMode;
using Client.Tests.Playtest;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.EventMode
{
    public class EventModeAutoStartBindingTest
    {
        [Test]
        public void RuntimeHookUsesAfterSceneLoadAndProductionSequence()
        {
            var hook = typeof(EventModeAutoStart).GetMethod(nameof(EventModeAutoStart.AutoStartIfEventMode));
            var attribute = hook.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute);
            Assert.AreEqual(RuntimeInitializeLoadType.AfterSceneLoad, attribute.loadType);
            var sequence = typeof(EventModeAutoStartSequence).GetMethod(nameof(EventModeAutoStartSequence.RunAsync),
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(sequence);
            Assert.IsTrue(MethodCallInspector.ContainsCall(hook, sequence));
        }

        [Test]
        public void ProductionActionsDeleteWorldBeforeStarting()
        {
            var action = typeof(EventModeAutoStartActions).GetMethod(nameof(EventModeAutoStartActions.StartFreshWorld));
            var delete = typeof(GameSystemPaths).GetMethod(nameof(GameSystemPaths.DeleteDefaultWorldDirectory));
            var start = typeof(LocalGameLauncher).GetMethod(nameof(LocalGameLauncher.StartLocalGame));
            Assert.IsTrue(MethodCallInspector.CallsInOrder(action, delete, start));
        }

        [Test]
        public void ProductionTimeoutLogsBeforeQuitting()
        {
            var action = typeof(EventModeAutoStartActions).GetMethod(nameof(EventModeAutoStartActions.QuitBecauseTitleGatesTimedOut));
            var log = typeof(Debug).GetMethod(nameof(Debug.LogError), new[] { typeof(object) });
            var quit = typeof(Application).GetMethod(nameof(Application.Quit), System.Type.EmptyTypes);
            Assert.IsTrue(MethodCallInspector.CallsInOrder(action, log, quit));
        }
    }
}
