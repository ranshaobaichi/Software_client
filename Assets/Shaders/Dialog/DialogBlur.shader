// 可分离高斯模糊 Shader，用于 Dialog 背景虚化效果。
// 使用两 Pass：水平方向 + 垂直方向（由 C# 代码通过 _BlurDirection 控制）。
Shader "Hidden/DialogBlur"
{
    Properties
    {
        _MainTex    ("Texture",     2D)     = "white" {}
        _BlurSize   ("Blur Size",   Float)  = 1.5
        // xy 分量指定模糊方向：(1,0) 水平，(0,1) 垂直
        _BlurDirection ("Blur Direction", Vector) = (1, 0, 0, 0)
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4    _MainTex_TexelSize;   // (1/w, 1/h, w, h)
    float     _BlurSize;
    float4    _BlurDirection;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv     : TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv  : TEXCOORD0;
    };

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv  = v.uv;
        return o;
    }

    // 9-tap 可分离高斯核（σ≈2）
    // 权重归一化：0.0625 + 0.25 + 0.375 + 0.25 + 0.0625 ... 双侧各4样本 + 中心
    static const int   SAMPLE_COUNT = 9;
    static const float OFFSETS[9]   = { -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0 };
    static const float WEIGHTS[9]   = { 0.0162, 0.0540, 0.1216, 0.1945, 0.2270,
                                         0.1945, 0.1216, 0.0540, 0.0162 };

    fixed4 frag(v2f i) : SV_Target
    {
        float2 dir = _BlurDirection.xy * _MainTex_TexelSize.xy * _BlurSize;
        fixed4 col = fixed4(0, 0, 0, 0);
        for (int s = 0; s < SAMPLE_COUNT; s++)
            col += tex2D(_MainTex, i.uv + dir * OFFSETS[s]) * WEIGHTS[s];
        return col;
    }
    ENDCG

    SubShader
    {
        // 不写深度、不做裁剪，纯后处理用途
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            ENDCG
        }
    }

    FallBack Off
}
