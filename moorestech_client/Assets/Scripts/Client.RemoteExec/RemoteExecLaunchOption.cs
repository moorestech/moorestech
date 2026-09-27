using System;
using UnityEngine;

namespace Client.RemoteExec
{
    // 起動引数から遠隔実行の許可を決める（ADR 0072）
    // Resolve remote execution permission from launch arguments (ADR 0072)
    public static class RemoteExecLaunchOption
    {
        public const string Marker = "-remote-exec";

        public static bool IsEnabled { get; private set; }

        public static void ResolveFromCommandLine(string[] args)
        {
            IsEnabled = Array.IndexOf(args, Marker) >= 0;
            if (IsEnabled) Debug.LogWarning($"[RemoteExec] {Marker} が指定されたため遠隔実行を有効にします");
        }
    }
}
