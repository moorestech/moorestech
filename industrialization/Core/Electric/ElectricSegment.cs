using System.Collections.Generic;
using industrialization.Core.GameSystem;

namespace industrialization.Core.Electric
{
    //１つの電力のセグメントの処理
    public class ElectricSegment : IUpdate
    {
        private readonly Dictionary<int,IInstallationElectric> _electrics;
        private readonly Dictionary<int,IPowerGenerator> _generators;

        public ElectricSegment()
        {
            GameUpdate.AddUpdateObject(this);
            _generators = new Dictionary<int,IPowerGenerator>();
            _electrics = new Dictionary<int,IInstallationElectric>();
        }
        
        
        public void Update()
        {
            //合計電力量の算出
            var powers = 0;
            foreach (var key in _generators.Keys)
            {
                powers += _generators[key].OutputPower();
            }
            
            //合計電力需要量の算出
            var requester = 0;
            foreach (var key in _electrics.Keys)
            {
                requester += _electrics[key].RequestPower();
            }
            
            //電力供給の割合の算出
            var powerRate = (double)powers / (double)requester;
            if (1 < powerRate) powerRate = 1;
            
            //電力を供給
            foreach (var key in _electrics.Keys)
            {
                _electrics[key].SupplyPower((int)(_electrics[key].RequestPower()*powerRate));
            }
        }

        public void AddInstallationElectric(IInstallationElectric installationElectric)
        {
            if (!_electrics.ContainsKey(installationElectric.GetIntId()))
            {
                _electrics.Add(installationElectric.GetIntId(),installationElectric);
            }
        }

        public void RemoveInstallationElectric(IInstallationElectric installationElectric)
        {
            if (_electrics.ContainsKey(installationElectric.GetIntId()))
            {
                _electrics.Remove(installationElectric.GetIntId());
            }
        }

        public void AddGenerator(IPowerGenerator powerGenerator)
        {
            if (!_generators.ContainsKey(powerGenerator.GetIntId()))
            {
                _generators.Add(powerGenerator.GetIntId(),powerGenerator);
            }
        }

        public void RemoveGenerator(IPowerGenerator powerGenerator)
        {
            if (_generators.ContainsKey(powerGenerator.GetIntId()))
            {
                _generators.Remove(powerGenerator.GetIntId());
            }
        }

    }
}