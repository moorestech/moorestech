using System.Collections.Generic;
using Core.Item.Interface;
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
        List<IItemStack> Decorate(MachineRecipeMasterElement recipe, List<IItemStack> realizedOutputs);
    }
}
