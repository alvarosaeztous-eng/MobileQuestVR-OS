Shader "MobileQuestVR/Hologram"
{
    Properties
    {
        _Color ("Tint Color", Color) = (0.06, 0.12, 0.24, 0.86)
        _RimColor ("Rim Glow Color", Color) = (0.0, 0.85, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 2.4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _RimColor;
            half _RimPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = _WorldSpaceCameraPos.xyz - worldPos;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.worldNormal);
                half3 v = normalize(i.viewDir);
                half rim = 1.0 - abs(dot(n, v));
                half glow = pow(saturate(rim), _RimPower);
                fixed3 rgb = lerp(_Color.rgb, _RimColor.rgb, glow * 0.75);
                fixed alpha = saturate(_Color.a + glow * 0.35);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
