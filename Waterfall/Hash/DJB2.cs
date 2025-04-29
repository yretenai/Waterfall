using Waterfall.Hash.Algorithms;

namespace Waterfall.Hash;

public static class DJB2 {
	public const uint Basis = 0x1505;
	public static DJB2Algorithm<uint> Create(uint basis = Basis) => new(basis);
	public static DJB2AlternateAlgorithm<uint> CreateAlternate(uint basis = Basis) => new(basis);

	public static uint HashData(string text, uint basis = Basis) => HashData(MemoryMarshal.AsBytes(text.AsSpan()), basis);

	public static uint HashData(ReadOnlySpan<byte> data, uint basis = Basis) {
		using var algorithm = Create(basis);
		return algorithm.ComputeHashValue(data);
	}

	public static uint AlternateHashData(string text, uint basis = Basis) => HashData(MemoryMarshal.AsBytes(text.AsSpan()), basis);

	public static uint AlternateHashData(ReadOnlySpan<byte> data, uint basis = Basis) {
		using var algorithm = CreateAlternate(basis);
		return algorithm.ComputeHashValue(data);
	}
}
