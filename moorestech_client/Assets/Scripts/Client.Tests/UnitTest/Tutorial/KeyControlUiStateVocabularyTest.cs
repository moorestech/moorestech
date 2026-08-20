using System;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.UI.UIState;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;

namespace Client.Tests.UnitTest.Tutorial
{
    // schemaのuiState optionsを正本としUIStateEnumとの語彙ズレを検出する
    // Treats the schema's uiState options as the source of truth and catches drift against UIStateEnum
    public class KeyControlUiStateVocabularyTest
    {
        [Test]
        public void UiStateOptionsMatchUIStateEnumExceptDebug()
        {
            var schemaOptions = SchemaUiStateOptionNames();
            var enumNames = Enum.GetNames(typeof(UIStateEnum))
                .Where(name => name != nameof(UIStateEnum.Debug))
                .ToHashSet();

            CollectionAssert.AreEquivalent(enumNames, schemaOptions);

            #region Internal

            string[] SchemaUiStateOptionNames()
            {
                // 生成型UiStateConstの定数フィールド名を読む(SourceGenerator出力がschema optionsの正本)
                // Read UiStateConst's const field names (SourceGenerator output is the schema options source of truth)
                var uiStateConstType = typeof(KeyControlTutorialParam).GetNestedType("UiStateConst");
                var fields = uiStateConstType.GetFields(BindingFlags.Public | BindingFlags.Static);
                return fields.Select(field => field.Name).ToArray();
            }

            #endregion
        }
    }
}
