using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Update;

namespace Core.Electric
{
    //１つの電力のセグメントの処理
    public class ElectricSegment : IUpdate
    {
        private readonly Dictionary<int, IBlockElectric> _electrics = new();
        private readonly Dictionary<int, IPowerGenerator> _generators = new();
        private readonly Dictionary<int, IElectricPole> _electricPoles = new();

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
            var powerRate = (double) powers / (double) requester;
            if (1 < powerRate) powerRate = 1;

            //電力を供給
            foreach (var key in _electrics.Keys)
            {
                _electrics[key].SupplyPower((int) (_electrics[key].GetRequestPower() * powerRate));
            }
        }

        public void AddBlockElectric(IBlockElectric blockElectric)
        {
            if (_electrics.ContainsKey(blockElectric.EntityId)) return;
            _electrics.Add(blockElectric.EntityId, blockElectric);
        }

        public void RemoveBlockElectric(IBlockElectric blockElectric)
        {
            if (!_electrics.ContainsKey(blockElectric.EntityId)) return;
            _electrics.Remove(blockElectric.EntityId);
        }

        public void AddGenerator(IPowerGenerator powerGenerator)
        {
            if (_generators.ContainsKey(powerGenerator.EntityId)) return;
            _generators.Add(powerGenerator.EntityId, powerGenerator);
        }

        public void RemoveGenerator(IPowerGenerator powerGenerator)
        {
            if (!_generators.ContainsKey(powerGenerator.EntityId)) return;
            _generators.Remove(powerGenerator.EntityId);
        }

        public void AddElectricPole(IElectricPole electricPole)
        {
            if (_electricPoles.ContainsKey(electricPole.EntityId)) return;
            _electricPoles.Add(electricPole.EntityId, electricPole);
        }

        public void RemoveElectricPole(IElectricPole electricPole)
        {
            if (!_electricPoles.ContainsKey(electricPole.EntityId)) return;
            _electricPoles.Remove(electricPole.EntityId);
        }

        public ReadOnlyDictionary<int, IBlockElectric> GetElectrics()
        {
            return new ReadOnlyDictionary<int, IBlockElectric>(_electrics);
        }

        public ReadOnlyDictionary<int, IPowerGenerator> GetGenerators()
        {
            return new ReadOnlyDictionary<int, IPowerGenerator>(_generators);
        }

        public ReadOnlyDictionary<int, IElectricPole> GetElectricPoles()
        {
            return new ReadOnlyDictionary<int, IElectricPole>(_electricPoles);
        }

        public bool ExistElectricPole(int id)
        {
            return _electricPoles.ContainsKey(id);
        }
    }
}