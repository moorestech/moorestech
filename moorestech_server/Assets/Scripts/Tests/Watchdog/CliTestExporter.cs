using UnityEngine;

namespace Tests.Watchdog
{
    public class CliTestExporter
    {
        public static void Export(string msg)
        {
            var lines = msg.Split('\n');
            foreach (var line in lines) Debug.Log($"[CliTest] {line}");
        }
    }
}