using System;
using System.IO;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server.Boot.Replay.World
{
    // 宣言の読み取り結果の3値。推定に回してよいのはキーの無い旧版の箱だけで、壊れた宣言は拒否する
    // The three outcomes of reading the declaration; only an old box without the key may fall back to inference, a broken declaration is refused
    public enum BugReportWorldDeclarationStatus
    {
        Declared,
        LegacyUndeclared,
        Malformed,
    }

    // 箱の manifest.json から worldDefinition の宣言を読む。宣言の有無・破損の判定はここ1箇所で行う（ADR 0064）
    // Reads the worldDefinition declaration from the box's manifest.json; whether it is declared or broken is decided here alone (ADR 0064)
    public static class BugReportManifestWorldDefinitionReader
    {
        private const string WorldDefinitionKey = "worldDefinition";

        // Declared なら definition に宣言、LegacyUndeclared と Malformed なら reason にログ・拒否用の理由を返す
        // Declared yields the declaration in definition; LegacyUndeclared and Malformed yield a reason for the log or the rejection
        public static BugReportWorldDeclarationStatus Read(string bundleDirectory, out BugReportWorldDefinition definition, out string reason)
        {
            definition = BugReportWorldDefinition.NotCaptured;
            reason = null;
            var manifestPath = Path.Combine(bundleDirectory, BugReportBundleLayout.ManifestFileName);
            if (!File.Exists(manifestPath)) return Malformed($"{BugReportBundleLayout.ManifestFileName} が無い path:{manifestPath}", out reason);

            var manifest = ParseManifest(manifestPath, out var parseError);
            if (manifest == null) return Malformed($"{BugReportBundleLayout.ManifestFileName} を読めない: {parseError} path:{manifestPath}", out reason);

            // キーが無いのは ADR 0064 以前の箱だけ。null・数値・未知の語は宣言の破損として扱い推定に回さない
            // Only a box predating ADR 0064 lacks the key; null, a number or an unknown word is a broken declaration and never falls back to inference
            if (!manifest.TryGetValue(WorldDefinitionKey, out var token))
            {
                reason = $"{WorldDefinitionKey} が無い（ADR 0064 以前の箱） path:{manifestPath}";
                return BugReportWorldDeclarationStatus.LegacyUndeclared;
            }
            if (token.Type != JTokenType.String) return Malformed($"{WorldDefinitionKey} が文字列でない（{token.Type}: {token.ToString(Formatting.None)}） path:{manifestPath}", out reason);
            var text = (string)token;
            if (!BugReportWorldDefinitionText.TryParse(text, out definition)) return Malformed($"{WorldDefinitionKey} が未知の語 '{text}' path:{manifestPath}", out reason);
            return BugReportWorldDeclarationStatus.Declared;

            #region Internal

            // manifest は別マシンで作られた外部入力（ファイルI/O・権限とJSONパースの境界）。読めない理由を返し、呼び出し側が拒否理由に載せる
            // The manifest is external input from another machine (file I/O, permission and JSON parse boundary); the reason is returned for the caller's rejection
            JObject ParseManifest(string path, out string error)
            {
                error = null;
                try
                {
                    return JObject.Parse(File.ReadAllText(path));
                }
                catch (JsonException e)
                {
                    error = e.Message;
                    return null;
                }
                catch (IOException e)
                {
                    error = e.Message;
                    return null;
                }
                catch (UnauthorizedAccessException e)
                {
                    error = e.Message;
                    return null;
                }
            }

            #endregion
        }

        private static BugReportWorldDeclarationStatus Malformed(string problem, out string reason)
        {
            reason = problem;
            return BugReportWorldDeclarationStatus.Malformed;
        }
    }
}
