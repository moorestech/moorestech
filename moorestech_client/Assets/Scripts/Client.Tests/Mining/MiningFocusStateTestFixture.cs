using System;
using System.Collections.Generic;
using Client.Game.InGame.Mining;
using Client.Game.InGame.SoundEffect;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Client.Localization;
using Client.Tests.Common;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     フォーカス状態テストの共有土台
    ///     Shared ground for the focus state tests
    /// </summary>
    public abstract class MiningFocusStateTestFixture : InputTestFixture
    {
        protected static readonly Guid ToolItemGuid = new("00000000-0000-0000-1234-000000000001");
        protected static readonly Guid EarnItemGuid = new("00000000-0000-0000-9999-000000000001");

        private readonly List<GameObject> _stubTargetObjects = new();
        private MouseCursorTooltipState _tooltip;

        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            InputSystem.AddDevice<Keyboard>();
            TestReflection.ResetInputManagerCache();

            // 文言解決は実辞書を通す
            // Resolve text through the real dictionary so a key dropped from it fails here too
            Localize.Initialize();
            _tooltip = new MouseCursorTooltipState();
        }

        public override void TearDown()
        {
            foreach (var stubTargetObject in _stubTargetObjects)
                UnityEngine.Object.DestroyImmediate(stubTargetObject);
            _stubTargetObjects.Clear();
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        protected IMiningState RunFocusState(MiningStartOutcome outcome, MiningFocusState focusState)
        {
            return RunFocusState(outcome, focusState, Array.Empty<Guid>());
        }

        protected IMiningState RunFocusState(MiningStartOutcome outcome, MiningFocusState focusState, IReadOnlyList<Guid> earnItemGuids)
        {
            var context = new MiningControllerContext(CreateEquipmentHoldingTool(), new ProgressBarState(), _tooltip);
            var stubTarget = new OutcomeStubMiningTarget(outcome, MasterHolder.ItemMaster.GetItemId(ToolItemGuid), earnItemGuids);
            _stubTargetObjects.Add(stubTarget.GameObject);
            context.SetFocusTarget(stubTarget);

            // 入力アセットは状態イベントより先に作る
            // The input asset must be created before the state event, otherwise its bindings never resolve
            Assert.IsFalse(InputManager.Playable.Interact.GetKey, "Fが押されていない前提が崩れている");
            return focusState.GetNextUpdate(context, 0.01f);
        }

        // F押下下でフォーカス状態を1回進める。押下分岐そのものを検証する唯一の経路
        // Advances the focus state once with F held down; the only path that exercises the press branch itself
        protected IMiningState RunFocusStateWithInteractPressed(MiningStartOutcome outcome, MiningFocusState focusState)
        {
            var context = new MiningControllerContext(CreateEquipmentHoldingTool(), new ProgressBarState(), _tooltip);
            var stubTarget = new OutcomeStubMiningTarget(outcome, MasterHolder.ItemMaster.GetItemId(ToolItemGuid), Array.Empty<Guid>());
            _stubTargetObjects.Add(stubTarget.GameObject);
            context.SetFocusTarget(stubTarget);

            InputManager.Playable.Interact.SetKeyDownForTest(true);
            var next = focusState.GetNextUpdate(context, 0.01f);
            InputManager.Playable.Interact.SetKeyDownForTest(false);
            return next;
        }

        // 表示中の1行目のキー文字列（提示は1行構成が前提）
        // Key string of the first shown line; the presentation is expected to be one line
        protected string ShownTooltipKey()
        {
            return _tooltip.GetPresentation().Lines[0].Key.Key;
        }

        protected IReadOnlyList<string> ShownTooltipParams()
        {
            return _tooltip.GetPresentation().Lines[0].TextParams;
        }

        protected TooltipPresentation ShownTooltipPresentation()
        {
            return _tooltip.GetPresentation();
        }

        private static LocalPlayerEquipment CreateEquipmentHoldingTool()
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var equipment = new LocalPlayerEquipment();
            equipment.Initialize(new List<IItemStack> { ServerContext.ItemStackFactory.Create(toolItemId, 1) }, 0);
            return equipment;
        }

        private sealed class OutcomeStubMiningTarget : IMiningTargetObject
        {
            private readonly MiningStartOutcome _outcome;
            private readonly List<ItemId> _recommendedToolItemIds;

            public GameObject GameObject { get; }
            public bool IsInteractAvailable => true;
            public SoundEffectType DestroySoundType => SoundEffectType.DestroyStone;
            public IReadOnlyList<Guid> EarnItemGuids { get; }

            public OutcomeStubMiningTarget(MiningStartOutcome outcome, ItemId recommendedToolItemId, IReadOnlyList<Guid> earnItemGuids)
            {
                _outcome = outcome;
                _recommendedToolItemIds = new List<ItemId> { recommendedToolItemId };
                EarnItemGuids = earnItemGuids;
                GameObject = new GameObject("OutcomeStubMiningTarget");
            }

            public IReadOnlyList<ItemId> RecommendedToolItemIds => _recommendedToolItemIds;

            public MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool)
            {
                tool = new MiningToolCandidate(equippedItemId, 1f);
                return _outcome;
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
