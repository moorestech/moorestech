using UnityEditor;

/// <summary>
/// Playerビルド1回分の入力（入口ごとの契約差はここで表現する）
/// Input for one Player build; per-entry contract differences live here
/// </summary>
public class PlayerBuildRequest
{
    public BuildTarget Target;

    // 成果物を配置するディレクトリ（この直下に実行ファイルとgame/が並ぶ）
    // Directory receiving the artifact (player executable and game/ sit directly under it)
    public string OutputDirectory;

    public bool IsDevelopmentBuild;

    // trueなら同梱失敗を即ビルド失敗にする（ローカル配布用）。falseはCI互換の警告のみ
    // True fails the build on bundling problems (local distribution); false keeps CI-compatible warnings
    public bool IsStrictBundling;

    // ../moorestech_master/server_v8 を game/ として同梱するか（CI入口では行わない）
    // Whether to bundle ../moorestech_master/server_v8 as game/ (skipped for CI entries)
    public bool BundleLocalGameData;
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
