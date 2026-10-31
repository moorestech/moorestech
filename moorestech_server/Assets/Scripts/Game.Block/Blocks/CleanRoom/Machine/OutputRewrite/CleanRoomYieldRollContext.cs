using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom.Machine.OutputRewrite
{
    /// <summary>
    ///     1サイクルの抽選に要る入力（部屋クラスの値・モジュール効果・工場の1セットとセット数・乱数列）
    ///     Inputs for one cycle's draw (class values, module effect, the factory's set and set count, random sequence)
    /// </summary>
    internal readonly struct CleanRoomYieldRollContext
    {
        // 抽選は稼働できる部屋でだけ起きるので、クラス値は必ずある
        // Draws only happen in an operable room, so the class values always exist
        public readonly CleanRoomClassYield ClassYield;
        public readonly MachineModuleEffect ModuleEffect;

        // 工場が作った1セット目（継承レシピは継承先が継承レベルの変種）と、工場が決めたセット数
        // The factory's first set (an inheritance recipe's target is already the inherited level's variant) and the set count the factory decided
        public readonly IReadOnlyList<IItemStack> FactorySet;
        public readonly int SetCount;
        public readonly CleanRoomDrawRandom Random;

        public CleanRoomYieldRollContext(CleanRoomClassYield classYield, MachineModuleEffect moduleEffect, IReadOnlyList<IItemStack> factorySet, int setCount, CleanRoomDrawRandom random)
        {
            ClassYield = classYield;
            ModuleEffect = moduleEffect;
            FactorySet = factorySet;
            SetCount = setCount;
            Random = random;
        }
    }
}
