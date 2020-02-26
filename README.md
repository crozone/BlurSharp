# BlurSharp
Blurhash implementation for C#

## Progress report

What is done:

* BlurHash core encoder
* Encoder wrapper for `System.Drawing`
* Encoder tests for a set of testing images, using all possible component values.

What is TODO:

* Decoder
* Decoder wrapper for `System.Drawing`
* Decoder tests

## Performance

### Results

Currently, this implementation is ~25x faster than the reference C implementation.

On my reference machine, BlurSharp (Release) can encode all six reference images in 1,1 to 8,8 component forms (64 combinations per image) in under 5 seconds. The C implementation (compiled with -O2) does the same in 125 seconds.

### How

The library has been written with the `System.Numerics` vector instructions, specifically `Vector3` is used heavily to speed up operations. A precalculated lookup table is used to do SRGB to Linear float conversions.

`Span<T>` is also used extensively in order to reduce object allocations and unnecessary copying of buffers.

For example, `Span<T>` allows for the encoding of `System.Drawing` bitmaps without copying the image buffer. Further work needs to be done to allow core support for different pixel formats, so that `Bitmap.LockBits` doesn't have to do work internally to present the image as `PixelFormat.Format24bppRgb`.

The library also leverages the new `Span<T>` based safe `stackalloc` keyword to avoid heap allocations. Depending on how much stack you allow the encoder to use, an image can be encoded without a single object allocation. This can reduce GC pressure under heavy workloads.

Performance TODO:

* Potentially use parallel processing (questionable benefit for web scenarios)
