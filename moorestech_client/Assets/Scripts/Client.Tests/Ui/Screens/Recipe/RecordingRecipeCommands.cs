using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Recipe.Commands;

namespace Client.Tests.Ui.Screens.Recipe
{
    /// <summary>
    /// レシピ画面の操作の偽。クラフトの呼び出しを記録し、決めた結果を返す
    /// Fake recipe commands that record craft calls and return a chosen result
    /// </summary>
    public sealed class RecordingRecipeCommands : IRecipeCommands
    {
        private UiCommandResult _result = UiCommandAccepted.Instance;

        public List<Guid> Crafted { get; } = new();

        public void SetResult(UiCommandResult result)
        {
            _result = result;
        }

        public UiCommandResult Craft(Guid recipeGuid)
        {
            Crafted.Add(recipeGuid);
            return _result;
        }
    }
}
