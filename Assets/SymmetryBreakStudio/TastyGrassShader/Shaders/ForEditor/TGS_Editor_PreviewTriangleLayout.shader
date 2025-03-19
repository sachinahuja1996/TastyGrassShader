Shader "Hidden/Symmetry Break Studio/Tasty Grass Shader/Editor/Preview Triangle Layout"
{
      Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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


            float _UpperLeftTriangle;
            float _LowerRightTriangle;
            float _CenterTriangle;

            // See TastyGrassShaderLibrary.hlsl
            float RndHash11(float p)
            {
                p = frac(p * .1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex.xyz);
                o.uv = v.uv ;
                return o;
            }

            float2 _Height, _Width, _Thickness, _ThicknessApex;

            float sdTriangle( in float2 p, in float2 p0, in float2 p1, in float2 p2 )
            {
                float2 e0 = p1-p0, e1 = p2-p1, e2 = p0-p2;
                float2 v0 = p -p0, v1 = p -p1, v2 = p -p2;
                float2 pq0 = v0 - e0*clamp( dot(v0,e0)/dot(e0,e0), 0.0, 1.0 );
                float2 pq1 = v1 - e1*clamp( dot(v1,e1)/dot(e1,e1), 0.0, 1.0 );
                float2 pq2 = v2 - e2*clamp( dot(v2,e2)/dot(e2,e2), 0.0, 1.0 );
                float s = sign( e0.x*e2.y - e0.y*e2.x );
                float2 d = min(min(float2(dot(pq0,pq0), s*(v0.x*e0.y-v0.y*e0.x)),
                                 float2(dot(pq1,pq1), s*(v1.x*e1.y-v1.y*e1.x))),
                                 float2(dot(pq2,pq2), s*(v2.x*e2.y-v2.y*e2.x)));
                return -sqrt(d.x)*sign(d.y);
            }

            float SdfToMask(float sdf, float pixelSize)
            {
                return  abs(sdf) < pixelSize * 1 ? 1.0 : 0.0;;
            }

            float4 frag (v2f i) : SV_Target
            {

                float4 val = 0.0;
                float pixelSize = 1.0 / _ScreenParams.y;
                float pixelSize2x = pixelSize * 2.0;

                _Height = max(0.0, _Height);
                _Width = max(0.0, _Width);

                const int interations = 64;
                for (int iter = 0; iter < interations; iter++)
                {

                    float height = lerp(_Height.x, _Height.y, RndHash11(iter * 1));
                    float width = lerp(_Width.x, _Width.y, RndHash11(iter * 2));
                    float thickness = lerp(_Thickness.x, _Thickness.y, RndHash11(iter * 3));
                    float thicknessApex = lerp(_ThicknessApex.x, _ThicknessApex.y, RndHash11(iter * 4));


                    float2 viewOffset = float2(0.5, 0.0);
                    float2 root = 0.0 + viewOffset;
                    float2 tip = float2(width, height) + viewOffset;
                    float2 side = float2(thickness, lerp(0, height, thicknessApex)) + viewOffset;

                    const float triangleSdf = sdTriangle(i.uv, root, tip, side);
                    const float triangleSdfMask = SdfToMask(triangleSdf, pixelSize);

                    val += triangleSdfMask / float(interations);
                }

                val.rgb = LinearToGammaSpace(val.rgb);
                return val;
            }
            ENDCG
        }
    }
}