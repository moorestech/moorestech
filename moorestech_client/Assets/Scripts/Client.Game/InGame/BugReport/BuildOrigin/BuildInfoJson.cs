using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.BugReport.BuildOrigin
{
    // build-info.json の文字列パースと既存消費側への射影。RepositoryStateProbe.ReadBuildInfo() の唯一の呼び出し元から使う
    // 分離した実体で、二重リーダーにはならない（ファイルI/Oは RepositoryStateProbe 側だけが行う。ADR 0059・行数分割のための分離）
    // Parses build-info.json text and projects it for existing consumers, used only by RepositoryStateProbe.ReadBuildInfo();
    // splitting this out is not a second reader since file I/O still happens only in RepositoryStateProbe (ADR 0059; split to respect the line limit)
    internal static class BuildInfoJson
    {
        // 外部入力JSONのパースは境界。壊れた配布物でも報告経路を落とさないためここだけcatchする
        // Parsing externally supplied JSON is a boundary; only here we catch so a broken artifact never kills the report path
        // 値の取り出しもキャスト例外を出す同じパースの一部なので、JObject.Parseと同じtryの内側に置く
        // Reading the values throws from the same parse, so it stays inside the try that wraps JObject.Parse
        public static BuildInfo Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("build-info.json の内容が空のため buildInfo は null になります");
                return null;
            }

            try
            {
                var obj = JObject.Parse(json);
                return new BuildInfo
                {
                    Commit = (string)obj["commit"] ?? "",
                    Branch = (string)obj["branch"] ?? "",
                    Dirty = (bool?)obj["dirty"] ?? false,
                    MasterDataCommit = (string)obj["masterCommit"],
                    MasterDataDirty = (bool?)obj["masterDirty"] ?? false,
                    SteamBuildLabel = (string)obj["steamBuildLabel"],
                    BuiltAt = (string)obj["builtAt"],
                    Target = (string)obj["target"],
                };
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"build-info.json を解釈できません: {exception.GetBaseException().Message}");
                return null;
            }
        }

        // BugReportRepositoryFiles等の既存消費側が使う形への射影。読み手を増やさず BuildInfo だけから導く（ADR 0059）
        // Projects into the shape existing consumers (e.g. BugReportRepositoryFiles) use, derived only from BuildInfo without adding a reader (ADR 0059)
        public static BugReportBuildInfo ToBugReportBuildInfo(BuildInfo buildInfo)
        {
            if (buildInfo == null) return new BugReportBuildInfo { Repository = new RepositoryState { Commit = "", Branch = "", Dirty = false } };

            var repository = new RepositoryState { Commit = buildInfo.Commit ?? "", Branch = buildInfo.Branch ?? "", Dirty = buildInfo.Dirty };

            // マスタを焼いていないビルドもあるため、masterCommit が読めたときだけマスタの状態を名乗る
            // Some builds bake no master, so the master state is claimed only when masterCommit was actually readable
            if (string.IsNullOrEmpty(buildInfo.MasterDataCommit))
            {
                Debug.LogWarning($"build-info.json に masterCommit が無いためマスタデータのリポジトリ状態は不明です path:{Path.Combine(Application.streamingAssetsPath, RepositoryStateProbe.BuildInfoFileName)}");
                return new BugReportBuildInfo { Repository = repository };
            }

            var masterData = new RepositoryState { Commit = buildInfo.MasterDataCommit, Branch = "", Dirty = buildInfo.MasterDataDirty };
            return new BugReportBuildInfo { Repository = repository, MasterData = masterData };
        }
    }
}
