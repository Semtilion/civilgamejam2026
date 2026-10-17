// The sprite's shape in one plain color (_Color; alpha = the sprite's alpha x _Color's), unlit: the enemies' hit flash
// (HitFlash draws one over a sprite while it flashes: 80% white) and the variants' colored outlines (SpriteOutline
// makes a copy of the material per color). Flipping is done by the object's scale.
Shader "Jam/Sprite Silhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 0.8)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _Color;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                return half4(_Color.rgb, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
