// SPDX-FileCopyrightText: 2023-2026 Neptuwunium
//
// SPDX-License-Identifier: EUPL-1.2

namespace Waterfall.Hash.Basis;

public record struct CRCVariant<T>(T Polynomial, T Init, T Xor, bool ReflectIn, bool ReflectOut);
