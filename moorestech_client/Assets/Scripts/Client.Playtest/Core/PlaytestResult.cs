using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Client.Playtest.Core
{
    /// <summary>
    ///     シナリオ1回分の実行結果。完走後にresult.jsonへ書き出し、CLI側はこのファイルだけを回収する
    ///     Result of a single scenario run. Written to result.json so the CLI only needs to poll one file
    /// </summary>
    [Serializable]
    public class PlaytestResult
    {
        public string RunName;
        public bool Success;
        public string Error;
        public string StartedAt;
        public string FinishedAt;
        public List<PlaytestAssertResult> Asserts = new();
        public List<string> Timeline = new();
        public List<string> ErrorLogs = new();
        public List<string> Screenshots = new();
        public string RecordingPath;

        public void Write(string runDirectory)
        {
            // 結果JSONを書き出す（このファイルの出現が完走のシグナル）
            // Write the result JSON (its appearance signals scenario completion)
            var path = Path.Combine(runDirectory, "result.json");
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
            Debug.Log($"[Playtest] result written: {path} success={Success}");
        }
    }

    [Serializable]
    public class PlaytestAssertResult
    {
        public string Label;
        public bool Passed;
        public string Message;
    }
}
