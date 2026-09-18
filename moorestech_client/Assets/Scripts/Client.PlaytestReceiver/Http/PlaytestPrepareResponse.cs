using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Client.PlaytestReceiver.Http
{
    public sealed class PlaytestPreparedUpload
    {
        public readonly string Path;
        public readonly string Url;
        public readonly long Bytes;

        public PlaytestPreparedUpload(string path, string url, long bytes)
        {
            Path = path;
            Url = url;
            Bytes = bytes;
        }
    }

    public enum PlaytestPrepareOutcome
    {
        Prepared,
        AlreadyAcked,
    }

    // prepare応答。判別は outcome 一本で、AlreadyAcked なら送るものは無く completeの冪等成功へ進む
    // The prepare response; the outcome alone discriminates, and AlreadyAcked means nothing to send before the idempotent complete
    public sealed class PlaytestPrepareResponse
    {
        public readonly PlaytestPrepareOutcome Outcome;
        public readonly IReadOnlyList<PlaytestPreparedUpload> Uploads;

        private PlaytestPrepareResponse(PlaytestPrepareOutcome outcome, IReadOnlyList<PlaytestPreparedUpload> uploads)
        {
            Outcome = outcome;
            Uploads = uploads;
        }

        // 受け口の応答は外部入力のJSON。形が違えば理由を返し、呼び出し側がRetryableとして扱う
        // The receiver's body is external JSON; a malformed one returns the reason so the caller treats it as retryable
        public static bool TryParse(string body, out PlaytestPrepareResponse response, out string detail)
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
            var outcome = root.Value<string>("outcome");
            if (outcome == "acked")
            {
                response = new PlaytestPrepareResponse(PlaytestPrepareOutcome.AlreadyAcked, new List<PlaytestPreparedUpload>());
                return true;
            }
            if (outcome != "prepared" || !(root["uploads"] is JArray uploads))
            {
                detail = $"prepare response has an unknown outcome or no uploads array: {outcome}";
                return false;
            }
            var parsed = new List<PlaytestPreparedUpload>();
            foreach (var entry in uploads)
            {
                var path = entry.Value<string>("path");
                var url = entry.Value<string>("url");
                var bytes = entry.Value<long?>("bytes");
                if (path == null || url == null || bytes == null)
                {
                    detail = $"prepare response entry is malformed: {entry}";
                    return false;
                }
                parsed.Add(new PlaytestPreparedUpload(path, url, bytes.Value));
            }
            response = new PlaytestPrepareResponse(PlaytestPrepareOutcome.Prepared, parsed);
            return true;
        }
    }
}
