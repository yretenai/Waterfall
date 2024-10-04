using System;
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Waterfall.Compression;

public static partial class Oodle {
	public enum OodleLZ_CompressionLevel {
		None = 0,
		SuperFast = 1,
		VeryFast = 2,
		Fast = 3,
		Normal = 4,
		Optimal1 = 5,
		Optimal2 = 6,
		Optimal3 = 7,
		Optimal4 = 8,
		Optimal5 = 9,
		HyperFast1 = -1,
		HyperFast2 = -2,
		HyperFast3 = -3,
		HyperFast4 = -4,
		HyperFast = HyperFast1,
		Optimal = Optimal2,
		Max = Optimal5,
		Min = HyperFast4,
		Invalid = 0x40000000,
	}

	public enum OodleLZ_Compressor {
		Invalid = -1,
		LZH = 0,
		LZHLW = 1,
		LZNIB = 2,
		None = 3,
		LZB16 = 4,
		LZBLW = 5,
		LZA = 6,
		LZNA = 7,
		Kraken = 8,
		Mermaid = 9,
		BitKnit = 10,
		Selkie = 11,
		Hydra = 12,
		Leviathan = 13,
	}

	public enum OodleLZ_Decode_ThreadPhase {
		ThreadPhase1 = 1,
		ThreadPhase2 = 2,
		ThreadPhaseAll = 3,
		Unthreaded = ThreadPhaseAll,
	}

	public enum OodleLZ_Jobify {
		Default = 0,
		Disable = 1,
		Normal = 2,
		Aggressive = 3,
	}

	public enum OodleLZ_Profile {
		OodleLZ_Profile_Main = 0,
		OodleLZ_Profile_Reduced = 1,
	}

	public enum OodleLZ_Verbosity {
		None = 0,
		Minimal = 1,
		Some = 2,
		Lots = 3,
	}

	static Oodle() {
		if(CompressionHelper.EnableLogging) {
			// not as a dllimport because these may not exist.
			var handle = CompressionHelper.DllImportResolver(NativeMethods.LIBRARY_NAME, Assembly.GetExecutingAssembly(), DllImportSearchPath.SafeDirectories);
			if (handle != IntPtr.Zero) {
				if (NativeLibrary.TryGetExport(handle, "OodleCore_Plugin_Printf_Verbose", out var callbackAddress)) {
					var callback = Marshal.GetDelegateForFunctionPointer<NativeMethods.OodleCore_Plugin_Printf>(callbackAddress);
					NativeMethods.OodleCore_Plugins_SetPrintf(callback);
				} else if (NativeLibrary.TryGetExport(handle, "OodleCore_Plugin_Printf_Default", out callbackAddress)) {
					var callback = Marshal.GetDelegateForFunctionPointer<NativeMethods.OodleCore_Plugin_Printf>(callbackAddress);
					NativeMethods.OodleCore_Plugins_SetPrintf(callback);
				}

				if (NativeLibrary.TryGetExport(handle, "OodleCore_Plugin_DisplayAssertion_Default", out callbackAddress)) {
					var callback = Marshal.GetDelegateForFunctionPointer<NativeMethods.OodleCore_Plugin_DisplayAssertion>(callbackAddress);
					NativeMethods.OodleCore_Plugins_SetAssertion(callback);
				}
			}

			NativeMethods.Oodle_LogHeader();

			var version = 0u;
			var expected = CreateOodleVersion(9, 0);
			if (NativeMethods.Oodle_CheckVersion(expected, ref version) != 1) {
				Console.WriteLine("Invalid Oodle version! Expected a version compatible with {Expected} ({Version:X8}), got {Parsed} ({Result:X8})", ParseOodleVersion(expected), expected, ParseOodleVersion(version), version);
			} else {
				Console.WriteLine("Loaded Oodle Version {Version}", ParseOodleVersion(version));
			}
		}

		BlockDecoderMemorySizeNeeded = NativeMethods.OodleLZDecoder_MemorySizeNeeded(OodleLZ_Compressor.Invalid, -1);
	}

	public static int BlockDecoderMemorySizeNeeded { get; }

	public static string ParseOodleVersion(uint value) {
		var check = value >> 28;
		var provider = (value >> 24) & 0xF;
		var major = (value >> 16) & 0xFF;
		var minor = (value >> 8) & 0xFF;
		var table = value & 0xFF;
		return $"{check}.{major}.{minor} (provider: {provider:X1}, seek: {table})";
	}

	public static uint CreateOodleVersion(int major, int minor, int seekTableSize = 48) => (46u << 24) | (uint) (major << 16) | (uint) (minor << 8) | (uint) seekTableSize;

	public static unsafe int Decompress(Memory<byte> input, Memory<byte> output) {
		using var inPin = input.Pin();
		using var outPin = output.Pin();
		using var pool = MemoryPool<byte>.Shared.Rent(BlockDecoderMemorySizeNeeded);
		using var poolPin = pool.Memory.Pin();
		return NativeMethods.OodleLZ_Decompress((byte*) inPin.Pointer, input.Length, (byte*) outPin.Pointer, output.Length, true, false, OodleLZ_Verbosity.Minimal, null, 0, null, null, (byte*) poolPin.Pointer, BlockDecoderMemorySizeNeeded, OodleLZ_Decode_ThreadPhase.Unthreaded);
	}

	public static IMemoryOwner<byte>? Decompress(Memory<byte> input, MemoryPool<byte>? pool = null) {
		var size = GetDecodeBufferSize(input, false);
		var output = (pool ?? MemoryPool<byte>.Shared).Rent(size);
		if (Decompress(input, output.Memory[..size]) != -1) {
			return output;
		}

		output.Dispose();
		return null;
	}

	public static IMemoryOwner<byte>? Compress(Memory<byte> input, OodleLZ_Compressor compressor, OodleLZ_CompressionLevel level, MemoryPool<byte>? pool = null) {
		var size = GetCompressedBufferSize(compressor, input.Length);
		var output = (pool ?? MemoryPool<byte>.Shared).Rent(size);
		if (Compress(input, output.Memory[..size], compressor, level) != -1) {
			return output;
		}

		output.Dispose();
		return null;
	}

	private static int Compress(Memory<byte> input, Memory<byte> output, OodleLZ_Compressor compressor, OodleLZ_CompressionLevel level) {
		var options = GetDefaultOptions(compressor, level);
		return Compress(input, output, Memory<byte>.Empty, compressor, level, options);
	}

	private static unsafe int Compress(Memory<byte> input, Memory<byte> output, Memory<byte> dict, OodleLZ_Compressor compressor, OodleLZ_CompressionLevel level, OodleLZ_CompressOptions options) {
		var compressorOptions = options;
		compressorOptions.Unused1 = compressorOptions.Unused2 = compressorOptions.Unused3 = compressorOptions.Unused4 = compressorOptions.Unused5 = compressorOptions.Unused6 = 0;

		int scratchBound;
		fixed (OodleLZ_CompressOptions* compressorOptionsPin = &Unsafe.AsRef(ref compressorOptions)) {
			scratchBound = (int) NativeMethods.OodleLZ_GetCompressScratchMemBound(compressor, level, input.Length + compressorOptions.DictionarySize, compressorOptionsPin);
		}

		if (scratchBound == -1) {
			scratchBound = BlockDecoderMemorySizeNeeded;
		}

		options.DictionarySize = dict.Length;

		using var inPin = input.Pin();
		using var outPin = output.Pin();
		using var dictPin = dict.Pin();
		using var scratch = MemoryPool<byte>.Shared.Rent(scratchBound);
		using var scratchPin = scratch.Memory.Pin();
		fixed (OodleLZ_CompressOptions* compressorOptionsPin = &Unsafe.AsRef(ref compressorOptions)) {
			return NativeMethods.OodleLZ_Compress(compressor, (byte*) inPin.Pointer, input.Length, (byte*) outPin.Pointer, level, compressorOptionsPin, (byte*) dictPin.Pointer, nint.Zero, (byte*) scratchPin.Pointer, scratchBound);
		}
	}

	public static unsafe int Compress(Memory<byte> input, Memory<byte> output) {
		using var inPin = input.Pin();
		using var outPin = output.Pin();
		using var pool = MemoryPool<byte>.Shared.Rent(BlockDecoderMemorySizeNeeded);
		using var poolPin = pool.Memory.Pin();
		return NativeMethods.OodleLZ_Decompress((byte*) inPin.Pointer, input.Length, (byte*) outPin.Pointer, output.Length, true, false, OodleLZ_Verbosity.Minimal, null, 0, null, null, (byte*) poolPin.Pointer, BlockDecoderMemorySizeNeeded, OodleLZ_Decode_ThreadPhase.Unthreaded);
	}

	private static unsafe OodleLZ_CompressOptions GetDefaultOptions(OodleLZ_Compressor compressor, OodleLZ_CompressionLevel level) {
		var options = Unsafe.Read<OodleLZ_CompressOptions>(NativeMethods.OodleLZ_CompressOptions_GetDefault(compressor, level));
		options.Unused1 = options.Unused2 = options.Unused3 = options.Unused4 = options.Unused5 = options.Unused6 = 0;
		return options;
	}

	public static int GetDecodeBufferSize(Memory<byte> input, bool corruptionPossible) => (int) NativeMethods.OodleLZ_GetDecodeBufferSize(GetCompressor(input), input.Length, corruptionPossible);

	public static int GetCompressedBufferSize(OodleLZ_Compressor compressor, int length) => (int) NativeMethods.OodleLZ_GetCompressedBufferSizeNeeded(compressor, length);

	public static unsafe OodleLZ_Compressor GetCompressor(Memory<byte> input) {
		using var inPin = input.Pin();
		var independent = false;
		return NativeMethods.OodleLZ_GetFirstChunkCompressor((byte*) inPin.Pointer, input.Length, ref independent);
	}

	public static string GetCompressorName(Memory<byte> input) {
		var compressor = GetCompressor(input);
		return GetCompressorName(compressor);
	}

	public static string GetCompressorName(OodleLZ_Compressor compressor) => NativeMethods.OodleLZ_Compressor_GetName(compressor);

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	public record struct OodleLZ_CompressOptions {
		public int Unused1 { get; set; }
		public int MinMatchLen { get; set; }
		public bool SeekChunkReset { get; set; }
		public int SeekChunkLen { get; set; }
		public OodleLZ_Profile Profile { get; set; }
		public int DictionarySize { get; set; }
		public int SpaceSpeedTradeoffBytes { get; set; }
		public int Unused2 { get; set; }
		public bool SendQuantumCRCs { get; set; }
		public int MaxLocalDictionarySize { get; set; }
		public bool MakeLongRangeMatcher { get; set; }
		public int MatchTableSizeLog2 { get; set; }
		public OodleLZ_Jobify Jobify { get; set; }
		public nint JobifyUserPtr { get; set; }
		public int FarMatchMinLen { get; set; }
		public int FarMatchOffsetLog2 { get; set; }
		public int Unused3 { get; set; }
		public int Unused4 { get; set; }
		public int Unused5 { get; set; }
		public int Unused6 { get; set; }
	}

	private static partial class NativeMethods {
		[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public delegate int OodleCore_Plugin_DisplayAssertion([MarshalAs(UnmanagedType.LPStr)] string file, int line, [MarshalAs(UnmanagedType.LPStr)] string function, [MarshalAs(UnmanagedType.LPStr)] string message);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public delegate int OodleCore_Plugin_Printf(int verboseLevel, [MarshalAs(UnmanagedType.LPStr)] string file, int line, [MarshalAs(UnmanagedType.LPStr)] string format);

		internal const string LIBRARY_NAME = "oo2core";

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int OodleLZ_Decompress(byte* srcBuf, long srcSize, byte* rawBuf, long rawSize, [MarshalAs(UnmanagedType.I4)] bool fuzzSafe, [MarshalAs(UnmanagedType.I4)] bool checkCRC, OodleLZ_Verbosity verbosity, byte* decBufBase, long decBufSize, void* fpCallback, void* callbackUserData, byte* decoderMemory, long decoderMemorySize, OodleLZ_Decode_ThreadPhase threadPhase);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial int OodleLZ_Compress(OodleLZ_Compressor compressor, byte* rawBuf, long rawSize, byte* compBuf, OodleLZ_CompressionLevel level, OodleLZ_CompressOptions* options, byte* dictionaryBase, nint lrm, byte* scratchMem, long scratchSize);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial OodleLZ_Compressor OodleLZ_GetFirstChunkCompressor(byte* srcBuf, long srcSize, [MarshalAs(UnmanagedType.I4)] ref bool independent);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		[return: MarshalAs(UnmanagedType.LPStr)]
		public static partial string OodleLZ_Compressor_GetName(OodleLZ_Compressor compressor);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial void Oodle_LogHeader();

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial int Oodle_CheckVersion(uint oodleHeaderVersion, ref uint oodleLibVersion);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		[return: MarshalAs(UnmanagedType.FunctionPtr)]
		public static partial void OodleCore_Plugins_SetPrintf([MarshalAs(UnmanagedType.FunctionPtr)] OodleCore_Plugin_Printf rrRawPrintf);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		[return: MarshalAs(UnmanagedType.FunctionPtr)]
		public static partial void OodleCore_Plugins_SetAssertion([MarshalAs(UnmanagedType.FunctionPtr)] OodleCore_Plugin_DisplayAssertion rrDisplayAssertion);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial int OodleLZDecoder_MemorySizeNeeded(OodleLZ_Compressor compressor, long size);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial long OodleLZ_GetCompressedBufferSizeNeeded(OodleLZ_Compressor compressor, long size);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial long OodleLZ_GetDecodeBufferSize(OodleLZ_Compressor compressor, long size, [MarshalAs(UnmanagedType.I4)] bool corruptionPossible);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial long OodleLZ_GetCompressScratchMemBound(OodleLZ_Compressor compressor, OodleLZ_CompressionLevel level, long size, OodleLZ_CompressOptions* options);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial OodleLZ_CompressOptions* OodleLZ_CompressOptions_GetDefault(OodleLZ_Compressor compressor, OodleLZ_CompressionLevel level);
	}
}
