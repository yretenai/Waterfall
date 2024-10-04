using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Waterfall.Compression;

public sealed partial class LZO {
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
		private const string LIBRARY_NAME = "lzo2";

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int lzo1x_decompress_safe(byte* src, int srcLen, byte* dst, ref int dstLen, void* wrkmem);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int lzo2a_decompress_safe(byte* src, int srcLen, byte* dst, ref int dstLen, void* wrkmem);
	}
}
