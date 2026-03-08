// SPDX-FileCopyrightText: 2023-2026 Neptuwunium
//
// SPDX-License-Identifier: EUPL-1.2

namespace Waterfall.Hash.Basis;

// https://tools.ietf.org/html/draft-eastlake-fnv-17
// http://www.isthe.com/chongo/tech/comp/fnv/index.html
public enum FNV32Basis : uint {
	Default = FNV1,
	FNV0 = 0,
	FNV1 = 0x811c9dc5, // chongo <Landon Curt Noll> /\../\
	FNV1B = 0x8d9a085e, // chongo (Landon Curt Noll) /\oo/\
}
