Shader "Custom/AnimMapShader_New"
{
    Properties
    {
		//Standard Properties
		[NoScaleOffset] _MainTex("Albedo", 2D) = "white" {}
		[NoScaleOffset] _Metallic("Metallic", 2D) = "white" {}
		[NoScaleOffset][Normal] _BumpMap("Normal Map", 2D) = "bump" {}
		[NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[NoScaleOffset] _OcclusionMap("Occlusion", 2D) = "white" {}

		//Anim Map Properties
		[NoScaleOffset] _AnimMap("AnimMap", 2D) = "white" {}
		_AnimLen("Anim Length", Float) = 0
		_IsVisible("Visible", Float) = 1
		Time("Time", Float) = 0

		//Selection Outline Properties
		_IsSelected("IsSelected", Float) = 0
		_OutlineColor("Outline Color", Color) = (1, 0, 0, 1)
		_OutlineWidth("Outline Width", Float) = 0.01
    }
	SubShader
	{
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "true" "RenderType" = "TransparentCutout" }
		LOD 100
		Cull Front

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#pragma target 3.0

			struct Input
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct Output
			{
				float4 pos : SV_POSITION;
			};

			sampler2D _AnimMap;
			float4 _AnimMap_TexelSize;//x == 1/width

			float _AnimLen;
			float _IsVisible;

			uniform float Time = 1.0f;

			float _IsSelected;
			float4 _OutlineColor;
			float _OutlineWidth;

			Output vert(Input i, uint vid : SV_VertexID)
			{
				Output o;

				float f = ((_Time.y - Time) * _IsVisible) / _AnimLen;

				if (_IsVisible == 2)
					f = 0.99;

				fmod(f, 1.0);

				float animMap_x = (vid + 0.5) * _AnimMap_TexelSize.x;
				float animMap_y = f;

				float4 pos = tex2Dlod(_AnimMap, float4(animMap_x, animMap_y, 0, 0));

				//Outline Setup
				float dist = distance(_WorldSpaceCameraPos, pos.xyz) / _ScreenParams.g;
				float expand = dist * 0.25 * _OutlineWidth * _IsSelected;
				pos = float4(pos.xyz + i.normal * expand, 1);

				o.pos = UnityObjectToClipPos(pos);
				return o;
			}

			float4 frag(Output o) : COLOR
			{
				return _OutlineColor;
			}
			ENDCG
		}

		Cull Back

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma enable_d3d11_debug.symbols

			#include "UnityCG.cginc"
			#include "UnityStandardBRDF.cginc"
			#include "UnityLightingCommon.cginc"

			#define WorldNormalVector(data,normal)

			struct appdata
			{
				float4 position : POSITION;
				float2 uv : TEXCOORD0;
				float2 NormalUV : TEXCOORD1;
				float3 normal : NORMAL;
				float4 tangent : TANGENT; // xyz = tangent direction, w = tangent sign
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float2 NormalUV : TEXCOORD1;
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
				float3 tangent : TEXCOORD2;
				float3 bitangent : TEXCOORD3;
				float3 worldPos : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			sampler2D _MainTex;
			sampler2D _BumpMap;
			sampler2D _Metallic;
			sampler2D _EmissionMap;
			sampler2D _OcclusionMap;

			sampler2D _AnimMap;
			float4 _AnimMap_TexelSize;//x == 1/width
			float _AnimLen;
			float _IsVisible;

			uniform float Time = 1.0f;


			v2f vert(appdata v, uint vid : SV_VertexID)
			{
				UNITY_SETUP_INSTANCE_ID(v);

				float f = ((_Time.y - Time) * _IsVisible) / _AnimLen;

				if (_IsVisible == 2)
					f = 0.99;

				fmod(f, 1.0);

				float animMap_x = (vid + 0.5) * _AnimMap_TexelSize.x;
				float animMap_y = f;

				float4 pos = tex2Dlod(_AnimMap, float4(animMap_x, animMap_y, 0, 0));

				v2f o;
				o.uv = v.uv;
				o.NormalUV = v.NormalUV;

				float2x2 rotationMatrix = float2x2(1, 1, 1, 0);

				o.vertex = UnityObjectToClipPos(pos);
				o.normal = UnityObjectToWorldNormal(v.normal);
				o.tangent = UnityObjectToWorldDir(v.tangent.xyz);
				o.bitangent = cross(o.normal, o.tangent) * (v.tangent.w * unity_WorldTransformParams.w);
				o.worldPos = mul(unity_ObjectToWorld, pos);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				//Lighting Setup
				float3 tangentSpaceNormal = UnpackNormal(tex2D(_BumpMap, i.NormalUV));

				float3x3 mtxTangentToWorld = {
					i.tangent.x, i.bitangent.x, i.normal.x,
					i.tangent.y, i.bitangent.y, i.normal.y,
					i.tangent.z, i.bitangent.z, i.normal.z
				};

				float3 N = mul(mtxTangentToWorld, tangentSpaceNormal);

				float3 L = _WorldSpaceLightPos0.xyz;
				float3 lambert = saturate(dot(N, L));
				float3 diffuseLight = lambert * _LightColor0.xyz;

				float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
				float3 H = normalize(L + V);

				float3 SpecLight = saturate(dot(H, N)) * (lambert > 0);

				float Gloss = .45;
				float SpecExponent = exp2(Gloss * 11) + 2;
				SpecLight = pow(SpecLight, SpecExponent) * (Gloss * 1.5);

				SpecLight *= _LightColor0.xyz;

				//Standard Texture Setup
				fixed4 col = tex2D(_MainTex, i.uv);

				float4 Metallic = tex2D(_Metallic, i.uv);
				Metallic = lerp(Metallic, tex2D(_EmissionMap, i.uv), tex2D(_EmissionMap, i.uv));
				Metallic = Metallic * tex2D(_OcclusionMap, i.uv);
				Metallic = Metallic + float4(-0.2, -0.2, -0.2, 1);

				float4 NormalMap = dot(tex2D(_BumpMap, i.NormalUV), .45);

				//Output Tests
				float4 Test = lerp(col, (Metallic + float4(SpecLight, 1)) * col, tex2D(_Metallic, i.uv).a > .5) * float4(NormalMap.x, NormalMap.y, NormalMap.z, 1);

				float4 FinalColor = lerp(col, (float4(diffuseLight, 1) * Metallic + float4(SpecLight, 1)) * col, tex2D(_Metallic, i.uv) > .3);

				float3 Blah = saturate(dot(UnpackNormal(tex2D(_BumpMap, i.NormalUV)) * 0.5 + 0.5, _LightColor0.xyz * .5));

				return float4(diffuseLight, 1) * lerp(col, (Metallic + float4(SpecLight, 1)) * col, tex2D(_Metallic, i.uv)/* > .3*/);
			}
			ENDCG
		}
	}
}
