using System;
using System.Collections.Generic;
using System.Globalization;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Game.InGame.BugReport.Playtest;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // バンドルの唯一の契約。取得側と再現側はこの形だけで会話する（ADR 0057）
    // The bundle's single contract; capture and reproduction sides talk only through this shape (ADR 0057)
    public sealed class BugReportManifest
    {
        // Vector3のnormalized等が自己参照ループを起こすため専用converterでx/y/zのみ書く
        // Vector3's normalized etc. trigger reference loops; a scoped converter writes only x/y/z
        private static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            Converters = { new Vector3JsonConverter() },
        };

        // 2: serverData（記録時にサーバーがマスタを読んだ場所）を追加。再現側はこれが無いと別マスタで再生する
        // 2: added serverData (where the server read its masters); without it the reproduction replays different masters
        // 3: worldDefinitionを追加（ADR 0064）
        // 3: added worldDefinition (ADR 0064)
        public int SchemaVersion = 3;
        public string CreatedAt;
        public string Description;

        // プレイ報告の種別の契約値（JSON化の時点で PlaytestReportKindText が綴る）。取り込み側は bug のときだけ自動修正ランを起動する（ADR 0058）
        // The report kind's contract value, spelled by PlaytestReportKindText at serialization; the ingest side starts an auto-fix run only for bug (ADR 0058)
        public string Kind;

        // 送り手のSteamIDと配布ビルドの出所。取れなければ空文字でなくnullで、理由は missing 列に残す。Editorなら buildInfo は null
        // The sender's SteamID and the build origin; null rather than an empty string when unavailable with the reason in missing, and null buildInfo in the Editor
        public string SteamId;
        public BuildInfo BuildInfo;
        public string Platform;
        public bool IsEditor;
        public ulong ReportTick;

        // world/の中身の契約値（BugReportWorldDefinitionText が綴る）。取り込めなかった箱（前回異常終了の箱を含む）は not-captured のまま出る
        // The contract value for world/'s contents, spelled by BugReportWorldDefinitionText; a box whose world was not captured (the previous-crash box included) goes out as not-captured
        public string WorldDefinition = BugReportWorldDefinitionText.ToContractText(BugReportWorldDefinition.NotCaptured);
        public List<ulong> SnapshotTicks = new();
        public List<string> SnapshotFiles = new();
        public List<string> PacketLogFiles = new();
        public RepositoryState Repository;
        public RepositoryState MasterData;
        public ServerDataLocation ServerData;
        public ClientStateSnapshot ClientState;
        public List<MissingItem> Missing = new();
        public double VideoSeconds;

        // 箱の種別に依らない共通見出し。crash と bug で別々に組み立てていた頃は片方だけ列が欠けても誰も気づけなかった
        // The header every kind of box shares; while crash and bug built it separately, a column missing on one side went unnoticed
        public static BugReportManifest CreateHeader(string description, PlaytestReportKind kind, string steamId, string steamIdAbsenceReason, BuildOriginReading buildOrigin)
        {
            var manifest = new BugReportManifest
            {
                CreatedAt = DateTime.UtcNow.ToString(BugReportBundleLayout.Utc8601Format, CultureInfo.InvariantCulture),
                Description = description,
                Kind = PlaytestReportKindText.ToContractText(kind),
                SteamId = string.IsNullOrEmpty(steamId) ? null : steamId,
                BuildInfo = buildOrigin.BuildInfo,
                Platform = Application.platform.ToString(),
                IsEditor = Application.isEditor,
            };

            // 空文字のSteamIDは「識別子が空の実テスター」に読める。nullで出し、取れなかった事実を欠損列へ残す（F02）
            // An empty SteamID reads as a real tester with a blank id; it goes out as null with the gap declared in missing (F02)
            if (manifest.SteamId == null) manifest.AddMissing("steamId", steamIdAbsenceReason);
            return manifest;
        }

        // 欠損は開発者向けログとmanifestの両方へ残す。片方だけだと調査時にもう片方へ辿り着けない
        // Every missing item goes to both the developer log and the manifest; one alone leaves an investigator stranded
        public void AddMissing(string item, string reason)
        {
            Debug.LogWarning($"バグ報告バンドルに {item} を入れられませんでした: {reason}");
            Missing.Add(new MissingItem { Item = item, Reason = reason });
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Settings);
        }
    }

    // 取れなかった値は null。false や "" は「クリーン」「ブランチ名が空」という実値に化けるため使わない（F02）
    // An unavailable value is null; false or "" would pose as real values such as "clean" or "an empty branch name" (F02)
    public sealed class RepositoryState
    {
        public string Commit;
        public string Branch;
        public bool? Dirty;
    }

    // ビルドに焼き込まれたリポジトリ状態。マスタ側は焼かれていないビルドがあるため、読めなかったときは null のまま名乗らない
    // The repository state baked into a build; the master side is null when the build baked none, so it is never claimed
    public sealed class BugReportBuildInfo
    {
        public RepositoryState Repository;
        public RepositoryState MasterData;
    }

    public sealed class MissingItem
    {
        public string Item;
        public string Reason;
    }

    // Vector3をx/y/zの3値だけへ明示的に詰め替えるconverter。循環参照設定に頼らない
    // Explicitly narrows a Vector3 down to x/y/z instead of relying on reference-loop settings
    public sealed class Vector3JsonConverter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // マニフェストはクライアントから自動修正ランへの片方向出力のみで復元経路は無い
            // The manifest is a one-way output from client to auto-fix run; there is no read-back path
            throw new NotSupportedException("BugReportManifestのVector3は書き込み専用");
        }
    }
}
