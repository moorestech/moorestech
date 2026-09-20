using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Client.PlaytestReceiver.Http.Responses
{
    // prepare応答。判別は outcome 一本で、AlreadyAcked なら送るものは無く completeの冪等成功へ進む
    // The prepare response; the outcome alone discriminates, and AlreadyAcked means nothing to send before the idempotent complete
    public sealed class PlaytestPrepareResponse
    {
        public readonly PlaytestPrepareOutcome Outcome;
        public readonly IReadOnlyList<PlaytestPreparedUpload> Uploads;
        public readonly IReadOnlyList<PlaytestPrepareConflict> Conflicts;

        private PlaytestPrepareResponse(PlaytestPrepareOutcome outcome, IReadOnlyList<PlaytestPreparedUpload> uploads, IReadOnlyList<PlaytestPrepareConflict> conflicts)
        {
            Outcome = outcome;
            Uploads = uploads;
            Conflicts = conflicts;
        }

        // 受け口の応答は外部入力のJSON。形が違えば理由を返し、呼び出し側が契約違反（MalformedResponse）として扱う
        // The receiver's body is external JSON; a malformed one returns the reason so the caller treats it as a contract breach (MalformedResponse)
        public static bool TryParse(string body, IReadOnlyList<PlaytestDeclaredFile> declaredFiles, out PlaytestPrepareResponse response, out string detail)
        {
            response = null;
            detail = "";
            JObject root;
            try
            {
                root = JObject.Parse(body);
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                detail = $"prepare response is not JSON: {e.Message}";
                return false;
            }
            // 型が想定外でも例外で走行を落とさないよう、値は型を確かめてから読む
            // Values are read only after checking their type, so an unexpected shape cannot throw out of the run
            var outcome = ReadString(root, "outcome");
            if (outcome == PlaytestReceiverConfig.PrepareOutcomeAcked)
            {
                response = new PlaytestPrepareResponse(PlaytestPrepareOutcome.AlreadyAcked, new List<PlaytestPreparedUpload>(), new List<PlaytestPrepareConflict>());
                return true;
            }
            if (outcome != PlaytestReceiverConfig.PrepareOutcomePrepared || !(root["uploads"] is JArray uploads) || !(root["conflicts"] is JArray conflicts))
            {
                detail = $"prepare response has an unknown outcome or no uploads/conflicts array: {outcome}";
                return false;
            }

            // 1つのパスは uploads と conflicts を通じて1回だけ、宣言にあるものだけが載る
            // A path appears at most once across uploads and conflicts, and only when it was declared
            var declaredByPath = new Dictionary<string, PlaytestDeclaredFile>();
            foreach (var file in declaredFiles) declaredByPath[file.Path] = file;
            var seenPaths = new HashSet<string>();
            var parsedUploads = new List<PlaytestPreparedUpload>();
            foreach (var entry in uploads)
            {
                var why = ReadUpload(entry, out var upload);
                if (why != null) return Malformed(entry, why, out detail);
                parsedUploads.Add(upload);
            }
            var parsedConflicts = new List<PlaytestPrepareConflict>();
            foreach (var entry in conflicts)
            {
                var why = ReadConflict(entry, out var conflict);
                if (why != null) return Malformed(entry, why, out detail);
                parsedConflicts.Add(conflict);
            }
            response = new PlaytestPrepareResponse(PlaytestPrepareOutcome.Prepared, parsedUploads, parsedConflicts);
            return true;

            #region Internal

            // uploads の1件。URLは絶対httpsに限る（相対URLはPUTの送信で例外になり、http は署名を平文で流す）
            // One uploads entry; the URL must be absolute https (a relative one throws when sent, plain http leaks the signature)
            string ReadUpload(JToken entry, out PlaytestPreparedUpload upload)
            {
                upload = null;
                var url = ReadString(entry, "url");
                var why = ReadDeclaredEntry(entry, "bytes", out var file);
                if (why != null) return why;
                if (url == null) return "no url string";
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return "url is not an absolute https URL";
                upload = new PlaytestPreparedUpload(file, url);
                return null;
            }

            string ReadConflict(JToken entry, out PlaytestPrepareConflict conflict)
            {
                conflict = null;
                var why = ReadDeclaredEntry(entry, "expectedBytes", out var file);
                if (why != null) return why;
                var actualBytes = ReadLong(entry, "actualBytes");
                if (actualBytes == null) return "no integer actualBytes";
                conflict = new PlaytestPrepareConflict(file, actualBytes.Value);
                return null;
            }

            // path が宣言にあり重複せず、長さの欄が宣言の長さと一致するかを見る。問題があれば理由を返す
            // Checks that the path is declared and unique and the length field matches the declaration; returns the problem if any
            string ReadDeclaredEntry(JToken entry, string bytesKey, out PlaytestDeclaredFile file)
            {
                file = null;
                var path = ReadString(entry, "path");
                if (path == null) return "no path string";
                if (!declaredByPath.TryGetValue(path, out var declared)) return $"undeclared path {path}";
                if (!seenPaths.Add(path)) return $"duplicated path {path}";
                var bytes = ReadLong(entry, bytesKey);
                if (bytes != declared.Bytes) return $"{bytesKey} {bytes} differs from the declared {declared.Bytes}";
                file = declared;
                return null;
            }

            // 理由に添える項目の写しからは url を外す。署名付きURLは1時間有効な権限で、ログや UPLOAD_ATTEMPTS に残さない
            // The entry copy attached to the reason drops url; a presigned URL is an hour-long authority kept out of logs and UPLOAD_ATTEMPTS
            bool Malformed(JToken entry, string why, out string malformedDetail)
            {
                var shown = entry.DeepClone();
                if (shown is JObject shownObject) shownObject.Remove("url");
                malformedDetail = $"prepare response entry is malformed ({why}): {shown.ToString(Newtonsoft.Json.Formatting.None)}";
                return false;
            }

            // objectの文字列値のみ返す。非object/キー無し/非文字列はnull
            // Returns an object's string value only; null when not an object, the key is absent, or not a string
            string ReadString(JToken token, string key)
            {
                if (!(token is JObject obj) || !(obj[key] is JValue { Type: JTokenType.String } value)) return null;
                return (string)value;
            }

            long? ReadLong(JToken token, string key)
            {
                if (!(token is JObject obj) || !(obj[key] is JValue { Type: JTokenType.Integer } value)) return null;
                return (long)value;
            }

            #endregion
        }
    }
}
