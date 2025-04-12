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
	internal const string OodleTexLibraryName = "oo2texrt";
	internal const string ZstdLibraryName = "zstd";
	internal const string DensityLibraryName = "density";

	static CompressionHelper() {
		NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);
	}

	public static bool EnableLogging { get; set; } = false;

	internal static nint DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) {
		if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle)) {
			return handle;
		}

		var name = Path.GetFileNameWithoutExtension(libraryName);
		var cwd = AppDomain.CurrentDomain.BaseDirectory;

		string ext;
		if (OperatingSystem.IsWindows()) {
			ext = ".dll";
		} else if (OperatingSystem.IsLinux()) {
			ext = ".so";
		} else if (OperatingSystem.IsMacOS()) {
			ext = ".dylib";
		} else {
			return nint.Zero;
		}

		foreach (var dir in new[] { Path.Combine(cwd, $"runtimes/{RuntimeInformation.RuntimeIdentifier}/native/"), cwd }) {
			foreach (var libName in new[] { name, "lib" + name, name + "-0", $"lib{name}-0" }) {
				var target = Path.Combine(dir, libName) + ext;
				if (File.Exists(target)) {
					var ptr = NativeLibrary.Load(target);
					if (ptr != nint.Zero) {
						return ptr;
					}
				}
			}
		}

		return nint.Zero;
	}

	internal static bool CanLoadLibrary(string libraryName) => NativeLibrary.TryLoad(libraryName, out _);

	public static bool IsSupported(CompressionType compressionType) {
		return compressionType switch {
			       CompressionType.None => true,
			       CompressionType.Oodle => CanLoadLibrary(OodleLibraryName),
			       CompressionType.OodleTex => CanLoadLibrary(OodleTexLibraryName),
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

	public static unsafe int Decompress(CompressionType type, Memory<byte> compressed, Memory<byte> decompressed) {
		switch (type) {
			case CompressionType.Zlib: {
				using var dataPin = compressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, compressed.Length);
				using var zlib = new ZLibStream(dataStream, CompressionMode.Decompress);
				zlib.ReadExactly(decompressed.Span);

				return decompressed.Length;
			}
			case CompressionType.Deflate: {
				using var dataPin = compressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, compressed.Length);
				using var deflate = new DeflateStream(dataStream, CompressionMode.Decompress);
				deflate.ReadExactly(decompressed.Span);

				return decompressed.Length;
			}
			case CompressionType.Zstd: {
				using var zstd = new ZStandard();
				var n = zstd.Decompress(compressed, decompressed);
				if (n < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				return n;
			}
			case CompressionType.Gzip: {
				using var dataPin = compressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, compressed.Length);
				dataStream.Position = 2;
				using var zlib = new GZipStream(dataStream, CompressionMode.Decompress);
				zlib.ReadExactly(decompressed.Span);

				return decompressed.Length;
			}
			case CompressionType.Oodle: {
				var n = Oodle.Decompress(compressed, decompressed);
				if (n < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				return n;
			}
			case CompressionType.OodleTex: {
				var n = OodleTex.Decompress(compressed, decompressed);
				if (n < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				return n;
			}
			case CompressionType.LZ4:
			case CompressionType.LZ4HC: {
				var n = LZ4Codec.Decode(compressed.Span, decompressed.Span);
				if (n == -1) {
					throw new InvalidOperationException("decompression failed");
				}

				return n;
			}
			case CompressionType.Brotli: {
				using var dataPin = compressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, compressed.Length);
				using var brotli = new BrotliStream(dataStream, CompressionMode.Decompress);
				brotli.ReadExactly(decompressed.Span);

				return decompressed.Length;
			}
			case CompressionType.LZO1: {
				var n = LZO.DecompressLzo1(compressed, decompressed);
				if (n < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				return n;
			}
			case CompressionType.LZO2: {
				var n = LZO.DecompressLzo2(compressed, decompressed);
				if (n < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				return n;
			}
			case CompressionType.LZX: {
				var n = LZX.Decompress(compressed, decompressed, 17);
				if (n < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				return n;
			}
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
					outStream.Flush();
				} finally {
					ArrayPool<byte>.Shared.Return(array);
				}

				return (int) outStream.Length;
			}
			case CompressionType.Density: {
				var n = Density.Decompress(compressed, decompressed);
				if (n < 0) {
					throw new InvalidOperationException("decompression failed");
				}

				return n;
			}
			case CompressionType.None:
				compressed.CopyTo(decompressed);
				return decompressed.Length;
			default:
				throw new NotSupportedException("Compression type is not supported");
		}
	}

	public static unsafe int Compress(CompressionType type, Memory<byte> compressed, Memory<byte> decompressed, CompressionLevel compressionLevel = CompressionLevel.Fastest) {
		switch (type) {
			case CompressionType.Zlib: {
				using var dataPin = decompressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, decompressed.Length);
				using var zlib = new ZLibStream(dataStream, compressionLevel);
				zlib.Write(compressed.Span);
				zlib.Flush();

				return (int) zlib.Position;
			}
			case CompressionType.Deflate: {
				using var dataPin = decompressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, decompressed.Length);
				using var zlib = new DeflateStream(dataStream, compressionLevel);
				zlib.Write(compressed.Span);
				zlib.Flush();

				return (int) zlib.Position;
			}
			case CompressionType.Zstd: {
				using var zstd = new ZStandard();
				var n = (int) zstd.Compress(decompressed, compressed,
				                            compressionLevel switch {
					                            CompressionLevel.Optimal => ZSTDCompressionLevel.BTOptimal,
					                            CompressionLevel.Fastest => ZSTDCompressionLevel.DecompressFast,
					                            CompressionLevel.NoCompression => ZSTDCompressionLevel.None,
					                            CompressionLevel.SmallestSize => ZSTDCompressionLevel.BTVeryUltra,
					                            _ => throw new ArgumentOutOfRangeException(nameof(compressionLevel), compressionLevel, null)
				                            });
				if (n < 0) {
					throw new InvalidOperationException("compression failed");
				}

				return n;
			}
			case CompressionType.Gzip: {
				using var dataPin = compressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, compressed.Length);
				dataStream.Position = 2;
				using var zlib = new GZipStream(dataStream, compressionLevel);
				zlib.Write(decompressed.Span);
				zlib.Flush();

				return (int) zlib.Position;
			}
			case CompressionType.Oodle: {
				var n = Oodle.Compress(decompressed, compressed, Oodle.OodleLZ_Compressor.Hydra,
				                       compressionLevel switch {
					                       CompressionLevel.Optimal => Oodle.OodleLZ_CompressionLevel.Optimal,
					                       CompressionLevel.Fastest => Oodle.OodleLZ_CompressionLevel.Min,
					                       CompressionLevel.NoCompression => Oodle.OodleLZ_CompressionLevel.None,
					                       CompressionLevel.SmallestSize => Oodle.OodleLZ_CompressionLevel.Max,
					                       _ => throw new ArgumentOutOfRangeException(nameof(compressionLevel), compressionLevel, null),
				                       });
				if (n < 0) {
					throw new InvalidOperationException("compression failed");
				}

				return n;
			}
			case CompressionType.LZ4:
			case CompressionType.LZ4HC: {
				var n = LZ4Codec.Encode(decompressed.Span, compressed.Span);
				if (n == -1) {
					throw new InvalidOperationException("compression failed");
				}

				return n;
			}
			case CompressionType.Brotli: {
				using var dataPin = compressed.Pin();
				using var dataStream = new UnmanagedMemoryStream((byte*) dataPin.Pointer, compressed.Length);
				using var brotli = new BrotliStream(dataStream, compressionLevel);
				brotli.Write(decompressed.Span);
				brotli.Flush();

				return (int) brotli.Position;
			}
			case CompressionType.None:
				decompressed.CopyTo(compressed);
				return decompressed.Length;
			default:
				throw new NotSupportedException("Compression type is not supported");
		}
	}
}
