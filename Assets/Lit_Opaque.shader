Shader "Lit/Opaque"
{
    SubShader
    {
        // Forward base pass
        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 shadowCoord : TEXCOORD0;
                float diff : TEXCOORD1;
            };

            float4x4 _WorldToShadowMatrix;
            float3 _LightDirection;
            
            Texture2D _ShadowMapTexture;
            SamplerComparisonState sampler_ShadowMapTexture;

            v2f vert (appdata_base v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);

                o.shadowCoord = mul(_WorldToShadowMatrix, mul( unity_ObjectToWorld, v.vertex ));// mul( unity_WorldToShadow[0], float4(0,0,0,1)); // 

                // Self lighting/shadowing (based on normal of vertex compared to light direction, doesn't use shadow maps)
                // dot product between normal and light direction for
                // standard diffuse (Lambert) lighting
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                o.diff = max(0, dot(worldNormal, _LightDirection.xyz));

                return o;
            }

            inline fixed SampleShadow (float4 shadowCoord)
            {
                fixed shadow = _ShadowMapTexture.SampleCmpLevelZero (sampler_ShadowMapTexture, (shadowCoord).xy,(shadowCoord).z);

                return shadow;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = 1;
                fixed shadow = SampleShadow(i.shadowCoord);

                return col * shadow * i.diff;
            }
            ENDCG
        }

        // Shadow caster pass
        Pass
        {
            Tags {"LightMode"="ShadowCaster"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f { 
                float4 pos : SV_POSITION;
            };
            
            float3 _LightDirection;

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                
                // Shadow acne cheap fix
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float cosTheta = saturate(dot(worldNormal, _LightDirection.xyz));
                float bias = 0.005*tan(acos(cosTheta));
                bias = clamp(bias, 0,0.01);

                o.pos.z -= bias;
                
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
