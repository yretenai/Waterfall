// SPDX-FileCopyrightText: 2011 Google, Inc.
// SPDX-FileCopyrightText: 2014 Gustavo J Knuppe (https://github.com/knuppe)
// SPDX-FileCopyrightText: 2026 Neptuwunium
//
// SPDX-License-Identifier: MIT
//
// Project site: https://github.com/leuchtraketen/cityhash
// Original code: https://code.google.com/p/cityhash/
// Waterfall modifications: net10 modernizations, span, bitoperations, binaryprimitives, etc
//

using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Waterfall.Hash.Algorithms;

public static class CityHashAlgorithm {
	// Some primes between 2^63 and 2^64 for various uses.
	private const ulong K0 = 0xc3a5c85c97cb3127UL;
	private const ulong K1 = 0xb492b66fbe98f273UL;
	private const ulong K2 = 0x9ae16a3b2f90404fUL;

	// Magic numbers for 32-bit hashing.  Copied from Murmur3.
	private const uint C1 = 0xcc9e2d51;
	private const uint C2 = 0x1b873593;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint FMix(uint h) {
		h ^= h >> 16;
		h *= 0x85ebca6b;
		h ^= h >> 13;
		h *= 0xc2b2ae35;
		h ^= h >> 16;
		return h;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint Mur(uint a, uint h) {
		// Helper from Murmur3 for combining two 32-bit values.
		a *= C1;
		a = BitOperations.RotateRight(a, 17);
		a *= C2;
		h ^= a;
		h = BitOperations.RotateRight(h, 19);
		return h * 5 + 0xe6546b64;
	}

	// todo: move this local
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint Fetch32(ReadOnlySpan<byte> value, int index = 0) => MemoryMarshal.Read<uint>(value[index..]);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Fetch64(ReadOnlySpan<byte> value, int index = 0) => MemoryMarshal.Read<ulong>(value[index..]);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint Hash32Len0To4(ReadOnlySpan<byte> value) {
		var l = (uint) value.Length;
		var b = 0u;
		var c = 9u;
		for (var i = 0; i < l; i++) {
			b = b * C1 + (uint) (sbyte) value[i];
			c ^= b;
		}

		return FMix(Mur(b, Mur(l, c)));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint Hash32Len5To12(ReadOnlySpan<byte> value) {
		uint a = (uint) value.Length, b = a * 5, c = 9, d = b;

		a += Fetch32(value);
		b += Fetch32(value, value.Length - 4);
		c += Fetch32(value, (value.Length >> 1) & 4);

		return FMix(Mur(c, Mur(b, Mur(a, d))));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static uint Hash32Len13To24(ReadOnlySpan<byte> value) {
		var a = Fetch32(value, (value.Length >> 1) - 4);
		var b = Fetch32(value, 4);
		var c = Fetch32(value, value.Length - 8);
		var d = Fetch32(value, value.Length >> 1);
		var e = Fetch32(value);
		var f = Fetch32(value, value.Length - 4);
		var h = (uint) value.Length;

		return FMix(Mur(f, Mur(e, Mur(d, Mur(c, Mur(b, Mur(a, h)))))));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong ShiftMix(ulong val) => val ^ (val >> 47);

	public static UInt128 MurHash(ReadOnlySpan<byte> value, UInt128 seed, int offset) {
		var a = seed.Low;
		var b = seed.High;
		ulong c;
		ulong d;

		var len = value.Length - offset;
		var l = len - 16;

		if (l <= 0) {
			// len <= 16
			a = ShiftMix(a * K1) * K1;
			c = b * K1 + HashLen0To16(value, offset);
			d = ShiftMix(a + (len >= 8 ? Fetch64(value, offset) : c));
		} else {
			// len > 16
			c = HashLen16(Fetch64(value, offset + len - 8) + K1, a);
			d = HashLen16(b + (ulong) len, c + Fetch64(value, offset + len - 16));
			a += d;

			var p = offset;
			do {
				a ^= ShiftMix(Fetch64(value, p) * K1) * K1;
				a *= K1;
				b ^= a;
				c ^= ShiftMix(Fetch64(value, p + 8) * K1) * K1;
				c *= K1;
				d ^= c;

				p += 16;
				l -= 16;
			} while (l > 0);
		}

		a = HashLen16(a, c);
		b = HashLen16(d, b);
		return new UInt128(HashLen16(b, a), a ^ b);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint Hash32(string text, Encoding? encoding = null) {
		var rented = default(byte[]?);
		encoding ??= Text.UTF8NoBOM;
		var length = encoding.GetByteCount(text);
		var buffer = length < 1024 ? stackalloc byte[length] : rented = ArrayPool<byte>.Shared.Rent(length);

		try {
			var n = encoding.GetBytes(text, buffer);
			return Hash32(buffer[..n]);
		} finally {
			if (rented != null) {
				ArrayPool<byte>.Shared.Return(rented);
			}
		}
	}

	public static uint Hash32(ReadOnlySpan<byte> value) {
		var len = (uint) value.Length;

		switch (len) {
			case <= 4:
				return Hash32Len0To4(value);
			case <= 12:
				return Hash32Len5To12(value);
			case <= 24:
				return Hash32Len13To24(value);
		}

		uint h = len, g = unchecked(C1 * len), f = g;
		{
			var a0 = BitOperations.RotateRight(Fetch32(value, (int) (len - 4)) * C1, 17) * C2;
			var a1 = BitOperations.RotateRight(Fetch32(value, (int) (len - 8)) * C1, 17) * C2;
			var a2 = BitOperations.RotateRight(Fetch32(value, (int) (len - 16)) * C1, 17) * C2;
			var a3 = BitOperations.RotateRight(Fetch32(value, (int) (len - 12)) * C1, 17) * C2;
			var a4 = BitOperations.RotateRight(Fetch32(value, (int) (len - 20)) * C1, 17) * C2;

			h ^= a0;
			h = BitOperations.RotateRight(h, 19);
			h = h * 5 + 0xe6546b64;
			h ^= a2;
			h = BitOperations.RotateRight(h, 19);
			h = h * 5 + 0xe6546b64;

			g ^= a1;
			g = BitOperations.RotateRight(g, 19);
			g = g * 5 + 0xe6546b64;
			g ^= a3;
			g = BitOperations.RotateRight(g, 19);
			g = g * 5 + 0xe6546b64;

			f += a4;
			f = BitOperations.RotateRight(f, 19);
			f = f * 5 + 0xe6546b64;
		}

		for (var i = 0; i < (len - 1) / 20; i++) {
			var a0 = BitOperations.RotateRight(Fetch32(value, 20 * i) * C1, 17) * C2;
			var a1 = Fetch32(value, 20 * i + 4);
			var a2 = BitOperations.RotateRight(Fetch32(value, 20 * i + 8) * C1, 17) * C2;
			var a3 = BitOperations.RotateRight(Fetch32(value, 20 * i + 12) * C1, 17) * C2;
			var a4 = Fetch32(value, 20 * i + 16);

			h ^= a0;
			h = BitOperations.RotateRight(h, 18);
			h = h * 5 + 0xe6546b64;

			f += a1;
			f = BitOperations.RotateRight(f, 19);
			f = f * C1;

			g += a2;
			g = BitOperations.RotateRight(g, 18);
			g = g * 5 + 0xe6546b64;

			h ^= a3 + a1;
			h = BitOperations.RotateRight(h, 19);
			h = h * 5 + 0xe6546b64;

			g ^= a4;
			g = BinaryPrimitives.ReverseEndianness(g) * 5;

			h += a4 * 5;
			h = BinaryPrimitives.ReverseEndianness(h);

			f += a0;

			(f, h, g) = (g, f, h);
		}

		g = BitOperations.RotateRight(g, 11) * C1;
		g = BitOperations.RotateRight(g, 17) * C1;

		f = BitOperations.RotateRight(f, 11) * C1;
		f = BitOperations.RotateRight(f, 17) * C1;

		h = BitOperations.RotateRight(h + g, 19);
		h = h * 5 + 0xe6546b64;
		h = BitOperations.RotateRight(h, 17) * C1;

		h = BitOperations.RotateRight(h + f, 19);
		h = h * 5 + 0xe6546b64;
		h = BitOperations.RotateRight(h, 17) * C1;

		return h;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong Hash64(string text, Encoding? encoding = null) {
		var rented = default(byte[]?);
		encoding ??= Text.UTF8NoBOM;
		var length = encoding.GetByteCount(text);
		var buffer = length < 1024 ? stackalloc byte[length] : rented = ArrayPool<byte>.Shared.Rent(length);

		try {
			var n = encoding.GetBytes(text, buffer);
			return Hash64(buffer[..n]);
		} finally {
			if (rented != null) {
				ArrayPool<byte>.Shared.Return(rented);
			}
		}
	}

	public static ulong Hash64(ReadOnlySpan<byte> value) {
		switch (value.Length) {
			case <= 16:
				return HashLen0To16(value);
			case <= 32:
				return HashLen17To32(value);
			case <= 64:
				return HashLen33To64(value);
		}

		// For strings over 64 bytes we hash the end first, and then as we
		// loop we keep 56 bytes of state: v, w, x, y, and z.

		var x = Fetch64(value, value.Length - 40);
		var y = Fetch64(value, value.Length - 16) + Fetch64(value, value.Length - 56);
		var z = HashLen16(
			Fetch64(value, value.Length - 48) + (ulong) value.Length,
			Fetch64(value, value.Length - 24));

		var v = WeakHashLen32WithSeeds(value, value.Length - 64, (ulong) value.Length, z);
		var w = WeakHashLen32WithSeeds(value, value.Length - 32, y + K1, x);

		x = x * K1 + Fetch64(value);

		// Decrease len to the nearest multiple of 64, and operate on 64-byte chunks.

		var pos = 0;
		var len = (value.Length - 1) & ~63;
		do {
			x = BitOperations.RotateRight(x + y + v.Low + Fetch64(value, pos + 8), 37) * K1;
			y = BitOperations.RotateRight(y + v.High + Fetch64(value, pos + 48), 42) * K1;
			x ^= w.High;
			y += v.Low + Fetch64(value, pos + 40);
			z = BitOperations.RotateRight(z + w.Low, 33) * K1;
			v = WeakHashLen32WithSeeds(value, pos, v.High * K1, x + w.Low);
			w = WeakHashLen32WithSeeds(value, pos + 32, z + w.High, y + Fetch64(value, pos + 16));
			(z, x) = (x, z);

			pos += 64;
			len -= 64;
		} while (len != 0);

		return HashLen16(HashLen16(v.Low, w.Low) + ShiftMix(y) * K1 + z, HashLen16(v.High, w.High) + x);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong Hash64(string text, ulong seed, Encoding? encoding = null) {
		var rented = default(byte[]?);
		encoding ??= Text.UTF8NoBOM;
		var length = encoding.GetByteCount(text);
		var buffer = length < 1024 ? stackalloc byte[length] : rented = ArrayPool<byte>.Shared.Rent(length);

		try {
			var n = encoding.GetBytes(text, buffer);
			return Hash64(buffer[..n], K2, seed);
		} finally {
			if (rented != null) {
				ArrayPool<byte>.Shared.Return(rented);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong Hash64(string text, ulong seed0, ulong seed1, Encoding? encoding = null) {
		var rented = default(byte[]?);
		encoding ??= Text.UTF8NoBOM;
		var length = encoding.GetByteCount(text);
		var buffer = length < 1024 ? stackalloc byte[length] : rented = ArrayPool<byte>.Shared.Rent(length);

		try {
			var n = encoding.GetBytes(text, buffer);
			return Hash64(buffer[..n], seed0, seed1);
		} finally {
			if (rented != null) {
				ArrayPool<byte>.Shared.Return(rented);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong Hash64(ReadOnlySpan<byte> value, ulong seed) => Hash64(value, K2, seed);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ulong Hash64(ReadOnlySpan<byte> value, ulong seed0, ulong seed1) => HashLen16(Hash64(value) - seed0, seed1);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UInt128 Hash128(string text, Encoding? encoding = null) {
		var rented = default(byte[]?);
		encoding ??= Text.UTF8NoBOM;
		var length = encoding.GetByteCount(text);
		var buffer = length < 1024 ? stackalloc byte[length] : rented = ArrayPool<byte>.Shared.Rent(length);

		try {
			var n = encoding.GetBytes(text, buffer);
			return Hash128(buffer[..n]);
		} finally {
			if (rented != null) {
				ArrayPool<byte>.Shared.Return(rented);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UInt128 Hash128(string text, UInt128 seed, Encoding? encoding = null) {
		var rented = default(byte[]?);
		encoding ??= Text.UTF8NoBOM;
		var length = encoding.GetByteCount(text);
		var buffer = length < 1024 ? stackalloc byte[length] : rented = ArrayPool<byte>.Shared.Rent(length);

		try {
			var n = encoding.GetBytes(text, buffer);
			return Hash128(buffer[..n]);
		} finally {
			if (rented != null) {
				ArrayPool<byte>.Shared.Return(rented);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UInt128 Hash128(ReadOnlySpan<byte> value) =>
		value.Length >= 16
			? Hash128(value, new UInt128(Fetch64(value, 8) + K0, Fetch64(value)), 16)
			: Hash128(value, new UInt128(K1, K0), 0);

	public static UInt128 Hash128(ReadOnlySpan<byte> value, UInt128 seed, int offset) {
		if (value.Length - offset < 128) {
			return MurHash(value, seed, offset);
		}

		// We expect len >= 128 to be the common case.  Keep 56 bytes of state:
		// v, w, x, y, and z.
		var len = value.Length - offset;
		var x = seed.Low;
		var y = seed.High;
		var z = (ulong) len * K1;
		var vl = BitOperations.RotateRight(seed.High ^ K1, 49) * K1 + Fetch64(value, offset);
		var vh = BitOperations.RotateRight(vl, 42) * K1 + Fetch64(value, offset + 8);
		var v = new UInt128(vh, vl);

		var w = new UInt128(BitOperations.RotateRight(seed.Low + Fetch64(value, offset + 88), 53) * K1, BitOperations.RotateRight(y + z, 35) * K1 + x);


		// This is the same inner loop as CityHash64(), manually unrolled.
		var s = offset;
		do {
			x = BitOperations.RotateRight(x + y + v.Low + Fetch64(value, s + 8), 37) * K1;
			y = BitOperations.RotateRight(y + v.High + Fetch64(value, s + 48), 42) * K1;
			x ^= w.High;
			y += v.Low + Fetch64(value, s + 40);
			z = BitOperations.RotateRight(z + w.Low, 33) * K1;
			v = WeakHashLen32WithSeeds(value, s, v.High * K1, x + w.Low);
			w = WeakHashLen32WithSeeds(value, s + 32, z + w.High, y + Fetch64(value, s + 16));

			(z, x) = (x, z);

			s += 64;

			x = BitOperations.RotateRight(x + y + v.Low + Fetch64(value, s + 8), 37) * K1;
			y = BitOperations.RotateRight(y + v.High + Fetch64(value, s + 48), 42) * K1;
			x ^= w.High;
			y += v.Low + Fetch64(value, s + 40);
			z = BitOperations.RotateRight(z + w.Low, 33) * K1;
			v = WeakHashLen32WithSeeds(value, s, v.High * K1, x + w.Low);
			w = WeakHashLen32WithSeeds(value, s + 32, z + w.High, y + Fetch64(value, s + 16));

			(z, x) = (x, z);

			s += 64;
			len -= 128;
		} while (len >= 128);

		x += BitOperations.RotateRight(v.Low + z, 49) * K0;
		y = y * K0 + BitOperations.RotateRight(w.High, 37);
		z = z * K0 + BitOperations.RotateRight(w.Low, 27);
		w.Low *= 9;
		v.Low *= K0;

		// If 0 < len < 128, hash up to 4 chunks of 32 bytes each from the end of s.
		for (var tail = 0; tail < len;) {
			tail += 32;

			y = BitOperations.RotateRight(x + y, 42) * K0 + v.High;
			w.Low += Fetch64(value, s + len - tail + 16);
			x = x * K0 + w.Low;
			z += w.High + Fetch64(value, s + len - tail);
			w.High += v.Low;
			v = WeakHashLen32WithSeeds(value, s + len - tail, v.Low + z, v.High);
			v.Low *= K0;
		}


		// At this point our 56 bytes of state should contain more than
		// enough information for a strong 128-bit hash.  We use two
		// different 56-byte-to-8-byte hashes to get a 16-byte final result.
		x = HashLen16(x, v.Low);
		y = HashLen16(y + z, w.Low);

		return new UInt128(HashLen16(x + w.High, y + v.High), HashLen16(x + v.High, w.High) + y);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong HashLen16(ulong u, ulong v) => Hash128To64(new UInt128(v, u));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong HashLen16(ulong u, ulong v, ulong mul) {
		// Murmur-inspired hashing.
		var a = (u ^ v) * mul;
		a ^= a >> 47;
		var b = (v ^ a) * mul;
		b ^= b >> 47;
		b *= mul;
		return b;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong HashLen0To16(ReadOnlySpan<byte> value, int offset = 0) {
		var len = (uint) (value.Length - offset);

		switch (len) {
			case >= 8: {
				var mul = K2 + (ulong) len * 2;
				var a = Fetch64(value, offset) + K2;
				var b = Fetch64(value, value.Length - 8);
				var c = BitOperations.RotateRight(b, 37) * mul + a;
				var d = (BitOperations.RotateRight(a, 25) + b) * mul;

				return HashLen16(c, d, mul);
			}
			case >= 4: {
				var mul = K2 + (ulong) len * 2;
				ulong a = Fetch32(value, offset);
				return HashLen16(len + (a << 3), Fetch32(value, (int) (offset + len - 4)), mul);
			}
			case > 0: {
				var a = value[offset];
				var b = value[(int) (offset + (len >> 1))];
				var c = value[(int) (offset + (len - 1))];

				var y = a + ((uint) b << 8);
				var z = len + ((uint) c << 2);

				return ShiftMix((y * K2) ^ (z * K0)) * K2;
			}
			default:
				return K2;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong HashLen17To32(ReadOnlySpan<byte> value) {
		var len = (ulong) value.Length;

		var mul = K2 + len * 2ul;
		var a = Fetch64(value) * K1;
		var b = Fetch64(value, 8);
		var c = Fetch64(value, value.Length - 8) * mul;
		var d = Fetch64(value, value.Length - 16) * K2;

		return HashLen16(BitOperations.RotateRight(a + b, 43) + BitOperations.RotateRight(c, 30) + d, a + BitOperations.RotateRight(b + K2, 18) + c, mul);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong HashLen33To64(ReadOnlySpan<byte> value) {
		var mul = K2 + (ulong) value.Length * 2ul;
		var a = Fetch64(value) * K2;
		var b = Fetch64(value, 8);
		var c = Fetch64(value, value.Length - 24);
		var d = Fetch64(value, value.Length - 32);
		var e = Fetch64(value, 16) * K2;
		var f = Fetch64(value, 24) * 9;
		var g = Fetch64(value, value.Length - 8);
		var h = Fetch64(value, value.Length - 16) * mul;

		var u = BitOperations.RotateRight(a + g, 43) + (BitOperations.RotateRight(b, 30) + c) * 9;
		var v = ((a + g) ^ d) + f + 1;
		var w = BinaryPrimitives.ReverseEndianness((u + v) * mul) + h;
		var x = BitOperations.RotateRight(e + f, 42) + c;
		var y = (BinaryPrimitives.ReverseEndianness((v + w) * mul) + g) * mul;
		var z = e + f + c;

		a = BinaryPrimitives.ReverseEndianness((x + z) * mul + y) + b;
		b = ShiftMix((z + a) * mul + d + h) * mul;
		return b + x;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static ulong Hash128To64(UInt128 x) {
		const ulong kMul = 0x9ddfea08eb382d69UL;

		var a = (x.Low ^ x.High) * kMul;
		a ^= a >> 47;

		var b = (x.High ^ a) * kMul;
		b ^= b >> 47;
		b *= kMul;

		return b;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static UInt128 WeakHashLen32WithSeeds(ulong w, ulong x, ulong y, ulong z, ulong a, ulong b) {
		a += w;
		b = BitOperations.RotateRight(b + a + z, 21);

		var c = a;
		a += x;
		a += y;

		b += BitOperations.RotateRight(a, 44);

		return new UInt128(b + c, a + z);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static UInt128 WeakHashLen32WithSeeds(ReadOnlySpan<byte> value, int offset, ulong a, ulong b) =>
		WeakHashLen32WithSeeds(
			Fetch64(value, offset),
			Fetch64(value, offset + 8),
			Fetch64(value, offset + 16),
			Fetch64(value, offset + 24),
			a,
			b);
}
