using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.EditModeInPlayingTest
{
    // CIの既知terrain欠損ログだけを起動区間で期待し、同期エラーは通常どおり検出する。
    // Expect only the known CI terrain asset log during startup; synchronization errors remain test failures.
    internal sealed class TrainStartupAssetLogScope : IDisposable
    {
        private int _expectedAssetLogCount;

        public TrainStartupAssetLogScope()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        public void Dispose()
        {
            Application.logMessageReceived -= OnLogMessage;
            Debug.Log($"[TrainStartupAssetLogScope] Expected {_expectedAssetLogCount} terrain asset logs; original errors retained in Editor log.");
        }

        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error || !Regex.IsMatch(message, @"\ATree prefab at index [0-9]+ is missing\.\z")) return;
            if (!stackTrace.Contains("UnityEngine.ResourceManagement.ResourceProviders.AssetDatabaseProvider:LoadAssetAtPath")) return;

            // 同じフレーム内で期待を積み、可変回数の既知ログをフレーム末尾の検査へ渡す。
            // Register each observed known log before the test runner evaluates expected logs at frame end.
            LogAssert.Expect(LogType.Error, message);
            _expectedAssetLogCount++;
        }
    }
}
