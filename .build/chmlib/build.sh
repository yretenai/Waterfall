#!/bin/sh

podman image build -t waterfall-build/chmlib .
podman create --name waterfall-chmlib waterfall-build/chmlib
podman cp --overwrite waterfall-chmlib:/app/out .
podman rm -f waterfall-chmlib
