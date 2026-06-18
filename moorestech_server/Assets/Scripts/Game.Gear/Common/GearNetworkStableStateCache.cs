namespace Game.Gear.Common
{
    internal class GearNetworkStableStateCache
    {
        private bool _hasStableState;

        public bool CanSkipUpdate(bool topologyDirty, bool calculationDirty, bool generatorOutputDirty)
        {
            return _hasStableState && !topologyDirty && !calculationDirty && !generatorOutputDirty;
        }

        public void Store()
        {
            _hasStableState = true;
        }

        public void Invalidate()
        {
            _hasStableState = false;
        }
    }
}
