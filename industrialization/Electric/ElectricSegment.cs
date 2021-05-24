using System.Collections.Generic;
using industrialization.GameSystem;

namespace industrialization.Electric
{
    //１つの電力のセグメントの処理
    public class ElectricSegment : IUpdate
    {
        private readonly List<IInstallationElectric> _electrics;
        private readonly List<IGenerator> _generators;

        public ElectricSegment()
        {
            GameUpdate.AddUpdateObject(this);
            _generators = new List<IGenerator>();
            _electrics = new List<IInstallationElectric>();
        }
        
        
        public void Update()
        {
            //合計電力量の算出
            var powersum = 0;
            foreach (var generator in _generators)
            {
                powersum += generator.OutputPower();
            }
            
            //合計電力需要量の算出
            var requestpower = 0;
            foreach (var electric in _electrics)
            {
                requestpower += electric.RequestPower();
            }
            
            //電力供給の割合の算出
            var powerRate = (double)powersum / (double)requestpower;
            if (1 < powerRate) powerRate = 1;
            
            //電力を供給
            foreach (var electric in _electrics)
            {
                electric.SupplyPower(electric.RequestPower()*powerRate);
            }
        }

        public void AddInstallationElectric(IInstallationElectric installationElectric)
        {
            _electrics.Add(installationElectric);
        }

        public void RemoveInstallationElectric(IInstallationElectric installationElectric)
        {
            _electrics.Remove(installationElectric);
        }

        public void AddGenerator(IGenerator generator)
        {
            _generators.Add(generator);
        }

        public void RemoveGenerator(IGenerator generator)
        {
            _generators.Remove(generator);
        }

    }
}