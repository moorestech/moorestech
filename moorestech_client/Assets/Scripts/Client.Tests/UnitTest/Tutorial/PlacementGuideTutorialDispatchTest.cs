using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Client.Game.InGame.BlockSystem.PlaceSystem.VeinRestriction;
using Client.Game.InGame.Block;
using Client.Game.InGame.Tutorial;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Game.InGame.Tutorial.UIHighlight;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.ChallengesModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial
{
    /// <summary>
    ///     設置システム案内の2チュートリアル型が専用managerへdispatchされ、共有状態へ書かれることを検証する
    ///     Verifies that the two placement-guide tutorial types dispatch to their managers and write the shared state
    /// </summary>
    public class PlacementGuideTutorialDispatchTest
    {
        private static readonly Guid ChallengeGuid = Guid.Parse("00000000-0000-0000-4567-000000000001");

        private ChallengeMaster _originalChallengeMaster;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _originalChallengeMaster = MasterHolder.ChallengeMaster;
            _root = new GameObject("PlacementGuideTutorialDispatchTest");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            SetChallengeMaster(_originalChallengeMaster);
        }

        [Test]
        public void veinRestrictedPlacementは専用managerへdispatchされ状態へ書く()
        {
            SetTutorial("veinRestrictedPlacement", new JObject
            {
                ["veinGuid"] = "11111111-0000-0000-0000-000000000001",
                ["blockGuid"] = "00000000-0000-0000-0000-000000000006",
            });
            var state = new VeinRestrictedPlacementState();
            var veinRestricted = _root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(state);
            var manager = CreateTutorialManager(veinRestricted, CreateRelativeManager(), new List<ITutorialViewManager>());

            manager.ApplyTutorial(ChallengeGuid);

            Assert.IsTrue(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ElectricMinerId, out var veinGuid));
            Assert.AreEqual(Guid.Parse("11111111-0000-0000-0000-000000000001"), veinGuid);

            manager.CompleteChallenge(ChallengeGuid);

            Assert.IsFalse(state.TryGetRestrictedVeinType(ForUnitTestModBlockId.ElectricMinerId, out _));
        }

        [Test]
        public void relativeBlockPlacePreviewは専用managerへdispatchされ完了で解除される()
        {
            SetTutorial("relativeBlockPlacePreview", new JObject
            {
                ["anchorBlockGuid"] = "00000000-0000-0000-0000-000000000014",
                ["blockGuid"] = "00000000-0000-0000-0000-00000000000e",
                ["offset"] = new JArray(0, 0, 1),
                ["blockDirection"] = "North",
                ["message"] = "テスト",
            });
            var relative = CreateRelativeManager();
            var veinRestricted = _root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(new VeinRestrictedPlacementState());
            var manager = CreateTutorialManager(veinRestricted, relative, new List<ITutorialViewManager>());

            // 専用managerへ振り分けられた時だけViewが返り、完了で解除される。dispatchが外れれば戻り値がnullになって落ちる
            // A view comes back only when the dedicated manager received the dispatch, and completion releases it; a broken dispatch returns null and fails here
            manager.ApplyTutorial(ChallengeGuid);
            Assert.IsInstanceOf<RelativeBlockPlacePreviewEntry>(GetAppliedView(manager));

            manager.CompleteChallenge(ChallengeGuid);
            Assert.IsNull(GetAppliedView(manager));
        }

        [Test]
        public void 相対ゴーストは目標セルでも向きが違えば完了しない()
        {
            SetTutorial("relativeBlockPlacePreview", CreateRelativeParam("00000000-0000-0000-0000-000000000014", "00000000-0000-0000-0000-00000000000e", 0, 0, 1));
            var relative = CreateRelativeManager();
            var veinRestricted = _root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(new VeinRestrictedPlacementState());
            var manager = CreateTutorialManager(veinRestricted, relative, new List<ITutorialViewManager>());

            manager.ApplyTutorial(ChallengeGuid);
            var entry = (RelativeBlockPlacePreviewEntry)GetAppliedView(manager);
            var targetCell = new Vector3Int(3, 0, 4);
            entry.SetTarget(targetCell, BlockDirection.East);

            // 繋がらない向きで置いても案内は残る
            // A direction that never connects leaves the guide up
            InvokeOnBlockPlaced(relative, CreatePlacedBlock(entry.TargetBlockId, targetCell, BlockDirection.North));
            Assert.IsTrue(HasActiveEntry(relative, entry.TutorialGuid), "a mismatched direction completed the guide");

            InvokeOnBlockPlaced(relative, CreatePlacedBlock(entry.TargetBlockId, targetCell, BlockDirection.East));
            Assert.IsFalse(HasActiveEntry(relative, entry.TutorialGuid), "the matching direction did not complete the guide");
        }

        [Test]
        public void 同一チャレンジ内の相対ゴースト2件は上書きされず両方生きる()
        {
            SetTutorials(
                CreateRelativeParam("00000000-0000-0000-0000-000000000014", "00000000-0000-0000-0000-00000000000e", 0, 0, 1),
                CreateRelativeParam("00000000-0000-0000-0000-000000000014", "00000000-0000-0000-0000-000000000006", 0, 0, 2));
            var relative = CreateRelativeManager();
            var veinRestricted = _root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(new VeinRestrictedPlacementState());
            var manager = CreateTutorialManager(veinRestricted, relative, new List<ITutorialViewManager>());

            manager.ApplyTutorial(ChallengeGuid);

            // 2エントリが独立し片方完了で他方は残る
            // Both entries stay alive as independent views; completing one never folds the other
            var views = GetAppliedViews(manager);
            Assert.AreEqual(2, views.Count);
            var first = (RelativeBlockPlacePreviewEntry)views[0];
            var second = (RelativeBlockPlacePreviewEntry)views[1];
            Assert.AreNotSame(first, second);
            Assert.IsTrue(HasActiveEntry(relative, first.TutorialGuid));
            Assert.IsTrue(HasActiveEntry(relative, second.TutorialGuid));

            first.CompleteTutorial();
            Assert.IsFalse(HasActiveEntry(relative, first.TutorialGuid));
            Assert.IsTrue(HasActiveEntry(relative, second.TutorialGuid));
        }

        [Test]
        public void chainBlockPlacePreviewは専用managerへdispatchされ状態へ書き完了で解除される()
        {
            SetTutorial("chainBlockPlacePreview", new JObject
            {
                ["placingBlockGuid"] = "00000000-0000-0000-0000-000000000014",
                ["chainBlocks"] = new JArray
                {
                    new JObject
                    {
                        ["blockGuid"] = "00000000-0000-0000-0000-00000000000e",
                        ["offset"] = new JArray(0, 0, 1),
                        ["blockDirection"] = "North",
                    },
                },
            });
            var state = new ChainPlacePreviewState();
            var chain = _root.AddComponent<ChainBlockPlacePreviewTutorialManager>();
            chain.Construct(state);
            var veinRestricted = _root.AddComponent<VeinRestrictedPlacementTutorialManager>();
            veinRestricted.Construct(new VeinRestrictedPlacementState());
            var manager = CreateTutorialManager(veinRestricted, CreateRelativeManager(), new List<ITutorialViewManager> { chain });

            manager.ApplyTutorial(ChallengeGuid);

            // 共有状態のJSON一致と完了解除を確認
            // Verifies the shared state was written per the JSON chain layout, and clears on completion
            var anchorBlockId = MasterHolder.BlockMaster.GetBlockId(Guid.Parse("00000000-0000-0000-0000-000000000014"));
            var expectedGhostBlockId = MasterHolder.BlockMaster.GetBlockId(Guid.Parse("00000000-0000-0000-0000-00000000000e"));
            Assert.IsTrue(state.TryGetChain(anchorBlockId, out var resultChain, out _));
            Assert.AreEqual(1, resultChain.Count);
            Assert.AreEqual(expectedGhostBlockId, resultChain[0].BlockId);
            Assert.AreEqual(new Vector3Int(0, 0, 1), resultChain[0].Offset);
            Assert.AreEqual(BlockDirection.North, resultChain[0].LocalDirection);

            manager.CompleteChallenge(ChallengeGuid);

            Assert.IsFalse(state.TryGetChain(anchorBlockId, out _, out _));
        }

        [Test]
        public void blockPlacePreviewの複数ゴーストはtutorialGuidごとに独立し片方の解除で他方が残る()
        {
            var manager = _root.AddComponent<BlockPlacePreviewTutorialManager>();
            manager.Construct(_root.AddComponent<BlockGameObjectDataStore>());
            const string firstGuid = "aaaaaaaa-0000-0000-0000-000000000001";
            const string secondGuid = "aaaaaaaa-0000-0000-0000-000000000002";

            manager.SetTargetCell(ForUnitTestModBlockId.Shaft, new Vector3Int(1, 0, 1), BlockDirection.North, firstGuid);
            manager.SetTargetCell(ForUnitTestModBlockId.Shaft, new Vector3Int(2, 0, 2), BlockDirection.North, secondGuid);

            Assert.IsTrue(HasGhostEntry(manager, firstGuid));
            Assert.IsTrue(HasGhostEntry(manager, secondGuid));

            manager.ClearTarget(firstGuid);

            Assert.IsFalse(HasGhostEntry(manager, firstGuid));
            Assert.IsTrue(HasGhostEntry(manager, secondGuid));
        }

        private static JObject CreateRelativeParam(string anchorGuid, string blockGuid, int x, int y, int z)
        {
            return new JObject
            {
                ["anchorBlockGuid"] = anchorGuid,
                ["blockGuid"] = blockGuid,
                ["offset"] = new JArray(x, y, z),
                ["blockDirection"] = "North",
                ["message"] = "テスト",
            };
        }

        // Initializeはプレハブを要するため値だけ注入する
        // BlockGameObject.Initialize needs a prefab load and a server subscription, so only the placed-block values are injected
        private BlockGameObject CreatePlacedBlock(BlockId blockId, Vector3Int cell, BlockDirection direction)
        {
            var block = new GameObject("PlacedBlock").AddComponent<BlockGameObject>();
            block.transform.SetParent(_root.transform);

            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockId)).GetSetMethod(true).Invoke(block, new object[] { blockId });
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockPosInfo)).GetSetMethod(true).Invoke(block, new object[] { new BlockPositionInfo(cell, direction, blockSize) });
            return block;
        }

        // 設置検知は購読経由なのでprivateハンドラを直接呼ぶ
        // The placement hook is only reachable through the datastore subscription, so the private handler is invoked directly
        private static void InvokeOnBlockPlaced(RelativeBlockPlacePreviewTutorialManager relative, BlockGameObject block)
        {
            typeof(RelativeBlockPlacePreviewTutorialManager)
                .GetMethod("OnBlockPlaced", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(relative, new object[] { block });
        }

        private TutorialManager CreateTutorialManager(VeinRestrictedPlacementTutorialManager veinRestricted, RelativeBlockPlacePreviewTutorialManager relative, List<ITutorialViewManager> extraManagers)
        {
            var managers = new List<ITutorialViewManager>
            {
                _root.AddComponent<UIHighlightTutorialManager>(),
                _root.AddComponent<KeyControlTutorialManager>(),
                _root.AddComponent<ItemViewHighLightTutorialManager>(),
                _root.AddComponent<BlockPlacePreviewTutorialManager>(),
                _root.AddComponent<UiDragGuideTutorialManager>(),
                veinRestricted,
                relative,
            };
            managers.AddRange(extraManagers);
            return new TutorialManager(managers);
        }

        private RelativeBlockPlacePreviewTutorialManager CreateRelativeManager()
        {
            var blockGameObjectDataStore = _root.AddComponent<BlockGameObjectDataStore>();
            var relative = _root.AddComponent<RelativeBlockPlacePreviewTutorialManager>();
            relative.Construct(blockGameObjectDataStore, _root.AddComponent<BlockPlacePreviewTutorialManager>());
            return relative;
        }

        // 適用中のViewは外向きAPIに現れないため、TutorialManagerが保持している実体を読み出して突き合わせる
        // The applied view is not exposed by the public API, so the instance TutorialManager holds is read back for comparison
        private static ITutorialView GetAppliedView(TutorialManager manager)
        {
            var applied = GetAppliedViews(manager);
            return 0 < applied.Count ? applied[0] : null;
        }

        private static List<ITutorialView> GetAppliedViews(TutorialManager manager)
        {
            var views = (Dictionary<Guid, List<ITutorialView>>)typeof(TutorialManager)
                .GetField("_tutorialViews", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            return views.TryGetValue(ChallengeGuid, out var applied) ? applied : new List<ITutorialView>();
        }

        // manager内部の保持中エントリはproductionに公開しないため、reflectionで読み出して突き合わせる
        // The manager's active entries are not exposed in production, so reflection reads them back for comparison
        private static bool HasActiveEntry(RelativeBlockPlacePreviewTutorialManager relative, Guid tutorialGuid)
        {
            var entries = (Dictionary<Guid, RelativeBlockPlacePreviewEntry>)typeof(RelativeBlockPlacePreviewTutorialManager)
                .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(relative);
            return entries.ContainsKey(tutorialGuid);
        }

        // guidごとのゴースト保持もproductionに公開しないため、同様にreflectionで読み出す
        // The per-guid ghost entries are likewise unexposed in production, so reflection reads them back
        private static bool HasGhostEntry(BlockPlacePreviewTutorialManager manager, string tutorialGuid)
        {
            var entries = (Dictionary<string, Client.Game.InGame.BlockSystem.PlaceSystem.PreviewGhost.PlacementGhostEntry>)typeof(BlockPlacePreviewTutorialManager)
                .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            return entries.ContainsKey(tutorialGuid);
        }

        // challenges.json の最初のチャレンジのチュートリアルを1件だけ差し替えて ChallengeMaster を作り直す
        // Rebuild the ChallengeMaster with the first challenge's tutorial list replaced by a single entry
        private void SetTutorial(string tutorialType, JObject tutorialParam)
        {
            SetTutorialsCore((tutorialType, tutorialParam));
        }

        // 相対ゴースト複数件をtutorialGuidを変えて差し込む
        // Injects multiple relative previews with distinct tutorial guids
        private void SetTutorials(params JObject[] relativeParams)
        {
            var entries = new (string, JObject)[relativeParams.Length];
            for (var i = 0; i < relativeParams.Length; i++) entries[i] = ("relativeBlockPlacePreview", relativeParams[i]);
            SetTutorialsCore(entries);
        }

        private void SetTutorialsCore(params (string tutorialType, JObject tutorialParam)[] entries)
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "challenges.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var tutorials = (JArray)json["data"][0]["challenges"][0]["tutorials"];
            var template = (JObject)tutorials[0].DeepClone();
            tutorials.Clear();
            for (var i = 0; i < entries.Length; i++)
            {
                var tutorial = (JObject)template.DeepClone();
                tutorial["tutorialGuid"] = new Guid($"aaaaaaaa-0000-0000-0000-00000000000{i + 1}").ToString("D");
                tutorial["tutorialType"] = entries[i].tutorialType;
                tutorial["tutorialParam"] = entries[i].tutorialParam;
                tutorials.Add(tutorial);
            }
            var master = new ChallengeMaster(json);
            master.Initialize();
            SetChallengeMaster(master);
        }

        private static void SetChallengeMaster(ChallengeMaster challengeMaster)
        {
            typeof(MasterHolder).GetProperty(nameof(MasterHolder.ChallengeMaster))
                .GetSetMethod(true).Invoke(null, new object[] { challengeMaster });
        }
    }
}
