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
            var context = new MapObjectMiningControllerContext(null);
            var firstTarget = new FocusTrackingMiningTarget("FirstTarget");
            var secondTarget = new FocusTrackingMiningTarget("SecondTarget");

            // 同じ対象を毎tick設定してもフォーカス通知を繰り返さない
            // Reassigning the same target every tick must not repeat focus notifications
            context.SetFocusTarget(firstTarget);
            context.SetFocusTarget(firstTarget);
            Assert.AreEqual(1, firstTarget.FocusEnabledCount);
            Assert.AreEqual(0, firstTarget.FocusDisabledCount);

            // 対象変更時だけ旧対象を解除して新対象へ通知する
            // Only a target change defocuses the old target and focuses the new one
            context.SetFocusTarget(secondTarget);
            Assert.AreEqual(1, firstTarget.FocusDisabledCount);
            Assert.AreEqual(1, secondTarget.FocusEnabledCount);
            Assert.AreSame(secondTarget, context.CurrentFocusTarget);

            // 対象が消えた場合も最後の対象へ解除を一度だけ通知する
            // Losing the target also sends exactly one defocus notification to the last target
            context.SetFocusTarget(null);
            context.SetFocusTarget(null);
            Assert.AreEqual(1, secondTarget.FocusDisabledCount);
            Assert.IsNull(context.CurrentFocusTarget);

            Object.DestroyImmediate(firstTarget.GameObject);
            Object.DestroyImmediate(secondTarget.GameObject);
        }

        private class FocusTrackingMiningTarget : IMiningTargetObject
        {
            public GameObject GameObject { get; }
            public bool IsAvailable => true;
            public bool IsPickUp => false;
            public List<ItemId> UsableToolItemIds { get; } = new();
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
            public int FocusEnabledCount { get; private set; }
            public int FocusDisabledCount { get; private set; }

            public FocusTrackingMiningTarget(string name)
            {
                GameObject = new GameObject(name);
            }

            public bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool)
            {
                tool = default;
                return false;
            }

            public void SetFocused(bool focused)
            {
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
