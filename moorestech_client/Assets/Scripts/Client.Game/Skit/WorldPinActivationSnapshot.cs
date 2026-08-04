using Client.Game.InGame.Tutorial;

namespace Client.Game.Skit
{
    internal sealed class WorldPinActivationSnapshot
    {
        private readonly IMapObjectPin _mapObjectPin;
        private readonly IVeinPin _veinPin;
        private readonly bool _mapObjectPinWasActive;
        private readonly bool _veinPinWasActive;

        internal WorldPinActivationSnapshot(IMapObjectPin mapObjectPin, IVeinPin veinPin)
        {
            _mapObjectPin = mapObjectPin;
            _veinPin = veinPin;
            _mapObjectPinWasActive = mapObjectPin.IsActiveSelf();
            _veinPinWasActive = veinPin.IsActiveSelf();
        }

        internal void Hide()
        {
            _mapObjectPin.SetActive(false);
            _veinPin.SetActive(false);
        }

        internal void Restore()
        {
            _mapObjectPin.SetActive(_mapObjectPinWasActive);
            _veinPin.SetActive(_veinPinWasActive);
        }
    }
}
