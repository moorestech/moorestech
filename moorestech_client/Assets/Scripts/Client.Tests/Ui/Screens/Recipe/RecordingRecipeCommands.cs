using System;
using System.Collections.Generic;
using Client.Ui.State.Recipe.Commands;

namespace Client.Tests.Ui.Screens.Recipe
{
    /// <summary>
    /// レシピ画面の操作の偽。クラフトの呼び出しを記録する
    /// Fake recipe commands that record craft calls
    /// </summary>
    public sealed class RecordingRecipeCommands : IRecipeCommands
    {
        public List<Guid> Crafted { get; } = new();

        public void Craft(Guid recipeGuid)
        {
            Crafted.Add(recipeGuid);
        }
    }
}
