Shader "MyShaders/2D/SpriteLitFlash"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}

        // --- オーバーレイプロパティ開始 ---
        [Header(Overlay Effect)]
        [Toggle(_OVERLAY_ON)] _OverlayOn("Enable Overlay", Float) = 0
        [Toggle(_OVERLAY_MULT_ON)] _OverlayMultOn("Overlay Multiply Mode", Float) = 0
        _OverlayTex("Overlay Texture", 2D) = "white" {}
        _OverlayColor("Overlay Color", Color) = (1, 1, 1, 1)
        _OverlayGlow("Overlay Glow", Range(0,25)) = 1
        _OverlayBlend("Overlay Blend", Range(0, 1)) = 1
        _OverlayTextureScrollXSpeed("Speed X Axis", Range(-5, 5)) = 0.25
        _OverlayTextureScrollYSpeed("Speed Y Axis", Range(-5, 5)) = 0.25
        // --- オーバーレイプロパティ終了 ---

        [Header(Flash Effect)]
        _FlashAmount ("Flash Amount", Range(0,1)) = 0.0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex CombinedShapeLightVertex
            #pragma fragment CombinedShapeLightFragment

            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __
            #pragma multi_compile _ DEBUG_DISPLAY

            // --- オーバーレイ機能の有効化スイッチ ---
            #pragma shader_feature_local _OVERLAY_ON
            #pragma shader_feature_local _OVERLAY_MULT_ON
            // ------------------------------------

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2  uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                half4   color       : COLOR;
                float2  uv          : TEXCOORD0;
                half2   lightingUV  : TEXCOORD1;
                #if defined(DEBUG_DISPLAY)
                float3  positionWS  : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            half4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;

            half _FlashAmount;
            half4 _FlashColor;

            // --- オーバーレイ用変数定義 ---
            TEXTURE2D(_OverlayTex);
            SAMPLER(sampler_OverlayTex);
            half4 _OverlayTex_ST;
            half4 _OverlayColor;
            half _OverlayGlow;
            half _OverlayBlend;
            half _OverlayTextureScrollXSpeed;
            half _OverlayTextureScrollYSpeed;
            // ---------------------------

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            Varyings CombinedShapeLightVertex(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(v.positionOS);
                #if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(v.positionOS);
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);

                o.color = v.color * _Color * _RendererColor;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 CombinedShapeLightFragment(Varyings i) : SV_Target
            {
                const half4 main = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);
                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(main.rgb, main.a, mask, surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);

                // 最終的な色を一度変数に受け取る
                half4 finalColor = CombinedShapeLightShared(surfaceData, inputData);

                // フラッシュの色を合成する
                finalColor.rgb = lerp(finalColor.rgb, _FlashColor.rgb, _FlashAmount);

                // --- オーバーレイ処理開始 ---
                #if defined(_OVERLAY_ON)
                    float2 overlayUvs = i.uv;
                    // 時間経過によるスクロール計算
                    overlayUvs.x += (_Time.y * _OverlayTextureScrollXSpeed) % 1.0;
                    overlayUvs.y += (_Time.y * _OverlayTextureScrollYSpeed) % 1.0;
                    
                    // テクスチャのサンプリングとST(Tiling/Offset)の適用
                    half4 overlayCol = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, TRANSFORM_TEX(overlayUvs, _OverlayTex));
                    
                    // 色と発光強度の適用
                    overlayCol.rgb *= _OverlayColor.rgb * _OverlayGlow;

                    #if !defined(_OVERLAY_MULT_ON)
                        // 加算合成
                        overlayCol.rgb *= overlayCol.a * _OverlayColor.rgb * _OverlayColor.a * _OverlayBlend;
                        finalColor.rgb += overlayCol.rgb;
                    #else
                        // 乗算合成
                        overlayCol.a *= _OverlayColor.a;
                        finalColor = lerp(finalColor, finalColor * overlayCol, _OverlayBlend);
                    #endif
                #endif
                // --- オーバーレイ処理終了 ---
                
                // 合成した色を返す
                return finalColor;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float4 tangent      : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS      : SV_POSITION;
                half4   color           : COLOR;
                float2  uv              : TEXCOORD0;
                half3   normalWS        : TEXCOORD1;
                half3   tangentWS       : TEXCOORD2;
                half3   bitangentWS     : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            half4 _NormalMap_ST;  // Is this the right way to do this?

            Varyings NormalsRenderingVertex(Attributes attributes)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(attributes);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(attributes.positionOS);
                o.uv = TRANSFORM_TEX(attributes.uv, _NormalMap);
                o.color = attributes.color;
                o.normalWS = -GetViewForwardDir();
                o.tangentWS = TransformObjectToWorldDir(attributes.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * attributes.tangent.w;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"

            half4 NormalsRenderingFragment(Varyings i) : SV_Target
            {
                const half4 mainTex = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));

                return NormalsRenderingShared(mainTex, normalTS, i.tangentWS.xyz, i.bitangentWS.xyz, i.normalWS.xyz);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            // --- オーバーレイ機能の有効化スイッチ ---
            #pragma shader_feature_local _OVERLAY_ON
            #pragma shader_feature_local _OVERLAY_MULT_ON
            //

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS      : SV_POSITION;
                float4  color           : COLOR;
                float2  uv              : TEXCOORD0;
                #if defined(DEBUG_DISPLAY)
                float3  positionWS  : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            half4 _RendererColor;

            half _FlashAmount;
            half4 _FlashColor;

            // --- オーバーレイ用変数定義 ---
            TEXTURE2D(_OverlayTex);
            SAMPLER(sampler_OverlayTex);
            half4 _OverlayTex_ST;
            half4 _OverlayColor;
            half _OverlayGlow;
            half _OverlayBlend;
            half _OverlayTextureScrollXSpeed;
            half _OverlayTextureScrollYSpeed;
            // ---------------------------

            Varyings UnlitVertex(Attributes attributes)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(attributes);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(attributes.positionOS);
                #if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(v.positionOS);
                #endif
                o.uv = TRANSFORM_TEX(attributes.uv, _MainTex);
                o.color = attributes.color * _Color * _RendererColor;
                return o;
            }

            float4 UnlitFragment(Varyings i) : SV_Target
            {
                float4 mainTex = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // フラッシュの色を合成する
                mainTex.rgb = lerp(mainTex.rgb, _FlashColor.rgb, _FlashAmount);

                // --- オーバーレイ処理開始 ---
                #if defined(_OVERLAY_ON)
                    float2 overlayUvs = i.uv;
                    // 時間経過によるスクロール計算
                    overlayUvs.x += (_Time.y * _OverlayTextureScrollXSpeed) % 1.0;
                    overlayUvs.y += (_Time.y * _OverlayTextureScrollYSpeed) % 1.0;
                    
                    // テクスチャのサンプリング
                    half4 overlayCol = SAMPLE_TEXTURE2D(_OverlayTex, sampler_OverlayTex, TRANSFORM_TEX(overlayUvs, _OverlayTex));
                    
                    // 色と発光強度の適用
                    overlayCol.rgb *= _OverlayColor.rgb * _OverlayGlow;

                    #if !defined(_OVERLAY_MULT_ON)
                        // 加算合成
                        overlayCol.rgb *= overlayCol.a * _OverlayColor.rgb * _OverlayColor.a * _OverlayBlend;
                        mainTex.rgb += overlayCol.rgb;
                    #else
                        // 乗算合成
                        overlayCol.a *= _OverlayColor.a;
                        mainTex = lerp(mainTex, mainTex * overlayCol, _OverlayBlend);
                    #endif
                #endif
                // --- オーバーレイ処理終了 ---

                #if defined(DEBUG_DISPLAY)
                SurfaceData2D surfaceData;
                InputData2D inputData;
                half4 debugColor = 0;

                InitializeSurfaceData(mainTex.rgb, mainTex.a, surfaceData);
                InitializeInputData(i.uv, inputData);
                SETUP_DEBUG_DATA_2D(inputData, i.positionWS);

                if(CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
                {
                    return debugColor;
                }
                #endif

                return mainTex;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
