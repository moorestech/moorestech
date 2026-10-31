using System;
using System.Linq;
using System.Reflection;
using Client.Common;
using Client.Network.API;
using Client.Starter.Initialization;
using Client.Starter.Initialization.Scene;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace Client.Tests.EditModeInPlayingTest.MachineUpgrade
{
    /// <summary>
    ///     実起動の途中で、届いた handshake の ModuleSlotStackLevel を差し替える起動 fixture。製品の初期化（MainGameInitializationFinalizer）が適用する前に書き換える
    ///     Boot fixture that swaps the received handshake's ModuleSlotStackLevel mid real startup, before the product initialization (MainGameInitializationFinalizer) applies it
    /// </summary>
    internal sealed class HandshakeModuleSlotStackLevelInjector
    {
        private readonly int _injectedLevel;

        public bool Injected { get; private set; }

        private HandshakeModuleSlotStackLevelInjector(int injectedLevel)
        {
            _injectedLevel = injectedLevel;
        }

        // 起動より前に購読し、主シーン読込の通知で製品の読込ハンドラより先に走る
        // Subscribe before boot so the main-scene notification reaches this ahead of the product's loaded handler
        public static HandshakeModuleSlotStackLevelInjector Install(int injectedLevel)
        {
            var injector = new HandshakeModuleSlotStackLevelInjector(injectedLevel);
            SceneManager.sceneLoaded += injector.OnSceneLoaded;
            return injector;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneConstant.MainGameSceneName) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // 同じ通知に並ぶ製品の読込ハンドラから、初期化へ渡る接続結果の handshake を取り出して書き換える
            // From the product's loaded handler queued on the same notification, take the handshake handed to initialization and rewrite it
            var handshake = FindPendingHandshake();
            SetField(handshake, "<ModuleSlotStackLevel>k__BackingField", _injectedLevel);
            Injected = true;

            #region Internal

            static InitialHandshakeResponse FindPendingHandshake()
            {
                var eventField = typeof(SceneManager).GetField("sceneLoaded", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(eventField, "SceneManager.sceneLoaded backing field was not found");
                var subscribers = ((Delegate)eventField.GetValue(null)).GetInvocationList();
                var handler = subscribers.Select(subscriber => subscriber.Target).OfType<MainGameSceneLoadedHandler>().SingleOrDefault();
                Assert.IsNotNull(handler, "MainGameSceneLoadedHandler was not subscribed when the main scene loaded");
                var serverResult = (ServerConnectionResult)GetField(handler, "_serverResult");
                return serverResult.HandshakeResponse;
            }

            static object GetField(object target, string name)
            {
                var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(field, $"{target.GetType().Name}.{name} was not found");
                return field.GetValue(target);
            }

            static void SetField(object target, string name, object value)
            {
                var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(field, $"{target.GetType().Name}.{name} was not found");
                field.SetValue(target, value);
            }

            #endregion
        }
    }
}
