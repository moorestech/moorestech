using System.Collections.Generic;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Core.Master;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Mining
{
    public class MiningTargetFocusContextTest
    {
        [Test]
        public void SetFocusTargetPushesOnlyWhenTargetChanges()
        {
            var context = new MiningControllerContext(null);
            var focusEventLog = new List<string>();
            var sharedGameObject = new GameObject("SharedTarget");
            var secondGameObject = new GameObject("SecondTarget");
            var firstTarget = new FocusTrackingMiningTarget("first", sharedGameObject, focusEventLog);
            var sameObjectWrapper = new FocusTrackingMiningTarget("same-object-wrapper", sharedGameObject, focusEventLog);
            var secondTarget = new FocusTrackingMiningTarget("second", secondGameObject, focusEventLog);

            // 同一実体は再通知しない
            // Same object sends no repeat
            context.SetFocusTarget(firstTarget);
            context.SetFocusTarget(firstTarget);
            context.SetFocusTarget(sameObjectWrapper);
            Assert.AreEqual(1, focusEventLog.Count);
            Assert.AreEqual(1, firstTarget.FocusEnabledCount);
            Assert.AreEqual(0, firstTarget.FocusDisabledCount);
            Assert.AreEqual(0, sameObjectWrapper.FocusEnabledCount);
            Assert.AreSame(sameObjectWrapper, context.CurrentFocusTarget);

            // 旧解除後に新規有効化
            // Defocus old before focusing new
            focusEventLog.Clear();
            context.SetFocusTarget(secondTarget);
            CollectionAssert.AreEqual(
                new[] { "same-object-wrapper:false", "second:true" },
                focusEventLog);
            Assert.AreEqual(1, sameObjectWrapper.FocusDisabledCount);
            Assert.AreEqual(1, secondTarget.FocusEnabledCount);
            Assert.AreSame(secondTarget, context.CurrentFocusTarget);

            // 消失時も解除は一度
            // Loss defocuses exactly once
            focusEventLog.Clear();
            context.SetFocusTarget(null);
            context.SetFocusTarget(null);
            CollectionAssert.AreEqual(new[] { "second:false" }, focusEventLog);
            Assert.AreEqual(1, secondTarget.FocusDisabledCount);
            Assert.IsNull(context.CurrentFocusTarget);

            Object.DestroyImmediate(sharedGameObject);
            Object.DestroyImmediate(secondGameObject);
        }

        private class FocusTrackingMiningTarget : IMiningTargetObject
        {
            public GameObject GameObject { get; }
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
            public int FocusEnabledCount { get; private set; }
            public int FocusDisabledCount { get; private set; }
            private readonly List<ItemId> _recommendedToolItemIds = new();
            private readonly string _name;
            private readonly List<string> _focusEventLog;

            public FocusTrackingMiningTarget(string name, GameObject gameObject, List<string> focusEventLog)
            {
                _name = name;
                GameObject = gameObject;
                _focusEventLog = focusEventLog;
            }

            public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds)
            {
                // フォーカス通知だけを見るfixtureなので採掘可否は問わない
                // This fixture only observes focus notifications, so minability is irrelevant
                tool = default;
                recommendedToolItemIds = _recommendedToolItemIds;
                return MiningStartOutcome.ToolMismatch;
            }

            public void SetFocused(bool focused)
            {
                _focusEventLog.Add($"{_name}:{focused.ToString().ToLowerInvariant()}");
                if (focused)
                    FocusEnabledCount++;
                else
                    FocusDisabledCount++;
            }

            public void SendAttack()
            {
            }
        }
    }
}
