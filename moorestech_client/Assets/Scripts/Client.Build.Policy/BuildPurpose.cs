using System;

namespace Client.Build.Policy
{
    /// <summary>
    /// Playerビルドの目的。strict検査と同梱物はここからだけ導く（ADR 0071）
    /// The purpose of a Player build; strict checks and bundled content derive from it alone (ADR 0071)
    /// </summary>
    public enum BuildPurpose
    {
        // 既定値がCiになると書き忘れが無言で通るため、0値は用途未指定に充てる
        // A forgotten assignment would silently mean Ci, so the zero value stands for "not specified"
        Unspecified,
        Ci,
        LocalDevelopment,
        Exhibition,
        SteamPlaytest,
    }

    /// <summary>
    /// 用途から導いた1回分のビルド方針
    /// The build policy derived from a purpose for one build
    /// </summary>
    public readonly struct BuildPurposePolicy
    {
        public readonly bool IsStrictBundling;
        public readonly bool BundlesLocalGameData;
        public readonly bool BundlesExhibitionLaunchScript;
        public readonly bool PinsAppleSilicon;
        public readonly bool ReSignsMacApp;
        public readonly bool IsDevelopmentBuild;

        public BuildPurposePolicy(bool isStrictBundling, bool bundlesLocalGameData, bool bundlesExhibitionLaunchScript,
            bool pinsAppleSilicon, bool reSignsMacApp, bool isDevelopmentBuild)
        {
            IsStrictBundling = isStrictBundling;
            BundlesLocalGameData = bundlesLocalGameData;
            BundlesExhibitionLaunchScript = bundlesExhibitionLaunchScript;
            PinsAppleSilicon = pinsAppleSilicon;
            ReSignsMacApp = reSignsMacApp;
            IsDevelopmentBuild = isDevelopmentBuild;
        }
    }

    /// <summary>
    /// 用途ごとの同梱・検査方針の単一の導出点
    /// The single place deriving bundling and check policy from a purpose
    /// </summary>
    public static class BuildPurposeRules
    {
        // 用途を1つ足すときに触るのはこのswitchだけになるよう、全方針を1度に返す
        // Adding a purpose touches only this switch, because every policy is returned at once
        public static BuildPurposePolicy Resolve(BuildPurpose purpose, bool localDevelopmentChoosesDevelopment)
        {
            switch (purpose)
            {
                // CIはmaster data無しで焼き、メモリ節約のためDevelopmentで固定する
                // CI builds without master data and pins Development to save memory
                case BuildPurpose.Ci:
                    return new BuildPurposePolicy(
                        isStrictBundling: false, bundlesLocalGameData: false, bundlesExhibitionLaunchScript: false,
                        pinsAppleSilicon: false, reSignsMacApp: false, isDevelopmentBuild: true);

                // 手元焼きは同梱失敗を警告で流し、Development可否だけ人が選ぶ
                // Local builds warn through bundling failures; only Development mode is chosen by a human
                case BuildPurpose.LocalDevelopment:
                    return new BuildPurposePolicy(
                        isStrictBundling: false, bundlesLocalGameData: true, bundlesExhibitionLaunchScript: false,
                        pinsAppleSilicon: false, reSignsMacApp: false, isDevelopmentBuild: localDevelopmentChoosesDevelopment);

                // 人へ配る成果物はarm64固定と再署名まで通し、欠けたらビルドを落とす
                // Artifacts handed to people go through arm64 pinning and re-signing, and fail the build when incomplete
                case BuildPurpose.Exhibition:
                    return new BuildPurposePolicy(
                        isStrictBundling: true, bundlesLocalGameData: true, bundlesExhibitionLaunchScript: true,
                        pinsAppleSilicon: true, reSignsMacApp: true, isDevelopmentBuild: false);

                case BuildPurpose.SteamPlaytest:
                    return new BuildPurposePolicy(
                        isStrictBundling: true, bundlesLocalGameData: true, bundlesExhibitionLaunchScript: false,
                        pinsAppleSilicon: true, reSignsMacApp: true, isDevelopmentBuild: false);

                default:
                    throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }
    }
}
