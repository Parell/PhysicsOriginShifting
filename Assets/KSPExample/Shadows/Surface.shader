Shader "Custom/Surface"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Scale ("Scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }  LOD 200
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows

        sampler2D _MainTex;

        sampler2D _ShadowTexture0, _ShadowTexture1, _ShadowTexture2, _ShadowTexture3;
        float4 _ShadowPosition0, _ShadowPosition1, _ShadowPosition2, _ShadowPosition3;
        float4x4 _WorldToShadow0, _WorldToShadow1, _WorldToShadow2, _WorldToShadow3;
        int _ShadowCount;
        float _Scale;

        struct Input { float2 uv_MainTex; float3 worldPos; float3 worldNormal; };

        float _ShadowOrthoHalf0, _ShadowOrthoHalf1, _ShadowOrthoHalf2, _ShadowOrthoHalf3;
        float _ShadowAspect0, _ShadowAspect1, _ShadowAspect2, _ShadowAspect3;
        float _ShadowTexWidth0, _ShadowTexWidth1, _ShadowTexWidth2, _ShadowTexWidth3;
        float _ShadowRadius0, _ShadowRadius1, _ShadowRadius2, _ShadowRadius3;
        float _StarHalfAngleTan;


        inline float PenumbraLOD(float z, float orthoHalf, float texW) {
            float r_world = z * _StarHalfAngleTan;
            float r_uv = r_world / max(1e-6, (2.0 * orthoHalf));
            float r_px = r_uv * texW;
            return max(0.0, log2(max(r_px, 1.0)));
        }

        inline float SampleShadowPenumbral(sampler2D tex, float4x4 w2s, float3 Pscaled, float3 Cscaled, float3 A, float orthoHalf, float texW, float aspect, float bodyRadius)
        {
            float3 PC = Pscaled - Cscaled;

            float z = dot(PC, A);
            if (z <= 0.0) return 1.0;

            float4 pr = mul(w2s, float4(Pscaled,1));
            float2 uv = pr.xy / pr.w * 0.5 + 0.5;

            //uv.x = (uv.x - 0.5) / max(aspect, 1e-6) + 0.5;

            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 1.0;

            float lod = PenumbraLOD(z, orthoHalf, texW);
            float s   = tex2Dlod(tex, float4(uv, 0, lod)).r;

            float k = bodyRadius / max(1e-5, z * _StarHalfAngleTan);
            float minimumLight = 1.0 - saturate(k*k);
            s = max(s, minimumLight);

            return s;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb;

            float3 L = normalize(_WorldSpaceLightPos0.xyz);
            float3 A = -L;

            float NdotL = dot(normalize(IN.worldNormal), L);
            float direct = saturate(NdotL);

            float3 P = IN.worldPos * _Scale;

            float f0 = (_ShadowCount>=1) ? SampleShadowPenumbral(_ShadowTexture0,_WorldToShadow0,P,_ShadowPosition0.xyz,A,_ShadowOrthoHalf0,_ShadowTexWidth0,_ShadowAspect0,_ShadowRadius0) : 1.0;
            float f1 = (_ShadowCount>=2) ? SampleShadowPenumbral(_ShadowTexture1,_WorldToShadow1,P,_ShadowPosition1.xyz,A,_ShadowOrthoHalf1,_ShadowTexWidth1,_ShadowAspect1,_ShadowRadius1) : 1.0;
            float f2 = (_ShadowCount>=3) ? SampleShadowPenumbral(_ShadowTexture2,_WorldToShadow2,P,_ShadowPosition2.xyz,A,_ShadowOrthoHalf2,_ShadowTexWidth2,_ShadowAspect2,_ShadowRadius2) : 1.0;
            float f3 = (_ShadowCount>=4) ? SampleShadowPenumbral(_ShadowTexture3,_WorldToShadow3,P,_ShadowPosition3.xyz,A,_ShadowOrthoHalf3,_ShadowTexWidth3,_ShadowAspect3,_ShadowRadius3) : 1.0;

            o.Albedo *= direct * (f0 * f1 * f2 * f3);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
