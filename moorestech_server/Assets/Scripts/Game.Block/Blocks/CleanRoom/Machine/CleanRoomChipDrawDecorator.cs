using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.State;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    // 清浄室機械のチップ抽選を実現出力の確定直後に適用する。容量判定より前に置換するので判定対象と挿入物が一致する
    // Applies the clean-room chip draw right after the outputs are realized; the swap precedes the capacity check so judged equals inserted
    internal class CleanRoomChipDrawDecorator : IRealizedOutputDecorator
    {
        // 抽選シードのブロック固有カウンタ。セーブへ持ち出すため公開する
        // Per-block counter feeding the draw seed; exposed because it is serialized
        public uint CycleCount { get; private set; }

        private readonly BlockInstanceId _blockInstanceId;
        private CleanRoomEffect _cleanRoomEffect = new(false, 0, 0);

        public CleanRoomChipDrawDecorator(BlockInstanceId blockInstanceId, uint cycleCount)
        {
            _blockInstanceId = blockInstanceId;
            CycleCount = cycleCount;
        }

        public void SetCleanRoomEffect(CleanRoomEffect effect)
        {
            _cleanRoomEffect = effect;
        }

        public List<IItemStack> Decorate(MachineRecipeMasterElement recipe, List<IItemStack> realizedOutputs)
        {
            if (!MasterHolder.CleanRoomMaster.TryGetChipDraw(recipe.MachineRecipeGuid, out var chipDraw)) return realizedOutputs;

            // 抽選のたびにカウンタを進め、ブロック固有で再現可能なシードにする
            // Advance the counter on every draw to keep the seed per-block and reproducible
            CycleCount++;
            var seed = ((long)_blockInstanceId.AsPrimitive() << 20) ^ CycleCount;
            var replaced = new List<IItemStack>(realizedOutputs.Count);
            for (var i = 0; i < realizedOutputs.Count; i++)
            {
                replaced.Add(DrawSlot(realizedOutputs[i], i));
            }
            return replaced;

            #region Internal

            IItemStack DrawSlot(IItemStack output, int outputIndex)
            {
                foreach (var distribution in chipDraw.OutputDistributions)
                {
                    if (MasterHolder.ItemMaster.GetItemId(distribution.OutputItemGuid) != output.Id) continue;
                    var levels = new List<(int level, double weight, ItemId chipItemId)>();
                    foreach (var level in distribution.Levels)
                    {
                        levels.Add((level.Level, level.Weight, MasterHolder.ItemMaster.GetItemId(level.ChipItemGuid)));
                    }
                    levels.Sort((a, b) => a.level.CompareTo(b.level));
                    var result = CleanRoomChipDraw.TryDraw(levels, _cleanRoomEffect.MaxChipLevel, _cleanRoomEffect.DownBinRate, chipDraw.EuvSuccessRate, seed, outputIndex, out var itemId);
                    return result == CleanRoomChipDraw.Result.Drawn
                        ? ServerContext.ItemStackFactory.Create(itemId, output.Count)
                        : ServerContext.ItemStackFactory.CreatEmpty();
                }
                // 抽選テーブル無ければ素の出力
                // If no distribution matches this recipe output, leave it unchanged
                return output;
            }

            #endregion
        }
    }
}
