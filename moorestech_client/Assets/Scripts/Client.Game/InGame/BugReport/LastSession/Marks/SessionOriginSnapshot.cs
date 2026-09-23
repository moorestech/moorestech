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
        internal readonly SessionSnapshotCapture SnapshotCapture;
        internal readonly IReadOnlyList<MissingItem> SalvageMissing;

        public SessionOriginSnapshot(string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin) : this(steamId, steamIdAbsenceReason, buildOrigin, SessionSnapshotCapture.NotStarted())
        {
        }

        internal SessionOriginSnapshot(string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin, SessionSnapshotCapture snapshotCapture) : this(steamId, steamIdAbsenceReason, buildOrigin, snapshotCapture, new List<MissingItem>())
        {
        }

        private SessionOriginSnapshot(string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin, SessionSnapshotCapture snapshotCapture, IReadOnlyList<MissingItem> salvageMissing)
        {
            SteamId = steamId;
            SteamIdAbsenceReason = steamIdAbsenceReason;
            BuildOrigin = buildOrigin;
            SnapshotCapture = snapshotCapture;
            SalvageMissing = salvageMissing;
        }

        // 所有印の付け直しでもSteamIDの欠損理由を落とさない。落とすと上書き後の印から理由が消える
        // Re-stamping ownership keeps the SteamID absence reason; dropping it would erase the reason from the rewritten mark
        internal SessionOriginSnapshot WithSnapshotCapture(SessionSnapshotCapture snapshotCapture)
        {
            return new SessionOriginSnapshot(SteamId, SteamIdAbsenceReason, BuildOrigin, snapshotCapture);
        }

        internal SessionOriginSnapshot WithSalvageMissing(IReadOnlyList<MissingItem> missing)
        {
            return new SessionOriginSnapshot(SteamId, SteamIdAbsenceReason, BuildOrigin, SnapshotCapture, new List<MissingItem>(missing));
        }

        public SalvageOperationResult WriteTo(string path)
        {
            var json = new JObject
            {
                ["steamId"] = SteamId,
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
            string steamIdAbsenceReason;
            string kindText;
            BuildInfo buildInfo;
            string buildOriginMissingReason;
            SessionSnapshotCapture snapshotCapture;
            IReadOnlyList<MissingItem> salvageMissing;

            // 読み込みはディスクIO、JObject.Parse は外部入力JSONのパース境界（途中で落ちたセッションは切れたJSONを残しうる）
            // Reading is disk IO and JObject.Parse is the external JSON parse boundary (a session that died midway can leave truncated JSON)
            try
            {
                var obj = JObject.Parse(File.ReadAllText(path));
                steamId = (string)obj["steamId"];
                steamIdAbsenceReason = ReadSteamIdAbsenceReason(steamId, obj["steamIdAbsenceReason"]);
                kindText = (string)obj["buildOriginKind"];
                var buildInfoToken = obj["buildInfo"];
                buildInfo = buildInfoToken == null || buildInfoToken.Type == JTokenType.Null ? null : buildInfoToken.ToObject<BuildInfo>(Serializer);
                buildOriginMissingReason = (string)obj["buildOriginMissingReason"];
                snapshotCapture = SessionSnapshotCapture.Read(obj["snapshotCapture"]);
                salvageMissing = ReadSalvageMissing(obj["salvageMissing"], snapshotCapture.Owner);
            }
            catch (Exception e) when (BugReportBundleWriter.IsDiskFailure(e) || e is JsonException || e is ArgumentException)
            {
                failureReason = $"セッション開始時の出所を読めない {path}: {e.GetBaseException().Message}";
                return null;
            }

            var buildOrigin = ToBuildOrigin(kindText, buildInfo, buildOriginMissingReason, path, out failureReason);
            return buildOrigin == null ? null : new SessionOriginSnapshot(steamId, steamIdAbsenceReason, buildOrigin, snapshotCapture, salvageMissing);

            #region Internal

            // SteamIDが有れば理由は不要。無いのに理由が無い（旧形式の印）なら、そう明示した理由にする
            // No reason is needed when a SteamID exists; a missing one without a reason (legacy mark) gets an explicit legacy reason
            static string ReadSteamIdAbsenceReason(string steamId, JToken reasonToken)
            {
                if (!string.IsNullOrEmpty(steamId)) return null;
                var reason = reasonToken?.Type == JTokenType.String ? (string)reasonToken : null;
                return string.IsNullOrWhiteSpace(reason) ? LegacyMarkSteamIdAbsenceReason : reason;
            }

            static IReadOnlyList<MissingItem> ReadSalvageMissing(JToken token, string owner)
            {
                // 旧形式や世代不一致を「欠損なし」にしない
                // Never interpret legacy or mismatched generations as having no missing evidence
                var unknown = new List<MissingItem> { new MissingItem { Item = "previousOrigin", Reason = "退避欠損の履歴が不明（旧形式・不正形式・所有世代不一致）" } };
                if (!(token is JObject ledger) || !JToken.DeepEquals(ledger["version"], new JValue(1)) ||
                    !JToken.DeepEquals(ledger["owner"], owner == null ? JValue.CreateNull() : new JValue(owner)) || !(ledger["items"] is JArray items)) return unknown;

                var result = new List<MissingItem>();
                foreach (var item in items)
                {
                    if (!(item is JObject entry) || entry["item"]?.Type != JTokenType.String || entry["reason"]?.Type != JTokenType.String ||
                        string.IsNullOrWhiteSpace((string)entry["item"]) || string.IsNullOrWhiteSpace((string)entry["reason"])) return unknown;
                    result.Add(new MissingItem { Item = (string)entry["item"], Reason = (string)entry["reason"] });
                }
                return result;
            }

            #endregion
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
