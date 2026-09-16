using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.BugReport.BuildOrigin
{
    // build-info.json の文字列パースと既存消費側への射影。RepositoryStateProbe.ReadBuildOrigin() の唯一の呼び出し元から使う
    // 分離した実体で、二重リーダーにはならない（ファイルI/Oは RepositoryStateProbe 側だけが行う。ADR 0059・行数分割のための分離）
    // Parses build-info.json text and projects it for existing consumers, used only by RepositoryStateProbe.ReadBuildOrigin();
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

            BuildInfo info;
            try
            {
                var obj = JObject.Parse(json);
                info = new BuildInfo
                {
                    Commit = NullIfEmpty((string)obj["commit"]),
                    Branch = NullIfEmpty((string)obj["branch"]),
                    Dirty = (bool?)obj["dirty"],
                    MasterDataCommit = NullIfEmpty((string)obj["masterCommit"]),
                    MasterDataDirty = (bool?)obj["masterDirty"],
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

            // 欠けた必須キーは既定値で埋めずnullのまま運び、理由だけを開発者ログへ残す
            // Missing required keys stay null instead of defaults, and only the reason goes to the developer log
            foreach (var missing in CollectMissingKeys(info)) Debug.LogWarning($"build-info.json の {missing.Item} が欠けています: {missing.Reason}");
            return info;
        }

        // 焼き込み情報のうち欠けていたキー。ログと報告の missing 列が同じ判定を使うためここ1箇所に置く
        // The baked keys that were missing; both the log and the report's missing column use this single rule
        public static List<MissingItem> CollectMissingKeys(BuildInfo info)
        {
            var missing = new List<MissingItem>();
            if (info.Commit == null) missing.Add(new MissingItem { Item = "buildInfo.commit", Reason = "build-info.json に commit が無い" });
            if (info.Branch == null) missing.Add(new MissingItem { Item = "buildInfo.branch", Reason = "build-info.json に branch が無い" });
            if (info.Dirty == null) missing.Add(new MissingItem { Item = "buildInfo.dirty", Reason = "build-info.json に dirty が無く未コミット変更の有無が不明" });
            if (info.MasterDataCommit != null && info.MasterDataDirty == null) missing.Add(new MissingItem { Item = "buildInfo.masterDirty", Reason = "build-info.json に masterDirty が無くマスタの未コミット変更の有無が不明" });
            return missing;
        }

        // BugReportRepositoryFiles等の既存消費側が使う形への射影。読み手を増やさず BuildInfo だけから導く（ADR 0059）
        // Projects into the shape existing consumers (e.g. BugReportRepositoryFiles) use, derived only from BuildInfo without adding a reader (ADR 0059)
        public static BugReportBuildInfo ToBugReportBuildInfo(BuildInfo buildInfo)
        {
            var repository = new RepositoryState { Commit = buildInfo.Commit, Branch = buildInfo.Branch, Dirty = buildInfo.Dirty };

            // マスタを焼いていないビルドもあるため、masterCommit が読めたときだけマスタの状態を名乗る
            // Some builds bake no master, so the master state is claimed only when masterCommit was actually readable
            if (buildInfo.MasterDataCommit == null)
            {
                Debug.LogWarning($"build-info.json に masterCommit が無いためマスタデータのリポジトリ状態は不明です path:{Path.Combine(Application.streamingAssetsPath, RepositoryStateProbe.BuildInfoFileName)}");
                return new BugReportBuildInfo { Repository = repository };
            }

            var masterData = new RepositoryState { Commit = buildInfo.MasterDataCommit, Branch = null, Dirty = buildInfo.MasterDataDirty };
            return new BugReportBuildInfo { Repository = repository, MasterData = masterData };
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
