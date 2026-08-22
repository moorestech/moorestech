using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Skit
{
    // 位置コマンドが必ずSkitOriginを加算する不変条件をソース走査で守る（ADR 0029）
    // Guards the invariant that positional commands always add SkitOrigin, via source scanning (ADR 0029)
    public class SkitPositionalCommandOriginGuardTest
    {
        [Test]
        public void EveryPositionalCommandAddsOriginToEveryPositionProperty()
        {
            var positionalCommands = CollectPositionalCommandsFromYaml();

            // 位置コマンドが1つも取れないのはパーサ側の故障。無言の全緑を防ぐ
            // Zero positional commands means the parser broke; never pass silently
            Assert.GreaterOrEqual(positionalCommands.Count, 4);

            foreach (var (commandId, positionProperties) in positionalCommands)
            {
                var source = ReadCommandSource(commandId);
                foreach (var property in positionProperties)
                {
                    StringAssert.Contains(
                        $"origin.ToWorld({ToPascalCase(property)}",
                        source,
                        $"{commandId} の {property} が origin.ToWorld を通っていない（ADR 0029違反）");
                }
            }

            #region Internal

            List<(string commandId, List<string> positionProperties)> CollectPositionalCommandsFromYaml()
            {
                var yamlPath = Path.Combine(Application.dataPath, "AddressableResources/Skit/commands.yaml");
                var results = new List<(string, List<string>)>();
                string currentCommand = null;
                string pendingProperty = null;
                var currentProperties = new List<string>();

                foreach (var line in File.ReadAllLines(yamlPath))
                {
                    var idMatch = Regex.Match(line, @"^\s*-\s+id:\s*(\w+)");
                    if (idMatch.Success)
                    {
                        FlushCurrentCommand();
                        currentCommand = idMatch.Groups[1].Value;
                        continue;
                    }

                    // 1行形式 `Position: { type: vector3, ... }` と複数行形式の両方を拾う
                    // Catch both the inline `Position: { type: vector3, ... }` and the multi-line form
                    var propertyMatch = Regex.Match(line, @"^\s+(\w+):\s*(.*)$");
                    if (!propertyMatch.Success) continue;

                    var name = propertyMatch.Groups[1].Value;
                    var rest = propertyMatch.Groups[2].Value;
                    // 型行は直前のプロパティ名に属するので、1行形式より先に判定する
                    // A type line belongs to the preceding property name, so judge it before the inline form
                    if (name == "type")
                    {
                        if (rest.StartsWith("vector3") && pendingProperty != null) AddIfPosition(pendingProperty);
                        pendingProperty = null;
                    }
                    else if (rest.Contains("vector3")) AddIfPosition(name);
                    else if (rest.Length == 0) pendingProperty = name;
                }

                FlushCurrentCommand();
                return results;

                void AddIfPosition(string propertyName)
                {
                    if (propertyName.ToLowerInvariant().Contains("position")) currentProperties.Add(propertyName);
                    pendingProperty = null;
                }

                void FlushCurrentCommand()
                {
                    if (currentCommand != null && currentProperties.Count > 0)
                        results.Add((currentCommand, new List<string>(currentProperties)));
                    currentProperties = new List<string>();
                    pendingProperty = null;
                }
            }

            string ReadCommandSource(string commandId)
            {
                var fileName = $"{ToPascalCase(commandId)}Command.cs";
                var path = Path.Combine(Application.dataPath, "Scripts/Client.Skit/Commands", fileName);
                Assert.IsTrue(File.Exists(path), $"位置コマンド {commandId} の実装 {fileName} が見つからない");
                return File.ReadAllText(path);
            }

            string ToPascalCase(string name)
            {
                return char.ToUpperInvariant(name[0]) + name.Substring(1);
            }

            #endregion
        }
    }
}
