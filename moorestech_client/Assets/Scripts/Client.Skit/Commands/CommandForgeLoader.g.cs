#if ENABLE_COMMAND_FORGE_GENERATOR
using System.Collections.Generic;

namespace CommandForgeGenerator.Command
{
    public class CommandForgeLoader
    {
        public static List<ICommandForgeCommand> LoadCommands(global::Newtonsoft.Json.Linq.JToken json)
        {
            var results = new List<ICommandForgeCommand>();
            var commandsJson = json["commands"];
            foreach (var commandJson in commandsJson)
            {
                var id = (int)commandJson["id"];
                var type = (string)commandJson["type"];
                
                var command = CreateCommand(id, type, commandJson);
                results.Add(command);
            }
            return results;
        }
        
        public static ICommandForgeCommand CreateCommand(int id, string type, global::Newtonsoft.Json.Linq.JToken commandJson)
        {
            return type switch
            {
                
                TransitionCommand.Type => TransitionCommand.Create(id, commandJson),
                CharacterTransformCommand.Type => CharacterTransformCommand.Create(id, commandJson),
                CameraworkCommand.Type => CameraworkCommand.Create(id, commandJson),
                WaitCommand.Type => WaitCommand.Create(id, commandJson),
                CameraWarpCommand.Type => CameraWarpCommand.Create(id, commandJson),
                TextCommand.Type => TextCommand.Create(id, commandJson),
                EmoteCommand.Type => EmoteCommand.Create(id, commandJson),
                SelectionCommand.Type => SelectionCommand.Create(id, commandJson),
                JumpCommand.Type => JumpCommand.Create(id, commandJson),
                MotionCommand.Type => MotionCommand.Create(id, commandJson),
                Group_startCommand.Type => Group_startCommand.Create(id, commandJson),
                Group_endCommand.Type => Group_endCommand.Create(id, commandJson),
                
                
                _ => throw new System.Exception($"Unknown command type: {type}")
            };
        }
    }
}
#endif