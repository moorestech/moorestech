using Client.Build.Policy;
using UnityEditor;

namespace Client.Editor.Build
{
    /// <summary>
    /// Playerビルド1回分の入力。用途ごとのfactoryだけが作れる（ADR 0071）
    /// Input for one Player build; only the per-purpose factories can construct it (ADR 0071)
    /// 用途とターゲットの不正な組合せを構築時点で作らせないため、コンストラクタは公開しない
    /// The constructor stays private so an invalid purpose/target pair cannot be built in the first place
    /// </summary>
    public class PlayerBuildRequest
    {
        public readonly BuildTarget Target;

        // 成果物を配置するディレクトリ（この直下に実行ファイルとgame/が並ぶ）
        // Directory receiving the artifact (player executable and game/ sit directly under it)
        public readonly string OutputDirectory;

        public readonly BuildPurpose Purpose;

        // 開発メニューだけが選ぶ。ほかの用途は規則から導く
        // Only the dev menu chooses; other purposes derive their mode from policy
        public readonly bool LocalDevelopmentChoosesDevelopment;

        private PlayerBuildRequest(BuildTarget target, string outputDirectory, BuildPurpose purpose, bool localDevelopmentChoosesDevelopment)
        {
            Target = target;
            OutputDirectory = outputDirectory;
            Purpose = purpose;
            LocalDevelopmentChoosesDevelopment = localDevelopmentChoosesDevelopment;
        }

        public static PlayerBuildRequest ForCi(BuildTarget target, string outputDirectory) =>
            new PlayerBuildRequest(target, outputDirectory, BuildPurpose.Ci, false);

        public static PlayerBuildRequest ForLocalDevelopment(BuildTarget target, string outputDirectory, bool choosesDevelopment) =>
            new PlayerBuildRequest(target, outputDirectory, BuildPurpose.LocalDevelopment, choosesDevelopment);

        // 展示会の起動スクリプトが.commandのためMacに固定する
        // The exhibition launch script is a .command, so the target is fixed to Mac
        public static PlayerBuildRequest ForExhibition(string outputDirectory) =>
            new PlayerBuildRequest(BuildTarget.StandaloneOSX, outputDirectory, BuildPurpose.Exhibition, false);

        public static PlayerBuildRequest ForSteamPlaytest(BuildTarget target, string outputDirectory) =>
            new PlayerBuildRequest(target, outputDirectory, BuildPurpose.SteamPlaytest, false);
    }

    /// <summary>
    /// Playerビルド1回分の結果（入口はこれを見て成果物の扱いを決める）
    /// Result of one Player build; entries decide what to do with the artifact from this
    /// </summary>
    public enum PlayerBuildOutcome
    {
        Succeeded,
        BuildTargetSwitchFailed,
        MacArchitecturePinFailed,
        AddressablesBuildFailed,
        PlayerBuildFailed,
        MacSigningFailed,
    }
}
