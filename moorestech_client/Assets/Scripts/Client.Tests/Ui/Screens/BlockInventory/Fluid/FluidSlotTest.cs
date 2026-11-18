using Client.Tests.Ui.Components;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Game.Block.Interface;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// タンクのパネルに出る液体の枠
    /// Fluid slots shown in a tank panel
    /// </summary>
    public class FluidSlotTest : FluidScreenTestHost
    {
        private BlockInventoryTestShell _shell;

        private void OpenTank(Texture2D fluidTexture, float? progress, params BlockFluidSlotState[] slots)
        {
            var panel = new OpenBlockInventoryPanel("Tank", TankGuid, default, new BlockInstanceId(1), new Vector3Int(1, 0, 0), new SlotItem[0], BlockInventoryPanelError.None);
            _shell = BlockInventoryTestShell.Create(this, new ReactiveProperty<BlockInventoryPanelState>(panel), Factories(ContentWith(fluidTexture)));
            SetFluids(progress, slots);
        }

        /// <web-test file="e2e/tests/block/fluidSlot.spec.ts">tank の block inventory パネルに Fluid Tank が表示される</web-test>
        [Test]
        public void 題はタンクの名前()
        {
            OpenTank(new Texture2D(4, 4) { hideFlags = HideFlags.DontSave }, null, BlockFluidSlotState.Filled(Water, 500, 1000));

            Assert.AreEqual("Fluid Tank", _shell.TitleText());
        }

        /// <web-test file="e2e/tests/block/fluidSlot.spec.ts">水の流体スロットが量 500 を表示する</web-test>
        [Test]
        public void 水の量の帯が出る()
        {
            OpenTank(new Texture2D(4, 4) { hideFlags = HideFlags.DontSave }, null, BlockFluidSlotState.Filled(Water, 500, 1000));

            Assert.AreEqual(SlotAmountFormat.Format(500), Amount(_shell.View.Q<FluidSlot>("fluid-slot")));
        }

        /// <web-test file="e2e/tests/block/fluidSlot.spec.ts">Water 名のツールチップが hover で表示される</web-test>
        [Test]
        public void 枠の印の文は水の名前()
        {
            OpenTank(new Texture2D(4, 4) { hideFlags = HideFlags.DontSave }, null, BlockFluidSlotState.Filled(Water, 500, 1000));

            var slot = _shell.View.Q<FluidSlot>("fluid-slot");
            Assert.IsTrue(HoverTooltipMark.TryFindMarked(slot.Q(className: "fluid-slot__icon"), out _, out var text));
            Assert.AreEqual("Test Water", text.Resolve());
        }

        /// <web-test file="e2e/tests/block/fluidSlot.spec.ts">progress-arrow-bar のフィル幅が 50% になる</web-test>
        [Test]
        public void 進捗の帯は割合の幅で出る()
        {
            OpenTank(new Texture2D(4, 4) { hideFlags = HideFlags.DontSave }, 0.5f, BlockFluidSlotState.Filled(Water, 500, 1000));

            var fill = _shell.View.Q("progress-arrow-bar").Q(className: "progress-arrow-bar__fill");
            Assert.AreEqual(Length.Percent(50f), fill.style.width.value);
        }

        /// <web-test file="e2e/tests/block/fluidSlot.spec.ts">通常のe2eでは液体アイコンが404になり背面フィルが残る</web-test>
        [Test]
        public void 絵が欠けても背面の面の色が残る()
        {
            OpenTank(null, null, BlockFluidSlotState.Filled(Water, 500, 1000));

            var fill = _shell.View.Q<FluidSlot>("fluid-slot").Q(className: "fluid-slot__fill");
            Assert.AreEqual(DisplayStyle.Flex, fill.resolvedStyle.display);
            ThemeTokenFile.AssertSameColor(new Color32(0x33, 0x99, 0xFF, 0xFF), fill.resolvedStyle.backgroundColor, "fill");
        }

        private static string Amount(VisualElement slot)
        {
            return slot.Q<Label>(null, "fluid-slot__amount", "outlined-count-label--glyph").text;
        }
    }
}
