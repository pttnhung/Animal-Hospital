Shader "VetNightmare/FocusOutline"
{
    Properties
    {
        _Test("Stencil compare", Float) = 8
        _Op("Stencil pass", Float) = 2
        _ColorMask("Color channels", Float) = 15
        _Pixels("Width", Float) = 3
        _Center("Mesh center", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -1,-1
            ColorMask [_ColorMask]
            Stencil { Ref 1 ReadMask 1 WriteMask 1 Comp [_Test] Pass [_Op] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Pixels;
            float4 _Center;
            float4 vert(float4 pos : POSITION) : SV_POSITION
            {
                float4 p = TransformObjectToHClip(pos.xyz);
                float4 center = TransformObjectToHClip(_Center.xyz);
                float2 direction = (p.xy / max(p.w, 0.0001) - center.xy / max(center.w, 0.0001)) * _ScreenParams.xy;
                p.xy += direction / max(length(direction), 0.0001) * (2 * _Pixels / _ScreenParams.xy) * p.w;
                return p;
            }
            half4 frag() : SV_Target { return half4(0.10, 1.0, 0.17, 1.0); }
            ENDHLSL
        }
    }
}
