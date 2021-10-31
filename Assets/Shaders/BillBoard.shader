// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/BillBoard"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
	SubShader
	{ 
		Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "DisableBatching" = "True" }
		Blend One OneMinusSrcAlpha
		ColorMask RGB
		Cull Off Lighting Off ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag   
			#pragma multi_compile_particles
            #pragma multi_compile_instancing

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_INSTANCING_BUFFER_END(Props)
			sampler2D _MainTex;
			float4 _MainTex_ST;

			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				float s = unity_ObjectToWorld[0].x;
				o.vertex = mul(UNITY_MATRIX_P,
					mul(UNITY_MATRIX_MV, float4(0.0, 0.0, 0.0, 1.0))
					+ float4(v.vertex.x, v.vertex.y, 0.0, 0.0)
					* float4(s, s, 1.0, 1.0));
				//o.vertex = UnityObjectToClipPos(o.vertex);
				//float4 ori = mul(UNITY_MATRIX_MV, float4(0, 0, 0, 1));
				//float4 vt = v.vertex;
				//vt.y = vt.z;
				//vt.z = 0;
				//vt.xyz += ori.xyz;//result is vt.z==ori.z ,so the distance to camera keeped ,and screen size keeped
				//o.vertex = mul(UNITY_MATRIX_P, vt);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				return col;
			}
			ENDCG
		}
	}
}
