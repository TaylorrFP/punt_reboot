
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
	CreateInputTexture2D( Texture_ps_0, Srgb, 8, "None", "_color", ",0/,0/0", DefaultFile( "prefabs/pitches/dev/shaderdev/sparkle/sparklemask.tga" ) );
	CreateInputTexture2D( Texture_ps_1, Srgb, 8, "None", "_color", ",0/,0/0", DefaultFile( "prefabs/pitches/dev/shaderdev/sparkle/sparklemask.tga" ) );
	Texture2D g_tTexture_ps_0 < Channel( RGBA, Box( Texture_ps_0 ), Linear ); OutputFormat( DXT5 ); SrgbRead( False ); >;
	Texture2D g_tTexture_ps_1 < Channel( RGBA, Box( Texture_ps_1 ), Linear ); OutputFormat( DXT5 ); SrgbRead( False ); >;
	float4 g_vBaseColour < UiType( Color ); UiGroup( ",0/,0/0" ); Default4( 0.10, 0.21, 0.05, 1.00 ); >;
	float g_flObjectSpaceTiling < UiGroup( ",0/,0/0" ); Default1( 256 ); Range1( 0, 1024 ); >;
	float g_flScreenSpaceTiling < UiGroup( ",0/,0/0" ); Default1( 10 ); Range1( 0, 100 ); >;
	float g_flGlitterHighlight < UiGroup( ",0/,0/0" ); Default1( 30 ); Range1( 0, 1000 ); >;
	float g_flFresnelPow < UiGroup( ",0/,0/0" ); Default1( 3 ); Range1( 0, 10 ); >;
	float g_flFresnelStrength < UiGroup( ",0/,0/0" ); Default1( 1 ); Range1( 0, 10 ); >;
	float g_flHighlightStrength < UiGroup( ",0/,0/0" ); Default1( 2 ); Range1( 0, 100 ); >;
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
		float3 l_1 = g_vCameraDirWs;
		float3 l_2 = float3( 0, 0, 0 ) + l_1;
		float3 l_3 = l_2 * float3( 100, 100, 100 );
		float2 l_4 = CalculateViewportUv( i.vPositionSs.xy );
		float2 l_5 = g_vViewportSize;
		float l_6 = g_flObjectSpaceTiling;
		float2 l_7 = l_5 / float2( l_6, l_6 );
		float2 l_8 = l_4 * l_7;
		float3 l_9 = l_3 + float3( l_8, 0 );
		float4 l_10 = Tex2DS( g_tTexture_ps_0, g_sSampler0, l_9.xy );
		float4 l_11 = pow( l_10, float4( 1, 1, 1, 1 ) );
		float2 l_12 = i.vTextureCoords.xy * float2( 1, 1 );
		float l_13 = g_flScreenSpaceTiling;
		float2 l_14 = l_12 * float2( l_13, l_13 );
		float4 l_15 = Tex2DS( g_tTexture_ps_1, g_sSampler0, l_14 );
		float4 l_16 = pow( l_15, float4( 1, 1, 1, 1 ) );
		float4 l_17 = l_11 * l_16;
		float2 l_18 = CalculateViewportUv( i.vPositionSs.xy );
		float3 l_19 = i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz;
		float3 l_20 = Light::From( l_19, (uint)(0.0), l_18.xy ).Direction;
		float l_21 = dot( i.vNormalWs, l_20 );
		float l_22 = pow( l_21, 3 );
		float3 l_23 = CalculatePositionToCameraDirWs( i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz );
		float3 l_24 = normalize( l_23 );
		float3 l_25 = l_20 + l_24;
		float3 l_26 = normalize( l_25 );
		float l_27 = dot( i.vNormalWs, l_26 );
		float l_28 = g_flGlitterHighlight;
		float l_29 = pow( l_27, l_28 );
		float l_30 = g_flFresnelPow;
		float3 l_31 = CalculatePositionToCameraDirWs( i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz );
		float3 l_32 = pow( 1.0 - dot( normalize( i.vNormalWs ), normalize( l_31 ) ), l_30 );
		float l_33 = g_flFresnelStrength;
		float3 l_34 = l_32 * float3( l_33, l_33, l_33 );
		float3 l_35 = float3( l_29, l_29, l_29 ) + l_34;
		float3 l_36 = float3( l_22, l_22, l_22 ) * l_35;
		float3 l_37 = l_36 * float3( 5, 5, 5 );
		float3 l_38 = max( l_37, 0 );
		float4 l_39 = l_17 * float4( l_38, 0 );
		float l_40 = g_flHighlightStrength;
		float4 l_41 = l_39 * float4( l_40, l_40, l_40, l_40 );
		float4 l_42 = l_0 * l_41;
		float l_43 = g_flRoughness;
		float l_44 = g_flMetalness;
		
		m.Albedo = l_0.xyz;
		m.Emission = l_42.xyz;
		m.Opacity = 1;
		m.Roughness = l_43;
		m.Metalness = l_44;
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
