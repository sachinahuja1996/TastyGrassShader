Shader "Hidden/Symmetry Break Studio/Tasty Grass Shader/Editor Preview"
{
    Properties
    {
        _MainTex ("Texture", 3D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler3D _MainTex;


            float _ValueScale, _ValueOffset, _Tiling;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv ;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float val = tex3D(_MainTex, float3(i.uv, 0.5)).r;
                val = val * _ValueScale + _ValueOffset;
                return float4(val.xxx, 1.0);
            }
            ENDCG
        }
    }
}
