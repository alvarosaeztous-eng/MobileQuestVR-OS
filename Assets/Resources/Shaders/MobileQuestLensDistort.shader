Shader "MobileQuestVR/LensDistortion"
{
    Properties
    {
        _MainTex ("Eye RenderTexture", 2D) = "black" {}
        _K1 ("Barrel Distortion K1", Range(-0.5, 0.8)) = 0.22
        _K2 ("Barrel Distortion K2", Range(-0.5, 0.8)) = 0.08
        _LensCenterOffset ("Lens Center Offset X", Range(-0.15, 0.15)) = 0.0
        _Brightness ("Brightness", Range(0.2, 1.3)) = 1.0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _K1;
            float _K2;
            float _LensCenterOffset;
            float _Brightness;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 center = float2(0.5 + _LensCenterOffset, 0.5);
                float2 p = (i.uv - center) * 2.0;
                float r2 = dot(p, p);
                float factor = 1.0 + _K1 * r2 + _K2 * r2 * r2;
                float2 distorted = center + (p * factor) * 0.5;

                if (distorted.x < 0.0 || distorted.x > 1.0 || distorted.y < 0.0 || distorted.y > 1.0)
                {
                    return fixed4(0, 0, 0, 1);
                }

                // Soft vignette around circular VR lens boundary
                float edge = smoothstep(1.18, 0.96, length(p * factor));
                fixed4 col = tex2D(_MainTex, distorted);
                return fixed4(col.rgb * edge * _Brightness, 1.0);
            }
            ENDCG
        }
    }
}
