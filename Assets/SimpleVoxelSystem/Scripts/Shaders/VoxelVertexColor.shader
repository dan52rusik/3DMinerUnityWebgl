// ============================================================
//  VoxelVertexColor.shader
//  WebGL / Yandex Games compatible shader.
//
//  Supports: URP (Unity 2020+) + Built-in RP.
//  Unity automatically picks the appropriate SubShader.
//  HLSL is cross-compiled by Unity into GLSL ES for WebGL.
//
//  Vertex Colors: each block type has its own color in vertices.
//  Lighting: simple Lambert (single dot operation) — fast on mobile.
//  Shadows: Shadow Caster only (no expensive shadow maps reception).
// ============================================================

Shader "SimpleVoxelSystem/VoxelVertexColor"
{
    Properties
    {
        _Ambient    ("Ambient Light", Range(0, 1))    = 0.30
        _ColorBoost ("Color Boost",   Range(0.5, 2))  = 1.00
    }

    // ──────────────────────────────────────────────────────────────────────
    // SubShader A: Universal Render Pipeline (URP) — Unity 2020+
    // ──────────────────────────────────────────────────────────────────────
    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ── Forward Lit Pass ──────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   VoxelVert
            #pragma fragment VoxelFrag

            // WebGL doesn't support instancing, but multi_compile_instancing
            // is safe to include — it just won't be used.
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Ambient;
                float _ColorBoost;
            CBUFFER_END

            // Input data from CPU (mesh attributes)
            struct VoxelAttributes
            {
                float4 posOS   : POSITION;
                float3 normOS  : NORMAL;
                float4 vcolor  : COLOR;      // ← vertex color (block type)
            };

            // Interpolated data between vertex and fragment stage
            struct VoxelVaryings
            {
                float4 posCS   : SV_POSITION;
                float3 normWS  : TEXCOORD0;
                float4 vcolor  : COLOR;
                float  fog     : TEXCOORD1;
            };

            VoxelVaryings VoxelVert(VoxelAttributes IN)
            {
                VoxelVaryings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.posOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normOS);

                OUT.posCS  = posInputs.positionCS;
                OUT.normWS = nrmInputs.normalWS;
                OUT.vcolor = IN.vcolor;
                OUT.fog    = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 VoxelFrag(VoxelVaryings IN) : SV_Target
            {
                // Get main directional light
                Light mainLight = GetMainLight();

                // Lambert: angle between normal and light direction
                float  NdotL = saturate(dot(normalize(IN.normWS), mainLight.direction));

                // Final brightness = ambient + diffuse
                float  lighting = _Ambient + (1.0 - _Ambient) * NdotL;

                // Final color = vertex color x brightness x light color
                half3  col = saturate(IN.vcolor.rgb * _ColorBoost) * lighting * mainLight.color.rgb;

                // Apply fog (if enabled in scene settings)
                col = MixFog(col, IN.fog);

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster Pass ──────────────────────────────────────────────
        // Allows the object to CAST shadows. Manual implementation to avoid
        // naming conflicts with Attribute structures in ShadowCasterPass.hlsl.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0         // don't write color — only depth

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            float3 _LightDirection;

            // Minimal input structure for shadows
            struct ShadowIn
            {
                float4 posOS   : POSITION;
                float3 normOS  : NORMAL;
                float4 texcoord: TEXCOORD0;
            };

            struct ShadowOut
            {
                float4 posCS : SV_POSITION;
            };

            ShadowOut ShadowVert(ShadowIn IN)
            {
                ShadowOut OUT;
                float3 posWS = TransformObjectToWorld(IN.posOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normOS);
                // Manual normal bias for cross-platform portability.
                posWS += normWS * 0.005;
                OUT.posCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 ShadowFrag(ShadowOut IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // SubShader B: Built-in Render Pipeline
    // (fallback for projects without URP installed)
    // ──────────────────────────────────────────────────────────────────────
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex   VoxelBIVert
            #pragma fragment VoxelBIFrag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            float _Ambient;
            float _ColorBoost;

            struct BIAppdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color  : COLOR;
            };

            struct BIV2f
            {
                float4 pos    : SV_POSITION;
                float3 normWS : TEXCOORD0;
                float4 color  : COLOR;
                UNITY_FOG_COORDS(1)
            };

            BIV2f VoxelBIVert(BIAppdata v)
            {
                BIV2f o;
                o.pos    = UnityObjectToClipPos(v.vertex);
                o.normWS = UnityObjectToWorldNormal(v.normal);
                o.color  = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 VoxelBIFrag(BIV2f i) : SV_Target
            {
                float NdotL  = saturate(dot(normalize(i.normWS), _WorldSpaceLightPos0.xyz));
                float light  = _Ambient + (1.0 - _Ambient) * NdotL;
                fixed3 col   = saturate(i.color.rgb * _ColorBoost) * light * _LightColor0.rgb;
                fixed4 final = fixed4(col, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, final);
                return final;
            }
            ENDCG
        }
    }

    // Minimal Diffuse fallback if both SubShaders fail.
    Fallback "Diffuse"
}
