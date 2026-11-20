Shader "Kepler/Star atlas" { Properties {_Seed("Seed",Float)=228} SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off Pass {
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float _Seed;
struct V
{
    float4 pos : SV_POSITION;
    float3 dir : TEXCOORD0;
};
V vert(float3 p : POSITION)
{
    V o;
    o.pos = TransformObjectToHClip(p);
    o.dir = p;
    return o;
}
float h(float2 p)
{
    return frac(sin(fmod(dot(p, float2(31.7, 73.3)) + _Seed, 625.0)) * 4375.2);
}
float stars(float2 uv, float scale)
{
    float2 p = uv * scale, i = floor(p), d = frac(p) - .2 - .6 * float2(h(i), h(i + 41));
    return step(.95, h(i + 13)) * exp(-dot(d, d) * 160) * (.4 + .6 * h(i + 21));
}
half4 frag(V v) : SV_Target
{
    float3 d = normalize(v.dir);
    float2 uv = float2(atan2(d.z, d.x), asin(clamp(d.y, -1, 1)));
    float band = exp(-abs(dot(d, normalize(float3(.3, .8, -.4)))) * 9);
    float s = stars(uv, 118) + stars(uv, 54);
    return half4(float3(.002, .004, .011) + band * float3(.008, .013, .026) + s * float3(.8, .89, 1), 1);
}
ENDHLSL
}
}
}
