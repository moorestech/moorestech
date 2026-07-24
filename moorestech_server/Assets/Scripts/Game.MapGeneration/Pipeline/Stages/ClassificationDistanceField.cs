using System.Threading.Tasks;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 分類ステージのビーチ遷移で使う 2乗ピクセル距離場（Felzenszwalb-Huttenlocher EDT）。
    // Squared-pixel distance field (Felzenszwalb-Huttenlocher EDT) used by the classification beach transition.
    internal static class ClassificationDistanceField
    {
        // バイナリマスクからの2乗ピクセル距離場を計算する。
        // findSeed=true: マスク値>0.5 をシード、false: マスク値<=0.5 をシード。
        // Compute the squared-pixel distance field from a binary mask.
        public static float[] ComputeSq(float[] mask, int res, bool findSeed)
        {
            const float INF = 1e10f;
            int n = res * res;
            var field = new float[n];
            for (int i = 0; i < n; i++)
                field[i] = ((mask[i] > 0.5f) == findSeed) ? 0f : INF;

            // Pass 1: 行方向 EDT
            // Pass 1: row-direction EDT
            var temp = new float[n];
            Parallel.For(0, res, y =>
            {
                var v = new int[res];
                var z = new float[res + 1];
                Edt1D(field, y * res, res, temp, y * res, v, z);
            });

            // Pass 2: 列方向 EDT
            // Pass 2: column-direction EDT
            var result = new float[n];
            Parallel.For(0, res, x =>
            {
                var colIn = new float[res];
                var colOut = new float[res];
                var v = new int[res];
                var z = new float[res + 1];
                for (int y = 0; y < res; y++)
                    colIn[y] = temp[y * res + x];
                Edt1D(colIn, 0, res, colOut, 0, v, z);
                for (int y = 0; y < res; y++)
                    result[y * res + x] = colOut[y];
            });

            return result;
        }

        // Felzenszwalb-Huttenlocher の1次元距離変換（ピクセル空間、スケール=1）。
        // One-dimensional distance transform (pixel space, scale = 1).
        static void Edt1D(float[] f, int offset, int n, float[] d, int dstOffset, int[] v, float[] z)
        {
            v[0] = 0;
            z[0] = -1e10f;
            z[1] = 1e10f;
            int k = 0;

            for (int q = 1; q < n; q++)
            {
                float fq = f[offset + q];
                float s;
                while (true)
                {
                    int vk = v[k];
                    float fvk = f[offset + vk];
                    s = ((fq + (float)q * q) - (fvk + (float)vk * vk)) / (2f * (q - vk));
                    if (k == 0 || s > z[k]) break;
                    k--;
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = 1e10f;
            }

            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                int vk = v[k];
                float diff = q - vk;
                d[dstOffset + q] = diff * diff + f[offset + vk];
            }
        }
    }
}
