using Game.Block.Blocks.Gear;

namespace Tests.Util
{
    public static class SimpleGearGeneratorExtension
    {
        public static void SetGenerateRpm(this SimpleGearGeneratorComponent simpleGearGeneratorComponent, float rpm)
        {
            simpleGearGeneratorComponent.SetGenerateRpmForDebug(rpm);
        }
        
        public static void SetGenerateTorque(this SimpleGearGeneratorComponent component, float torque)
        {
            component.SetGenerateTorqueForDebug(torque);
        }
    }
}
