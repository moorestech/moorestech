using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.MapVein;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer;

namespace Client.Playtest.Operations
{
    public static class PlaytestPlacementGuideChecks
    {
        public static async UniTask VerifyVeinRestriction(PlaytestDriver p, Guid logVeinGuid, string minerName)
        {
            // 7. 鉱脈限定設置（対象鉱脈だけを強調する）
            // 7. Vein-restricted placement highlighting only the target vein
            p.Note("原木鉱脈だけを強調する設置制限をかけ、その鉱脈だけが緑で描かれるかを見る");
            var registry = ClientDIContext.DIContainer.DIContainerResolver.Resolve<MapVeinAabbRegistry>();
            p.Assert(registry.Veins.Count > 0, $"ワールドレイアウトに鉱脈がある（{registry.Veins.Count}件）");
            if (registry.Veins.Count == 0) throw new InvalidOperationException("the world layout has no vein at all");

            // 原木鉱脈があればそれを、無ければ最寄りの鉱脈を強調対象にする
            // Prefer the log vein when the world has one; otherwise highlight the nearest vein instead
            var logVeins = registry.Veins.Where(vein => vein.VeinTypeGuid == logVeinGuid).ToList();
            // 海中の露頭は絵に映らないので、同種のうち最も標高の高い1件を強調対象にする
            // A submerged vein would not show on camera, so highlight the highest one of its kind
            var targetVein = (logVeins.Count > 0 ? logVeins : registry.Veins.ToList())
                .OrderByDescending(vein => vein.Bounds.max.y).First();
            var targetVeinTypeGuid = targetVein.VeinTypeGuid;
            var targetVeinName = MasterHolder.MapVeinMaster.GetElementOrNull(targetVeinTypeGuid)?.VeinName;
            p.Note($"強調対象の鉱脈: {targetVeinName}（同種{logVeins.Count}件のうち最寄り）");

            var minerBlockId = MasterHolder.BlockMaster.GetBlockId(BlockGuidOf(minerName));
            var restrictionState = ClientDIContext.DIContainer.DIContainerResolver.Resolve<VeinRestrictedPlacementState>();
            restrictionState.SetRestriction(targetVeinTypeGuid, minerBlockId);
            p.Assert(restrictionState.IsRestrictedBlock(minerBlockId), "風力掘削機に対象鉱脈限定の制限がかかった");

            var veinCenter = targetVein.Bounds.center;
            // 範囲表示はカメラから96m以内の鉱脈を描くので、対象鉱脈の真上やや南へ立って画に収める
            // The range view draws veins within 96m of the camera, so stand just above and south of the target vein to frame it
            p.WarpPlayer(new Vector3(veinCenter.x, veinCenter.y + 2f, veinCenter.z - 8f));
            await p.WaitSeconds(0.5f);
            await p.Hotbar.AssignHotbar(1, minerName);
            await p.Hotbar.EnterBuildMode(1);
            await p.WaitSeconds(1f);

            var rangeViewRoot = GameObject.Find(MapVeinRangeViewService.RootObjectName);
            p.Assert(rangeViewRoot != null, "鉱脈範囲表示のルートが生成されている");
            var highlightedBoxes = rangeViewRoot == null
                ? 0
                : rangeViewRoot.transform.Cast<Transform>()
                    .Count(child => child.gameObject.activeSelf &&
                                    child.GetComponent<MeshRenderer>().sharedMaterial.name.Contains("Highlight"));
            var visibleBoxes = rangeViewRoot == null
                ? 0
                : rangeViewRoot.transform.Cast<Transform>().Count(child => child.gameObject.activeSelf);
            p.Assert(highlightedBoxes >= 1, $"対象の{targetVeinName}が強調マテリアルで描かれた（{highlightedBoxes}件）");
            p.Assert(visibleBoxes == highlightedBoxes, $"強調中は対象鉱脈以外が描かれない（表示{visibleBoxes}件・うち強調{highlightedBoxes}件）");

            // 制限は台帳のGUID絞り込みと底面XZ重なりで効く。鉱脈外セルは設置不可側になる
            // The restriction runs on the registry's per-GUID selection plus the footprint XZ overlap, so a cell outside the vein falls on the not-placeable side
            var insideCell = Vector3Int.FloorToInt(veinCenter);
            var outsideCell = insideCell + new Vector3Int(200, 0, 200);
            var targetVeins = registry.SelectVeinsOfType(targetVeinTypeGuid);
            p.Assert(targetVeins.Any(vein => new BlockPositionInfo(insideCell, BlockDirection.North, Vector3Int.one).OverlapsVeinXz(vein.MinCell, vein.MaxCell)), "鉱脈中心のセルは対象鉱脈の内側と判定される");
            p.Assert(!targetVeins.Any(vein => new BlockPositionInfo(outsideCell, BlockDirection.North, Vector3Int.one).OverlapsVeinXz(vein.MinCell, vein.MaxCell)), "遠く離れたセルは対象鉱脈の外側と判定される");
            await p.Screenshot("05-vein-restricted-highlight");

            p.Note("制限を解除し、鉱脈表示が種別表示へ戻ることを確認する");
            restrictionState.Clear();
            await p.WaitSeconds(0.5f);
            var afterClearHighlighted = rangeViewRoot == null
                ? 0
                : rangeViewRoot.transform.Cast<Transform>()
                    .Count(child => child.gameObject.activeSelf &&
                                    child.GetComponent<MeshRenderer>().sharedMaterial.name.Contains("Highlight"));
            p.Assert(afterClearHighlighted == 0, "制限解除で強調表示が消えた");
            await p.Screenshot("06-restriction-cleared");
            await p.Hotbar.ExitBuildMode(1);

        }
        // マスタの日本語名からブロックGUIDを引く
        // Look up a block GUID by its Japanese master name
        public static Guid BlockGuidOf(string blockName)
        {
            foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                if (master.Name == blockName) return master.BlockGuid;
            }
            throw new InvalidOperationException($"block not found in the live master: {blockName}");
        }

        // ライブのchallenges.jsonへチュートリアルを1件差し込み、生成型として取り出す
        // Insert one tutorial into the live challenges.json and take it back as the generated type
        public static TutorialsElement BuildTutorial(string tutorialType, JObject tutorialParam)
        {
            var challengesJson = JObject.Parse(File.ReadAllText(ChallengesMasterPath()));
            var challenge = (JObject)challengesJson["data"][0]["challenges"][0];
            var tutorials = (JArray)challenge["tutorials"];
            tutorials.Clear();
            tutorials.Add(new JObject
            {
                ["tutorialGuid"] = Guid.NewGuid().ToString("D"),
                ["tutorialType"] = tutorialType,
                ["tutorialParam"] = tutorialParam,
            });
            var challengeMaster = new ChallengeMaster(challengesJson);
            challengeMaster.Initialize();
            return challengeMaster.GetChallenge(Guid.Parse(challenge["challengeGuid"].Value<string>())).Tutorials[0];
        }

        private static string ChallengesMasterPath()
        {
            var found = Directory.GetFiles(Server.Boot.ServerDirectory.GetDirectory(), "challenges.json", SearchOption.AllDirectories);
            if (found.Length == 0) throw new InvalidOperationException("challenges.json was not found under the server directory");
            return found[0];
        }

    }
}
