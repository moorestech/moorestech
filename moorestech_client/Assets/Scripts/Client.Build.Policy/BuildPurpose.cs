using System;

namespace Client.Editor.Build
{
    /// <summary>
    /// Playerビルドの目的。strict検査と同梱物はここからだけ導く（ADR 0071）
    /// The purpose of a Player build; strict checks and bundled content derive from it alone (ADR 0071)
    /// </summary>
    public enum BuildPurpose
    {
        Ci,
        LocalDevelopment,
        Exhibition,
        SteamPlaytest,
    }

    /// <summary>
    /// 用途ごとの同梱・検査方針の単一の導出点
    /// The single place deriving bundling and check policy from a purpose
    /// </summary>
    public static class BuildPurposeRules
    {
        // 人へ配る成果物だけは同梱・出所の問題でビルドを落とす
        // Only artifacts handed to people fail the build on bundling or origin problems
        public static bool IsStrictBundling(BuildPurpose purpose)
        {
            switch (purpose)
            {
                case BuildPurpose.Ci:
                case BuildPurpose.LocalDevelopment:
                    return false;
                case BuildPurpose.Exhibition:
                case BuildPurpose.SteamPlaytest:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }

        // CIはmaster data無しで焼くため同梱しない
        // CI builds without master data, so it never bundles game data
        public static bool BundlesLocalGameData(BuildPurpose purpose)
        {
            switch (purpose)
            {
                case BuildPurpose.Ci:
                    return false;
                case BuildPurpose.LocalDevelopment:
                case BuildPurpose.Exhibition:
                case BuildPurpose.SteamPlaytest:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }

        // 展示会ブースの再起動ループは展示会ビルドにだけ入れる
        // The booth restart loop ships with exhibition builds only
        public static bool BundlesExhibitionLaunchScript(BuildPurpose purpose)
        {
            switch (purpose)
            {
                case BuildPurpose.Exhibition:
                    return true;
                case BuildPurpose.Ci:
                case BuildPurpose.LocalDevelopment:
                case BuildPurpose.SteamPlaytest:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }

        // CIのDevelopmentはメモリ節約、配布はRelease固定
        // CI uses Development to save memory; distribution always uses Release
        public static bool IsDevelopmentBuild(BuildPurpose purpose, bool localDevelopmentChoice)
        {
            switch (purpose)
            {
                case BuildPurpose.Ci:
                    return true;
                case BuildPurpose.LocalDevelopment:
                    return localDevelopmentChoice;
                case BuildPurpose.Exhibition:
                case BuildPurpose.SteamPlaytest:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }
    }
}
