using System.IO;
using System.IO.Compression;
using K4os.Compression.LZ4;
using SevenZip.Compression.LZMA;

namespace Waterfall.Compression;

public static class CompressionHelper {
	// could use mspack, but we'd have to implement our own io handlers.
	internal const string LzxLibraryName = "chm";
	internal const string LzoLibraryName = "lzo2";
	internal const string OodleLibraryName = "oo2core";
	internal const string ZstdLibraryName = "zstd";
	internal const string DensityLibraryName = "density";

	static CompressionHelper() {
		NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);
	}

	public static bool EnableLogging { get; set; } = false;

	internal static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) {
		if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle)) {
			return handle;
		}

		var cwd = AppDomain.CurrentDomain.BaseDirectory;
		var target = Path.Combine(cwd, $"runtimes/{RuntimeInformation.RuntimeIdentifier}/native/{libraryName}");

		if (OperatingSystem.IsWindows()) {
			target += ".dll";
		} else if (OperatingSystem.IsLinux()) {
			target += ".so";
		} else if (OperatingSystem.IsMacOS()) {
			target += ".dylib";
		} else {
			return IntPtr.Zero;
		}

		if (File.Exists(target)) {
			return NativeLibrary.Load(target);
		}

		if (!libraryName.StartsWith("lib")) {
			var prefixedName = Path.Combine(Path.GetDirectoryName(target)!, "lib" + Path.GetFileName(target));

			if (File.Exists(prefixedName)) {
				return NativeLibrary.Load(prefixedName);
			}
		}

		return IntPtr.Zero;
	}

	internal static bool CanLoadLibrary(string libraryName) => NativeLibrary.TryLoad(libraryName, out _);

	public static bool IsSupported(CompressionType compressionType) {
		return compressionType switch {
			       CompressionType.None => true,
			       CompressionType.Oodle => CanLoadLibrary(OodleLibraryName),
			       CompressionType.Brotli => true,
			       CompressionType.Zlib => true,
			       CompressionType.Deflate => true,
			       CompressionType.Gzip => true,
			       CompressionType.LZ4 => true,
			       CompressionType.LZ4HC => true,
			       CompressionType.LZO1 => CanLoadLibrary(LzoLibraryName),
			       CompressionType.LZO2 => CanLoadLibrary(LzoLibraryName),
			       CompressionType.LZX => CanLoadLibrary(LzxLibraryName),
			       CompressionType.LZMA => true,
			       CompressionType.SafeLZMA => true,
			       CompressionType.RawLZMA => true,
			       CompressionType.Zstd => CanLoadLibrary(ZstdLibraryName),
			       CompressionType.Density => CanLoadLibrary(DensityLibraryName),
			       _ => false,
		       };
	}


	public static unsafe void Decompress(CompressionType type, Memory<byte> compressed, Memory<byte> decompressed) {
		switch (type) {
			case CompressionType.Zlib: {
				using var dataPin = compressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, compressed.Length);
				try {
					using var zlib = new ZLibStream(dataStream, CompressionMode.Decompress);
					zlib.ReadExactly(decompressed.Span);
				} catch (Exception e) {
					throw new InvalidOperationException("decompression failed", e);
				}

				break;
			}
			case CompressionType.Zstd: {
				using var zstd = new ZStandard();
				if (zstd.Decompress(compressed, decompressed) < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				break;
			}
			case CompressionType.Gzip: {
				using var dataPin = compressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, compressed.Length);
				dataStream.Position = 2;
				using var zlib = new GZipStream(dataStream, CompressionMode.Decompress);
				zlib.ReadExactly(decompressed.Span);

				break;
			}
			case CompressionType.Oodle: {
				if (Oodle.Decompress(compressed, decompressed) < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				break;
			}
			case CompressionType.LZ4:
			case CompressionType.LZ4HC: {
				if (LZ4Codec.Decode(compressed.Span, decompressed.Span) == -1) {
					throw new InvalidOperationException("decompression failed");
				}

				break;
			}
			case CompressionType.Brotli: {
				using var dataPin = compressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, compressed.Length);
				using var brotli = new BrotliStream(dataStream, CompressionMode.Decompress);
				brotli.ReadExactly(decompressed.Span);

				break;
			}
			case CompressionType.LZO1: {
				if (LZO.DecompressLzo1(compressed, decompressed) < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				break;
			}
			case CompressionType.LZO2: {
				if (LZO.DecompressLzo2(compressed, decompressed) < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				break;
			}
			case CompressionType.LZX:
				if (LZX.Decompress(compressed, decompressed, 17) < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				break;
			case CompressionType.LZMA:
			case CompressionType.SafeLZMA:
			case CompressionType.RawLZMA: {
				using var inPin = compressed.Pin();
				using var inStream = new UnmanagedMemoryStream((byte*) inPin.Pointer, compressed.Length, compressed.Length, FileAccess.Read);
				using var outPin = decompressed.Pin();
				using var outStream = new UnmanagedMemoryStream((byte*) outPin.Pointer, decompressed.Length, decompressed.Length, FileAccess.ReadWrite);
				var array = ArrayPool<byte>.Shared.Rent(5);
				try {
					var coder = new Decoder();
					compressed[..5].CopyTo(array);
					coder.SetDecoderProperties(array[..5]);
					inStream.Position = 5;
					switch (type) {
						case CompressionType.LZMA:
							inStream.Position += 16;
							break;
						case CompressionType.SafeLZMA:
							inStream.Position += 8;
							break;
					}

					coder.Code(inStream, outStream, inStream.Length - inStream.Position, outStream.Length, null);
				} finally {
					ArrayPool<byte>.Shared.Return(array);
				}

				break;
			}
			case CompressionType.Density: {
				if (Density.Decompress(compressed, decompressed) < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				break;
			}
			case CompressionType.None:
				compressed.CopyTo(decompressed);
				break;
			default:
				throw new NotSupportedException("Compression type is not supported");
		}
	}
}
