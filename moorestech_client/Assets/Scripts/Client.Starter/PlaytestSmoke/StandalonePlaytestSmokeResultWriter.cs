using System;
using System.IO;
using UnityEngine;

namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 通し検証の結果を result.json として書く（Mac mini 側が回収して合否に使う）
    /// Writes the smoke result as result.json, which the Mac mini collects to decide pass/fail
    /// </summary>
    public static class StandalonePlaytestSmokeResultWriter
    {
        private const string ResultFileName = "result.json";

        internal static void Write(string resultDirectory, StandalonePlaytestSmokeResult result)
        {
            var path = Path.Combine(resultDirectory, ResultFileName);

            // 結果の置き場は検証機のディスク（外部資源）。書けなくても終了まで進めるよう隔離し、理由はログへ残す
            // The result lives on the verifier's disk, an external resource; isolate it so the run still quits, logging the reason
            // result.json が無いこと自体を回収側が失敗として扱うので、ログと欠損の両方がここで揃う
            // The collector treats a missing result.json as a failure, so the log plus the absent file together declare the gap
            try
            {
                Directory.CreateDirectory(resultDirectory);
                File.WriteAllText(path, JsonUtility.ToJson(result, true));
                Debug.Log($"[PlaytestSmoke] result written: {path} success={result.success}");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"[PlaytestSmoke] result.json could not be written: {path} success={result.success} message={result.message}: {e.Message}");
            }
        }

        // 1ステップで打ち切った結果を組む（前提確認失敗時）
        // Builds a result that stopped at one step (pre-launch precondition failure)
        internal static StandalonePlaytestSmokeResult CreateSingleStepFailure(string phase, string stepName, string message)
        {
            var step = new StandalonePlaytestSmokeStep { name = stepName, success = false, message = message, elapsedSeconds = 0f };
            return new StandalonePlaytestSmokeResult
            {
                phase = phase,
                success = false,
                message = message,
                reportBundleDirectory = "",
                steps = new[] { step },
            };
        }
    }
}
