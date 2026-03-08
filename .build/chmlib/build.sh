#!/bin/sh

# SPDX-FileCopyrightText: 2023-2026 Neptuwunium
#
# SPDX-License-Identifier: 0BSD

mkdir -p ../out
podman build -t waterfall-build/chmlib -v $(realpath ../out):/app/out .
