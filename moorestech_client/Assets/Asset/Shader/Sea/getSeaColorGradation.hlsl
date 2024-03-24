void getSeaColorGradation_float(float gradationValue, out float3 Color)
{
    float4 PHASES = float4(0.29, 0.47, 0.00, 0.0);
    float4 AMPLITUDES = float4(3.33, 0.28, 0.37, 0.0);
    float4 FREQUENCIES = float4(0.00, 0.61, 0.30, 0.0);
    float4 OFFSETS = float4(0.00, 0.00, 0.13, 0.0);

    float TAU = 2.0 * 3.14159265;

    PHASES *= TAU;
    gradationValue *= TAU;


    Color = float3(
        OFFSETS.x + AMPLITUDES.x * 0.5 * cos(gradationValue * FREQUENCIES.x + PHASES.x) + 0.5,
        OFFSETS.y + AMPLITUDES.y * 0.5 * cos(gradationValue * FREQUENCIES.y + PHASES.y) + 0.5,
        OFFSETS.z + AMPLITUDES.z * 0.5 * cos(gradationValue * FREQUENCIES.z + PHASES.z) + 0.5
    );
}