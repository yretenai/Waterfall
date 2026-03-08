// SPDX-FileCopyrightText: 2023-2026 Neptuwunium
//
// SPDX-License-Identifier: 0BSD

using System.Text;

namespace Waterfall;

public static class Text {
	public static UTF8Encoding UTF8NoBOM { get; } = new(false);
	public static UnicodeEncoding UTF16BENoBOM { get; } = new(true, false);
	public static UnicodeEncoding UTF16LENoBOM { get; } = new(false, false);
}
