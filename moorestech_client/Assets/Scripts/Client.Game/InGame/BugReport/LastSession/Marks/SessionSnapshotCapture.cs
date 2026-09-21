using System.IO;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.BugReport.LastSession
{
    internal sealed class SessionSnapshotCapture
    {
        internal readonly string Directory;
        internal readonly string Owner;
        internal readonly string MissingReason;
        private readonly string _state;

        private SessionSnapshotCapture(string state, string directory, string owner, string missingReason)
        {
            _state = state;
            Directory = directory;
            Owner = owner;
            MissingReason = missingReason;
        }

        internal static SessionSnapshotCapture NotStarted()
        {
            return new SessionSnapshotCapture("notStarted", null, null, "このセッションのsnapshot記録開始は未記録（リモート接続・起動未完了・収集無効を含む）");
        }

        internal static SessionSnapshotCapture Started(string directory, int processId, string sessionName)
        {
            return new SessionSnapshotCapture("started", Path.GetFullPath(directory), $"pid_{processId}/{sessionName}", null);
        }

        internal JObject ToJson()
        {
            return new JObject { ["version"] = 1, ["state"] = _state, ["directory"] = Directory, ["owner"] = Owner, ["missingReason"] = MissingReason };
        }

        internal static SessionSnapshotCapture Read(JToken token)
        {
            // 旧形式や未知形式から保存元を推測しない
            // Never infer a source directory from legacy or unknown formats
            if (token is not JObject obj || obj["version"]?.Type != JTokenType.Integer || obj["version"].ToString() != "1")
                return new SessionSnapshotCapture("unknown", null, null, "snapshot所有情報が無い旧形式、または未対応の形式");
            var state = obj["state"]?.Type == JTokenType.String ? (string)obj["state"] : null;
            if ((state == "notStarted" || state == "unknown") && obj["missingReason"]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)obj["missingReason"]))
                return new SessionSnapshotCapture(state, null, null, (string)obj["missingReason"]);
            if (state == "started" && obj["directory"]?.Type == JTokenType.String && obj["owner"]?.Type == JTokenType.String &&
                Path.IsPathFullyQualified((string)obj["directory"]) && !string.IsNullOrWhiteSpace((string)obj["owner"]))
                return new SessionSnapshotCapture(state, (string)obj["directory"], (string)obj["owner"], null);
            return new SessionSnapshotCapture("unknown", null, null, "snapshot所有情報の必須値が欠けている");
        }
    }
}
