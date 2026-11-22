Shader "ProtocolAer/TargetDepth"
{
    SubShader
    {
        Tags { "Queue"="Geometry" }
        Pass
        {
            ColorMask 0
            ZWrite On ZTest LEqual Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 vertex:POSITION):SV_POSITION { return UnityObjectToClipPos(vertex); }
            fixed4 frag():SV_Target { return 0; }
            ENDCG
        }
    }
}
