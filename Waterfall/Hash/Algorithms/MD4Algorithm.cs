using System.Numerics;
using System.Security.Cryptography;

namespace Waterfall.Hash.Algorithms;

public class MD4Algorithm : HashAlgorithm {
	private BlockStack Block { get; set; }
	private StateStack Stack { get; set; } = new(0x67452301, 0xefcdab89, 0x98badcfe, 0x10325476);
	private uint Length { get; set; }

	protected override void HashCore(byte[] array, int ibStart, int cbSize) {
		HashCore(array.AsSpan(ibStart, cbSize));
	}

	public void HashCore(Span<byte> array) {
		var block = Block;
		foreach (var b in array) {
			var c = Length & 63;
			var k = (int) (c >> 2);
			var s = (c & 3) << 3;
			block[k] = (block[k] & ~((uint) 255 << (int) s)) | ((uint) b << (int) s);

			if (c == 63) {
				ProcessBlock();
			}

			Length++;
		}

		Block = block;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public void HashCore<T>(T val) where T : unmanaged {
		// ReSharper disable once ArrangeRedundantParentheses
		var bytes = (stackalloc T[1]);
		bytes[0] = val;
		HashCore(MemoryMarshal.AsBytes(bytes));
	}

	protected override byte[] HashFinal() {
		var result = new byte[16];
		HashFinal(result);
		return result;
	}

	public void HashFinal(Span<byte> result) {
		var length = ((Length + 8) & 0x7fffffc0) + 56 - Length;
		var bytes = Length << 3;
		HashCore((byte) 0x80);

		if (length > 1) {
			Span<byte> align = stackalloc byte[(int) length - 1];
			HashCore(align);
		}

		HashCore(bytes);
		HashCore(0);

		var stack = Stack;
		MemoryMarshal.AsBytes((Span<uint>) stack).CopyTo(result);
		Reset();
	}

	public void Reset() {
		Initialize();
	}

	public override void Initialize() {
		Block = new BlockStack();
		Stack = new StateStack(0x67452301, 0xefcdab89, 0x98badcfe, 0x10325476);
		Length = 0;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private void ProcessBlock() {
		var stack = Stack;

		stack[0] = BitOperations.RotateLeft(stack[0] + F(stack[1], stack[2], stack[3]) + Block[0], 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + F(stack[0], stack[1], stack[2]) + Block[1], 7);
		stack[2] = BitOperations.RotateLeft(stack[2] + F(stack[3], stack[0], stack[1]) + Block[2], 11);
		stack[1] = BitOperations.RotateLeft(stack[1] + F(stack[2], stack[3], stack[0]) + Block[3], 19);
		stack[0] = BitOperations.RotateLeft(stack[0] + F(stack[1], stack[2], stack[3]) + Block[4], 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + F(stack[0], stack[1], stack[2]) + Block[5], 7);
		stack[2] = BitOperations.RotateLeft(stack[2] + F(stack[3], stack[0], stack[1]) + Block[6], 11);
		stack[1] = BitOperations.RotateLeft(stack[1] + F(stack[2], stack[3], stack[0]) + Block[7], 19);
		stack[0] = BitOperations.RotateLeft(stack[0] + F(stack[1], stack[2], stack[3]) + Block[8], 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + F(stack[0], stack[1], stack[2]) + Block[9], 7);
		stack[2] = BitOperations.RotateLeft(stack[2] + F(stack[3], stack[0], stack[1]) + Block[10], 11);
		stack[1] = BitOperations.RotateLeft(stack[1] + F(stack[2], stack[3], stack[0]) + Block[11], 19);
		stack[0] = BitOperations.RotateLeft(stack[0] + F(stack[1], stack[2], stack[3]) + Block[12], 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + F(stack[0], stack[1], stack[2]) + Block[13], 7);
		stack[2] = BitOperations.RotateLeft(stack[2] + F(stack[3], stack[0], stack[1]) + Block[14], 11);
		stack[1] = BitOperations.RotateLeft(stack[1] + F(stack[2], stack[3], stack[0]) + Block[15], 19);

		stack[0] = BitOperations.RotateLeft(stack[0] + G(stack[1], stack[2], stack[3]) + Block[0] + 0x5A827999, 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + G(stack[0], stack[1], stack[2]) + Block[4] + 0x5A827999, 5);
		stack[2] = BitOperations.RotateLeft(stack[2] + G(stack[3], stack[0], stack[1]) + Block[8] + 0x5A827999, 9);
		stack[1] = BitOperations.RotateLeft(stack[1] + G(stack[2], stack[3], stack[0]) + Block[12] + 0x5A827999, 13);
		stack[0] = BitOperations.RotateLeft(stack[0] + G(stack[1], stack[2], stack[3]) + Block[1] + 0x5A827999, 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + G(stack[0], stack[1], stack[2]) + Block[5] + 0x5A827999, 5);
		stack[2] = BitOperations.RotateLeft(stack[2] + G(stack[3], stack[0], stack[1]) + Block[9] + 0x5A827999, 9);
		stack[1] = BitOperations.RotateLeft(stack[1] + G(stack[2], stack[3], stack[0]) + Block[13] + 0x5A827999, 13);
		stack[0] = BitOperations.RotateLeft(stack[0] + G(stack[1], stack[2], stack[3]) + Block[2] + 0x5A827999, 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + G(stack[0], stack[1], stack[2]) + Block[6] + 0x5A827999, 5);
		stack[2] = BitOperations.RotateLeft(stack[2] + G(stack[3], stack[0], stack[1]) + Block[10] + 0x5A827999, 9);
		stack[1] = BitOperations.RotateLeft(stack[1] + G(stack[2], stack[3], stack[0]) + Block[14] + 0x5A827999, 13);
		stack[0] = BitOperations.RotateLeft(stack[0] + G(stack[1], stack[2], stack[3]) + Block[3] + 0x5A827999, 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + G(stack[0], stack[1], stack[2]) + Block[7] + 0x5A827999, 5);
		stack[2] = BitOperations.RotateLeft(stack[2] + G(stack[3], stack[0], stack[1]) + Block[11] + 0x5A827999, 9);
		stack[1] = BitOperations.RotateLeft(stack[1] + G(stack[2], stack[3], stack[0]) + Block[15] + 0x5A827999, 13);

		stack[0] = BitOperations.RotateLeft(stack[0] + H(stack[1], stack[2], stack[3]) + Block[0] + 0x6ED9EBA1, 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + H(stack[0], stack[1], stack[2]) + Block[8] + 0x6ED9EBA1, 9);
		stack[2] = BitOperations.RotateLeft(stack[2] + H(stack[3], stack[0], stack[1]) + Block[4] + 0x6ED9EBA1, 11);
		stack[1] = BitOperations.RotateLeft(stack[1] + H(stack[2], stack[3], stack[0]) + Block[12] + 0x6ED9EBA1, 15);
		stack[0] = BitOperations.RotateLeft(stack[0] + H(stack[1], stack[2], stack[3]) + Block[2] + 0x6ED9EBA1, 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + H(stack[0], stack[1], stack[2]) + Block[10] + 0x6ED9EBA1, 9);
		stack[2] = BitOperations.RotateLeft(stack[2] + H(stack[3], stack[0], stack[1]) + Block[6] + 0x6ED9EBA1, 11);
		stack[1] = BitOperations.RotateLeft(stack[1] + H(stack[2], stack[3], stack[0]) + Block[14] + 0x6ED9EBA1, 15);
		stack[0] = BitOperations.RotateLeft(stack[0] + H(stack[1], stack[2], stack[3]) + Block[1] + 0x6ED9EBA1, 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + H(stack[0], stack[1], stack[2]) + Block[9] + 0x6ED9EBA1, 9);
		stack[2] = BitOperations.RotateLeft(stack[2] + H(stack[3], stack[0], stack[1]) + Block[5] + 0x6ED9EBA1, 11);
		stack[1] = BitOperations.RotateLeft(stack[1] + H(stack[2], stack[3], stack[0]) + Block[13] + 0x6ED9EBA1, 15);
		stack[0] = BitOperations.RotateLeft(stack[0] + H(stack[1], stack[2], stack[3]) + Block[3] + 0x6ED9EBA1, 3);
		stack[3] = BitOperations.RotateLeft(stack[3] + H(stack[0], stack[1], stack[2]) + Block[11] + 0x6ED9EBA1, 9);
		stack[2] = BitOperations.RotateLeft(stack[2] + H(stack[3], stack[0], stack[1]) + Block[7] + 0x6ED9EBA1, 11);
		stack[1] = BitOperations.RotateLeft(stack[1] + H(stack[2], stack[3], stack[0]) + Block[15] + 0x6ED9EBA1, 15);

		unchecked {
			Stack = new StateStack(Stack[0] + stack[0], Stack[1] + stack[1], Stack[2] + stack[2], Stack[3] + stack[3]);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static uint F(uint x, uint y, uint z) => (x & y) | (~x & z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static uint G(uint x, uint y, uint z) => (x & y) | (x & z) | (y & z);

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private static uint H(uint x, uint y, uint z) => x ^ y ^ z;

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4), InlineArray(16)]
	private struct BlockStack {
		public uint Value;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4), InlineArray(4)]
	private struct StateStack {
		public uint Value;

		public StateStack(uint a, uint b, uint c, uint d) {
			this[0] = a;
			this[1] = b;
			this[2] = c;
			this[3] = d;
		}
	}
}
