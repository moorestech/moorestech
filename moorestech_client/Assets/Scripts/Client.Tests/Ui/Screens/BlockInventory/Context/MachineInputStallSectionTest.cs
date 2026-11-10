using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Context;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 搬入停止欄の文と赤（View を直に組み、状態を差して見る）
    /// Inflow-stall section text and red marking (builds the View directly and feeds it states)
    /// </summary>
    public class MachineInputStallSectionTest : ContextSectionTestHost
    {
        private const string InsufficientClass = "lack-highlight-text--insufficient";

        private MachineInputStallSectionView Render(ContextEntry<MachineInputStall> entry)
        {
            var view = new MachineInputStallSectionView(Content);
            view.Render(entry);
            Mount(view);
            return view;
        }

        /// <web-test file="src/features/blockInventory/details/machineInputStall/__tests__/MachineInputStallSection.test.ts">レベル違いは品の名前を差し込んで赤字で描く</web-test>
        [Test]
        public void レベル違いは品の名前を差し込んで赤字で描く()
        {
            var view = Render(ContextEntry<MachineInputStall>.Fetched(new MachineInputStall(MachineInputStallReasonKind.MixedLevel, PlateId)));

            var row = view.Q<Label>("machine-input-stall");
            Assert.IsTrue(row.ClassListContains(InsufficientClass));
            Assert.AreEqual("Stopped: Test Plate of a different level arrived", row.text);
        }

        /// <web-test file="src/features/blockInventory/details/machineInputStall/__tests__/MachineInputStallSection.test.ts">上限超えは製作上限の文言で描く</web-test>
        [Test]
        public void 上限超えは製作上限の文言で描く()
        {
            var view = Render(ContextEntry<MachineInputStall>.Fetched(new MachineInputStall(MachineInputStallReasonKind.AboveCraftCap, PlateId)));

            Assert.AreEqual("Stopped: Test Plate above the craft cap arrived", view.Q<Label>("machine-input-stall").text);
        }

        /// <web-test file="src/features/blockInventory/details/machineInputStall/__tests__/MachineInputStallSection.test.ts">取得できていない間は赤字にせず取得状態の1行だけを描く</web-test>
        [Test]
        public void 取得できていない間は赤字にせず取得状態の1行だけを描く()
        {
            var view = Render(ContextEntry<MachineInputStall>.Unavailable(BlockInfoFetchState.UnsupportedTarget));

            Assert.IsNull(view.Q<VisualElement>("machine-input-stall"));
            var row = view.Q<Label>("machine-input-stall-fetch-state");
            Assert.IsFalse(row.ClassListContains(InsufficientClass));
            Assert.AreEqual("Inflow stall: not available for this block", row.text);
        }
    }
}
