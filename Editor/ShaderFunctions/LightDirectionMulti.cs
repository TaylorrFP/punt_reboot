using Editor.ShaderGraph;
using Sandbox;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using static Editor.ShaderGraph.BaseNode;

[Title( "Specular Sum (4)" )]
[Category( "Lighting" )]
[Description( "Per-light Blinn-Phong specular summed for up to 4 lights. ShadowStrength controls how much Light.Visibility (shadows) affects the result." )]
public sealed class SpecularSum4Node : ShaderNode, IErroringNode
{
	[Input( typeof( Vector4 ) )]
	[Title( "Screen Position" )]
	public NodeInput ScreenPosition { get; set; }

	[Input( typeof( Vector3 ) )]
	[Title( "World Position" )]
	public NodeInput WorldPosition { get; set; }

	[Input( typeof( Vector3 ) )]
	[Title( "Normal WS" )]
	public NodeInput NormalWs { get; set; }

	[Input( typeof( Vector3 ) )]
	[Title( "View Dir WS" )]
	public NodeInput ViewDirWs { get; set; }

	[Input( typeof( float ) )]
	[Title( "Spec Power (Optional)" )]
	public NodeInput SpecPowerInput { get; set; }

	[InputDefault( nameof( SpecPowerInput ) )]
	[Title( "Spec Power" )]
	public float DefaultSpecPower { get; set; } = 64.0f;

	[Input( typeof( float ) )]
	[Title( "Strength (Optional)" )]
	public NodeInput StrengthInput { get; set; }

	[InputDefault( nameof( StrengthInput ) )]
	[Title( "Strength" )]
	public float DefaultStrength { get; set; } = 1.0f;

	[Input( typeof( float ) )]
	[Title( "Point Boost (Optional)" )]
	public NodeInput PointBoostInput { get; set; }

	[InputDefault( nameof( PointBoostInput ) )]
	[Title( "Point Boost" )]
	public float DefaultPointBoost { get; set; } = 1.0f;

	[Input( typeof( float ) )]
	[Title( "Shadow Strength (Optional)" )]
	public NodeInput ShadowStrengthInput { get; set; }

	[InputDefault( nameof( ShadowStrengthInput ) )]
	[Title( "Shadow Strength" )]
	public float DefaultShadowStrength { get; set; } = 1.0f; // 0 = ignore shadows, 1 = full shadows

	private static string FloatLiteral( float v )
		=> v.ToString( "0.0####", CultureInfo.InvariantCulture );

	private static string Guarded( int i, string ws, string expr, int components )
	{
		var zero = components == 3 ? "float3(0,0,0)" : "0";
		return $"( Light::Count({ws}) > {i}u ? ({expr}) : {zero} )";
	}

	private static string Norm( string v3 ) => $"normalize({v3})";

	[Output( typeof( Vector3 ) )]
	[Title( "Spec Color Sum" )]
	public NodeResult.Func SpecColorSum => ( GraphCompiler compiler ) =>
	{
		var ssExpr = $"{compiler.Result( ScreenPosition )}";
		var wsExpr = $"{compiler.Result( WorldPosition )}";
		var uvExpr = $"{ssExpr}.xy";

		var nExpr = $"{compiler.Result( NormalWs )}";
		var vExpr = $"{compiler.Result( ViewDirWs )}";

		var pExpr = SpecPowerInput.IsValid ? $"{compiler.Result( SpecPowerInput )}" : FloatLiteral( DefaultSpecPower );
		var sExpr = StrengthInput.IsValid ? $"{compiler.Result( StrengthInput )}" : FloatLiteral( DefaultStrength );
		var pbExpr = PointBoostInput.IsValid ? $"{compiler.Result( PointBoostInput )}" : FloatLiteral( DefaultPointBoost );
		var shExpr = ShadowStrengthInput.IsValid ? $"{compiler.Result( ShadowStrengthInput )}" : FloatLiteral( DefaultShadowStrength );

		string From( int i ) => $"Light::From({wsExpr}, {i}u, {uvExpr})";

		// Boost point/spot-ish lights based on attenuation
		string Boost( int i ) => $"lerp(1.0, ({pbExpr}), saturate(1.0 - {From( i )}.Attenuation))";

		// Shadow control: lerp(1, Visibility, ShadowStrength)
		string ShadowFactor( int i ) => $"lerp(1.0, {From( i )}.Visibility, saturate({shExpr}))";

		string SpecFor( int i )
		{
			var L = Norm( $"{From( i )}.Direction" );
			var N = Norm( nExpr );
			var V = Norm( vExpr );
			var H = Norm( $"({V} + {L})" );

			var ndh = $"saturate(dot({N}, {H}))";
			var ndl = $"saturate(dot({N}, {L}))";

			var spec = $"pow({ndh}, ({pExpr})) * {ndl}";

			return $"(({spec}) * {From( i )}.Color * {From( i )}.Attenuation * {ShadowFactor( i )} * {Boost( i )} * ({sExpr}))";
		}

		var sum =
			$"({Guarded( 0, wsExpr, SpecFor( 0 ), 3 )} + " +
			  $"{Guarded( 1, wsExpr, SpecFor( 1 ), 3 )} + " +
			  $"{Guarded( 2, wsExpr, SpecFor( 2 ), 3 )} + " +
			  $"{Guarded( 3, wsExpr, SpecFor( 3 ), 3 )})";

		return new NodeResult( 3, sum );
	};

	[Output( typeof( float ) )]
	[Title( "Spec Mask Sum" )]
	public NodeResult.Func SpecMaskSum => ( GraphCompiler compiler ) =>
	{
		var ssExpr = $"{compiler.Result( ScreenPosition )}";
		var wsExpr = $"{compiler.Result( WorldPosition )}";
		var uvExpr = $"{ssExpr}.xy";

		var nExpr = $"{compiler.Result( NormalWs )}";
		var vExpr = $"{compiler.Result( ViewDirWs )}";

		var pExpr = SpecPowerInput.IsValid ? $"{compiler.Result( SpecPowerInput )}" : FloatLiteral( DefaultSpecPower );
		var sExpr = StrengthInput.IsValid ? $"{compiler.Result( StrengthInput )}" : FloatLiteral( DefaultStrength );
		var shExpr = ShadowStrengthInput.IsValid ? $"{compiler.Result( ShadowStrengthInput )}" : FloatLiteral( DefaultShadowStrength );

		string From( int i ) => $"Light::From({wsExpr}, {i}u, {uvExpr})";
		string ShadowFactor( int i ) => $"lerp(1.0, {From( i )}.Visibility, saturate({shExpr}))";

		string MaskFor( int i )
		{
			var L = Norm( $"{From( i )}.Direction" );
			var N = Norm( nExpr );
			var V = Norm( vExpr );
			var H = Norm( $"({V} + {L})" );

			var ndh = $"saturate(dot({N}, {H}))";
			var ndl = $"saturate(dot({N}, {L}))";

			return $"(pow({ndh}, ({pExpr})) * {ndl} * {From( i )}.Attenuation * {ShadowFactor( i )} * ({sExpr}))";
		}

		var sum =
			$"({Guarded( 0, wsExpr, MaskFor( 0 ), 1 )} + " +
			  $"{Guarded( 1, wsExpr, MaskFor( 1 ), 1 )} + " +
			  $"{Guarded( 2, wsExpr, MaskFor( 2 ), 1 )} + " +
			  $"{Guarded( 3, wsExpr, MaskFor( 3 ), 1 )})";

		return new NodeResult( 1, sum );
	};

	public List<string> GetErrors()
	{
		var errors = new List<string>();

		if ( !ScreenPosition.IsValid ) errors.Add( "Screen Position input is required." );
		if ( !WorldPosition.IsValid ) errors.Add( "World Position input is required." );
		if ( !NormalWs.IsValid ) errors.Add( "Normal WS input is required." );
		if ( !ViewDirWs.IsValid ) errors.Add( "View Dir WS input is required." );

		return errors;
	}
}
