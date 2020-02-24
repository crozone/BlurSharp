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

The library has been written to make extensive use of `Span<T>`, in the hopes that it will allow faster processing of images and less object allocations.

Currently, this allows for the encoding of `System.Drawing` bitmaps without copying the image buffer. Further work needs to be done to allow core support for different pixel formats, so that `Bitmap.LockBits` doesn't have to do work internally to present the image as `PixelFormat.Format24bppRgb`.

The library also leverages the new, `Span<T>` based safe `stackalloc` keyword to avoid heap allocations. Depending on how much stack you allow the encoder to use, an image can be encoded without a single object allocation. This can reduce GC pressure under heavy workloads.

Performance TODO:

* Vectorise image calculations
* Use single precision `float` instead of double precision `double`
* Potentially use parallel processing (questionable benefit for web scenarios)

