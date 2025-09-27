using System.Reflection;
using Game.Block.Blocks.Gear;
using Game.Gear.Common;

namespace Tests.Util
{
    public static class SimpleGearGeneratorExtension
    {
        public static void SetGenerateRpm(this SimpleGearGeneratorComponent simpleGearGeneratorComponent, float rpm)
        {
            var value = new RPM(rpm);
            
            var type = typeof(SimpleGearGeneratorComponent);
            var property = type.GetProperty("GenerateRpm", BindingFlags.Public | BindingFlags.Instance);
            var backingFieldName = $"<{property.Name}>k__BackingField";
            var backingField = type.GetField(backingFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            backingField.SetValue(simpleGearGeneratorComponent, value);
        }
        
        public static void SetGenerateTorque(this SimpleGearGeneratorComponent component, float torque)
        {
            var value = new Torque(torque);
            
            var type = typeof(SimpleGearGeneratorComponent);
            var property = type.GetProperty("GenerateTorque", BindingFlags.Public | BindingFlags.Instance);
            var backingFieldName = $"<{property.Name}>k__BackingField";
            var backingField = type.GetField(backingFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            backingField.SetValue(component, value);
        }
    }
}