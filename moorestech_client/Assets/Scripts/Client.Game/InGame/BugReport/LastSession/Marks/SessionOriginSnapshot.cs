using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.DiskOperations;
using Client.RemoteExec.Access;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Client.Game.InGame.BugReport.LastSession
{
    // セッション開始時点の出所（ビルド・SteamID・スナップショット記録）。前回クラッシュの箱に「今回起動したビルドやワールド」を付けないため、落ちたセッション自身が書き残す（F12・D-C3）
    // The origin at session start (build, SteamID, snapshot capture); the crashed session writes it itself so the previous crash's box never carries the build or world launched this time (F12, D-C3)
    public sealed class SessionOriginSnapshot
    {
        // 読み書きで同じ設定を使い、並行する処理に可変のSerializer本体は共有しない
        // Share one setting across reads and writes without sharing a mutable serializer between concurrent operations
        internal static JsonSerializer CreateSerializer()
        {
            return JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
        }

        // 旧形式の印（理由キー無し）を読んだときの理由。無音でnullにすると欠損の原因が箱から消える
        // Reason used for a legacy mark without the reason key; a silent null would erase the gap's cause from the box
        internal const string LegacyMarkSteamIdAbsenceReason = "SteamIDの欠損理由が記録されていない旧形式の印（前回セッションの開始時点でテスター識別が無かった）";

        public string SteamId { get; }
        public string SteamIdAbsenceReason { get; }
        public BuildOriginReading BuildOrigin { get; }

        // null＝遠隔実行が無効だったセッション。有効なら台帳名と2つの印を1つのペイロードとして持つ
        // Null means the session had remote execution disabled; an enabled one carries the ledger name and both signals as one payload
        public RemoteExecOriginMark RemoteExec { get; }
        internal readonly SessionSnapshotCapture SnapshotCapture;
        internal readonly IReadOnlyList<MissingItem> SalvageMissing;

        // remoteExecはnull＝無効。呼び出し側がRemoteExecLaunchOption等から解決した印をそのまま渡す
        // remoteExec is null when disabled; callers pass the mark they already resolved from RemoteExecLaunchOption etc.
        public SessionOriginSnapshot(string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin, RemoteExecOriginMark remoteExec) : this(steamId, steamIdAbsenceReason, buildOrigin, remoteExec, SessionSnapshotCapture.NotStarted())
        {
        }

        internal SessionOriginSnapshot(string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin, RemoteExecOriginMark remoteExec, SessionSnapshotCapture snapshotCapture) : this(steamId, steamIdAbsenceReason, buildOrigin, remoteExec, snapshotCapture, new List<MissingItem>())
        {
        }

        internal SessionOriginSnapshot(string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin, RemoteExecOriginMark remoteExec, SessionSnapshotCapture snapshotCapture, IReadOnlyList<MissingItem> salvageMissing)
        {
            SteamId = steamId;
            SteamIdAbsenceReason = steamIdAbsenceReason;
            BuildOrigin = buildOrigin;
            RemoteExec = remoteExec;
            SnapshotCapture = snapshotCapture;
            SalvageMissing = salvageMissing;
        }

        // 所有印の付け直しでもSteamIDの欠損理由を落とさない。落とすと上書き後の印から理由が消える
        // Re-stamping ownership keeps the SteamID absence reason; dropping it would erase the reason from the rewritten mark
        internal SessionOriginSnapshot WithSnapshotCapture(SessionSnapshotCapture snapshotCapture)
        {
            return new SessionOriginSnapshot(SteamId, SteamIdAbsenceReason, BuildOrigin, RemoteExec, snapshotCapture, new List<MissingItem>());
        }

        internal SessionOriginSnapshot WithSalvageMissing(IReadOnlyList<MissingItem> missing)
        {
            return new SessionOriginSnapshot(SteamId, SteamIdAbsenceReason, BuildOrigin, RemoteExec, SnapshotCapture, new List<MissingItem>(missing));
        }

        public SalvageOperationResult WriteTo(string path)
        {
            var serializer = CreateSerializer();
            // 遠隔実行の真偽は印ファイルが正本。ここは台帳の在処だけを書き、2つの印はJSONへ複写しない
            // The signal files own the remote-exec truths; only the ledger's location is written here and neither signal is copied into JSON
            var json = new JObject
            {
                ["steamId"] = SteamId,
                ["remoteExecLedgerFileName"] = RemoteExec?.LedgerFileName,
                ["steamIdAbsenceReason"] = SteamIdAbsenceReason,
                ["buildOriginKind"] = BuildOrigin.Kind.ToString(),
                ["buildInfo"] = BuildOrigin.BuildInfo == null ? JValue.CreateNull() : JObject.FromObject(BuildOrigin.BuildInfo, serializer),
                ["buildOriginMissingReason"] = BuildOrigin.MissingReason,
                ["snapshotCapture"] = SnapshotCapture.ToJson(),
                ["salvageMissing"] = new JObject { ["version"] = 1, ["owner"] = SnapshotCapture.Owner, ["items"] = JArray.FromObject(SalvageMissing, serializer) },
            };

            // 出所の書き出しはディスクIO。失敗しても起動は続け、次回の箱では出所不明として欠損に表明される
            // Writing the origin is disk IO; boot continues on failure, and the next box declares the origin as unknown
            var write = BugReportFileOperations.WriteTextAtomically(path, json.ToString(Formatting.Indented));
            if (write.Succeeded) return write;
            var reason = $"セッションの出所を書けませんでした（このセッションが落ちると出所不明の箱になります） {write.FailureReason}";
            UnityEngine.Debug.LogError(reason);
            return SalvageOperationResult.Failure(reason);
        }

        // 前回セッションの出所の読み取りと型検証は専用の読み手へ委ねる
        // Delegate reading and type checks of the previous origin to its reader
        public static SessionOriginSnapshot ReadFrom(string path, out string failureReason)
        {
            return SessionOriginSnapshotReader.ReadFrom(path, out failureReason);
        }
    }
}
