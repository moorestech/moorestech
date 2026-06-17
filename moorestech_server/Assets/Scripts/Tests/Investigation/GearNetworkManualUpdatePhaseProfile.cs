using System.Collections.Generic;
using System.Linq;

namespace Tests.Investigation
{
    public class GearNetworkManualUpdatePhaseProfile
    {
        public readonly Dictionary<string, double> PhaseMilliseconds = new();

        public int VisitedGearCount;
        public int RequiredTorqueCalls;
        public int GetGearConnectCalls;
        public int GearConnectEdges;
        public int EnergyBalanceTransformerIterations;
        public int EnergyBalanceGeneratorIterations;
        public int DistributeTransformerIterations;
        public int DistributeGeneratorIterations;
        public int SupplyTransformerCalls;
        public int SupplyGeneratorCalls;
        public int StopCalls;
        public bool IsStopped;

        public double TotalMilliseconds { get; private set; }

        public void AddPhase(string phase, double milliseconds)
        {
            if (!PhaseMilliseconds.TryAdd(phase, milliseconds))
            {
                PhaseMilliseconds[phase] += milliseconds;
            }
        }

        public void SetTotalMilliseconds(double milliseconds)
        {
            TotalMilliseconds = milliseconds;
        }

        public string ToCounterFields()
        {
            var phases = string.Join(",", PhaseMilliseconds.Select(pair => $"{pair.Key}:{pair.Value:F3}"));
            return $"totalMs={TotalMilliseconds:F3} visited={VisitedGearCount} requiredTorqueCalls={RequiredTorqueCalls} getConnectCalls={GetGearConnectCalls} connectEdges={GearConnectEdges} energyBalanceTransformerIterations={EnergyBalanceTransformerIterations} energyBalanceGeneratorIterations={EnergyBalanceGeneratorIterations} distributeTransformerIterations={DistributeTransformerIterations} distributeGeneratorIterations={DistributeGeneratorIterations} supplyTransformerCalls={SupplyTransformerCalls} supplyGeneratorCalls={SupplyGeneratorCalls} stopCalls={StopCalls} stopped={IsStopped} phases={phases}";
        }
    }
}
