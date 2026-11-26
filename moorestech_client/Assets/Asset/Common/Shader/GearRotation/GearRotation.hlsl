#ifndef MOORESTECH_GEAR_ROTATION_INCLUDED
#define MOORESTECH_GEAR_ROTATION_INCLUDED

// 歯車の回す部品を頂点シェーダーで回す共通処理
// 部品ごとの値は頂点のUV6/UV7、ブロックごとの回転状態は位置をキーにしたハッシュ表(GearRotationGpuTable)から読む
// UV6 = (部品の回転軸xyz, rotationSpeed×反転の符号), UV7 = (部品から見たブロック原点xyz, 向き固定なら1)
// 表の行 = key(ブロック原点×2を丸めた整数xyz, 使用中1/削除済み2/空0), state(符号付き角度, 絶対角度, 符号付き角速度deg/s, 基準時刻)
// Shared code that spins gear parts in the vertex shader
// Per-part values come from vertex UV6/UV7; per-block rotation state comes from a hash table keyed by position (GearRotationGpuTable)
// UV6 = (part rotation axis xyz, rotationSpeed times reverse sign), UV7 = (block origin seen from the part xyz, 1 when direction is fixed)
// Row = key (rounded block origin times two xyz, used 1 / removed 2 / empty 0), state (signed angle, absolute angle, signed deg/s, base time)

struct GearRotationRow
{
    int4 key;
    float4 state;
};

StructuredBuffer<GearRotationRow> _GearRotationTable;
// Shader.SetGlobalIntegerで渡すのでintで受ける
// Received as int because they are set with Shader.SetGlobalInteger
int _GearRotationTableMask;
int _GearRotationProbeLimit;
float _GearRotationTime;

// CPU側(GearRotationGpuHashTable.Hash)と同じ式でなければならない
// Must match the CPU side (GearRotationGpuHashTable.Hash)
uint GearRotationHash(int3 key)
{
    return ((uint)key.x * 73856093u) ^ ((uint)key.y * 19349663u) ^ ((uint)key.z * 83492791u);
}

bool GearRotationFindState(int3 key, out float4 state)
{
    state = 0;
    uint hash = GearRotationHash(key);
    [loop]
    for (uint i = 0; i < (uint)_GearRotationProbeLimit; i++)
    {
        GearRotationRow row = _GearRotationTable[(hash + i) & (uint)_GearRotationTableMask];
        if (row.key.w == 0) return false;
        if (row.key.w == 1 && all(row.key.xyz == key))
        {
            state = row.state;
            return true;
        }
    }
    return false;
}

// 軸をワールドへ向けたときの支配成分の符号。CPU側のGearWorldRotationSign.GetWorldAxisSignと同じ規約
// Sign of the dominant component of the axis in world space; same convention as GearWorldRotationSign.GetWorldAxisSign
float GearRotationWorldAxisSign(float3 axisOS)
{
    float3 axisWS = mul((float3x3)GetObjectToWorldMatrix(), axisOS);
    float3 absAxis = abs(axisWS);
    if (absAxis.x >= absAxis.y && absAxis.x >= absAxis.z) return axisWS.x >= 0 ? 1.0 : -1.0;
    if (absAxis.y >= absAxis.z) return axisWS.y >= 0 ? 1.0 : -1.0;
    return axisWS.z >= 0 ? 1.0 : -1.0;
}

// 部品の回転行列。UV6が無い頂点や表に行が無いブロックは回さない
// Rotation matrix of the part; vertices without UV6 and blocks without a row stay still
float3x3 GearRotationMatrix(float4 axisAndSpeed, float4 originAndMode)
{
    float3x3 identity = float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
    if (dot(axisAndSpeed.xyz, axisAndSpeed.xyz) < 0.5) return identity;

    float3 originWS = mul(GetObjectToWorldMatrix(), float4(originAndMode.xyz, 1.0)).xyz;
    float4 state;
    if (!GearRotationFindState((int3)round(originWS * 2.0), state)) return identity;

    // 向き固定の部品は絶対角度、それ以外は符号付き角度にワールド符号を掛ける
    // Direction-fixed parts use the absolute angle; others use the signed angle times the world sign
    float elapsed = _GearRotationTime - state.w;
    float angle = originAndMode.w > 0.5
        ? state.y + abs(state.z) * elapsed
        : (state.x + state.z * elapsed) * GearRotationWorldAxisSign(axisAndSpeed.xyz);
    float radian = radians(angle * axisAndSpeed.w);

    // ロドリゲスの回転公式。UnityのQuaternion.AngleAxisと同じ向きに回る
    // Rodrigues' rotation formula; turns the same way as Unity's Quaternion.AngleAxis
    float3 k = axisAndSpeed.xyz;
    float s = sin(radian);
    float c = cos(radian);
    float t = 1.0 - c;
    return float3x3(
        t * k.x * k.x + c,       t * k.x * k.y - s * k.z, t * k.x * k.z + s * k.y,
        t * k.x * k.y + s * k.z, t * k.y * k.y + c,       t * k.y * k.z - s * k.x,
        t * k.x * k.z - s * k.y, t * k.y * k.z + s * k.x, t * k.z * k.z + c);
}

#endif
