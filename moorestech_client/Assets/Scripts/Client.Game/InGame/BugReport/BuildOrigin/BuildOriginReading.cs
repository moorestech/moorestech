using System.Collections.Generic;

namespace Client.Game.InGame.BugReport.BuildOrigin
{
    // 実行中の実体がどこから来たか。Editor・焼き込み情報つきビルド・焼き込み情報の無いビルドの3状態だけを持つ
    // Where the running binary came from, limited to three states: the Editor, a build with baked info, and a build without it
    public enum BuildOriginKind
    {
        Editor,
        BakedBuild,
        BuildWithoutInfo,
    }

    // 出所の読み取り結果。読み手は RepositoryStateProbe.ReadBuildOrigin() の1本だけで、消費側の分岐はこの型だけを見る（F01）
    // The origin as read; RepositoryStateProbe.ReadBuildOrigin() is the only reader, and consumers branch on this type alone (F01)
    // 「build-info.json が無い配布ビルド」をnullで表すとEditorと同じ見た目になり、配布先で git probe 分岐へ入ってしまう
    // Encoding "a distributed build with no build-info.json" as null would look like the Editor and send a distributed build into the git-probe branch
    public sealed class BuildOriginReading
    {
        public BuildOriginKind Kind { get; }

        // BakedBuild のときだけ非null
        // Non-null only for BakedBuild
        public BuildInfo BuildInfo { get; }

        // BuildWithoutInfo のときだけ、焼き込み情報を読めなかった理由が入る
        // Only for BuildWithoutInfo: why the baked info could not be read
        public string MissingReason { get; }

        // BakedBuild で、焼き込み情報のうち欠けていたキーと理由
        // For BakedBuild: which keys of the baked info were missing, with reasons
        public IReadOnlyList<MissingItem> Missing { get; }

        private BuildOriginReading(BuildOriginKind kind, BuildInfo buildInfo, string missingReason, IReadOnlyList<MissingItem> missing)
        {
            Kind = kind;
            BuildInfo = buildInfo;
            MissingReason = missingReason;
            Missing = missing;
        }

        public static BuildOriginReading Editor()
        {
            return new BuildOriginReading(BuildOriginKind.Editor, null, null, new List<MissingItem>());
        }

        public static BuildOriginReading Baked(BuildInfo buildInfo)
        {
            return new BuildOriginReading(BuildOriginKind.BakedBuild, buildInfo, null, BuildInfoJson.CollectMissingKeys(buildInfo));
        }

        public static BuildOriginReading WithoutInfo(string reason)
        {
            return new BuildOriginReading(BuildOriginKind.BuildWithoutInfo, null, reason, new List<MissingItem>());
        }
    }
}
