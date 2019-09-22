// Copyright (c) Henning Thoele.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using Vortice.Direct3D12;
using System.IO;
using Vortice.Dxc;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;

namespace D3D12SampleRaytracerSharp
{
    public sealed partial class D3D12GraphicsDevice
    {
        struct TriangleColors
        {
            public Vector4 A;
            public Vector4 B;
            public Vector4 C;
        }

        private ID3D12Resource[] triangleConstantBuffers;

        private void CreateConstantBuffer()
        {
            triangleConstantBuffers = new ID3D12Resource[3];

            TriangleColors[] bufferData = new TriangleColors[]
            {
                new TriangleColors()
                {
                    A = new Vector4(0.0f, 0.2f, 1.0f, 1.0f),
                    B = new Vector4(0.0f, 0.2f, 1.0f, 1.0f),
                    C = new Vector4(0.0f, 0.2f, 1.0f, 1.0f),
                },
                new TriangleColors()
                {
                    A = new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
                    B = new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                    C = new Vector4(0.0f, 1.0f, 1.0f, 1.0f),
                },
                new TriangleColors()
                {
                    A = new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
                    B = new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                    C = new Vector4(1.0f, 0.0f, 1.0f, 1.0f),
                },
            };

            HeapProperties upload_properties = new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);

            for (int i = 0; i < triangleConstantBuffers.Length; i++)
            {
                triangleConstantBuffers[i] = CreateBuffer(device, Unsafe.SizeOf<TriangleColors>(),
                    ResourceFlags.None, ResourceStates.GenericRead, upload_properties);

                IntPtr data = triangleConstantBuffers[i].Map(0);
                Helpers.CopyMemory<TriangleColors>(data, new ReadOnlySpan<TriangleColors>(bufferData).Slice(i, 1));
                triangleConstantBuffers[i].Unmap(0);
            }
        }
    }
}
