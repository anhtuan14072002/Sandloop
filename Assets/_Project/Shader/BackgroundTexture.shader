Shader "Custom/Map-Texture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 1
        _MinShadow ("Min Shadow Brightness", Range(0,1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }

            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // cần để Built-in compile shadow/light variants
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ShadowStrength;
            float _MinShadow;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                SHADOW_COORDS(1)
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;

                // 1 = sáng, 0 = trong bóng
                fixed shadowAtten = SHADOW_ATTENUATION(i); 

                // giữ texture, chỉ nhân bóng
                // _ShadowStrength = 0 => gần như không thấy bóng
                // _ShadowStrength = 1 => nhận bóng đầy đủ
                fixed shadowFactor = lerp(1.0, max(shadowAtten, _MinShadow), _ShadowStrength);

                tex.rgb *= shadowFactor;

                UNITY_APPLY_FOG(i.fogCoord, tex);
                return tex;
            }
            ENDCG
        }

        // pass này để object có thể đổ bóng lên vật khác
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }

    FallBack "VertexLit"
}
