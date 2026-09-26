using UnityEditor;

namespace Client.Editor.Build
{
    /// <summary>
    /// Playerビルド1回分の入力（入口ごとの違いは用途で表す）
    /// Input for one Player build; per-entry differences are expressed by the purpose
    /// </summary>
    public class PlayerBuildRequest
    {
        public BuildTarget Target;

        // 成果物を配置するディレクトリ（この直下に実行ファイルとgame/が並ぶ）
        // Directory receiving the artifact (player executable and game/ sit directly under it)
        public string OutputDirectory;

        public BuildPurpose Purpose;

        // 開発メニューだけが選ぶ。ほかの用途は規則から導く
        // Only the dev menu chooses; other purposes derive their mode from policy
        public bool LocalDevelopmentChoosesDevelopment;
    }

    /// <summary>
    /// Playerビルド1回分の結果（入口はこれを見て成果物の扱いを決める）
    /// Result of one Player build; entries decide what to do with the artifact from this
    /// </summary>
    public enum PlayerBuildOutcome
    {
        Succeeded,
        AddressablesBuildFailed,
        PlayerBuildFailed,
    }
}
