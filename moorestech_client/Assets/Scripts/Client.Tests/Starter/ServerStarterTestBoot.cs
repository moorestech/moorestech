using System;
using System.IO;
using System.Reflection;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.Starter
{
    // EditModeでServerStarterを実際に起動・破棄する。Start/OnDestroyはEditModeでは呼ばれないので、Unityが呼ぶ分をここで呼ぶ
    // Boot and tear down a real ServerStarter in EditMode; Start/OnDestroy are not called there, so invoke what Unity would call
    public static class ServerStarterTestBoot
    {
        public static string NewWorldRoot()
        {
            return Path.Combine(Path.GetTempPath(), $"moorestech-server-starter-test-{Guid.NewGuid():N}");
        }

        public static ServerStarter Boot(string worldRoot)
        {
            var starter = new GameObject("ServerStarterTestBoot").AddComponent<ServerStarter>();
            starter.SetArgs(new[]
            {
                "--worldDirectory", worldRoot,
                "--mapMode", "template",
                "--port", "0",
                "--autoSave", "false",
                "--serverDataDirectory", TestModDirectory.ForUnitTestModDirectory,
            });
            typeof(ServerStarter).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(starter, null);
            return starter;
        }

        public static void Teardown(ServerStarter starter)
        {
            // EditModeのDestroyImmediateはOnDestroyを呼ばないので、先に呼んでから破棄する
            // EditMode's DestroyImmediate skips OnDestroy, so call it first and then destroy
            typeof(ServerStarter).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(starter, null);
            Object.DestroyImmediate(starter.gameObject);
        }

        public static ServerInstanceManager InstanceManager(ServerStarter starter)
        {
            return (ServerInstanceManager)typeof(ServerStarter).GetField("_startServer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(starter);
        }
    }
}
