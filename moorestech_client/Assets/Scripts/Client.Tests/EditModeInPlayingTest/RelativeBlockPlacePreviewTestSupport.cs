using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Game.InGame.BlockSystem.PlaceSystem.PreviewGhost;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util.AnchorRelative;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    ///     相対座標ゴーストのPlayModeテストで共有するチュートリアル生成
    ///     Shared tutorial construction for the relative-ghost PlayMode tests
    /// </summary>
    public static class RelativeBlockPlacePreviewTestSupport
    {
        // テストmodのchallenges.jsonへ相対座標プレビューのチュートリアルを1件差し込み、生成型として取り出す
        // Insert one relative-placement-preview tutorial into the test mod's challenges.json and take it back as the generated type
        public static TutorialsElement CreateTutorial(string anchorBlockName, string blockName, Vector3Int offset, string direction)
        {
            var path = Path.Combine(EditModeInPlayingTestServerDirectoryPath, "mods", "EditModeInPlayingTestMod", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var challenge = (JObject)json["data"][0]["challenges"][0];
            var tutorials = (JArray)challenge["tutorials"];
            tutorials.Clear();
            tutorials.Add(new JObject
            {
                ["tutorialGuid"] = Guid.NewGuid().ToString("D"),
                ["tutorialType"] = "relativeBlockPlacePreview",
                ["tutorialParam"] = new JObject
                {
                    ["anchorBlockGuid"] = FindBlockGuid(anchorBlockName).ToString("D"),
                    ["blockGuid"] = FindBlockGuid(blockName).ToString("D"),
                    ["offset"] = new JArray(offset.x, offset.y, offset.z),
                    ["blockDirection"] = direction,
                    ["message"] = "relative preview test",
                },
            });
            var master = new ChallengeMaster(json);
            master.Initialize();
            return master.GetChallenge(Guid.Parse(challenge["challengeGuid"].Value<string>())).Tutorials[0];
        }

        private static Guid FindBlockGuid(string blockName)
        {
            foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                if (master.Name == blockName) return master.BlockGuid;
            }
            throw new InvalidOperationException($"block not found in the test mod: {blockName}");
        }
    }
}
