using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Waterfall.Compression;

public enum ZSTDDecompressionParameter {
	WindowLogMax = 100,
	Format = 1000,
	StableOutBuffer = 1001,
	ForceIgnoreChecksum = 1002,
	RefMultipleDDicts = 1003,
	DisableHuffmanAssembly = 1004,
	MaxBlockSize = 1005,
}

public enum ZSTDCompressionLevel {
	Fast = 1,
	DecompressFast = 2,
	Greedy = 3,
	Lazy = 4,
	VeryLazy = 5,
	BTLazy = 6,
	BTOptimal = 7,
	BTUltra = 8,
	BTVeryUltra = 9,
}

public enum ZSTDCompressionParameter {
	CompressionLevel = 100,
	WindowLog = 101,
	HashLog = 102,
	ChainLog = 103,
	SearchLog = 104,
	MinMatch = 105,
	TargetLength = 106,
	Strategy = 107,
	TargetCBlockSize = 130,
	EnableLongDistanceMatching = 160,
	LdmHashLog = 161,
	LdmMinMatch = 162,
	LdmBucketSizeLog = 163,
	LdmHashRateLog = 164,
	ContentSizeFlag = 200,
	ChecksumFlag = 201,
	DictIDFlag = 202,
	NbWorkers = 400,
	JobSize = 401,
	OverlapLog = 402,
	RSyncable = 500,
	Format = 10,
	ForceMaxWindow = 1000,
	ForceAttachDict = 1001,
	LiteralCompressionMode = 1002,
	SrcSizeHint = 1004,
	EnableDedicatedDictSearch = 1005,
	StableInBuffer = 1006,
	StableOutBuffer = 1007,
	BlockDelimiters = 1008,
	ValidateSequences = 1009,
	UseBlockSplitter = 1010,
	UseRowMatchFinder = 1011,
	DeterministicRefPrefix = 1012,
	PrefetchCDictTables = 1013,
	EnableSeqProducerFallback = 1014,
	MaxBlockSize = 1015,
	SearchForExternalRepCodes = 1016,
}

public enum ZSTDFormat {
	Normal = 0,
	Magicless = 1,
}

public enum ZSTDDictLoadMethod {
	ByCopy = 0,
	ByRef = 1,
}

public enum ZSTDDictContentType {
	Auto = 0,
	RawContent = 1,
	Full = 2,
}

public enum ZSTDForceIgnoreChecksum {
	ValidateChecksum = 0,
	IgnoreChecksum = 1,
}

public enum ZSTDRefMultipleDDicts {
	SingleDDict = 0,
	MultipleDDicts = 1,
}

public sealed partial class ZStandard : IDisposable {
	public ZStandard() {
		DContext = NativeMethods.ZSTD_createDCtx();
		if (DContext < 0) {
			throw new UnreachableException();
		}

		CContext = NativeMethods.ZSTD_createCCtx();
		if (CContext < 0) {
			throw new UnreachableException();
		}
	}

	private Memory<byte> Dict { get; set; } = Memory<byte>.Empty;
	private nint DContext { get; set; }
	private nint CContext { get; set; }
	private MemoryHandle DictPin { get; set; }

	public void Dispose() {
		ReleaseUnmanagedResources();
		GC.SuppressFinalize(this);
	}

	public bool SetParameter(ZSTDDecompressionParameter parameter, int value) => NativeMethods.ZSTD_DCtx_setParameter(DContext, parameter, value) == 0;
	public bool SetParameter(ZSTDCompressionParameter parameter, int value) => NativeMethods.ZSTD_CCtx_setParameter(CContext, parameter, value) == 0;

	public int GetParameter(ZSTDDecompressionParameter parameter) => NativeMethods.ZSTD_DCtx_getParameter(CContext, parameter, out var value) == 0 ? value : 0;
	public int GetParameter(ZSTDCompressionParameter parameter) => NativeMethods.ZSTD_CCtx_getParameter(CContext, parameter, out var value) == 0 ? value : 0;

	public unsafe bool LoadDict(Memory<byte> dict, ZSTDDictLoadMethod loadMethod = ZSTDDictLoadMethod.ByRef, ZSTDDictContentType contentType = ZSTDDictContentType.Auto) {
		if (dict.IsEmpty) {
			return UnloadDict();
		}

		Dict = dict;
		DictPin = Dict.Pin();
		var result = NativeMethods.ZSTD_DCtx_loadDictionary_advanced(DContext, (byte*) DictPin.Pointer, Dict.Length, loadMethod, contentType);
		if (result < 0) {
			FreeDict();
			return false;
		}

		result = NativeMethods.ZSTD_CCtx_loadDictionary_advanced(CContext, (byte*) DictPin.Pointer, Dict.Length, loadMethod, contentType);
		if (result < 0) {
			FreeDict();
			return false;
		}

		if (loadMethod != ZSTDDictLoadMethod.ByRef) {
			FreeDict();
		}

		return true;
	}

	public unsafe int Decompress(Memory<byte> input, Memory<byte> output) {
		using var inPin = input.Pin();
		using var outPin = output.Pin();
		return (int) NativeMethods.ZSTD_decompressDCtx(DContext, (byte*) outPin.Pointer, output.Length, (byte*) inPin.Pointer, input.Length);
	}

	public IMemoryOwner<byte>? Decompress(Memory<byte> input, MemoryPool<byte>? pool = null) {
		var size = GetDecompressBound(input);
		var output = (pool ?? MemoryPool<byte>.Shared).Rent(size);
		if (Decompress(input, output.Memory[..size]) != -1) {
			return output;
		}

		output.Dispose();
		return null;
	}

	public unsafe long Compress(Memory<byte> input, Memory<byte> output, ZSTDCompressionLevel compressionLevel) {
		using var inPin = input.Pin();
		using var outPin = output.Pin();
		return NativeMethods.ZSTD_compressCCtx(DContext, (byte*) outPin.Pointer, output.Length, (byte*) inPin.Pointer, input.Length, compressionLevel);
	}

	public IMemoryOwner<byte>? Compress(Memory<byte> input, ZSTDCompressionLevel compressionLevel, MemoryPool<byte>? pool = null) {
		var size = GetCompressBound(input);
		var output = (pool ?? MemoryPool<byte>.Shared).Rent(size);
		if (Compress(input, output.Memory[..size], compressionLevel) != -1) {
			return output;
		}

		output.Dispose();
		return null;
	}

	public static int GetCompressBound(Memory<byte> bytes) => (int) NativeMethods.ZSTD_compressBound(bytes.Length);

	public static unsafe int GetDecompressBound(Memory<byte> bytes) {
		using var pin = bytes.Pin();
		return (int) NativeMethods.ZSTD_decompressBound((byte*) pin.Pointer, bytes.Length);
	}

	public unsafe bool UnloadDict() {
		var result1 = NativeMethods.ZSTD_DCtx_loadDictionary_advanced(DContext, (byte*) nint.Zero, 0, 0, 0);
		var result2 = NativeMethods.ZSTD_CCtx_loadDictionary_advanced(CContext, (byte*) nint.Zero, 0, 0, 0);
		if ((nint) DictPin.Pointer != IntPtr.Zero) {
			FreeDict();
		}

		return result1 == 0 && result2 == 0;
	}

	private unsafe void FreeDict() {
		if ((nint) DictPin.Pointer != IntPtr.Zero) {
			DictPin.Dispose();
			DictPin = default;
			Dict = Memory<byte>.Empty;
		}
	}

	private void FreeContext() {
		if (DContext > 0) {
			DContext = NativeMethods.ZSTD_freeDCtx(DContext);
		}

		if (CContext > 0) {
			CContext = NativeMethods.ZSTD_freeCCtx(CContext);
		}
	}

	private void ReleaseUnmanagedResources() {
		FreeContext();
		FreeDict();
	}

	~ZStandard() {
		ReleaseUnmanagedResources();
	}

	private static partial class NativeMethods {
		private const string LIBRARY_NAME = "zstd";

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial nint ZSTD_DCtx_setParameter(nint dctx, ZSTDDecompressionParameter param, int value);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial nint ZSTD_CCtx_setParameter(nint cctx, ZSTDCompressionParameter param, int value);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial nint ZSTD_DCtx_getParameter(nint dctx, ZSTDDecompressionParameter param, out int value);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial nint ZSTD_CCtx_getParameter(nint cctx, ZSTDCompressionParameter param, out int value);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial nint ZSTD_DCtx_loadDictionary_advanced(nint dctx, byte* dict, long dictSize, ZSTDDictLoadMethod loadMethod, ZSTDDictContentType contentType);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial nint ZSTD_CCtx_loadDictionary_advanced(nint cctx, byte* dict, long dictSize, ZSTDDictLoadMethod loadMethod, ZSTDDictContentType contentType);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial nint ZSTD_createDCtx();

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial nint ZSTD_createCCtx();

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial nint ZSTD_freeDCtx(nint dctx);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static partial nint ZSTD_freeCCtx(nint cctx);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial nint ZSTD_decompressDCtx(nint dctx, byte* dst, long dstCapacity, byte* src, long srcSize);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial nint ZSTD_compressCCtx(nint cctx, byte* dst, long dstCapacity, byte* src, long srcSize, ZSTDCompressionLevel compressionLevel);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial long ZSTD_decompressBound(byte* src, long srcSize);

		[LibraryImport(LIBRARY_NAME), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
		public static unsafe partial long ZSTD_compressBound(long size);
	}
}
