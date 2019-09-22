// Copyright (c) Henning Thoele.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace D3D12SampleRaytracerSharp
{
    static class Helpers
    {
        /// <summary>
        /// Native memcpy.
        /// </summary>
        /// <param name="dest">The destination memory location.</param>
        /// <param name="src">The source memory location.</param>
        /// <param name="sizeInBytesToCopy">The byte count.</param>
        public static void CopyMemory(IntPtr dest, IntPtr src, int sizeInBytesToCopy)
        {
            unsafe
            {
                Unsafe.CopyBlockUnaligned((void*)dest, (void*)src, (uint)sizeInBytesToCopy);
            }
        }

        /// <summary>
        /// Native memcpy.
        /// </summary>
        /// <param name="dest">The destination memory location.</param>
        /// <param name="src">The source memory location.</param>
        /// <param name="sizeInBytesToCopy">The byte count.</param>
        public static void CopyMemory<T>(IntPtr dest, ReadOnlySpan<T> src)
            where T : struct
        {
            unsafe
            {
                src.CopyTo(new Span<T>((void*)dest, src.Length));
            }
        }
    }
}
