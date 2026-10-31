using System;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master.CleanRoomRoles
{
    /// <summary>
    ///     清浄室工程（成膜・EUV・ダイシング）でレシピが担う役割。出力の添字やアイテムIDは索引のときに1回だけ解決して持つ
    ///     The role a recipe plays in the clean-room chip process (deposition, EUV, dicing); output indices and item ids are resolved once at indexing
    /// </summary>
    public abstract class CleanRoomRecipeRole
    {
        // 種別はこのディレクトリの役割の型に閉じる
        // The kinds are closed to the role types in this directory
        private protected CleanRoomRecipeRole()
        {
        }

        // 役割表が指す出力の添字。ちょうど1つあることは起動時の検証が索引より先に保証する
        // Index of the output a role table points at; boot validation guarantees exactly one before indexing
        private protected static int IndexOfOutput(MachineRecipeMasterElement recipe, Guid itemGuid)
        {
            for (var i = 0; i < recipe.OutputItems.Length; i++)
            {
                if (recipe.OutputItems[i].ItemGuid == itemGuid) return i;
            }
            throw new InvalidOperationException($"検証済みのはずの出力 {itemGuid} がレシピ {recipe.MachineRecipeGuid} にありません");
        }
    }
}
