using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mooresmaster.Localization.Generated;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Localization.Skit
{
    public class SkitLocalizationRuntimeContentTest
    {
        private static readonly string[] QaSentinels =
        {
            "MOD ENGLISH",
            "MOD JAPANESE",
            "SKIT ENGLISH",
            "SKIT JAPANESE",
        };

        // LanguageCatalog由来で全言語を走査しgerman等の未検査を防ぐ
        // Drive from LanguageCatalog so german and future languages are never left unchecked
        private static IEnumerable<string> LanguageCodes()
        {
            return LanguageCatalog.Languages.Select(language => language.Code);
        }

        [TestCaseSource(nameof(LanguageCodes))]
        public void AddressableSkitRuntimeValuesExcludeQaSentinels(string languageCode)
        {
            var path = Path.Combine(
                Application.dataPath,
                "AddressableResources",
                "Skit",
                "i18n",
                languageCode + ".json");
            var translations = (JObject)JObject.Parse(File.ReadAllText(path))["translations"];

            // 実行時skit値からQA識別子を排除
            // Protect only runtime-resolved skit values from QA identifiers
            foreach (var property in translations.Properties())
            {
                if (!property.Name.StartsWith("skit.", StringComparison.Ordinal)) continue;
                AssertRuntimeValue(property.Name, (string)property.Value);
            }
        }

        private static void AssertRuntimeValue(string location, string value)
        {
            foreach (var sentinel in QaSentinels)
            {
                StringAssert.DoesNotContain(sentinel, value, location);
            }
        }
    }
}
