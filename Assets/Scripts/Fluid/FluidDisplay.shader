// Draws the fluid's dye full-screen from the RawImage that FluidSimulation.cs creates.
Shader "Hidden/Fluid/Display"
{
    Properties
    {
        // Unused, but the UI insists on handing every material a _MainTex.
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Dye ("Dye", 2D) = "black" {}
        _BackColor ("Back Color", Color) = (0,0,0,1)
        [Toggle(SHADING)] _Shading ("Shading", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Background" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local SHADING
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _Dye;
            float3 _BackColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_Dye, i.uv).rgb;

            #ifdef SHADING
                // Treats the dye's brightness as a height map and lights it from the front.
                float2 texelSize = _ScreenParams.zw - 1.0;
                float3 lc = tex2D(_Dye, i.uv - float2(texelSize.x, 0.0)).rgb;
                float3 rc = tex2D(_Dye, i.uv + float2(texelSize.x, 0.0)).rgb;
                float3 tc = tex2D(_Dye, i.uv + float2(0.0, texelSize.y)).rgb;
                float3 bc = tex2D(_Dye, i.uv - float2(0.0, texelSize.y)).rgb;

                float dx = length(rc) - length(lc);
                float dy = length(tc) - length(bc);

                float3 n = normalize(float3(dx, dy, length(texelSize)));
                float diffuse = clamp(n.z + 0.7, 0.7, 1.0);
                c *= diffuse;
            #endif

                float a = saturate(max(c.r, max(c.g, c.b)));
                c += _BackColor * (1.0 - a);

                // The dye is mixed in display (gamma) space, as it is in the browser.
            #ifndef UNITY_COLORSPACE_GAMMA
                c = GammaToLinearSpace(max(c, 0.0));
            #endif
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
}
