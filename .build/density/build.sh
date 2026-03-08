#!/bin/sh

# SPDX-FileCopyrightText: 2023-2026 Neptuwunium
#
# SPDX-License-Identifier: CC0-1.0

mkdir -p ../out
podman build -t waterfall-build/density -v $(realpath ../out):/app/out .
