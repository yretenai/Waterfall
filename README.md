# Waterfall

Wrappers around Native compression methods.

Native modules are **not** provided right now.

- Windows: I'm sorry but you're going to have to build them yourself for now.
- Linux: Your distro package manager will have almost all dependencies (`apt install libchm1 lzo2 zstd`, `portage emerge dev-libs/chmlib dev-libs/lzo app-arch/zstd`, etc)
- macOS: same as linux, but use Homebrew (`brew install chmlib lzo zstd`)

### Native Bindings

- Density: [DENSITY](https://github.com/g1mv/density).
- LZO1/LZO2: [LZO](https://www.oberhumer.com/opensource/lzo/).
- LZX: [CHMLib](http://morte.jedrea.com/~jedwin/projects/chmlib/), requires the GNU compiler or source edits. Will be provided when I can reliably build DLLs. Might have a windows-native workaround.
- Oodle: Epic Games, native module will never be provided.
- ZStandard: [ZStandard](https://github.com/facebook/zstd/).

### Package Wrapper Bindings

- LZMA: [LZMA-SDK](https://www.nuget.org/packages/LZMA-SDK)
- LZ4: [K4os.Compression.LZ4](https://www.nuget.org/packages/K4os.Compression.LZ4/)
- Brotli: System.IO.Compression
- Gzip: System.IO.Compression
- Zlib: System.IO.Compression
- Deflate: System.IO.Compression
