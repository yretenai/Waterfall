// SPDX-FileCopyrightText: 2023-2026 Neptuwunium
//
// SPDX-License-Identifier: 0BSD

namespace Waterfall.Compression;

// Note: Freeze Order! Used in Shard
public enum CompressionType {
	None,
	Oodle,
	OodleTex,
	Brotli,
	Zlib,
	Deflate,
	Gzip,
	LZ4,
	LZ4HC,
	LZO1,
	LZO2,
	LZX,
	LZMA,
	SafeLZMA,
	RawLZMA,
	Zstd,
	Density,
}
