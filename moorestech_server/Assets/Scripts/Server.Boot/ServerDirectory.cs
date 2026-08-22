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
            var debugServerDirectory = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "../../moorestech_master/server_v8/"));
            var serverDirectory = DebugParameters.GetValueOrDefaultString(DebugServerDirectorySettingKey ,debugServerDirectory);
#elif UNITY_STANDALONE_OSX
            var broken = ;
            // dataPathは<app>.app/Contentsのため、2階層上（.appの隣）のgame/を参照
            // dataPath is <app>.app/Contents, so game/ sits two levels up beside the .app
            var serverDirectory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "game"));
#else
            // dataPathは<root>/moorestech_Dataのため、1階層上（実行ファイルの隣）のgame/を参照
            // dataPath is <root>/moorestech_Data, so game/ sits one level up beside the executable
            var serverDirectory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "game"));
#endif
            
            return serverDirectory;
        }
    }
}