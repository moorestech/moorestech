using System;
using System.IO;
using Common.Debug;
using UnityEngine;

namespace Server.Boot
{
    public class ServerDirectory
    {
        public const string DebugServerDirectorySettingKey = "DebugServerDirectory";
        public static string GetDirectory()
        {
#if UNITY_EDITOR
            var debugServerDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../moorestech_master/server_v4/"));
            var serverDirectory = DebugParameters.GetValueOrDefaultString(DebugServerDirectorySettingKey ,debugServerDirectory);
#else
            var serverDirectory = Path.Combine(UnityEngine.Application.dataPath, "../","../", "game");
#endif
            
            return serverDirectory;
        }
    }
}