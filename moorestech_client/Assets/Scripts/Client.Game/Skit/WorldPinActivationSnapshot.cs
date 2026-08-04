using Client.Game.InGame.Tutorial;

namespace Client.Game.Skit
{
    internal sealed class WorldPinActivationSnapshot
    {
        private readonly IMapObjectPin _mapObjectPin;
        private readonly IVeinPin _veinPin;
        private readonly bool _mapObjectPinWasSuppressed;
        private readonly bool _veinPinWasSuppressed;

        internal WorldPinActivationSnapshot(IMapObjectPin mapObjectPin, IVeinPin veinPin)
        {
            _mapObjectPin = mapObjectPin;
            _veinPin = veinPin;
            _mapObjectPinWasSuppressed = mapObjectPin.IsSkitSuppressed();
            _veinPinWasSuppressed = veinPin.IsSkitSuppressed();
        }

        internal void Hide()
        {
            _mapObjectPin.SetSkitSuppressed(true);
            _veinPin.SetSkitSuppressed(true);
        }

        internal void Restore()
        {
            _mapObjectPin.SetSkitSuppressed(_mapObjectPinWasSuppressed);
            _veinPin.SetSkitSuppressed(_veinPinWasSuppressed);
        }
    }
}
