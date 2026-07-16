using System.Runtime.CompilerServices;

// EditMode テストアセンブリから internal API（RunClassificationDetailed /
// WindowClassification / GetEnabledBiomeTypesPublic 等）を参照可能にする。
[assembly: InternalsVisibleTo("MapGenerator.Tests.EditMode")]
