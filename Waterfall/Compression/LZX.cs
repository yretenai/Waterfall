namespace Waterfall.Compression;

public sealed partial class LZX {
	public static unsafe int Decompress(Memory<byte> input, Memory<byte> output, int windowSize) {
		using var inPin = input.Pin();
		using var outPin = output.Pin();
		var len = output.Length;
		var state = NativeMethods.LZXinit(windowSize);
		if (state == nint.Zero) {
			return -1;
		}

		try {
			if (NativeMethods.LZXdecompress(state, (byte*) inPin.Pointer, (byte*) outPin.Pointer, input.Length, output.Length) != 0) {
				return -1;
			}
		} finally {
			NativeMethods.LZXteardown(state);
		}

		return len;
	}

	private static partial class NativeMethods {
		[LibraryImport(CompressionHelper.LzxLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial nint LZXinit(int window);

		[LibraryImport(CompressionHelper.LzxLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial void LZXteardown(nint state);

		[LibraryImport(CompressionHelper.LzxLibraryName), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int LZXdecompress(nint state, byte* inpos, byte* outpos, int inlen, int outlen);
	}
}
