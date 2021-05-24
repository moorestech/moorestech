using System.Collections.Generic;
using industrialization.GameSystem;

namespace industrialization.Electric
{
    public class ElectricSegment : IUpdate
    {
        private readonly List<IInstallationElectric> _electrics;

        public ElectricSegment()
        {
            GameUpdate.AddUpdateObject(this);
            _electrics = new List<IInstallationElectric>();
        }

        public void AddInstallationElectric(IInstallationElectric installationElectric)
        {
            _electrics.Add(installationElectric);
        }

        public void RemoveInstallationElectric(IInstallationElectric installationElectric)
        {
            _electrics.Remove(installationElectric);
        }

        public void Update()
        {
            throw new System.NotImplementedException();
        }
    }
}