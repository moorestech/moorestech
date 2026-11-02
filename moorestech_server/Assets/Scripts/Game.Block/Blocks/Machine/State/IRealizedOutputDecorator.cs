using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.Machine.Module;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.State
{
    /// <summary>
    ///     実現出力が確定した直後に内容を差し替える口。容量判定より前に置換を終えるため、判定した物と挿入する物が常に一致する
    ///     Hook that rewrites the realized outputs the moment they are fixed; the swap lands before the capacity check,
    ///     so what was judged is always what gets inserted
    /// </summary>
    internal interface IRealizedOutputDecorator
    {
        // 継承レベル（継承しないレシピは null）・出力を作ったのと同じモジュール効果の集計・工場のセット数（実現出力は1セットをその回数並べたもの）を受け取る
        // Receives the inherited level (null for non-inheritance recipes), the module-effect aggregate that built the outputs, and the factory's set count (the outputs are one set laid out that many times)
        // 差し替えたときは確定に使った値を drawRecord に返し、素通しなら null を返す
        // When it rewrites, it returns the values used for fixing in drawRecord; a pass-through returns null
        List<IItemStack> Decorate(MachineRecipeMasterElement recipe, int? inheritedLevel, MachineModuleEffect moduleEffect, int setCount, List<IItemStack> realizedOutputs, out IRealizedOutputDrawRecord drawRecord);
    }

    /// <summary>
    ///     差し替え口が確定時に使った値の記録。中身は差し替え口ごとの型が持ち、機械の汎用処理は中身を読まずに運ぶだけ
    ///     Record of the values a rewrite hook used when fixing; each hook's own type holds the content, and the generic machine flow only carries it
    /// </summary>
    internal interface IRealizedOutputDrawRecord
    {
    }
}
