using System.Numerics;
using Waterfall.Hash.Algorithms;
using Waterfall.Hash.Basis;

namespace Waterfall.Hash;

public static class CRC {
	public static CRCAlgorithm<ulong> Create(CRCVariant<ulong> variant) => new(variant.Polynomial, variant.Init, variant.Xor, variant.ReflectIn, variant.ReflectOut);
	public static CRCAlgorithm<uint> Create(CRCVariant<uint> variant) => new(variant.Polynomial, variant.Init, variant.Xor, variant.ReflectIn, variant.ReflectOut);
	public static CRCAlgorithm<ushort> Create(CRCVariant<ushort> variant) => new(variant.Polynomial, variant.Init, variant.Xor, variant.ReflectIn, variant.ReflectOut);
	public static CRCAlgorithm<byte> Create(CRCVariant<byte> variant) => new(variant.Polynomial, variant.Init, variant.Xor, variant.ReflectIn, variant.ReflectOut);

	public static T HashData<T>(CRCVariant<T> variant, string text) where T : unmanaged, IConvertible, INumber<T>, IBitwiseOperators<T, T, T>, IShiftOperators<T, int, T>, IMinMaxValue<T> => HashData(variant, MemoryMarshal.AsBytes(text.AsSpan()));

	public static T HashData<T>(CRCVariant<T> variant, ReadOnlySpan<byte> data) where T : unmanaged, IConvertible, INumber<T>, IBitwiseOperators<T, T, T>, IShiftOperators<T, int, T>, IMinMaxValue<T> {
		using var algorithm = new CRCAlgorithm<T>(variant.Polynomial, variant.Init, variant.Xor, variant.ReflectIn, variant.ReflectOut);
		return algorithm.ComputeHashValue(data);
	}
}
