Shader "Unlit/Equirectangular360"
{
    Properties
    {
        _MainTex ("Equirectangular (2:1)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Lighting Off
        Cull Off           // Show inside of sphere
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 ray : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.ray = normalize(v.vertex.xyz); // Direction from sphere center
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float longitude = atan2(i.ray.z, i.ray.x);
                float latitude = asin(i.ray.y);

                float2 uv;
                uv.x = 1.0 - (0.5 + longitude / (2.0 * UNITY_PI)); // Fix mirrored image
                uv.y = 0.5 + latitude / UNITY_PI;                  // Fix upside-down image

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
