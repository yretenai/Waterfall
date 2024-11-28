#!/bin/sh

podman image build -t waterfall-build/density .
podman create --name waterfall-density waterfall-build/density
podman cp --overwrite waterfall-density:/app/out .
podman rm -f waterfall-density
