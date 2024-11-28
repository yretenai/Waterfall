namespace Waterfall.Compression;

public sealed partial class LZO {
	static LZO() {
		var version = NativeMethods.lzo_version();
		var cbSize = Unsafe.SizeOf<nint>() * 4 + Unsafe.SizeOf<ulong>() * 2;
		var result = NativeMethods.__lzo_init_v2(version, sizeof(short), sizeof(int), sizeof(long), sizeof(uint), sizeof(uint), Unsafe.SizeOf<nint>(), Unsafe.SizeOf<nint>(), Unsafe.SizeOf<nint>(), cbSize);
		if (result != 0) {
			throw new InvalidOperationException("LZO failed to initialize");
		}
	}

	public static unsafe int DecompressLzo1(Memory<byte> input, Memory<byte> output) {
		using var inPin = input.Pin();
		using var outPin = output.Pin();
		var len = output.Length;
		if (NativeMethods.lzo1x_decompress_safe((byte*) inPin.Pointer, input.Length, (byte*) outPin.Pointer, ref len, null) != 0) {
			return -1;
		}

		return len;
	}

	public static unsafe int DecompressLzo2(Memory<byte> input, Memory<byte> output) {
		using var inPin = input.Pin();
		using var outPin = output.Pin();
		var len = output.Length;
		if (NativeMethods.lzo2a_decompress_safe((byte*) inPin.Pointer, input.Length, (byte*) outPin.Pointer, ref len, null) != 0) {
			return -1;
		}

		return len;
	}

	private static partial class NativeMethods {
		[LibraryImport(CompressionHelper.LzoLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int __lzo_init_v2(uint version, int szShort, int szInt, int szLong, int szUint32, int szUint, int sDict, int szPtr, int szVoid, int szCb);

		[LibraryImport(CompressionHelper.LzoLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial uint lzo_version();

		[LibraryImport(CompressionHelper.LzoLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int lzo1x_decompress_safe(byte* src, int srcLen, byte* dst, ref int dstLen, void* wrkmem);

		[LibraryImport(CompressionHelper.LzoLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int lzo2a_decompress_safe(byte* src, int srcLen, byte* dst, ref int dstLen, void* wrkmem);
	}
}
