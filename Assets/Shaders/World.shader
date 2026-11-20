Shader "Kepler/Orbital body" {
Properties { _Tint("Palette", Color)=(1,1,1,1) _Kind("Type", Float)=0 _Seed("Seed", Float)=0 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
Pass {
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float3 _KeplerSolPosition;
CBUFFER_START(UnityPerMaterial)
float4 _Tint;
float _Kind, _Seed;
CBUFFER_END
struct Attr
{
    float4 p : POSITION;
    float3 n : NORMAL;
};
struct Var
{
    float4 p : SV_POSITION;
    float3 n : TEXCOORD0;
    float3 obj : TEXCOORD1;
    float3 eye : TEXCOORD2;
    float3 lightDir : TEXCOORD3;
};
Var vert(Attr v)
{
    Var o;
    float3 w = TransformObjectToWorld(v.p.xyz);
    o.p = TransformWorldToHClip(w);
    o.eye = GetCameraPositionWS() - w;
    o.lightDir = _KeplerSolPosition - w;
    o.n = TransformObjectToWorldNormal(v.n);
    o.obj = v.n;
    return o;
}
float hash3(float3 p)
{
    p = frac(p * .1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}
float noise3(float3 p)
{
    float3 i = floor(p), f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(lerp(hash3(i), hash3(i + float3(1, 0, 0)), f.x),
                     lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x),
                     lerp(hash3(i + float3(0, 1, 1)), hash3(i + 1), f.x), f.y),
                f.z);
}
float field(float3 p)
{
    float s = 0, a = .52;
    for (int i = 0; i < 5; i++)
    {
        s += a * noise3(p);
        p = p * 2.07 + 8.7;
        a *= .5;
    }
    return s;
}
half4 frag(Var v) : SV_Target
{
    float3 n = normalize(v.n), p = normalize(v.obj);
    float f = field(p * 5 + _Seed);
    float3 col = _Tint.rgb * (.6 + .7 * f);
    if (_Kind == 1)
        return half4(col * (1.8 + .7 * f), 1);
    if (_Kind == 2)
    {
        col = lerp(float3(.013, .10, .22),
                   lerp(float3(.06, .20, .12), float3(.46, .33, .17), smoothstep(.5, .66, f)),
                   smoothstep(.48, .505, f));
        col = lerp(col, float3(.88, .94, 1),
                   max(smoothstep(.85, .96, abs(p.y)),
                       smoothstep(.50, .64, field(p * float3(11, 22, 11) + 5 * f))));
    }
    if (_Kind == 3)
        col *= .9 + .22 * sin(p.y * 58 + 5 * f);
    float lit = dot(n, normalize(v.lightDir));
    float rim = pow(1 - saturate(dot(n, normalize(v.eye))), 4);
    return half4(col * (.045 + .96 * saturate(lit)) +
                     (_Kind == 2 ? float3(.13, .46, .82) * rim * saturate(lit + .35) : 0),
                 1);
}
ENDHLSL
}
}
}
