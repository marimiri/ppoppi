Shader "BlockXX/Ink"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) _ZWrite ("Depth Write", Int) = 1 _ZTest ("Depth Test", Int) = 4 _Cull ("Cull", Int) = 0 _ViewBias ("View depth bias", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; };
            fixed4 _Color;
            float _ViewBias;
            v2f vert(appdata v)
            {
                v2f o;float4 view=mul(UNITY_MATRIX_MV,v.vertex);view.z+=_ViewBias;
                o.vertex=mul(UNITY_MATRIX_P,view);o.color=v.color*_Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
