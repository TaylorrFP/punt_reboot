using Editor.ShaderGraph;
using Sandbox;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using static Editor.ShaderGraph.BaseNode;

[Title( "Light Direction (Index)" )]
[Category( "Lighting" )]
public sealed class LightDirectionIndexNode : ShaderNode, IErroringNode
{
	[Input( typeof( Vector4 ) )]
	[Title( "Screen Position" )]
	public NodeInput ScreenPosition { get; set; }

	[Input( typeof( Vector3 ) )]
	[Title( "World Position" )]
	public NodeInput WorldPosition { get; set; }

	[Input( typeof( float ) )]
	[Title( "Light Index" )]
	public NodeInput LightIndex { get; set; }

	[InputDefault( nameof( LightIndex ) )]
	[Title( "Default Light Index" )]
	public float DefaultLightIndex { get; set; } = 0.0f;

	private NodeResult GetIndex( GraphCompiler compiler )
	{
		if ( LightIndex.IsValid )
			return compiler.Result( LightIndex );

		// Literal float node result
		var lit = DefaultLightIndex.ToString( "0.0####", CultureInfo.InvariantCulture );
		return new NodeResult( 1, lit );
	}

	[Output( typeof( Vector3 ) )]
	[Title( "Direction WS" )]
	public NodeResult.Func DirectionWs => ( GraphCompiler compiler ) =>
	{
		var ss = compiler.Result( ScreenPosition ); // float4
		var ws = compiler.Result( WorldPosition );  // float3
		var idx = GetIndex( compiler );             // float

		// UPDATED SIGNATURE:
		// Light::From( float3 vPositionWs, uint nLightIndex, float2 vLightMapUV )
		return new NodeResult( 3, $"Light::From( {ws}, (uint)({idx}), {ss}.xy ).Direction" );
	};

	[Output( typeof( Vector3 ) )]
	[Title( "Color" )]
	public NodeResult.Func Color => ( GraphCompiler compiler ) =>
	{
		var ss = compiler.Result( ScreenPosition );
		var ws = compiler.Result( WorldPosition );
		var idx = GetIndex( compiler );

		return new NodeResult( 3, $"Light::From( {ws}, (uint)({idx}), {ss}.xy ).Color" );
	};

	[Output( typeof( float ) )]
	[Title( "Attenuation" )]
	public NodeResult.Func Attenuation => ( GraphCompiler compiler ) =>
	{
		var ss = compiler.Result( ScreenPosition );
		var ws = compiler.Result( WorldPosition );
		var idx = GetIndex( compiler );

		return new NodeResult( 1, $"Light::From( {ws}, (uint)({idx}), {ss}.xy ).Attenuation" );
	};

	public List<string> GetErrors()
	{
		var errors = new List<string>();
		if ( !ScreenPosition.IsValid ) errors.Add( "Screen Position input is required." );
		if ( !WorldPosition.IsValid ) errors.Add( "World Position input is required." );
		return errors;
	}
}
