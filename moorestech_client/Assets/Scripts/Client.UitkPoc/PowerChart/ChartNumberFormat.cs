using UnityEngine;

namespace Client.UitkPoc.PowerChart
{
    // SI接頭辞つきの短い数値表記と、目盛りに向く切りのよい上限
    // Short SI-prefixed numbers and a round axis ceiling
    public static class ChartNumberFormat
    {
        private static readonly string[] Prefixes = { "", "k", "M", "G", "T" };

        public static string Si(float value, string unit)
        {
            var index = 0;
            while (Mathf.Abs(value) >= 1000f && index < Prefixes.Length - 1)
            {
                value /= 1000f;
                index++;
            }
            var digits = value >= 100f ? "0" : value >= 10f ? "0.#" : "0.##";
            return value.ToString(digits) + " " + Prefixes[index] + unit;
        }

        public static float NiceCeiling(float value)
        {
            // 1,2,2.5,5,10 × 10^n のうち値以上で最小のもの
            // Smallest of 1, 2, 2.5, 5, 10 x 10^n that is >= value
            var magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(value)));
            foreach (var step in new[] { 1f, 2f, 2.5f, 5f, 10f })
            {
                if (step * magnitude >= value) return step * magnitude;
            }
            return 10f * magnitude;
        }
    }
}
