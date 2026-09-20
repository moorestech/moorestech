using System;
using System.IO;
using Client.Game.InGame.BugReport.BuildOrigin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // セッション開始時点の出所（ビルド・SteamID・退避元）。前回クラッシュの箱に「今回起動したビルドやワールド」を付けないため、落ちたセッション自身が書き残す（F12・D-C3）
    // The origin at session start (build, SteamID, salvage source); the crashed session writes it itself so the previous crash's box never carries the build or world launched this time (F12, D-C3)
    public sealed class SessionOriginSnapshot
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

        public string SteamId { get; }
        public BuildOriginReading BuildOrigin { get; }

        // 退避元を記録しない旧版の印はnull。無音で今回の既定ワールドへ落とさず「スナップショット源不明」として欠損に表明する（D-C3）
        // Null for an older mark that recorded no source; instead of silently falling back to this boot's default world it is declared missing as an unknown source (D-C3)
        public SessionSnapshotSource SnapshotSource { get; }

        public SessionOriginSnapshot(string steamId, BuildOriginReading buildOrigin, SessionSnapshotSource snapshotSource)
        {
            SteamId = steamId;
            BuildOrigin = buildOrigin;
            SnapshotSource = snapshotSource;
        }

        public void WriteTo(string path)
        {
            var json = new JObject
            {
                ["steamId"] = SteamId,
                ["buildOriginKind"] = BuildOrigin.Kind.ToString(),
                ["buildInfo"] = BuildOrigin.BuildInfo == null ? JValue.CreateNull() : JObject.FromObject(BuildOrigin.BuildInfo, Serializer),
                ["buildOriginMissingReason"] = BuildOrigin.MissingReason,
                ["snapshotSource"] = SnapshotSource == null
                    ? JValue.CreateNull()
                    : new JObject
                    {
                        ["isRemoteConnection"] = SnapshotSource.IsRemoteConnection,
                        ["worldSnapshotDirectory"] = SnapshotSource.WorldSnapshotDirectory,
                    },
            };

            // 出所の書き出しはディスクIO。失敗しても起動は続け、次回の箱では出所不明として欠損に表明される
            // Writing the origin is disk IO; boot continues on failure, and the next box declares the origin as unknown
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json.ToString(Formatting.Indented));
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e))
            {
                Debug.LogError($"セッションの出所を書けませんでした（このセッションが落ちると出所不明の箱になります） {path}: {e.Message}");
            }
        }

        // 読めなければnullと理由を返す。無音で今回のビルドへ差し替えると、別ビルドのクラッシュとして再現される
        // Returns null with a reason when unreadable; silently substituting this boot's build would reproduce the crash on a different build
        public static SessionOriginSnapshot ReadFrom(string path, out string failureReason)
        {
            failureReason = null;
            if (!File.Exists(path))
            {
                failureReason = $"セッション開始時の出所の印が無い: {path}";
                return null;
            }

            string steamId;
            string kindText;
            BuildInfo buildInfo;
            string buildOriginMissingReason;
            SessionSnapshotSource snapshotSource;

            // 読み込みはディスクIO、JObject.Parse は外部入力JSONのパース境界（途中で落ちたセッションは切れたJSONを残しうる）
            // Reading is disk IO and JObject.Parse is the external JSON parse boundary (a session that died midway can leave truncated JSON)
            try
            {
                var obj = JObject.Parse(File.ReadAllText(path));
                steamId = (string)obj["steamId"];
                kindText = (string)obj["buildOriginKind"];
                var buildInfoToken = obj["buildInfo"];
                buildInfo = buildInfoToken == null || buildInfoToken.Type == JTokenType.Null ? null : buildInfoToken.ToObject<BuildInfo>(Serializer);
                buildOriginMissingReason = (string)obj["buildOriginMissingReason"];
                snapshotSource = ReadSnapshotSource(obj["snapshotSource"]);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e) || e is JsonException || e is ArgumentException)
            {
                failureReason = $"セッション開始時の出所を読めない {path}: {e.GetBaseException().Message}";
                return null;
            }

            var buildOrigin = ToBuildOrigin(kindText, buildInfo, buildOriginMissingReason, path, out failureReason);
            return buildOrigin == null ? null : new SessionOriginSnapshot(steamId, buildOrigin, snapshotSource);
        }

        // 退避元を持たない旧版の印はnullのまま返す。読めた値だけを信じ、欠けている項目を今回の起動設定で埋めない（D-C3）
        // An older mark without a source comes back as null; only what was read is trusted and no missing field is filled from this boot's settings (D-C3)
        private static SessionSnapshotSource ReadSnapshotSource(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) return null;
            var isRemoteConnection = (bool?)token["isRemoteConnection"];
            if (isRemoteConnection == null) return null;
            return new SessionSnapshotSource(isRemoteConnection.Value, (string)token["worldSnapshotDirectory"]);
        }

        private static BuildOriginReading ToBuildOrigin(string kindText, BuildInfo buildInfo, string missingReason, string path, out string failureReason)
        {
            failureReason = null;
            if (!Enum.TryParse<BuildOriginKind>(kindText, out var kind))
            {
                failureReason = $"セッション開始時の出所の種類が読めない value:{kindText} path:{path}";
                return null;
            }

            if (kind == BuildOriginKind.Editor) return BuildOriginReading.Editor();
            if (kind == BuildOriginKind.BuildWithoutInfo) return BuildOriginReading.WithoutInfo(missingReason ?? "前回セッションの開始時点で build-info.json を読めていなかった");
            if (buildInfo != null) return BuildOriginReading.Baked(buildInfo);

            failureReason = $"焼き込み情報つきビルドと記録されているのに buildInfo が無い path:{path}";
            return null;
        }
    }
}
