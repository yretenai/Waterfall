// SPDX-FileCopyrightText: 2023-2026 Neptuwunium
//
// SPDX-License-Identifier: EUPL-1.2

namespace Waterfall;

internal static class UInt128Extensions {
	extension(ref UInt128 v) {
		public ulong Low {
			get => (ulong) v;
			set => v = new UInt128((ulong) (v >> 64), value);
		}

		public ulong High {
			get => (ulong) (v >> 64);
			set => v = new UInt128(value, (ulong) v);
		}
	}
}
