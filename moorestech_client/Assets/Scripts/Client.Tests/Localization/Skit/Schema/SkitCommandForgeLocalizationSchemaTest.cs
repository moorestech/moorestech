using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mooresmaster.Localization.Generated;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Localization.Skit.Schema
{
    public class SkitCommandForgeLocalizationSchemaTest
    {
        [Test]
        public void DictionariesContainRequiredSchemaKeysWithoutStaleKeys()
        {
            var skitRoot = Path.Combine(Application.dataPath, "AddressableResources", "Skit");
            var schemaKeys = CommandForgeLocalizationSchema.Load(Path.Combine(skitRoot, "commands.yaml"));

            // schema集合へ全言語を照合する
            // Match every language against the same schema-derived sets
            foreach (var language in LanguageCatalog.Languages)
            {
                var dictionaryPath = Path.Combine(skitRoot, "i18n", language.Code + ".json");
                var translations = (JObject)JObject.Parse(File.ReadAllText(dictionaryPath))["translations"];
                var commandForgeKeys = GetCommandForgeKeys(translations);

                CollectionAssert.IsSubsetOf(schemaKeys.Required, commandForgeKeys, language.Code);
                CollectionAssert.IsSubsetOf(commandForgeKeys, schemaKeys.Allowed, language.Code);
            }

            #region Internal

            static IEnumerable<string> GetCommandForgeKeys(JObject translations)
            {
                return translations.Properties()
                    .Select(property => property.Name)
                    .Where(key => key.StartsWith("command.", StringComparison.Ordinal) ||
                                  key.StartsWith("master.", StringComparison.Ordinal));
            }

            #endregion
        }
    }
}
