using System;
using System.Collections.Generic;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.ProgressBar;
using Client.Localization;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Mining
{
    public class MiningTargetFocusContextTest
    {
        private static readonly Guid FirstEarnItemGuid = new("00000000-0000-0000-9999-000000000001");
        private static readonly Guid SecondEarnItemGuid = new("00000000-0000-0000-9999-000000000002");
        private static readonly Guid ToolItemGuid = new("00000000-0000-0000-1234-000000000001");

        [Test]
        public void SetFocusTargetは同一対象を再設定しない()
        {
            var context = new MiningControllerContext(null, new ProgressBarState(), null);
            var sharedGameObject = new GameObject("SharedTarget");
            var firstTarget = new StubMiningTarget(sharedGameObject, Array.Empty<Guid>());
            var secondTarget = new StubMiningTarget(new GameObject("Second"), Array.Empty<Guid>());

            context.SetFocusTarget(firstTarget);
            Assert.AreEqual(1, firstTarget.EarnItemGuidsAccessCount);

            // 同一対象への再設定はEarnItemGuidsを再解決しない（アクセス回数が増えないことで検証する）
            // Re-setting the same target must not re-resolve EarnItemGuids (verified by the access count staying flat)
            context.SetFocusTarget(firstTarget);
            Assert.AreEqual(1, firstTarget.EarnItemGuidsAccessCount);
            Assert.AreSame(firstTarget, context.CurrentFocusTarget);

            context.SetFocusTarget(secondTarget);
            Assert.AreEqual(1, secondTarget.EarnItemGuidsAccessCount);
            Assert.AreSame(secondTarget, context.CurrentFocusTarget);

            context.SetFocusTarget(null);
            Assert.IsNull(context.CurrentFocusTarget);

            UnityEngine.Object.DestroyImmediate(sharedGameObject);
            UnityEngine.Object.DestroyImmediate(secondTarget.GameObject);
        }

        [Test]
        public void 取得アイテム名はフォーカス変化時に組み立てて保持される()
        {
            // 実辞書を通す。未登録キーは[!key]
            // Resolve through the real dictionary; unknown keys fall back to [!key], which is enough here
            Localize.Initialize();

            var context = new MiningControllerContext(null, new ProgressBarState(), null);
            var twoItemObject = new GameObject("TwoItemTarget");
            var noItemObject = new GameObject("NoItemTarget");
            var twoItemTarget = new StubMiningTarget(twoItemObject, new[] { FirstEarnItemGuid, SecondEarnItemGuid });
            var noItemTarget = new StubMiningTarget(noItemObject, Array.Empty<Guid>());

            Assert.AreEqual(string.Empty, context.CurrentFocusTargetEarnItemNames);

            context.SetFocusTarget(twoItemTarget);
            var expected =
                $"{Localize.GetContent(ContentLocalizationKeys.ItemName(FirstEarnItemGuid))}, " +
                $"{Localize.GetContent(ContentLocalizationKeys.ItemName(SecondEarnItemGuid))}";
            Assert.AreEqual(expected, context.CurrentFocusTargetEarnItemNames);

            // 取得物ゼロの対象では名前欄を空に戻す
            // A target that yields nothing clears the name slot
            context.SetFocusTarget(noItemTarget);
            Assert.AreEqual(string.Empty, context.CurrentFocusTargetEarnItemNames);

            context.SetFocusTarget(null);
            Assert.AreEqual(string.Empty, context.CurrentFocusTargetEarnItemNames);

            UnityEngine.Object.DestroyImmediate(twoItemObject);
            UnityEngine.Object.DestroyImmediate(noItemObject);
        }

        [Test]
        public void 推奨ツール名もフォーカス変化時に組み立てて保持される()
        {
            // ツール名はItemIdからマスタ経由でGuidを引くため、実マスタと実辞書を通す
            // A tool name resolves its guid through the master, so the real master and dictionary are loaded
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();

            var context = new MiningControllerContext(null, new ProgressBarState(), null);
            var toolTargetObject = new GameObject("ToolTarget");
            var noToolObject = new GameObject("NoToolTarget");
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var toolTarget = new StubMiningTarget(toolTargetObject, Array.Empty<Guid>(), new List<ItemId> { toolItemId });
            var noToolTarget = new StubMiningTarget(noToolObject, Array.Empty<Guid>());

            Assert.AreEqual(string.Empty, context.CurrentFocusTargetRecommendedToolNames);

            context.SetFocusTarget(toolTarget);
            Assert.AreEqual(Localize.GetContent(ContentLocalizationKeys.ItemName(ToolItemGuid)), context.CurrentFocusTargetRecommendedToolNames);

            // ツールを求めない対象では欄を空へ戻す
            // A target requiring no tool clears the slot again
            context.SetFocusTarget(noToolTarget);
            Assert.AreEqual(string.Empty, context.CurrentFocusTargetRecommendedToolNames);

            UnityEngine.Object.DestroyImmediate(toolTargetObject);
            UnityEngine.Object.DestroyImmediate(noToolObject);
        }

        [Test]
        public void 言語切替で保持中の取得アイテム名が作り直される()
        {
            // 単体テスト用マスタに辞書が無く全言語で同じ[!key]へ落ちるため、文字列の異同ではなく再解決の発火を観測する
            // The unit-test master ships no dictionary so every language yields the same [!key]; observe the re-resolution firing instead of the text
            Localize.Initialize();
            var originalLanguageCode = Localize.GetCurrentLanguageCode();

            var context = new MiningControllerContext(null, new ProgressBarState(), null);
            var twoItemObject = new GameObject("TwoItemTarget");
            var twoItemTarget = new StubMiningTarget(twoItemObject, new[] { FirstEarnItemGuid, SecondEarnItemGuid });

            context.SetFocusTarget(twoItemTarget);
            var accessCountBeforeSwitch = twoItemTarget.EarnItemGuidsAccessCount;

            var otherLanguageCode = Localize.GetLanguageCodes().Find(code => code != originalLanguageCode);
            Assert.IsTrue(Localize.TrySetLanguage(otherLanguageCode), otherLanguageCode);

            Assert.Less(accessCountBeforeSwitch, twoItemTarget.EarnItemGuidsAccessCount);

            Localize.TrySetLanguage(originalLanguageCode);
            UnityEngine.Object.DestroyImmediate(twoItemObject);
        }

        private class StubMiningTarget : IMiningTargetObject
        {
            public GameObject GameObject { get; }
            public bool IsInteractAvailable => true;
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;

            // 取得回数を数えることで、SetFocusTargetの再解決有無をテストから観測できるようにする
            // Counts reads so tests can observe whether SetFocusTarget re-resolved this target
            public int EarnItemGuidsAccessCount { get; private set; }

            private readonly IReadOnlyList<Guid> _earnItemGuids;
            private readonly List<ItemId> _recommendedToolItemIds;

            public IReadOnlyList<Guid> EarnItemGuids
            {
                get
                {
                    EarnItemGuidsAccessCount++;
                    return _earnItemGuids;
                }
            }

            public StubMiningTarget(GameObject gameObject, IReadOnlyList<Guid> earnItemGuids) : this(gameObject, earnItemGuids, new List<ItemId>())
            {
            }

            public StubMiningTarget(GameObject gameObject, IReadOnlyList<Guid> earnItemGuids, List<ItemId> recommendedToolItemIds)
            {
                GameObject = gameObject;
                _earnItemGuids = earnItemGuids;
                _recommendedToolItemIds = recommendedToolItemIds;
            }

            public IReadOnlyList<ItemId> RecommendedToolItemIds => _recommendedToolItemIds;

            public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool)
            {
                // フォーカス解決だけを見るfixtureなので採掘可否は問わない
                // This fixture only observes focus resolution, so minability is irrelevant
                tool = default;
                return MiningStartOutcome.ToolMismatch;
            }

            public void SetHighlighted(bool highlighted)
            {
            }

            public void SendAttack()
            {
            }
        }
    }
}
