using System.Collections.Generic;
using System.Linq;
using Core.Update;

namespace Core.Electric
{
    //１つの電力のセグメントの処理
    public class ElectricSegment : IUpdate
    {
        private readonly Dictionary<int,IBlockElectric> _electrics = new();
        private readonly Dictionary<int,IPowerGenerator> _generators = new();
        private readonly Dictionary<int,IElectricPole> _electricPoles = new();

        public ElectricSegment()
        {
            GameUpdate.AddUpdateObject(this);
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
                requester += _electrics[key].GetRequestPower();
            }
            
            //電力供給の割合の算出
            var powerRate = (double)powers / (double)requester;
            if (1 < powerRate) powerRate = 1;
            
            //電力を供給
            foreach (var key in _electrics.Keys)
            {
                _electrics[key].SupplyPower((int)(_electrics[key].GetRequestPower()*powerRate));
            }
        }
        public void AddBlockElectric(IBlockElectric blockElectric)
        {
            if (!_electrics.ContainsKey(blockElectric.GetIntId()))
            {
                _electrics.Add(blockElectric.GetIntId(),blockElectric);
            }
        }
        public void RemoveBlockElectric(IBlockElectric blockElectric)
        {
            if (_electrics.ContainsKey(blockElectric.GetIntId()))
            {
                _electrics.Remove(blockElectric.GetIntId());
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
        public void AddElectricPole(IElectricPole electricPole)
        {
            if (!_electricPoles.ContainsKey(electricPole.GetIntId()))
            {
                _electricPoles.Add(electricPole.GetIntId(),electricPole);
            }
        }
        public void RemoveElectricPole(IElectricPole electricPole)
        {
            if (_electricPoles.ContainsKey(electricPole.GetIntId()))
            {
                _electricPoles.Remove(electricPole.GetIntId());
            }
        }
        
        public Dictionary<int,IBlockElectric> GetElectrics() { return _electrics.Keys.ToDictionary(key => key, key => _electrics[key]); }
        public Dictionary<int,IPowerGenerator> GetGenerators() { return _generators.Keys.ToDictionary(key => key, key => _generators[key]); }
        public Dictionary<int,IElectricPole> GetElectricPoles() { return _electricPoles.Keys.ToDictionary(key => key, key => _electricPoles[key]); }

        public bool ExistElectricPole(int id)
        {
            return _electricPoles.ContainsKey(id);
        }
    }
}