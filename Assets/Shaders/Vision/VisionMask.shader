// Writes the visibility mask (R channel) from the vision mesh.
// Pass 0: draws the mesh (uv.x = angular weight, uv.y = distance / radius), combined with MAX blending.
// Pass 1: full-screen subtraction, used to make the persistent memory texture fade out.
Shader "ProjectMayham/Vision/Mask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Mask"
            Blend One One
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4x4 _VisionVP;
            float _VisionFalloffStart;

            struct appdata { float3 pos : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = mul(_VisionVP, float4(v.pos, 1.0));
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float v = i.uv.x * (1.0 - smoothstep(_VisionFalloffStart, 1.0, i.uv.y));
                return half4(v, v, v, v);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Fade"
            Blend One One
            BlendOp RevSub

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float _VisionFadeAmount;

            struct appdata { float3 pos : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = float4(v.pos.xy, 0.0, 1.0);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(_VisionFadeAmount, _VisionFadeAmount, _VisionFadeAmount, _VisionFadeAmount);
            }
            ENDHLSL
        }
    }
}
