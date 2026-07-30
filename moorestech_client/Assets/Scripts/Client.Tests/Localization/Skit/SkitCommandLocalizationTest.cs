using System.IO;
using System.Collections.Generic;
using Client.Skit.Context;
using Client.Skit.Localization;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Localization.Skit
{
    public class SkitCommandLocalizationTest
    {
        [Test]
        public void LineUsesResolvedDisplayValuesAndPreservesVoiceSourceBody()
        {
            var resolver = new RecordingResolver();
            var identity = new SkitExecutionIdentity("opening");

            var line = SkitCommandLocalization.ResolveLine(
                resolver,
                identity,
                7,
                "chr_001",
                true,
                "???",
                "Raw Body");

            Assert.AreEqual("speaker:???", line.SpeakerName);
            Assert.AreEqual("body:Raw Body", line.DisplayBody);
            Assert.AreEqual("Raw Body", line.VoiceSourceBody);
        }

        [Test]
        public void SelectionUsesExactSchemaFieldNamesForAllOptions()
        {
            var resolver = new RecordingResolver();
            var identity = new SkitExecutionIdentity("opening");

            var option1 = SkitCommandLocalization.ResolveOption1(
                resolver, identity, 9, "One");
            var option2 = SkitCommandLocalization.ResolveOption2(
                resolver, identity, 9, "Two");
            var option3 = SkitCommandLocalization.ResolveOption3(
                resolver, identity, 9, "Three");

            CollectionAssert.AreEqual(
                new[] { "Option1Tag", "Option2Tag", "Option3Tag" },
                resolver.Fields);
            CollectionAssert.AreEqual(
                new[] { "Option1Tag:One", "Option2Tag:Two", "Option3Tag:Three" },
                new[] { option1, option2, option3 });
        }

        [TestCase("TextCommand.cs", 2)]
        [TestCase("BackgroundSkitTextCommand.cs", 1)]
        public void VoiceLookupUsesOnlyPreservedSourceBody(string fileName, int expectedCount)
        {
            var commandSource = ReadCommand(fileName);

            Assert.AreEqual(
                expectedCount,
                CountOccurrences(commandSource, "GetVoiceClip(CharacterId, line.VoiceSourceBody)"));
            StringAssert.DoesNotContain(
                "GetVoiceClip(CharacterId, line.DisplayBody)",
                commandSource);
        }

        [Test]
        public void TextCommandSharesOneResolvedLineAcrossWebAndUgui()
        {
            var commandSource = ReadCommand("TextCommand.cs");

            StringAssert.Contains("SkitCommandLocalization.ResolveLine(", commandSource);
            StringAssert.Contains(
                "ExecuteWebPresentationAsync(\n                    line.SpeakerName,\n                    line.DisplayBody,",
                commandSource);
            StringAssert.Contains("skitUi.SetText(line.SpeakerName, line.DisplayBody)", commandSource);
        }

        [Test]
        public void BackgroundCommandSharesOneResolvedLineAcrossWebAndUgui()
        {
            var commandSource = ReadCommand("BackgroundSkitTextCommand.cs");

            StringAssert.Contains("SkitCommandLocalization.ResolveLine(", commandSource);
            StringAssert.Contains(
                "SkitPresentationStateStore.Instance.SetBackgroundText(\n                line.SpeakerName,\n                line.DisplayBody);",
                commandSource);
            StringAssert.Contains("skitUi.SetText(line.SpeakerName, line.DisplayBody);", commandSource);
        }

        [Test]
        public void SelectionCommandCannotSupplyLocalizationFieldNames()
        {
            var commandSource = ReadCommand("SelectionCommand.cs");

            StringAssert.Contains("SkitCommandLocalization.ResolveOption1(", commandSource);
            StringAssert.Contains("SkitCommandLocalization.ResolveOption2(", commandSource);
            StringAssert.Contains("SkitCommandLocalization.ResolveOption3(", commandSource);
            StringAssert.DoesNotContain("\"Option1Tag\"", commandSource);
            StringAssert.DoesNotContain("\"Option2Tag\"", commandSource);
            StringAssert.DoesNotContain("\"Option3Tag\"", commandSource);
            Assert.AreEqual(3, CountOccurrences(commandSource, "labels.Add(label);"));
            Assert.AreEqual(3, CountOccurrences(commandSource, "choices.Add(CreateChoice(label));"));
        }

        private static string ReadCommand(string fileName)
        {
            var path = Path.Combine(
                Application.dataPath,
                "Scripts/Client.Skit/Commands",
                fileName);
            return File.ReadAllText(path);
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var offset = 0;
            while ((offset = source.IndexOf(value, offset, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }

        private sealed class RecordingResolver : ISkitLocalizationResolver
        {
            public readonly List<string> Fields = new();

            public string ResolveCommandField(
                string skitTitle,
                int commandId,
                string field,
                string sourceText)
            {
                Fields.Add(field);
                return $"{field}:{sourceText}";
            }

            public string ResolveCharacterName(
                string characterId,
                string skitTitle,
                int commandId,
                bool useOverride,
                string overrideSource)
            {
                return $"speaker:{overrideSource}";
            }
        }
    }
}
