using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // complete の409が数えた欠損を送信済み集合から外す。外さないと次の試行でPUTが全部飛び同じ409を繰り返す（不明なら全部送り直す）
    // Removes the gaps a complete 409 counted from the sent set; otherwise the next attempt skips every PUT and repeats the same 409 (when unknown, resend all)
    internal static class PlaytestCompleteMissingPaths
    {
        // 戻り値は読めた欠損パス。読めず全部送り直す側に倒したときは空
        // Returns the missing paths it could read; empty when it fell back to resending everything
        public static IReadOnlyList<string> Forget(string body, HashSet<string> sentPaths)
        {
            // 受け口の応答は外部入力のJSON。読めなければ全部送り直す側に倒す
            // The receiver's body is external JSON; when unreadable, fall back to resending everything
            JObject root;
            try
            {
                root = JToken.Parse(body) as JObject;
            }
            catch (JsonException exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] complete answered 409 with an unreadable body; resending every file: {exception.Message}");
                sentPaths.Clear();
                return new List<string>();
            }

            var reason = root?["reason"] is JValue { Type: JTokenType.String } reasonValue ? (string)reasonValue : "unknown";
            if (!(root?["missing"] is JArray missing) || missing.Count == 0)
            {
                Debug.LogWarning($"[PlaytestReceiver] complete answered 409 without a missing list (reason: {reason}); resending every file");
                sentPaths.Clear();
                return new List<string>();
            }

            // path は型を確かめて読む。1件でも読めなければ欠けを特定できないので全部送り直す（無視すると同じ409を繰り返す）
            // Paths are read after a type check; if any entry is unreadable the gap is unknown, so resend all (ignoring it would repeat the same 409)
            var missingPaths = new List<string>();
            foreach (var entry in missing)
            {
                if (!(entry is JObject missingEntry) || !(missingEntry["path"] is JValue { Type: JTokenType.String } path))
                {
                    Debug.LogWarning($"[PlaytestReceiver] complete answered 409 with an unreadable missing entry (reason: {reason}); resending every file: {entry.ToString(Formatting.None)}");
                    sentPaths.Clear();
                    return new List<string>();
                }
                missingPaths.Add((string)path);
            }

            var removed = 0;
            foreach (var missingPath in missingPaths)
            {
                if (sentPaths.Remove(missingPath)) removed++;
            }
            // 送信済み集合から1件も外れなければ次の試行もPUTを全部飛ばし同じ409を繰り返す。全部送り直す側に倒す
            // When nothing leaves the sent set, the next attempt skips every PUT and repeats the same 409; fall back to resending everything
            if (removed == 0)
            {
                Debug.LogWarning($"[PlaytestReceiver] complete answered 409 (reason: {reason}) but no sent path matched; resending every file");
                sentPaths.Clear();
            }
            return missingPaths;
        }
    }
}
