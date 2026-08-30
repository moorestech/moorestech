using System.Collections.Generic;
using System.IO;
using CommandForgeGeneratorUtil;
using Newtonsoft.Json.Linq;

namespace Client.Tests.Localization.Skit.Schema
{
    internal static class CommandForgeLocalizationSchema
    {
        public static (HashSet<string> Required, HashSet<string> Allowed) Load(string schemaPath)
        {
            var root = JObject.Parse(Yaml.ToJson(File.ReadAllText(schemaPath)));
            var required = new HashSet<string>();
            var allowed = new HashSet<string>();

            // マスタ値は共有翻訳キーを必須とする
            // Require shared translation keys for every master value
            foreach (var masterProperty in ((JObject)root["master"]).Properties())
            foreach (var value in (JArray)masterProperty.Value)
                AddRequired($"master.{masterProperty.Name}.{value}");

            // schemaから参照キーを導出する
            // Derive all addressable command and property keys from the schema
            foreach (var command in (JArray)root["commands"])
                AddCommand((JObject)command);

            // Editor組み込みコマンドも必須化する
            // Require commands built into the editor too
            AddReservedCommand("group_start", new[] { "groupName", "isCollapsed" });
            AddReservedCommand("group_end", new string[0]);
            return (required, allowed);

            #region Internal

            void AddCommand(JObject command)
            {
                var commandPrefix = $"command.{(string)command["id"]}";
                AddRequired(commandPrefix + ".name");
                AddAllowed(commandPrefix + ".description");

                // 表示property名を必須にする
                // Require property names because the editor always renders them
                foreach (var property in ((JObject)command["properties"]).Properties())
                    AddProperty(commandPrefix, property);
            }

            void AddProperty(string commandPrefix, JProperty property)
            {
                var propertyPrefix = $"{commandPrefix}.property.{property.Name}";
                AddRequired(propertyPrefix + ".name");
                AddAllowed(propertyPrefix + ".description");
                AddAllowed(propertyPrefix + ".placeholder");

                // 直書きenumだけを固有キー化する
                // Use property-specific keys only for inline enum options
                var options = ((JObject)property.Value)["options"];
                if (options is not JArray enumValues) return;
                foreach (var value in enumValues)
                    AddRequired($"{propertyPrefix}.enum.{value}");
            }

            void AddReservedCommand(string commandId, IEnumerable<string> properties)
            {
                var commandPrefix = $"command.{commandId}";
                AddRequired(commandPrefix + ".name");
                AddAllowed(commandPrefix + ".description");

                foreach (var property in properties)
                {
                    var propertyPrefix = $"{commandPrefix}.property.{property}";
                    AddRequired(propertyPrefix + ".name");
                    AddAllowed(propertyPrefix + ".description");
                    AddAllowed(propertyPrefix + ".placeholder");
                }
            }

            void AddRequired(string key)
            {
                required.Add(key);
                allowed.Add(key);
            }

            void AddAllowed(string key)
            {
                allowed.Add(key);
            }

            #endregion
        }
    }
}
