Shader "UI/MaskEraser"
{
    Properties
    {
        _MainTex ("Mask Shape", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Stencil ("Stencil Ref", Range(0,255)) = 1
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        // Mask writes stencil so parent background won't draw here
        Stencil
        {
            Ref [_Stencil]
            Comp Always
            Pass Replace
        }

        ColorMask 0 // don’t draw visible color

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            sampler2D _MainTex;
            float4 _Color;
            float _Cutoff;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                // only mark stencil where alpha > cutoff
                clip(c.a - _Cutoff);
                return c;
            }
            ENDCG
        }
    }
}
