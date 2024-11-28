namespace Waterfall.Compression;

public static partial class Density {
	public static unsafe int Decompress(Memory<byte> input, Memory<byte> output) {
		using var inPin = input.Pin();
		using var outPin = output.Pin();
		var len = output.Length;
		if (NativeMethods.density_decompress((byte*) inPin.Pointer, input.Length, (byte*) outPin.Pointer, len) != 0) {
			return -1;
		}

		return len;
	}

	private static partial class NativeMethods {
		[LibraryImport(CompressionHelper.DensityLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int density_decompress(byte* src, int srcLen, byte* dst, int dstLen);
	}
}
