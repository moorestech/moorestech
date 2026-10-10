using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Save
{
    // 搬送の組から、1つのベルコンblockに属する保存内容を切り出す
    // Cuts the save content belonging to one belt block out of the transport assembly
    public static class BeltTransportSaveStateBuilder
    {
        public static BeltConveyorSaveJsonObject Build(BeltTransportAssembly assembly, BlockInstanceId blockInstanceId)
        {
            // 設置直後で組にまだ無いblockは空。次のtick先頭で組に載る
            // A block placed this tick is not in the assembly yet and saves as empty; it joins at the next tick head
            var state = new BeltConveyorSaveJsonObject();
            if (!assembly.Locator.TryLocate(blockInstanceId, out var location)) return state;
            var segment = assembly.Segments[location.SegmentIndex];
            var layout = assembly.Layouts[location.SegmentIndex];

            CollectRunningItems();
            CollectBufferAndPriority();
            CollectInternalItems();
            return state;

            #region Internal

            void CollectRunningItems()
            {
                // segmentの走行列のうち、先頭がこのマスの範囲にあるものだけを、マスの出口までの距離で保存する
                // Of the segment run, keep only items whose head lies within this cell, saved by distance to the cell exit
                foreach (var captured in segment.CaptureItems())
                {
                    var distanceToCellExit = captured.DistanceToExit - location.CellExitDistance;
                    if (distanceToCellExit < 0 || BeltConstants.ItemWidth <= distanceToCellExit) continue;
                    state.Items.Add(new BeltItemSaveJsonObject(captured.Item, distanceToCellExit));
                }
            }

            void CollectBufferAndPriority()
            {
                // 合流・分岐のbufferと優先順は末尾マスのblockに属する
                // The buffer and priority order of a merge or branch belong to the last cell's block
                if (!location.IsLastCell || segment is not BeltBufferedSegment buffered) return;
                state.PriorityOrder = buffered.PriorityOrder;
                if (buffered.Buffer.TryGetItem(out var held)) state.BufferItem = new BeltItemSaveJsonObject(held, 0);
            }

            void CollectInternalItems()
            {
                // 合流の入力のうち内部segmentを挟むものは、その内部segment上のアイテムを入力方向つきで保存する
                // For merge inputs that go through an internal segment, save that segment's items tagged with the input direction
                if (segment.Kind != BeltSegmentKind.Merge) return;
                foreach (var input in layout.Inputs)
                {
                    if (input.IsMachine || !assembly.Layouts[input.PartnerSegmentIndex].IsInternal) continue;
                    foreach (var captured in assembly.Segments[input.PartnerSegmentIndex].CaptureItems())
                        state.InternalItems.Add(new BeltInternalItemSaveJsonObject(input.Direction, captured.Item, captured.DistanceToExit));
                }
            }

            #endregion
        }
    }
}
