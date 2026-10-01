// Full-screen darkness layer. Drawn on a camera-attached quad with multiply blending:
// final = scene * lerp(darkness, 1, visibility), where visibility = max(current vision mask, remembered area).
Shader "ProjectMayham/Vision/Composite"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "VisionComposite"
            Blend DstColor Zero
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_VisionMask);   SAMPLER(sampler_VisionMask);
            TEXTURE2D(_VisionMemory); SAMPLER(sampler_VisionMemory);

            CBUFFER_START(UnityPerMaterial)
                float4 _CamRect;      // xy = camera centre (world), zw = visible world size
                float4 _MemoryRect;   // xy = memory min corner (world), zw = memory world size
                half4 _DarkColor;
                half _MemoryBrightness;
                half _MemoryWrap;     // 1 = the memory texture repeats (wrapped level)
            CBUFFER_END

            struct appdata { float3 pos : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.pos);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half current = SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, i.uv).r;

                float2 world = _CamRect.xy + (i.uv - 0.5) * _CamRect.zw;
                float2 memUV = (world - _MemoryRect.xy) / _MemoryRect.zw;
                float inside = max(_MemoryWrap, step(0.0, memUV.x) * step(memUV.x, 1.0) * step(0.0, memUV.y) * step(memUV.y, 1.0));
                half memory = SAMPLE_TEXTURE2D(_VisionMemory, sampler_VisionMemory, memUV).r * _MemoryBrightness * inside;

                half lit = saturate(max(current, memory));
                half3 color = lerp(_DarkColor.rgb, half3(1, 1, 1), lit);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
