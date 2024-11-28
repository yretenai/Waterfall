#!/bin/sh

mkdir -p ../out
podman build -t waterfall-build/chmlib -v $(realpath ../out):/app/out .
