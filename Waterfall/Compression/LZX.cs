using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
		// could use mspack, but we'd have to implement our own io handlers.
		private const string LIBRARY_NAME = "chm";

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial nint LZXinit(int window);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial void LZXteardown(nint state);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int LZXdecompress(nint state, byte* inpos, byte* outpos, int inlen, int outlen);
	}
}
