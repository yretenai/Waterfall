namespace Waterfall.Compression;

public sealed partial class OodleTex {
	public enum BC7PrepDecodeFlags : uint {
		None = 0,
		DestinationIsCached = 1,
		AvoidWideVectors = 2,
	}

	[InlineArray(10), StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct BC7ModeCounts : IEquatable<BC7ModeCounts> {
		public uint Value;

		public bool Equals(BC7ModeCounts other) => ((Span<uint>) other).SequenceEqual(this);

		public override bool Equals(object? obj) => obj is BC7ModeCounts other && Equals(other);

		public override int GetHashCode() {
			var hashCode = new HashCode();
			hashCode.AddBytes(MemoryMarshal.AsBytes((ReadOnlySpan<uint>) this));
			return hashCode.ToHashCode();
		}

		public static bool operator ==(BC7ModeCounts left, BC7ModeCounts right) => left.Equals(right);
		public static bool operator !=(BC7ModeCounts left, BC7ModeCounts right) => !(left == right);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public record struct BC7PrepHeader {
		public uint Version { get; set; }
		public uint Flags { get; set; }
		public BC7ModeCounts ModeCounts { get; set; }
	}

	public static int Decompress(Memory<byte> input, Memory<byte> output) => Decompress(input[Unsafe.SizeOf<BC7PrepHeader>()..], output, MemoryMarshal.Read<BC7PrepHeader>(input.Span));

	public static unsafe int Decompress(Memory<byte> input, Memory<byte> output, BC7PrepHeader header, BC7PrepDecodeFlags flags = BC7PrepDecodeFlags.None) {
		using var inPin = input.Pin();
		using var outPin = output.Pin();
		if (NativeMethods.OodleTexRT_BC7Prep_ReadHeader(ref header, out var blocks, out var payloadSize) != 0) {
			return -1;
		}

		if (payloadSize > output.Length) {
			return -1;
		}

		var scratchBound = NativeMethods.OodleTexRT_BC7Prep_MinDecodeScratchSize(blocks);
		using var scratch = MemoryPool<byte>.Shared.Rent((int) scratchBound);
		using var scratchPin = scratch.Memory.Pin();
		return NativeMethods.OodleTexRT_BC7Prep_Decode((byte*) outPin.Pointer, output.Length, (byte*) inPin.Pointer, input.Length, ref header, flags, (byte*) scratchPin.Pointer, scratch.Memory.Length);
	}

	private static partial class NativeMethods {
		[LibraryImport(CompressionHelper.OodleTexLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int OodleTexRT_BC7Prep_ReadHeader(ref BC7PrepHeader prepHeader, out long numBlocks, out long payloadSize);

		[LibraryImport(CompressionHelper.OodleTexLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial long OodleTexRT_BC7Prep_MinDecodeScratchSize(long numBlocks);

		[LibraryImport(CompressionHelper.OodleTexLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int OodleTexRT_BC7Prep_Decode(byte* output, long outputBuf, byte* bc7prepData, long bc7prepSize, ref BC7PrepHeader header, BC7PrepDecodeFlags flags, byte* scratchBuf, long scratchSize);
	}
}
