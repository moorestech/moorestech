namespace Game.Block.Blocks.CleanRoom.Machine.Yield
{
    /// <summary>
    ///     清浄室の抽選に使う決定的な乱数列（SplitMix64）。同じシードからは同じ列が出る
    ///     Deterministic random sequence (SplitMix64) for clean-room draws; the same seed yields the same sequence
    /// </summary>
    public class CleanRoomDrawRandom
    {
        private ulong _state;

        public CleanRoomDrawRandom(long seed)
        {
            _state = unchecked((ulong)seed);
        }

        // [0,1) の一様乱数を1つ進めて返す
        // Advance the sequence and return a uniform double in [0, 1)
        public double NextDouble()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                var z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (z >> 11) * (1.0 / (1UL << 53));
            }
        }
    }
}
