using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// action 名の C#⇔TS パリティテスト: 実装済みハンドラの ActionType 集合を正準フィクスチャと照合する
    /// フィクスチャは TS 側 vitest（actionNames.test.ts）と同一ファイルを参照する単一ソース
    /// web 契約外の action（playtest 系等）は excludedFromWebContract にデータとして列挙し、暗黙の除外を作らない
    /// C#⇔TS parity test for action names: match the implemented handlers' ActionType set against the canonical fixture
    /// The fixture is the single source, referenced by the TS-side vitest (actionNames.test.ts) too
    /// Actions outside the web contract (e.g. playtest) are listed as data in excludedFromWebContract; no implicit exclusions
    /// </summary>
    public class WireContractActionNamesTest
    {
        [Test]
        public void ActionNamesFixtureCoversAllHandlers()
        {
            var implemented = CollectImplementedActionTypes();
            var fixture = JObject.Parse(LoadFixture("action_names.json"));
            var shared = fixture["actions"].ToObject<List<string>>();
            var excluded = fixture["excludedFromWebContract"].ToObject<List<string>>();

            // 重複と非定数(null/空)は HashSet 比較で消える前に検出する
            // Catch duplicates and non-constant (null/empty) values before the set comparison can absorb them
            Assert.AreEqual(shared.Count, new HashSet<string>(shared).Count, "action_names.json に重複がある / duplicate action names");
            Assert.That(implemented, Is.All.Not.Null.And.Not.Empty, "ActionType が定数を返していない / an ActionType is not a constant");
            Assert.AreEqual(implemented.Count, new HashSet<string>(implemented).Count, "ActionType が重複している / duplicate ActionType across handlers");
            Assert.That(shared.Intersect(excluded), Is.Empty, "actions と excludedFromWebContract が重複 / an action is listed in both sections");
            Assert.That(implemented, Is.EquivalentTo(shared.Concat(excluded)), "実装ハンドラ全体が actions + excludedFromWebContract と不一致 / all implemented handlers must equal actions + excludedFromWebContract");

            #region Internal

            List<string> CollectImplementedActionTypes()
            {
                // Client.* の全アセンブリを走査し、Client.Playtest 等の web 契約外ハンドラも漏らさず数える
                // Scan every Client.* assembly so handlers outside the web contract (e.g. Client.Playtest) are also counted
                var types = System.AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name.StartsWith("Client."))
                    .SelectMany(a => a.GetTypes())
                    .Where(t => typeof(IActionHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                // ハンドラのコンストラクタはゲーム状態の依存を要求するため、生成せずに ActionType だけを読む
                // Handler constructors demand game-state dependencies, so read ActionType without constructing them
                return types.Select(t => ((IActionHandler)FormatterServices.GetUninitializedObject(t)).ActionType).ToList();
            }

            string LoadFixture(string fixtureName)
            {
                var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
                return File.ReadAllText(path);
            }

            #endregion
        }
    }
}
