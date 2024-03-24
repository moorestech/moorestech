float2 rand(float2 st, int seed)
{
    float2 s = float2(dot(st, float2(127.1, 311.7)) + seed, dot(st, float2(269.5, 183.3)) + seed);
    return -1 + 2 * frac(sin(s) * 43758.5453123);
}

void noise_float(float2 st, int seed, float flowSpeed, float3 flowDirection, out float noiseValue)
{
    st += _Time[1] * flowSpeed * flowDirection;

    float2 p = floor(st);
    float2 f = frac(st);

    float w00 = dot(rand(p, seed), f);
    float w10 = dot(rand(p + float2(1, 0), seed), f - float2(1, 0));
    float w01 = dot(rand(p + float2(0, 1), seed), f - float2(0, 1));
    float w11 = dot(rand(p + float2(1, 1), seed), f - float2(1, 1));

    float2 u = f * f * (3 - 2 * f);

    noiseValue = lerp(lerp(w00, w10, u.x), lerp(w01, w11, u.x), u.y);
}

void anistropy_float(float3 v, float strength, out float anistropy)
{
    anistropy = saturate(1 / ddy((length(v.xz))) / strength);
}