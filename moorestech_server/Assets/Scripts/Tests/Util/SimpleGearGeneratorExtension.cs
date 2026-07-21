using System.Reflection;
using Game.Block.Blocks.Gear;
using Game.Gear.Common;
using Game.Context;

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

            // 出力変化を本番と同じ経路で通知し、所属networkの再計算を要求する
            // Notify the output change through the production path so the owning network is recalculated
            ServerContext.GetService<IGearNetworkDatastore>().NotifyGeneratorOutputChanged(simpleGearGeneratorComponent);
        }

        public static void SetGenerateTorque(this SimpleGearGeneratorComponent component, float torque)
        {
            var value = new Torque(torque);

            var type = typeof(SimpleGearGeneratorComponent);
            var property = type.GetProperty("GenerateTorque", BindingFlags.Public | BindingFlags.Instance);
            var backingFieldName = $"<{property.Name}>k__BackingField";
            var backingField = type.GetField(backingFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            backingField.SetValue(component, value);

            // 出力変化を本番と同じ経路で通知し、所属networkの再計算を要求する
            // Notify the output change through the production path so the owning network is recalculated
            ServerContext.GetService<IGearNetworkDatastore>().NotifyGeneratorOutputChanged(component);
        }
    }
}
