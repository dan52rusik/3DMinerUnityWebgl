// ============================================================
//  VoxelVertexColor.shader
//  WebGL / Яндекс Игры — совместимый шейдер.
//
//  Поддерживает: URP (Unity 6) + Built-in RP.
//  Unity автоматически выбирает нужный SubShader.
//  HLSL → кросс-компилируется Unity в GLSL ES для WebGL.
//
//  Vertex Colors: каждый тип блока = свой цвет в вершинах.
//  Освещение: простой Lambert (одна операция dot) — быстро на мобильных.
//  Тени: только отбрасывание (Shadow Caster), без дорогих shadow maps на приём.
// ============================================================

Shader "SimpleVoxelSystem/VoxelVertexColor"
{
    Properties
    {
        _Ambient    ("Ambient Light", Range(0, 1))    = 0.30
        _ColorBoost ("Color Boost",   Range(0.5, 2))  = 1.00
    }

    // ──────────────────────────────────────────────────────────────────────
    // SubShader A: Universal Render Pipeline (URP) — Unity 2020+ / Unity 6
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

            // WebGL не поддерживает instancing, но multi_compile_instancing
            // безопасен для включения — просто не используется.
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Ambient;
                float _ColorBoost;
            CBUFFER_END

            // Входные данные от CPU (из нашего меша)
            struct VoxelAttributes
            {
                float4 posOS   : POSITION;
                float3 normOS  : NORMAL;
                float4 vcolor  : COLOR;      // ← наш vertex color (тип блока)
            };

            // Данные между vertex и fragment stage
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
                // Получаем главный направленный свет (Direction Light)
                Light mainLight = GetMainLight();

                // Lambert: угол между нормалью и направлением света
                float  NdotL = saturate(dot(normalize(IN.normWS), mainLight.direction));

                // Итоговая яркость = ambient + diffuse
                float  lighting = _Ambient + (1.0 - _Ambient) * NdotL;

                // Финальный цвет = vertex color × яркость × цвет света
                half3  col = saturate(IN.vcolor.rgb * _ColorBoost) * lighting * mainLight.color.rgb;

                // Добавляем туман (если включён в настройках сцены)
                col = MixFog(col, IN.fog);

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster Pass (без конфликта имён) ───────────────────────
        // Нужен чтобы объект ОТБРАСЫВАЛ тень. Написан вручную — без Include
        // ShadowCasterPass.hlsl, чтобы избежать конфликта struct Attributes.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0         // не пишем цвет — только глубину

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Core.hlsl даёт TransformObjectToWorld, ApplyShadowBias, TransformWorldToHClip.
            // ShadowCasterPass.hlsl не включаем — он конфликтует с ручными структурами ниже.

            // Входная структура для shadow (минимальная)
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
                // Смещение по нормали — устраняет shadow acne
                posWS = ApplyShadowBias(posWS, normWS, GetMainLight().direction);
                OUT.posCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 ShadowFrag(ShadowOut IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // SubShader B: Built-in Render Pipeline
    // (используется, если URP не установлен)
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

    // Если оба SubShader не подошли — минимальный Diffuse (всегда работает)
    Fallback "Diffuse"
}
