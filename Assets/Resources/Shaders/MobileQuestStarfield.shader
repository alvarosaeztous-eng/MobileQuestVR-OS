Shader "MobileQuestVR/StarfieldSky"
{
    Properties
    {
        _TopColor ("Zenith Color", Color) = (0.02, 0.04, 0.11, 1.0)
        _HorizonColor ("Horizon Color", Color) = (0.06, 0.14, 0.30, 1.0)
        _BottomColor ("Nadir Color", Color) = (0.01, 0.02, 0.05, 1.0)
        _StarDensity ("Star Density", Range(20.0, 220.0)) = 110.0
        _StarIntensity ("Star Intensity", Range(0.0, 2.0)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            fixed4 _BottomColor;
            float _StarDensity;
            float _StarIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                fixed3 grad = h >= 0.0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, saturate(pow(h, 0.65)))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(pow(-h, 0.75)));

                float3 grid = floor(d * _StarDensity);
                float rnd = hash31(grid);
                float star = step(0.986, rnd) * saturate(h + 0.15);
                float twinkle = 0.7 + 0.3 * sin(_Time.y * 2.2 + rnd * 62.8);
                fixed3 starCol = lerp(fixed3(0.65, 0.85, 1.0), fixed3(1.0, 0.92, 0.82), frac(rnd * 17.3));
                return fixed4(grad + starCol * (star * twinkle * _StarIntensity), 1.0);
            }
            ENDCG
        }
    }
}
