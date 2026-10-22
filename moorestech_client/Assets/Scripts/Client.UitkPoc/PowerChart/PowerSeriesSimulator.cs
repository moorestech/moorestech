using UnityEngine;

namespace Client.UitkPoc.PowerChart
{
    // 電力系列の擬似データ。系列ごとのリングバッファへ、揺らぎと突発スパイクを足した値を積む
    // Synthetic power series: ring buffers fed with jittered values plus sudden spikes
    public class PowerSeriesSimulator
    {
        public readonly int SeriesCount;
        public readonly int PointCount;
        private readonly float[,] _values;
        private readonly float[] _baseValues;
        private readonly float[] _spikes;
        private int _head;

        public PowerSeriesSimulator(int seriesCount, int pointCount, float[] baseValues)
        {
            SeriesCount = seriesCount;
            PointCount = pointCount;
            _values = new float[seriesCount, pointCount];
            _baseValues = baseValues;
            _spikes = new float[seriesCount];
            for (var i = 0; i < pointCount; i++) Step(1f);
        }

        // 1サンプル進める。volatilityは時間幅が長いほど均されることを表す
        // Advance one sample; volatility models how wider buckets average out
        public void Step(float volatility)
        {
            for (var s = 0; s < SeriesCount; s++)
            {
                if (Random.value < 0.004f * volatility) _spikes[s] = _baseValues[s] * Random.Range(2f, 6f);
                _spikes[s] *= 0.93f;
                var noise = (Mathf.PerlinNoise(s * 13.1f, Time.realtimeSinceStartup * 0.7f + _head * 0.05f) - 0.5f) * 0.6f * volatility;
                _values[s, _head] = Mathf.Max(0f, _baseValues[s] * (1f + noise) + _spikes[s]);
            }
            _head = (_head + 1) % PointCount;
        }

        // index=0が最古、PointCount-1が最新
        // index 0 is oldest, PointCount-1 is newest
        public float Get(int series, int index)
        {
            return _values[series, (_head + index) % PointCount];
        }

        public float Latest(int series)
        {
            return Get(series, PointCount - 1);
        }

        public float MaxVisible()
        {
            var max = 1f;
            for (var s = 0; s < SeriesCount; s++)
            {
                for (var i = 0; i < PointCount; i++) max = Mathf.Max(max, _values[s, i]);
            }
            return max;
        }
    }
}
