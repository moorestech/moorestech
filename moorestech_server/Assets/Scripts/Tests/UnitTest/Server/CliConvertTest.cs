#nullable enable
using System;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;

namespace Tests.UnitTest.Server
{
    /// <summary>
    /// このコードはバイブコーディングの結果です。
    /// This code is the result of the vibe coding.
    /// </summary>
    public class CliConvertTest
    {
        #region Test Models
        
        // 基本的なテスト用モデル
        public class BasicTestSettings
        {
            [Option(false, "--string", "-s")]
            public string StringValue { get; set; } = "default";
            
            [Option(false, "--int", "-i")]
            public int IntValue { get; set; } = 0;
            
            [Option(false, "--bool", "-b")]
            public bool BoolValue { get; set; } = false;
            
            [Option(true, "--flag", "-f")]
            public bool FlagValue { get; set; } = false;
        }
        
        // 列挙型を含むモデル
        public enum TestEnum
        {
            None,
            First,
            Second,
            Third
        }
        
        public class EnumTestSettings
        {
            [Option(false, "--enum", "-e")]
            public TestEnum EnumValue { get; set; } = TestEnum.None;
            
            [Option(false, "--mode")]
            public TestEnum Mode { get; set; } = TestEnum.First;
        }
        
        // 複数の名前を持つオプション
        public class MultiNameSettings
        {
            [Option(false, "--verbose", "-v", "--debug")]
            public bool Verbose { get; set; } = false;
            
            [Option(false, "--output", "-o", "--out")]
            public string OutputPath { get; set; } = "";
        }
        
        // デフォルト値を持つ複雑なモデル
        public class ComplexDefaultSettings
        {
            [Option(false, "--path")]
            public string Path { get; set; } = "/default/path";
            
            [Option(false, "--count")]
            public int Count { get; set; } = 10;
            
            [Option(true, "--enabled")]
            public bool Enabled { get; set; } = true;
            
            [Option(false, "--threshold")]
            public int Threshold { get; set; } = 100;
        }
        
        // 属性のないプロパティを含むモデル
        public class MixedAttributeSettings
        {
            [Option(false, "--name")]
            public string Name { get; set; } = "";
            
            public string NoAttribute { get; set; } = "ignored";
            
            [Option(false, "--id")]
            public int Id { get; set; } = 0;
        }
        
        // 空のモデル（属性なし）
        public class EmptySettings
        {
            public string Property1 { get; set; } = "";
            public int Property2 { get; set; } = 0;
        }
        
        // 特殊文字を含む値のテスト用
        public class SpecialCharSettings
        {
            [Option(false, "--text")]
            public string Text { get; set; } = "";
            
            [Option(false, "--path")]
            public string Path { get; set; } = "";
        }
        
        #endregion
        
        #region Basic Parsing Tests
        
        [Test]
        public void Parse_StringOption_ShouldParseCorrectly()
        {
            var args = new[] { "--string", "test-value" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("test-value", result.StringValue);
            Assert.AreEqual(0, result.IntValue);
            Assert.AreEqual(false, result.BoolValue);
            Assert.AreEqual(false, result.FlagValue);
        }
        
        [Test]
        public void Parse_ShortFormOption_ShouldParseCorrectly()
        {
            var args = new[] { "-s", "short-value" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("short-value", result.StringValue);
        }
        
        [Test]
        public void Parse_IntOption_ShouldParseCorrectly()
        {
            var args = new[] { "--int", "42" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(42, result.IntValue);
        }
        
        [Test]
        public void Parse_NegativeInt_ShouldParseCorrectly()
        {
            var args = new[] { "--int", "-123" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(-123, result.IntValue);
        }
        
        [Test]
        public void Parse_BoolOption_ShouldParseCorrectly()
        {
            var args = new[] { "--bool", "true" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(true, result.BoolValue);
        }
        
        [Test]
        public void Parse_BoolFalseOption_ShouldParseCorrectly()
        {
            var args = new[] { "--bool", "false" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(false, result.BoolValue);
        }
        
        [Test]
        public void Parse_FlagOption_ShouldSetToTrue()
        {
            var args = new[] { "--flag" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(true, result.FlagValue);
        }
        
        [Test]
        public void Parse_ShortFlagOption_ShouldSetToTrue()
        {
            var args = new[] { "-f" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(true, result.FlagValue);
        }
        
        [Test]
        public void Parse_MultipleOptions_ShouldParseAllCorrectly()
        {
            var args = new[] { "--string", "hello", "--int", "999", "--bool", "true", "--flag" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("hello", result.StringValue);
            Assert.AreEqual(999, result.IntValue);
            Assert.AreEqual(true, result.BoolValue);
            Assert.AreEqual(true, result.FlagValue);
        }
        
        [Test]
        public void Parse_MixedShortAndLongOptions_ShouldParseCorrectly()
        {
            var args = new[] { "-s", "mixed", "--int", "100", "-f" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("mixed", result.StringValue);
            Assert.AreEqual(100, result.IntValue);
            Assert.AreEqual(true, result.FlagValue);
        }
        
        #endregion
        
        #region Enum Parsing Tests
        
        [Test]
        public void Parse_EnumOption_ShouldParseCorrectly()
        {
            var args = new[] { "--enum", "Second" };
            var result = CliConvert.Parse<EnumTestSettings>(args);
            
            Assert.AreEqual(TestEnum.Second, result.EnumValue);
        }
        
        [Test]
        public void Parse_EnumCaseInsensitive_ShouldParseCorrectly()
        {
            var args = new[] { "--enum", "third" };
            var result = CliConvert.Parse<EnumTestSettings>(args);
            
            Assert.AreEqual(TestEnum.Third, result.EnumValue);
        }
        
        [Test]
        public void Parse_EnumWithNumericValue_ShouldParseCorrectly()
        {
            var args = new[] { "--enum", "2" };
            var result = CliConvert.Parse<EnumTestSettings>(args);
            
            Assert.AreEqual(TestEnum.Second, result.EnumValue);
        }
        
        #endregion
        
        #region Multi-Name Options Tests
        
        [Test]
        public void Parse_MultiNameLongForm_ShouldParseCorrectly()
        {
            var args = new[] { "--verbose", "true" };
            var result = CliConvert.Parse<MultiNameSettings>(args);
            
            Assert.AreEqual(true, result.Verbose);
        }
        
        [Test]
        public void Parse_MultiNameShortForm_ShouldParseCorrectly()
        {
            var args = new[] { "-v", "true" };
            var result = CliConvert.Parse<MultiNameSettings>(args);
            
            Assert.AreEqual(true, result.Verbose);
        }
        
        [Test]
        public void Parse_MultiNameAlternativeForm_ShouldParseCorrectly()
        {
            var args = new[] { "--debug", "true" };
            var result = CliConvert.Parse<MultiNameSettings>(args);
            
            Assert.AreEqual(true, result.Verbose);
        }
        
        [Test]
        public void Parse_MultipleNamesForDifferentOptions_ShouldParseCorrectly()
        {
            var args = new[] { "--out", "/output/path", "-v", "true" };
            var result = CliConvert.Parse<MultiNameSettings>(args);
            
            Assert.AreEqual("/output/path", result.OutputPath);
            Assert.AreEqual(true, result.Verbose);
        }
        
        #endregion
        
        #region Serialization Tests
        
        [Test]
        public void Serialize_DefaultValues_ShouldReturnEmptyArray()
        {
            var settings = new BasicTestSettings();
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(0, result.Length);
        }
        
        [Test]
        public void Serialize_SingleStringValue_ShouldSerializeCorrectly()
        {
            var settings = new BasicTestSettings { StringValue = "custom" };
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("--string", result[0]);
            Assert.AreEqual("custom", result[1]);
        }
        
        [Test]
        public void Serialize_IntValue_ShouldSerializeCorrectly()
        {
            var settings = new BasicTestSettings { IntValue = 123 };
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("--int", result[0]);
            Assert.AreEqual("123", result[1]);
        }
        
        [Test]
        public void Serialize_BoolValue_ShouldSerializeCorrectly()
        {
            var settings = new BasicTestSettings { BoolValue = true };
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("--bool", result[0]);
            Assert.AreEqual("True", result[1]);
        }
        
        [Test]
        public void Serialize_FlagValue_ShouldSerializeAsSwitch()
        {
            var settings = new BasicTestSettings { FlagValue = true };
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("--flag", result[0]);
        }
        
        [Test]
        public void Serialize_MultipleValues_ShouldSerializeAllCorrectly()
        {
            var settings = new BasicTestSettings 
            { 
                StringValue = "test",
                IntValue = 456,
                BoolValue = true,
                FlagValue = true
            };
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(7, result.Length);
            Assert.Contains("--string", result);
            Assert.Contains("test", result);
            Assert.Contains("--int", result);
            Assert.Contains("456", result);
            Assert.Contains("--bool", result);
            Assert.Contains("True", result);
            Assert.Contains("--flag", result);
        }
        
        [Test]
        public void Serialize_EnumValue_ShouldSerializeCorrectly()
        {
            var settings = new EnumTestSettings { EnumValue = TestEnum.Third };
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("--enum", result[0]);
            Assert.AreEqual("Third", result[1]);
        }
        
        [Test]
        public void Serialize_ComplexDefaults_ShouldSkipDefaults()
        {
            var settings = new ComplexDefaultSettings();
            var result = CliConvert.Serialize(settings);
            
            // Enabled=true is default but it's a flag, should not appear
            // Other properties have non-default initial values but should not appear
            Assert.AreEqual(0, result.Length);
        }
        
        [Test]
        public void Serialize_MultiNameOption_ShouldUsePrimaryName()
        {
            var settings = new MultiNameSettings { Verbose = true };
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("--verbose", result[0]); // Primary name
            Assert.AreEqual("True", result[1]);
        }
        
        #endregion
        
        #region Round-Trip Tests
        
        [Test]
        public void RoundTrip_BasicSettings_ShouldPreserveValues()
        {
            var original = new BasicTestSettings 
            { 
                StringValue = "round-trip",
                IntValue = 789,
                BoolValue = true,
                FlagValue = true
            };
            
            var serialized = CliConvert.Serialize(original);
            var parsed = CliConvert.Parse<BasicTestSettings>(serialized);
            
            Assert.AreEqual(original.StringValue, parsed.StringValue);
            Assert.AreEqual(original.IntValue, parsed.IntValue);
            Assert.AreEqual(original.BoolValue, parsed.BoolValue);
            Assert.AreEqual(original.FlagValue, parsed.FlagValue);
        }
        
        [Test]
        public void RoundTrip_EnumSettings_ShouldPreserveValues()
        {
            var original = new EnumTestSettings 
            { 
                EnumValue = TestEnum.Second,
                Mode = TestEnum.Third
            };
            
            var serialized = CliConvert.Serialize(original);
            var parsed = CliConvert.Parse<EnumTestSettings>(serialized);
            
            Assert.AreEqual(original.EnumValue, parsed.EnumValue);
            Assert.AreEqual(original.Mode, parsed.Mode);
        }
        
        [Test]
        public void RoundTrip_ServerSettings_ShouldPreserveValues()
        {
            var original = new StartServerSettings
            {
                SaveFilePath = "/custom/save.json",
                AutoSave = false
            };
            
            var serialized = CliConvert.Serialize(original);
            var parsed = CliConvert.Parse<StartServerSettings>(serialized);
            
            Assert.AreEqual(original.SaveFilePath, parsed.SaveFilePath);
            Assert.AreEqual(original.AutoSave, parsed.AutoSave);
        }
        
        #endregion
        
        #region Error Cases Tests
        
        [Test]
        public void Parse_UnknownOption_ShouldThrow()
        {
            var args = new[] { "--unknown", "value" };
            
            Assert.Throws<ArgumentException>(() => 
                CliConvert.Parse<BasicTestSettings>(args));
        }
        
        [Test]
        public void Parse_NonOptionToken_ShouldThrow()
        {
            var args = new[] { "not-an-option" };
            
            Assert.Throws<ArgumentException>(() => 
                CliConvert.Parse<BasicTestSettings>(args));
        }
        
        [Test]
        public void Parse_MissingValueForOption_ShouldThrow()
        {
            var args = new[] { "--string" };
            
            Assert.Throws<ArgumentException>(() => 
                CliConvert.Parse<BasicTestSettings>(args));
        }
        
        [Test]
        public void Parse_InvalidIntValue_ShouldThrow()
        {
            var args = new[] { "--int", "not-a-number" };
            
            Assert.Throws<FormatException>(() => 
                CliConvert.Parse<BasicTestSettings>(args));
        }
        
        [Test]
        public void Parse_InvalidBoolValue_ShouldThrow()
        {
            var args = new[] { "--bool", "not-a-bool" };
            
            Assert.Throws<FormatException>(() => 
                CliConvert.Parse<BasicTestSettings>(args));
        }
        
        [Test]
        public void Parse_InvalidEnumValue_ShouldThrow()
        {
            var args = new[] { "--enum", "InvalidValue" };
            
            Assert.Throws<ArgumentException>(() => 
                CliConvert.Parse<EnumTestSettings>(args));
        }
        
        [Test]
        public void Parse_ValueAfterFlag_ShouldTreatAsUnknownOption()
        {
            var args = new[] { "--flag", "unexpected-value" };
            
            // Flag doesn't consume the next token, so "unexpected-value" is treated as a new token
            Assert.Throws<ArgumentException>(() => 
                CliConvert.Parse<BasicTestSettings>(args));
        }
        
        #endregion
        
        #region Edge Cases Tests
        
        [Test]
        public void Parse_EmptyArgs_ShouldReturnDefaults()
        {
            var args = new string[0];
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("default", result.StringValue);
            Assert.AreEqual(0, result.IntValue);
            Assert.AreEqual(false, result.BoolValue);
            Assert.AreEqual(false, result.FlagValue);
        }
        
        [Test]
        public void Parse_EmptyStringValue_ShouldParseCorrectly()
        {
            var args = new[] { "--string", "" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("", result.StringValue);
        }
        
        [Test]
        public void Parse_WhitespaceStringValue_ShouldParseCorrectly()
        {
            var args = new[] { "--string", "   " };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("   ", result.StringValue);
        }
        
        [Test]
        public void Parse_SpecialCharactersInString_ShouldParseCorrectly()
        {
            var args = new[] { "--text", "Hello@World#123!" };
            var result = CliConvert.Parse<SpecialCharSettings>(args);
            
            Assert.AreEqual("Hello@World#123!", result.Text);
        }
        
        [Test]
        public void Parse_PathWithSpaces_ShouldParseCorrectly()
        {
            var args = new[] { "--path", "/path with spaces/file.txt" };
            var result = CliConvert.Parse<SpecialCharSettings>(args);
            
            Assert.AreEqual("/path with spaces/file.txt", result.Path);
        }
        
        [Test]
        public void Parse_ZeroInt_ShouldParseCorrectly()
        {
            var args = new[] { "--int", "0" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(0, result.IntValue);
        }
        
        [Test]
        public void Parse_MaxIntValue_ShouldParseCorrectly()
        {
            var args = new[] { "--int", int.MaxValue.ToString() };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(int.MaxValue, result.IntValue);
        }
        
        [Test]
        public void Parse_MinIntValue_ShouldParseCorrectly()
        {
            var args = new[] { "--int", int.MinValue.ToString() };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(int.MinValue, result.IntValue);
        }
        
        [Test]
        public void Parse_DuplicateOption_ShouldUseLastValue()
        {
            var args = new[] { "--string", "first", "--string", "second" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("second", result.StringValue);
        }
        
        [Test]
        public void Parse_DuplicateFlag_ShouldRemainTrue()
        {
            var args = new[] { "--flag", "--flag" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(true, result.FlagValue);
        }
        
        #endregion
        
        #region Mixed Attribute Tests
        
        [Test]
        public void Parse_MixedAttributeClass_ShouldIgnoreNonAttributedProperties()
        {
            var args = new[] { "--name", "TestName", "--id", "123" };
            var result = CliConvert.Parse<MixedAttributeSettings>(args);
            
            Assert.AreEqual("TestName", result.Name);
            Assert.AreEqual(123, result.Id);
            Assert.AreEqual("ignored", result.NoAttribute); // Should keep default
        }
        
        [Test]
        public void Serialize_MixedAttributeClass_ShouldOnlySerializeAttributedProperties()
        {
            var settings = new MixedAttributeSettings
            {
                Name = "Test",
                NoAttribute = "ShouldNotAppear",
                Id = 456
            };
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(4, result.Length);
            Assert.Contains("--name", result);
            Assert.Contains("Test", result);
            Assert.Contains("--id", result);
            Assert.Contains("456", result);
            Assert.IsFalse(Array.Exists(result, s => s == "ShouldNotAppear"));
        }
        
        [Test]
        public void Parse_EmptySettingsClass_ShouldReturnInstance()
        {
            var args = new string[0];
            var result = CliConvert.Parse<EmptySettings>(args);
            
            Assert.IsNotNull(result);
            Assert.AreEqual("", result.Property1);
            Assert.AreEqual(0, result.Property2);
        }
        
        [Test]
        public void Serialize_EmptySettingsClass_ShouldReturnEmptyArray()
        {
            var settings = new EmptySettings { Property1 = "value", Property2 = 100 };
            var result = CliConvert.Serialize(settings);
            
            Assert.AreEqual(0, result.Length);
        }
        
        #endregion
        
        #region Complex Scenarios Tests
        
        [Test]
        public void Parse_ComplexCommandLine_ShouldParseCorrectly()
        {
            var args = new[] 
            { 
                "-s", "complex",
                "--int", "999",
                "-f",
                "--bool", "true",
                "-i", "888", // Override previous int
                "--flag" // Duplicate flag
            };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("complex", result.StringValue);
            Assert.AreEqual(888, result.IntValue); // Last value wins
            Assert.AreEqual(true, result.BoolValue);
            Assert.AreEqual(true, result.FlagValue);
        }
        
        [Test]
        public void Parse_OptionsInRandomOrder_ShouldParseCorrectly()
        {
            var args = new[] 
            { 
                "--flag",
                "--int", "123",
                "--string", "random",
                "--bool", "false"
            };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("random", result.StringValue);
            Assert.AreEqual(123, result.IntValue);
            Assert.AreEqual(false, result.BoolValue);
            Assert.AreEqual(true, result.FlagValue);
        }
        
        [Test]
        public void Serialize_NullPropertyValue_ShouldSerializeAsEmpty()
        {
            var settings = new BasicTestSettings { StringValue = null! };
            var result = CliConvert.Serialize(settings);
            
            // Since null != "default", it should serialize
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("--string", result[0]);
            Assert.AreEqual("", result[1]); // null becomes empty string
        }
        
        [Test]
        public void Parse_BoolCaseVariations_ShouldParseCorrectly()
        {
            var testCases = new[] { "True", "true", "TRUE", "False", "false", "FALSE" };
            var expectedValues = new[] { true, true, true, false, false, false };
            
            for (int i = 0; i < testCases.Length; i++)
            {
                var args = new[] { "--bool", testCases[i] };
                var result = CliConvert.Parse<BasicTestSettings>(args);
                Assert.AreEqual(expectedValues[i], result.BoolValue, 
                    $"Failed for bool value: {testCases[i]}");
            }
        }
        
        [Test]
        public void Parse_ConsecutiveFlags_ShouldParseCorrectly()
        {
            // Create a test model with multiple flags
            var args = new[] { "-f", "-s", "value", "-b", "true" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(true, result.FlagValue);
            Assert.AreEqual("value", result.StringValue);
            Assert.AreEqual(true, result.BoolValue);
        }
        
        [Test]
        public void Parse_HyphenInValue_ShouldParseCorrectly()
        {
            var args = new[] { "--string", "value-with-hyphens" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("value-with-hyphens", result.StringValue);
        }
        
        [Test]
        public void Parse_NumericStringValue_ShouldParseAsString()
        {
            var args = new[] { "--string", "12345" };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("12345", result.StringValue);
        }
        
        #endregion
        
        #region StartServerSettings Integration Tests
        
        [Test]
        public void Parse_StartServerSettings_DefaultValues()
        {
            var args = new string[0];
            var result = CliConvert.Parse<StartServerSettings>(args);
            
            Assert.AreEqual(MoorestechServerDIContainerOptions.DefaultSaveJsonFilePath, result.SaveFilePath);
            Assert.AreEqual(true, result.AutoSave);
        }
        
        [Test]
        public void Parse_StartServerSettings_CustomSavePath()
        {
            var args = new[] { "--saveFilePath", "/custom/path/save.json" };
            var result = CliConvert.Parse<StartServerSettings>(args);
            
            Assert.AreEqual("/custom/path/save.json", result.SaveFilePath);
            Assert.AreEqual(true, result.AutoSave);
        }
        
        [Test]
        public void Parse_StartServerSettings_DisableAutoSave()
        {
            var args = new[] { "--autoSave", "false" };
            var result = CliConvert.Parse<StartServerSettings>(args);
            
            // AutoSave is now a regular bool option, not a flag
            Assert.AreEqual(false, result.AutoSave);
        }
        
        [Test]
        public void Parse_StartServerSettings_ShortForms()
        {
            var args = new[] { "-s", "/short/save.json", "-c", "true" };
            var result = CliConvert.Parse<StartServerSettings>(args);
            
            Assert.AreEqual("/short/save.json", result.SaveFilePath);
            Assert.AreEqual(true, result.AutoSave);
        }
        
        [Test]
        public void Parse_StartServerSettings_ComplexScenario()
        {
            var args = new[] { "-c", "true", "--saveFilePath", "/complex/save.json" };
            var result = CliConvert.Parse<StartServerSettings>(args);
            
            Assert.AreEqual("/complex/save.json", result.SaveFilePath);
            Assert.AreEqual(true, result.AutoSave);
        }
        
        #endregion
        
        #region Performance Edge Cases
        
        [Test]
        public void Parse_VeryLongStringValue_ShouldHandleCorrectly()
        {
            var longString = new string('x', 10000);
            var args = new[] { "--string", longString };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual(longString, result.StringValue);
        }
        
        [Test]
        public void Parse_ManyOptions_ShouldHandleCorrectly()
        {
            var args = new[]
            {
                "--string", "value1",
                "--int", "100",
                "--bool", "true",
                "--flag",
                "-s", "value2",  // Override
                "-i", "200",      // Override
                "-b", "false",    // Override
                "-f"              // Already true
            };
            var result = CliConvert.Parse<BasicTestSettings>(args);
            
            Assert.AreEqual("value2", result.StringValue);
            Assert.AreEqual(200, result.IntValue);
            Assert.AreEqual(false, result.BoolValue);
            Assert.AreEqual(true, result.FlagValue);
        }
        
        #endregion
    }
}