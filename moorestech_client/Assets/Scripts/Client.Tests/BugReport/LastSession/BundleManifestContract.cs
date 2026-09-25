using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.WebUiHost.Game.Actions;
using Game.Paths;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests
{
    // plan C の prepare-run.sh / ship-outbox.sh が読むキーの検査。bug の箱と crash の箱で別々に書くと片側だけ欠けても気づけない
    // Checks the keys plan C's prepare-run.sh and ship-outbox.sh read; written separately per box kind, a gap on one side goes unnoticed
    public static class BundleManifestContract
    {
        public static void AssertPlanC(string bundle, JObject manifest)
        {
            Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportOutbox.ReadyMarkerFileName)), "READYが無い箱は運搬されない");
            Assert.IsNotNull(manifest["repository"], "prepare-run.sh が読む repository が無い");

            // 40桁で確認する。スレッド違反でgitを呼べていないと空文字のまま通ってしまう
            // Checked as 40 digits: a thread violation that never reaches git would slip through as an empty string
            Assert.AreEqual(40, ((string)manifest["repository"]["commit"]).Length, "prepare-run.sh が読む repository.commit がコミットハッシュでない");
            Assert.IsNotNull((string)manifest["repository"]["branch"], "prepare-run.sh が読む repository.branch が無い");
            Assert.IsNotNull(manifest["masterData"], "prepare-run.sh が読む masterData が無い");
            Assert.IsNotNull((string)manifest["masterData"]["commit"], "prepare-run.sh が読む masterData.commit が無い");
            Assert.IsInstanceOf<JArray>(manifest["snapshotTicks"], "prepare-run.sh が読む snapshotTicks が配列でない");
            Assert.IsInstanceOf<JArray>(manifest["missing"], "missing が配列でない");
        }

        // 文字列配列の列（snapshotFiles・packetLogFiles）を取り出す。分類先の取り違えを一覧で突き合わせるため
        // Pulls out a column of strings (snapshotFiles, packetLogFiles) so a misrouted classification can be compared as a list
        public static List<string> StringList(JObject manifest, string key)
        {
            return ((JArray)manifest[key]).Select(item => (string)item).ToList();
        }

        // 欠損は同じ項目名が複数回載りうる（確保側と書き出し側の両方が理由を足す）ため一覧のまま扱う
        // The same item can appear more than once (both capture and writer add reasons), so keep it as a list
        public static List<string> MissingItemNames(JObject manifest)
        {
            return ((JArray)manifest["missing"]).Select(item => (string)item["item"]).ToList();
        }

    }
}
