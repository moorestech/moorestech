using System;
using UnityEngine;

namespace Client.WebUiHost.Common
{
    public enum WebUiHostMode
    {
        Development,
        Production,
    }

    /// <summary>
    /// 実行環境と環境変数からWeb UI配信モードを決定する
    /// Resolves the Web UI serving mode from the runtime and environment
    /// </summary>
    public static class WebUiHostModeResolver
    {
        public const string EnvironmentVariable = "MOORESTECH_WEBUI_MODE";

        public static WebUiHostMode Resolve()
        {
            // 環境変数を最優先する
            // Prefer the environment override for CI/device tests, otherwise use the runtime default
            var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (string.Equals(configured, "dev", StringComparison.OrdinalIgnoreCase)) return WebUiHostMode.Development;
            if (string.Equals(configured, "prod", StringComparison.OrdinalIgnoreCase)) return WebUiHostMode.Production;

            return Application.isEditor ? WebUiHostMode.Development : WebUiHostMode.Production;
        }
    }
}
