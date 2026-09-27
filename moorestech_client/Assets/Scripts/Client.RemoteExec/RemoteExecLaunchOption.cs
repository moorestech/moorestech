using System;
using Client.RemoteExec.Access;
using UnityEngine;

namespace Client.RemoteExec
{
    // 起動引数から遠隔実行の許可を決める（ADR 0072）
    // Resolve remote execution permission from launch arguments (ADR 0072)
    public static class RemoteExecLaunchOption
    {
        internal const string Marker = "-remote-exec";

        public static bool IsEnabled { get; private set; }

        public static void ResolveFromCommandLine(string[] args)
        {
            IsEnabled = 0 <= Array.IndexOf(args, Marker);
            if (!IsEnabled) RemoteExecAccessFile.ClearToken();
            if (IsEnabled) Debug.LogWarning($"[RemoteExec] {Marker} が指定されたため遠隔実行を有効にします");
        }
    }
}
