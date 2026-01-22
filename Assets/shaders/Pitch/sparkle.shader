
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#ifndef S_ALPHA_TEST
	#define S_ALPHA_TEST 0
	#endif
	#ifndef S_TRANSLUCENT
	#define S_TRANSLUCENT 0
	#endif
	
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define S_UV2 1
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 vPositionOs : TEXCOORD14;
	float3 vNormalOs : TEXCOORD15;
	float4 vTangentUOs_flTangentVSign : TANGENT	< Semantic( TangentU_SignV ); >;
	float4 vColor : COLOR0;
	float4 vTintColor : COLOR1;
	#if ( PROGRAM == VFX_PROGRAM_PS )
		bool vFrontFacing : SV_IsFrontFace;
	#endif
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		
		PixelInput i = ProcessVertex( v );
		i.vPositionOs = v.vPositionOs.xyz;
		i.vColor = v.vColor;
		
		ExtraShaderData_t extraShaderData = GetExtraPerInstanceShaderData( v.nInstanceTransformID );
		i.vTintColor = extraShaderData.vTint;
		
		VS_DecodeObjectSpaceNormalAndTangent( v, i.vNormalOs, i.vTangentUOs_flTangentVSign );
		return FinalizeVertex( i );
		
	}
}

PS
{
	#include "common/pixel.hlsl"
	RenderState( CullMode, F_RENDER_BACKFACES ? NONE : DEFAULT );
		
	SamplerState g_sSampler0 < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;
	CreateInputTexture2D( SparkleMask, Srgb, 8, "None", "_color", ",0/,0/0", DefaultFile( "prefabs/pitches/dev/shaderdev/sparkle/sparklemask.tga" ) );
	CreateInputTexture2D( SparkleMask_0, Srgb, 8, "None", "_color", ",0/,0/0", DefaultFile( "prefabs/pitches/dev/shaderdev/sparkle/sparklemask.tga" ) );
	Texture2D g_tSparkleMask < Channel( RGBA, Box( SparkleMask ), Linear ); OutputFormat( DXT5 ); SrgbRead( False ); >;
	Texture2D g_tSparkleMask_0 < Channel( RGBA, Box( SparkleMask_0 ), Linear ); OutputFormat( DXT5 ); SrgbRead( False ); >;
	float4 g_vBaseColour < UiType( Color ); UiGroup( ",0/,0/0" ); Default4( 0.10, 0.21, 0.05, 1.00 ); >;
	float g_flSpecPower < UiGroup( ",0/,0/0" ); Default1( 64 ); Range1( 0, 256 ); >;
	float g_flStrength < UiGroup( ",0/,0/0" ); Default1( 1 ); Range1( 0, 50 ); >;
	float g_flPointBoost < UiGroup( ",0/,0/0" ); Default1( 1 ); Range1( 0, 50 ); >;
	float g_flShadowMask < UiGroup( ",0/,0/0" ); Default1( 1 ); Range1( 0, 1 ); >;
	float g_flObjectSpaceTiling < UiGroup( ",0/,0/0" ); Default1( 256 ); Range1( 0, 1024 ); >;
	float g_flScreenSpaceTiling < UiGroup( ",0/,0/0" ); Default1( 10 ); Range1( 0, 100 ); >;
	float g_flRoughness < UiGroup( ",0/,0/0" ); Default1( 0.44643673 ); Range1( 0, 1 ); >;
	float g_flMetalness < UiGroup( ",0/,0/0" ); Default1( 0.8718644 ); Range1( 0, 1 ); >;
	
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		
		Material m = Material::Init( i );
		m.Albedo = float3( 1, 1, 1 );
		m.Normal = float3( 0, 0, 1 );
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		m.TintMask = 1;
		m.Opacity = 1;
		m.Emission = float3( 0, 0, 0 );
		m.Transmission = 0;
		
		float4 l_0 = g_vBaseColour;
		float2 l_1 = CalculateViewportUv( i.vPositionSs.xy );
		float3 l_2 = i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz;
		float3 l_3 = CalculatePositionToCameraDirWs( i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz );
		float l_4 = g_flSpecPower;
		float l_5 = g_flStrength;
		float l_6 = g_flPointBoost;
		float l_7 = g_flShadowMask;
		float3 l_8 = (( Light::Count(l_2) > 0u ? (((pow(saturate(dot(normalize(i.vNormalWs), normalize((normalize(l_3) + normalize(Light::From(l_2, 0u, l_1.xy).Direction))))), (l_4)) * saturate(dot(normalize(i.vNormalWs), normalize(Light::From(l_2, 0u, l_1.xy).Direction)))) * Light::From(l_2, 0u, l_1.xy).Color * Light::From(l_2, 0u, l_1.xy).Attenuation * lerp(1.0, Light::From(l_2, 0u, l_1.xy).Visibility, saturate(l_7)) * lerp(1.0, (l_6), saturate(1.0 - Light::From(l_2, 0u, l_1.xy).Attenuation)) * (l_5))) : float3(0,0,0) ) + ( Light::Count(l_2) > 1u ? (((pow(saturate(dot(normalize(i.vNormalWs), normalize((normalize(l_3) + normalize(Light::From(l_2, 1u, l_1.xy).Direction))))), (l_4)) * saturate(dot(normalize(i.vNormalWs), normalize(Light::From(l_2, 1u, l_1.xy).Direction)))) * Light::From(l_2, 1u, l_1.xy).Color * Light::From(l_2, 1u, l_1.xy).Attenuation * lerp(1.0, Light::From(l_2, 1u, l_1.xy).Visibility, saturate(l_7)) * lerp(1.0, (l_6), saturate(1.0 - Light::From(l_2, 1u, l_1.xy).Attenuation)) * (l_5))) : float3(0,0,0) ) + ( Light::Count(l_2) > 2u ? (((pow(saturate(dot(normalize(i.vNormalWs), normalize((normalize(l_3) + normalize(Light::From(l_2, 2u, l_1.xy).Direction))))), (l_4)) * saturate(dot(normalize(i.vNormalWs), normalize(Light::From(l_2, 2u, l_1.xy).Direction)))) * Light::From(l_2, 2u, l_1.xy).Color * Light::From(l_2, 2u, l_1.xy).Attenuation * lerp(1.0, Light::From(l_2, 2u, l_1.xy).Visibility, saturate(l_7)) * lerp(1.0, (l_6), saturate(1.0 - Light::From(l_2, 2u, l_1.xy).Attenuation)) * (l_5))) : float3(0,0,0) ) + ( Light::Count(l_2) > 3u ? (((pow(saturate(dot(normalize(i.vNormalWs), normalize((normalize(l_3) + normalize(Light::From(l_2, 3u, l_1.xy).Direction))))), (l_4)) * saturate(dot(normalize(i.vNormalWs), normalize(Light::From(l_2, 3u, l_1.xy).Direction)))) * Light::From(l_2, 3u, l_1.xy).Color * Light::From(l_2, 3u, l_1.xy).Attenuation * lerp(1.0, Light::From(l_2, 3u, l_1.xy).Visibility, saturate(l_7)) * lerp(1.0, (l_6), saturate(1.0 - Light::From(l_2, 3u, l_1.xy).Attenuation)) * (l_5))) : float3(0,0,0) ));
		float l_9 = (( Light::Count(l_2) > 0u ? ((pow(saturate(dot(normalize(i.vNormalWs), normalize((normalize(l_3) + normalize(Light::From(l_2, 0u, l_1.xy).Direction))))), (l_4)) * saturate(dot(normalize(i.vNormalWs), normalize(Light::From(l_2, 0u, l_1.xy).Direction))) * Light::From(l_2, 0u, l_1.xy).Attenuation * lerp(1.0, Light::From(l_2, 0u, l_1.xy).Visibility, saturate(l_7)) * (l_5))) : 0 ) + ( Light::Count(l_2) > 1u ? ((pow(saturate(dot(normalize(i.vNormalWs), normalize((normalize(l_3) + normalize(Light::From(l_2, 1u, l_1.xy).Direction))))), (l_4)) * saturate(dot(normalize(i.vNormalWs), normalize(Light::From(l_2, 1u, l_1.xy).Direction))) * Light::From(l_2, 1u, l_1.xy).Attenuation * lerp(1.0, Light::From(l_2, 1u, l_1.xy).Visibility, saturate(l_7)) * (l_5))) : 0 ) + ( Light::Count(l_2) > 2u ? ((pow(saturate(dot(normalize(i.vNormalWs), normalize((normalize(l_3) + normalize(Light::From(l_2, 2u, l_1.xy).Direction))))), (l_4)) * saturate(dot(normalize(i.vNormalWs), normalize(Light::From(l_2, 2u, l_1.xy).Direction))) * Light::From(l_2, 2u, l_1.xy).Attenuation * lerp(1.0, Light::From(l_2, 2u, l_1.xy).Visibility, saturate(l_7)) * (l_5))) : 0 ) + ( Light::Count(l_2) > 3u ? ((pow(saturate(dot(normalize(i.vNormalWs), normalize((normalize(l_3) + normalize(Light::From(l_2, 3u, l_1.xy).Direction))))), (l_4)) * saturate(dot(normalize(i.vNormalWs), normalize(Light::From(l_2, 3u, l_1.xy).Direction))) * Light::From(l_2, 3u, l_1.xy).Attenuation * lerp(1.0, Light::From(l_2, 3u, l_1.xy).Visibility, saturate(l_7)) * (l_5))) : 0 ));
		float3 l_10 = l_8 * float3( l_9, l_9, l_9 );
		float3 l_11 = g_vCameraDirWs;
		float3 l_12 = float3( 0, 0, 0 ) + l_11;
		float3 l_13 = l_12 * float3( 100, 100, 100 );
		float2 l_14 = CalculateViewportUv( i.vPositionSs.xy );
		float2 l_15 = g_vViewportSize;
		float l_16 = g_flObjectSpaceTiling;
		float2 l_17 = l_15 / float2( l_16, l_16 );
		float2 l_18 = l_14 * l_17;
		float3 l_19 = l_13 + float3( l_18, 0 );
		float4 l_20 = Tex2DS( g_tSparkleMask, g_sSampler0, l_19.xy );
		float4 l_21 = pow( l_20, float4( 1, 1, 1, 1 ) );
		float2 l_22 = i.vTextureCoords.xy * float2( 1, 1 );
		float l_23 = g_flScreenSpaceTiling;
		float2 l_24 = l_22 * float2( l_23, l_23 );
		float4 l_25 = Tex2DS( g_tSparkleMask_0, g_sSampler0, l_24 );
		float4 l_26 = pow( l_25, float4( 1, 1, 1, 1 ) );
		float4 l_27 = l_21 * l_26;
		float4 l_28 = float4( l_10, 0 ) * l_27;
		float4 l_29 = l_28 * l_0;
		float l_30 = g_flRoughness;
		float l_31 = g_flMetalness;
		
		m.Albedo = l_0.xyz;
		m.Emission = l_29.xyz;
		m.Opacity = 1;
		m.Roughness = l_30;
		m.Metalness = l_31;
		m.AmbientOcclusion = 1;
		
		
		m.AmbientOcclusion = saturate( m.AmbientOcclusion );
		m.Roughness = saturate( m.Roughness );
		m.Metalness = saturate( m.Metalness );
		m.Opacity = saturate( m.Opacity );
		
		// Result node takes normal as tangent space, convert it to world space now
		m.Normal = TransformNormal( m.Normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		
		// for some toolvis shit
		m.WorldTangentU = i.vTangentUWs;
		m.WorldTangentV = i.vTangentVWs;
		m.TextureCoords = i.vTextureCoords.xy;
				
		return ShadingModelStandard::Shade( m );
	}
}
