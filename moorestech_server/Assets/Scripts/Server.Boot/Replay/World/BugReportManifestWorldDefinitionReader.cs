using System;
using System.IO;
using Game.Paths;
using Newtonsoft.Json;

namespace Server.Boot.Replay.World
{
    // 箱の manifest.json から worldDefinition の宣言を読む。宣言が無い・読めない箱は理由を返し、扱いは呼び出し側が決める
    // Reads the worldDefinition declaration from the box's manifest.json; an undeclared or unreadable box yields a reason and the caller decides
    public static class BugReportManifestWorldDefinitionReader
    {
        private const string WorldDefinitionKey = "worldDefinition";

        // 読むのは worldDefinition だけ。キーが無ければ null で、宣言の無い旧版の箱と分かる
        // Only worldDefinition is read; an absent key stays null, marking an older box that declares nothing
        private sealed class ManifestWorldDefinitionJson
        {
            [JsonProperty(WorldDefinitionKey)] public BugReportWorldDefinition? WorldDefinition;
        }

        public static bool TryRead(string bundleDirectory, out BugReportWorldDefinition definition, out string undeclaredReason)
        {
            definition = BugReportWorldDefinition.NotCaptured;
            undeclaredReason = null;
            var manifestPath = Path.Combine(bundleDirectory, BugReportBundleLayout.ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                undeclaredReason = $"{BugReportBundleLayout.ManifestFileName} が無い path:{manifestPath}";
                return false;
            }

            // manifest は別マシンで作られた外部入力（ファイルI/O・権限とJSONパースの境界）。未知の語も StringEnumConverter が JsonSerializationException にするのでここで閉じる
            // The manifest is external input from another machine (file I/O, permission and JSON parse boundary); StringEnumConverter turns an unknown word into a JsonSerializationException, so it is contained here too
            try
            {
                var declared = JsonConvert.DeserializeObject<ManifestWorldDefinitionJson>(File.ReadAllText(manifestPath))?.WorldDefinition;
                if (declared == null)
                {
                    undeclaredReason = $"{WorldDefinitionKey} が無い（ADR 0064 以前の箱） path:{manifestPath}";
                    return false;
                }
                definition = declared.Value;
                return true;
            }
            catch (JsonException e)
            {
                undeclaredReason = $"{WorldDefinitionKey} を読めない: {e.Message} path:{manifestPath}";
                return false;
            }
            catch (IOException e)
            {
                undeclaredReason = $"{BugReportBundleLayout.ManifestFileName} を読めない: {e.Message} path:{manifestPath}";
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                undeclaredReason = $"{BugReportBundleLayout.ManifestFileName} を読めない: {e.Message} path:{manifestPath}";
                return false;
            }
        }
    }
}
