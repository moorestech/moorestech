using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Client.Tests.UnitTest.CiShard
{
    // 専用shardのCategory集合をCIのshard filterスクリプトから導く。正本はスクリプトの配列1つのまま保つ
    // shard名→Category名の規則はスクリプトのshard_category_nameと同じ（CiShard + ハイフン区切りの各語の先頭大文字化）
    // Derives the dedicated shard categories from the CI shard filter script, keeping its array as the single source of truth
    // The shard-to-category rule mirrors the script's shard_category_name (CiShard + each hyphen segment capitalized)
    public static class DedicatedShardCategoryCatalog
    {
        private static readonly Regex DedicatedShardsArrayPattern = new(@"^dedicated_shards=\((.*?)\)", RegexOptions.Multiline | RegexOptions.Singleline);
        private static readonly Regex ShellCommentPattern = new(@"#.*$", RegexOptions.Multiline);

        // Application.dataPathはmoorestech_client/Assets。repoルートはその2つ上
        // Application.dataPath is moorestech_client/Assets, so the repo root is two levels up
        public static string ShardFilterScriptPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".github", "scripts", "unity-test-shard-filter.sh"));

        public static HashSet<string> Load()
        {
            return Parse(File.ReadAllText(ShardFilterScriptPath));
        }

        public static HashSet<string> Parse(string shardFilterScript)
        {
            // 配列を読めないならメタテストの前提が崩れている。空集合で全件違反に化けさせず原因を名指す
            // An unreadable array breaks the meta test's premise, so name the cause instead of turning it into all-offender noise
            var match = DedicatedShardsArrayPattern.Match(shardFilterScript);
            if (!match.Success) throw new InvalidOperationException("dedicated_shards=( ... ) was not found in the shard filter script.");

            var shardNames = ShellCommentPattern.Replace(match.Groups[1].Value, "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return new HashSet<string>(shardNames.Select(ToCategoryName));
        }

        public static string ToCategoryName(string shardName)
        {
            var segments = shardName.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            return "CiShard" + string.Concat(segments.Select(segment => char.ToUpperInvariant(segment[0]) + segment.Substring(1)));
        }
    }
}
