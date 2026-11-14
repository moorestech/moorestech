using System.Linq;
using Client.Tests.Ui.TestHost;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface.State;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    /// <summary>
    /// 宇宙エレベーターの L1 の共通の土台（本文の描画・見本の枠・格子の枠の並び・文字の連結）
    /// Shared L1 base for the space elevator (body rendering, sample slots, grid frame order, joined text)
    /// </summary>
    public abstract class SpaceElevatorTestHost : SpecialScreenTestHost
    {
        protected SpaceElevatorBodyView Render(SlotItem[] slots, SpaceElevatorDetail detail)
        {
            return Render(slots, SpecialDetailSamples.ElevatorWithNetwork(detail));
        }

        protected SpaceElevatorBodyView Render(SlotItem[] slots, SpecialBlockDetailViewModel detail)
        {
            var view = Mount(new SpaceElevatorBodyView(Kit(LoadContent())));
            view.Render(slots, detail);

            // 本文は Render で格子の枠を組むので、配置を解いてから押下を送る（Task 7 と同じ）
            // The body builds grid frames in Render, so resolve layout before sending presses (same as Task 7)
            simulate.FrameUpdate();
            return view;
        }

        protected static SpaceElevatorDetail Unconnected(SpaceElevatorBaseStatus status)
        {
            return SpecialDetailSamples.SpaceElevator(status, false, 1000, 0f);
        }

        protected static SlotItem[] Slots(bool empty)
        {
            return empty
                ? new[] { new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(ItemMaster.EmptyItemId, 0) }
                : new[] { new SlotItem(ItemMaster.EmptyItemId, 0), new SlotItem(new ItemId(1), 4), new SlotItem(new ItemId(2), 7) };
        }

        // 格子の中の枠の testid を並び順で
        // Frame testids inside a grid, in display order
        protected static string[] FrameIds(VisualElement root, string gridTestid)
        {
            return Find<VisualElement>(root, gridTestid).Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith(gridTestid + "-slot-")).ToList().Select(e => e.name).ToArray();
        }

        protected static string AllText(VisualElement root)
        {
            return string.Join(" ", root.Query<Label>().ToList().Select(l => l.PlainText()));
        }
    }
}
