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
    /// C#⇔TS parity test for action names: match the implemented handlers' ActionType set against the canonical fixture
    /// The fixture is the single source, referenced by the TS-side vitest (actionNames.test.ts) too
    /// </summary>
    public class WireContractActionNamesTest
    {
        [Test]
        public void ActionNamesFixtureCoversAllHandlers()
        {
            var implemented = CollectImplementedActionTypes();
            var shared = JObject.Parse(LoadFixture("action_names.json"))["actions"].ToObject<List<string>>();

            Assert.AreEqual(shared.Count, new HashSet<string>(shared).Count, "action_names.json に重複がある / duplicate action names");
            Assert.That(new HashSet<string>(shared), Is.EquivalentTo(implemented), "action_names.json が C# の action 集合と不一致 / mismatch with the C# action set");
        }

        // ハンドラのコンストラクタはゲーム状態の依存を要求するため、生成せずに ActionType だけを読む
        // Handler constructors demand game-state dependencies, so read ActionType without constructing them
        private static HashSet<string> CollectImplementedActionTypes()
        {
            var types = typeof(IActionHandler).Assembly.GetTypes()
                .Where(t => typeof(IActionHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            return new HashSet<string>(types.Select(t => ((IActionHandler)FormatterServices.GetUninitializedObject(t)).ActionType));
        }

        private static string LoadFixture(string fixtureName)
        {
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            return File.ReadAllText(path);
        }
    }
}
