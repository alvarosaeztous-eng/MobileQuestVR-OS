Shader "MobileQuestVR/Surface"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.2, 0.25, 0.35, 1.0)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        _Roughness ("Roughness", Range(0.05, 1.0)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode"="ForwardBase" }
        LOD 150

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _EmissionColor;
            half _Roughness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.worldNormal);
                half3 l = normalize(_WorldSpaceLightPos0.xyz + half3(0.0001, 0.5, 0.0001));
                half ndotl = saturate(dot(n, l) * 0.65 + 0.35);

                half3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                half3 h = normalize(l + viewDir);
                half specPower = lerp(48.0, 4.0, _Roughness);
                half spec = pow(saturate(dot(n, h)), specPower) * (1.0 - _Roughness) * 0.35;

                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;
                half3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * 0.75 + half3(0.12, 0.14, 0.20);
                half3 lit = tex.rgb * (ambient + _LightColor0.rgb * ndotl) + spec + _EmissionColor.rgb;
                fixed4 col = fixed4(lit, tex.a);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Mobile/Diffuse"
}
