#!/bin/sh

mkdir -p ../out
podman build -t waterfall-build/density -v $(realpath ../out):/app/out .
