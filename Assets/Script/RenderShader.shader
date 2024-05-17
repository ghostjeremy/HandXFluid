Shader "Instanced/RenderShader" {
	Properties {
		
	}
	SubShader {
		// Specifies the rendering queue
		Tags {"Queue"="Geometry" }

		Pass {

			CGPROGRAM
			// Vertex and fragment shader entry points
			#pragma vertex vert
			#pragma fragment frag
			 // Specifies the shader target version
			#pragma target 4.5
			  // Include common Unity shader functions and definitions
			#include "UnityCG.cginc"
			 // Structured buffers for positions and velocities
			StructuredBuffer<float3> Positions;
			StructuredBuffer<float3> Velocities;
			 // Texture for color mapping
			Texture2D<float4> ColourMap;
			 // Sampler state for the color map texture
			SamplerState linear_clamp_sampler;
			 // Maximum velocity for normalization
			float velocityMax;
			 // Scale factor for vertex positions
			float scale;
			 // Base color for the shader
			float3 colour;
			// Local to world transformation matrix
			float4x4 localToWorld;
			 // Structure for passing data from vertex to fragment shader
			struct v2f
			{
				float4 pos : SV_POSITION; // Clip space position
				float2 uv : TEXCOORD0; // Texture coordinates
				float3 colour : TEXCOORD1;// Vertex color
				 
			};
			 // Vertex shader
			v2f vert (appdata_full v, uint instanceID : SV_InstanceID)
			{
				 // Get the center position of the current instance
				float3 centreWorld = Positions[instanceID];
				 // Calculate the world position of the vertex
				float3 worldVertPos = centreWorld + mul(unity_ObjectToWorld, v.vertex * scale);
				 // Transform the world position to object space
				float3 objectVertPos = mul(unity_WorldToObject, float4(worldVertPos.xyz, 1));
				v2f o;
				o.uv = v.texcoord;
				// Transform the object space position to clip space
				o.pos = UnityObjectToClipPos(objectVertPos);
				  // Calculate the speed of the particle
				float speed = length(Velocities[instanceID]);
				 // Normalize the speed based on the maximum velocity
				float speedT = saturate(speed / velocityMax);
				float colT = speedT;
				// Sample the color map texture based on the normalized speed
				o.colour = ColourMap.SampleLevel(linear_clamp_sampler, float2(colT, 0.5), 0);
		
				return o;
			}
			 // Fragment shader
			float4 frag (v2f i) : SV_Target
			{
				// Return the color with full opacity
				return float4(i.colour, 1);
			}

			ENDCG
		}
	}
}