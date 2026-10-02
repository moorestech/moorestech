using System.Collections.Generic;
using Core.BeltTransport;

namespace Tests.UnitTest.Core.BeltTransport.Connection
{
    // 搬出可否を外から決められる搬入元。問い合わせの方向を全て記録する
    // A source whose output availability is set from outside. Records every queried direction
    public sealed class FakeBeltSource : IBeltSource
    {
        public readonly List<BeltDirection> Queries = new();
        private bool _hasOutput;

        public FakeBeltSource(bool hasOutput)
        {
            _hasOutput = hasOutput;
        }

        public void SetHasOutput(bool hasOutput)
        {
            _hasOutput = hasOutput;
        }

        public bool TryGetOutput(BeltDirection inputDirection)
        {
            Queries.Add(inputDirection);
            return _hasOutput;
        }
    }
}
