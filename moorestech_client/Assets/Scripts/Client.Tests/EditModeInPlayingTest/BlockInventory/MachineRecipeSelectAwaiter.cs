using System;
using Client.Game.InGame.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.BlockInventory
{
    /// <summary>
    ///     画面の行と同じ UITK のコマンドでレシピを選び、選択が機械の区画のモデルへ届くまで待つ（結果はトーストの係へ行くので戻り値では見ない）
    ///     Selects a recipe through the UITK command the screen row uses and waits until the selection reaches the machine section model (results go to the toaster, not a return value)
    /// </summary>
    public static class MachineRecipeSelectAwaiter
    {
        private const int MaxPolls = 50;
        private const int PollMilliseconds = 100;

        public static async UniTask SelectAndWait(Guid recipeGuid)
        {
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var model = resolver.Resolve<IMachineSectionModel>();
            resolver.Resolve<IMachineRecipeSelectCommand>().Select(recipeGuid);

            // 拒否されたら選択は変わらず、[UiCommandResultToaster] の警告ログに理由が出る
            // A rejection leaves the selection unchanged and its reason appears in the [UiCommandResultToaster] warning log
            for (var i = 0; i < MaxPolls && !IsSelected(); i++) await UniTask.Delay(PollMilliseconds);
            Assert.IsTrue(IsSelected(), $"recipe {recipeGuid} was not selected; see the [UiCommandResultToaster] machine_recipe.select warning for the rejection");

            #region Internal

            bool IsSelected()
            {
                return model.Current.Value.TryGetPresent(out var machine) && machine.Detail.Selection.Matches(recipeGuid);
            }

            #endregion
        }
    }
}
