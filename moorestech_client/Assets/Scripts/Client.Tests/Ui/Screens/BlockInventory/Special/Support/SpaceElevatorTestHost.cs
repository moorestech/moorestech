using System.Linq;
using Client.Tests.Ui.TestHost;
using Client.Ui.Screens.BlockInventory.Special.Space;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using UnityEngine.UIElements;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

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
            // 見本は選んだ積荷を持つので、一覧を出さないモードで本文を直に描く（一覧を出すかは Presenter の役）
            // Samples carry a selected cargo, so draw the body directly in the non-list mode (deciding the mode is the presenter's job)
            var view = Mount(new SpaceElevatorBodyView(Kit(LoadContent()), false));
            view.Render(slots, detail, false);

            // 見出しを載せた後のスタイル解決で余白が変わるため、次フレームの配置で押下先の座標を取る
            // Resolving styles after mounting the headline changes spacing; use the next frame's layout for click coordinates
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return view;
        }

        // Presenter を通して描く（一覧を出すかを Presenter の局所状態が決める経路）
        // Draw through the presenter (the path where its local state decides whether the cargo list shows)
        protected SpaceElevatorBodyView Attach(SlotItem[] slots, SpaceElevatorDetail detail)
        {
            var view = Mount(new SpaceElevatorBodyView(Kit(LoadContent()), false));
            Model.Set(SpecialDetailSamples.ElevatorWithNetwork(detail));
            Presenters.OrbitalCargoBody(view).Attach(OpenPanel(BlockMasterElement.BlockTypeConst.SpaceElevatorBase, S.SpaceElevatorBase, slots));
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
