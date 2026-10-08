// Simulation passes for FluidSimulation.cs, ported from Pavel Dobryakov's WebGL-Fluid-Simulation (MIT).
// The pass order here must match the Pass* constants in FluidSimulation.cs.
Shader "Hidden/Fluid/Simulation"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _Velocity;
    sampler2D _Curl;
    sampler2D _Divergence;
    sampler2D _Pressure;

    float2 _SimTexelSize;
    float _AspectRatio;
    float2 _SplatPoint;
    float3 _SplatColor;
    float _Radius;
    float _Dt;
    float _Dissipation;
    float _CurlStrength;
    float _Value;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float2 l : TEXCOORD1;
        float2 r : TEXCOORD2;
        float2 t : TEXCOORD3;
        float2 b : TEXCOORD4;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        o.l = o.uv - float2(_SimTexelSize.x, 0.0);
        o.r = o.uv + float2(_SimTexelSize.x, 0.0);
        o.t = o.uv + float2(0.0, _SimTexelSize.y);
        o.b = o.uv - float2(0.0, _SimTexelSize.y);
        return o;
    }

    // _MainTex is the velocity or the dye; the splat is added on top of it.
    float4 fragSplat(v2f i) : SV_Target
    {
        float2 p = i.uv - _SplatPoint;
        p.x *= _AspectRatio;
        float3 splat = exp(-dot(p, p) / _Radius) * _SplatColor;
        float3 base = tex2D(_MainTex, i.uv).xyz;
        return float4(base + splat, 1.0);
    }

    // _MainTex is the field being carried along by _Velocity.
    float4 fragAdvection(v2f i) : SV_Target
    {
        float2 coord = i.uv - _Dt * tex2D(_Velocity, i.uv).xy * _SimTexelSize;
        float4 result = tex2D(_MainTex, coord);
        float decay = 1.0 + _Dissipation * _Dt;
        return result / decay;
    }

    float4 fragDivergence(v2f i) : SV_Target
    {
        float L = tex2D(_MainTex, i.l).x;
        float R = tex2D(_MainTex, i.r).x;
        float T = tex2D(_MainTex, i.t).y;
        float B = tex2D(_MainTex, i.b).y;

        // Walls: the velocity just outside the edge mirrors the velocity inside.
        float2 C = tex2D(_MainTex, i.uv).xy;
        if (i.l.x < 0.0) { L = -C.x; }
        if (i.r.x > 1.0) { R = -C.x; }
        if (i.t.y > 1.0) { T = -C.y; }
        if (i.b.y < 0.0) { B = -C.y; }

        float div = 0.5 * (R - L + T - B);
        return float4(div, 0.0, 0.0, 1.0);
    }

    float4 fragCurl(v2f i) : SV_Target
    {
        float L = tex2D(_MainTex, i.l).y;
        float R = tex2D(_MainTex, i.r).y;
        float T = tex2D(_MainTex, i.t).x;
        float B = tex2D(_MainTex, i.b).x;
        float vorticity = R - L - T + B;
        return float4(0.5 * vorticity, 0.0, 0.0, 1.0);
    }

    float4 fragVorticity(v2f i) : SV_Target
    {
        float L = tex2D(_Curl, i.l).x;
        float R = tex2D(_Curl, i.r).x;
        float T = tex2D(_Curl, i.t).x;
        float B = tex2D(_Curl, i.b).x;
        float C = tex2D(_Curl, i.uv).x;

        float2 force = 0.5 * float2(abs(T) - abs(B), abs(R) - abs(L));
        force /= length(force) + 0.0001;
        force *= _CurlStrength * C;
        force.y *= -1.0;

        float2 velocity = tex2D(_MainTex, i.uv).xy;
        velocity += force * _Dt;
        velocity = clamp(velocity, -1000.0, 1000.0);
        return float4(velocity, 0.0, 1.0);
    }

    // One Jacobi iteration; _MainTex is the previous pressure.
    float4 fragPressure(v2f i) : SV_Target
    {
        float L = tex2D(_MainTex, i.l).x;
        float R = tex2D(_MainTex, i.r).x;
        float T = tex2D(_MainTex, i.t).x;
        float B = tex2D(_MainTex, i.b).x;
        float divergence = tex2D(_Divergence, i.uv).x;
        float pressure = (L + R + B + T - divergence) * 0.25;
        return float4(pressure, 0.0, 0.0, 1.0);
    }

    float4 fragGradientSubtract(v2f i) : SV_Target
    {
        float L = tex2D(_Pressure, i.l).x;
        float R = tex2D(_Pressure, i.r).x;
        float T = tex2D(_Pressure, i.t).x;
        float B = tex2D(_Pressure, i.b).x;
        float2 velocity = tex2D(_MainTex, i.uv).xy;
        velocity -= float2(R - L, T - B);
        return float4(velocity, 0.0, 1.0);
    }

    float4 fragClear(v2f i) : SV_Target
    {
        return _Value * tex2D(_MainTex, i.uv);
    }
    ENDCG

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Splat"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragSplat
            ENDCG
        }

        Pass
        {
            Name "Advection"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAdvection
            ENDCG
        }

        Pass
        {
            Name "Divergence"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDivergence
            ENDCG
        }

        Pass
        {
            Name "Curl"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragCurl
            ENDCG
        }

        Pass
        {
            Name "Vorticity"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragVorticity
            ENDCG
        }

        Pass
        {
            Name "Pressure"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPressure
            ENDCG
        }

        Pass
        {
            Name "GradientSubtract"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragGradientSubtract
            ENDCG
        }

        Pass
        {
            Name "Clear"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragClear
            ENDCG
        }
    }
}
