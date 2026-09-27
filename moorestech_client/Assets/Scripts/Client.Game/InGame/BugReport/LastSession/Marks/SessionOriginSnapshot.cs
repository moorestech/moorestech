using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.DiskOperations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // セッション開始時点の出所（ビルド・SteamID・スナップショット記録）。前回クラッシュの箱に「今回起動したビルドやワールド」を付けないため、落ちたセッション自身が書き残す（F12・D-C3）
    // The origin at session start (build, SteamID, snapshot capture); the crashed session writes it itself so the previous crash's box never carries the build or world launched this time (F12, D-C3)
    public sealed class SessionOriginSnapshot
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

        // 旧形式の印（理由キー無し）を読んだときの理由。無音でnullにすると欠損の原因が箱から消える
        // Reason used for a legacy mark without the reason key; a silent null would erase the gap's cause from the box
        internal const string LegacyMarkSteamIdAbsenceReason = "SteamIDの欠損理由が記録されていない旧形式の印（前回セッションの開始時点でテスター識別が無かった）";

        public string SteamId { get; }
        public string SteamIdAbsenceReason { get; }
        public BuildOriginReading BuildOrigin { get; }
        public string RemoteExecLedgerFileName { get; }

        // 有効判定は台帳ファイル名の有無だけから導く。専用のboolを別に持つと不整合な組み合わせが生じる
        // Derive the enabled state solely from the ledger file name's presence; a separate bool would allow an inconsistent combination
        public bool RemoteExecEnabled => RemoteExecLedgerFileName != null;
        internal readonly SessionSnapshotCapture SnapshotCapture;
        internal readonly IReadOnlyList<MissingItem> SalvageMissing;

        // remoteExecLedgerFileNameはnull＝無効。呼び出し側がRemoteExecLaunchOption等から解決した値をそのまま渡す
        // remoteExecLedgerFileName is null when disabled; callers pass the value they already resolved from RemoteExecLaunchOption etc.
        public SessionOriginSnapshot(string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin, string remoteExecLedgerFileName) : this(steamId, steamIdAbsenceReason, buildOrigin, remoteExecLedgerFileName, SessionSnapshotCapture.NotStarted())
        {
        }

        internal SessionOriginSnapshot(string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin, string remoteExecLedgerFileName, SessionSnapshotCapture snapshotCapture) : this(steamId, steamIdAbsenceReason, buildOrigin, remoteExecLedgerFileName, snapshotCapture, new List<MissingItem>())
        {
        }

        internal SessionOriginSnapshot(string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin, string remoteExecLedgerFileName, SessionSnapshotCapture snapshotCapture, IReadOnlyList<MissingItem> salvageMissing)
        {
            SteamId = steamId;
            SteamIdAbsenceReason = steamIdAbsenceReason;
            BuildOrigin = buildOrigin;
            RemoteExecLedgerFileName = remoteExecLedgerFileName;
            SnapshotCapture = snapshotCapture;
            SalvageMissing = salvageMissing;
        }

        // 所有印の付け直しでもSteamIDの欠損理由を落とさない。落とすと上書き後の印から理由が消える
        // Re-stamping ownership keeps the SteamID absence reason; dropping it would erase the reason from the rewritten mark
        internal SessionOriginSnapshot WithSnapshotCapture(SessionSnapshotCapture snapshotCapture)
        {
            return new SessionOriginSnapshot(SteamId, SteamIdAbsenceReason, BuildOrigin, RemoteExecLedgerFileName, snapshotCapture, new List<MissingItem>());
        }

        internal SessionOriginSnapshot WithSalvageMissing(IReadOnlyList<MissingItem> missing)
        {
            return new SessionOriginSnapshot(SteamId, SteamIdAbsenceReason, BuildOrigin, RemoteExecLedgerFileName, SnapshotCapture, new List<MissingItem>(missing));
        }

        public SalvageOperationResult WriteTo(string path)
        {
            var json = new JObject
            {
                ["steamId"] = SteamId,
                ["remoteExecEnabled"] = RemoteExecEnabled,
                ["remoteExecLedgerFileName"] = RemoteExecLedgerFileName,
                ["steamIdAbsenceReason"] = SteamIdAbsenceReason,
                ["buildOriginKind"] = BuildOrigin.Kind.ToString(),
                ["buildInfo"] = BuildOrigin.BuildInfo == null ? JValue.CreateNull() : JObject.FromObject(BuildOrigin.BuildInfo, Serializer),
                ["buildOriginMissingReason"] = BuildOrigin.MissingReason,
                ["snapshotCapture"] = SnapshotCapture.ToJson(),
                ["salvageMissing"] = new JObject { ["version"] = 1, ["owner"] = SnapshotCapture.Owner, ["items"] = JArray.FromObject(SalvageMissing, Serializer) },
            };

            // 出所の書き出しはディスクIO。失敗しても起動は続け、次回の箱では出所不明として欠損に表明される
            // Writing the origin is disk IO; boot continues on failure, and the next box declares the origin as unknown
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // 容量不足でも既存の所有証明を壊さない
                // Preserve existing ownership evidence even when disk space runs out
                var temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, json.ToString(Formatting.Indented));
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
                return SalvageOperationResult.Success();
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                var reason = $"セッションの出所を書けませんでした（このセッションが落ちると出所不明の箱になります） {path}: {e.Message}";
                Debug.LogError(reason);
                return SalvageOperationResult.Failure(reason);
            }
        }

        // 前回セッションの出所の読み取りと型検証は専用の読み手へ委ねる
        // Delegate reading and type checks of the previous origin to its reader
        public static SessionOriginSnapshot ReadFrom(string path, out string failureReason)
        {
            return SessionOriginSnapshotReader.ReadFrom(path, out failureReason);
        }
    }
}
