// Copyright (c) Henning Thoele.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using Vortice.Direct3D12;
using System.Runtime.CompilerServices;

namespace D3D12SampleRaytracerSharp
{
    public sealed partial class D3D12GraphicsDevice
    {
        private const uint D3D12ShaderIdentifierSizeInBytes = 32;
        private const uint D3D12RaytracingShaderRecordByteAlignment = 32;

        private ID3D12Resource shaderTable;
        uint shaderTableEntrySize = 0;

        private unsafe void CreateShaderTable()
        {
            /** The shader-table layout is as follows:
                Entry 0 - Ray-gen program
                Entry 1 - Miss program
                Entry 2 - Miss program for Shadow
                Entry 3,4 - Hit program for triangle 0 (pimary, shadow)
                Entry 5,6 - Hit program for the plane (pimary, shadow)
                Entry 7,8 - Hit program for triangle 1 (pimary, shadow)
                Entry 9,10 - Hit program for triangle 2 (pimary, shadow)
                All entries in the shader-table must have the same size, so we will choose it base on the largest required entry.
                The triangle hit program requires the largest entry - sizeof(program identifier) + 8 bytes for the constant-buffer root descriptor.
                The entry size must be aligned up to D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT
            */

            shaderTableEntrySize = D3D12ShaderIdentifierSizeInBytes;
            shaderTableEntrySize += 16;     // TLAS + Constant Buffer
            shaderTableEntrySize = align_to(D3D12RaytracingShaderRecordByteAlignment, shaderTableEntrySize);
            uint shaderTableSize = shaderTableEntrySize * 11;

            HeapProperties upload_properties = new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);

            shaderTable = CreateBuffer(device, shaderTableSize, ResourceFlags.None, ResourceStates.GenericRead, upload_properties);

            IntPtr data = shaderTable.Map(0);

            ID3D12StateObjectProperties props = pipelineState.QueryInterface<ID3D12StateObjectProperties>();
            IntPtr rayGenPtr = props.GetShaderIdentifier("rayGen");
            IntPtr missPtr = props.GetShaderIdentifier("miss");
            IntPtr hitGroupPtr = props.GetShaderIdentifier("HitGroup");
            IntPtr planeHitGroupPtr = props.GetShaderIdentifier("HitGroupPlane");

            IntPtr shadowHitGroupPtr = props.GetShaderIdentifier("ShadowHitGroup");
            IntPtr shadowMissPtr = props.GetShaderIdentifier("shadowMiss");

            // Entry 0 - ray-gen program ID and descriptor data
            Helpers.CopyMemory(data, rayGenPtr, (int)D3D12ShaderIdentifierSizeInBytes);
            ulong heapStart = (ulong)srvUavHeap.GetGPUDescriptorHandleForHeapStart().Ptr;
            Unsafe.Write<ulong>((data + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), heapStart);

            // Entry 1 - miss program
            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, missPtr, (int)D3D12ShaderIdentifierSizeInBytes);

            // Entry 2 - shadow miss program
            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, shadowMissPtr, (int)D3D12ShaderIdentifierSizeInBytes);

            int increment = device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);

            // Entry 3,4 - Triangle 0 hit program. ProgramID and constant-buffer data
            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, hitGroupPtr, (int)D3D12ShaderIdentifierSizeInBytes);
            ulong constantBufferAddress = (ulong)triangleConstantBuffers[1].GPUVirtualAddress;
            Unsafe.Write<ulong>((data + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), constantBufferAddress);
            Unsafe.Write<ulong>((data + sizeof(ulong) + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), heapStart + (ulong)increment);

            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, shadowHitGroupPtr, (int)D3D12ShaderIdentifierSizeInBytes);

            // Entry 5,6 - Triangle 1 hit program. ProgramID and constant-buffer data
            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, hitGroupPtr, (int)D3D12ShaderIdentifierSizeInBytes);
            constantBufferAddress = (ulong)triangleConstantBuffers[0].GPUVirtualAddress;
            Unsafe.Write<ulong>((data + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), constantBufferAddress);
            Unsafe.Write<ulong>((data + sizeof(ulong) + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), heapStart + (ulong)increment);

            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, shadowHitGroupPtr, (int)D3D12ShaderIdentifierSizeInBytes);

            // Entry 7,8 - Plane hit program. ProgramID only
            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, planeHitGroupPtr, (int)D3D12ShaderIdentifierSizeInBytes);
            Unsafe.Write<ulong>((data + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), 
                heapStart + (ulong)device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView));

            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, shadowHitGroupPtr, (int)D3D12ShaderIdentifierSizeInBytes);

            // Entry 9,10 - Triangle 2 hit program. ProgramID and constant-buffer data
            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, hitGroupPtr, (int)D3D12ShaderIdentifierSizeInBytes);
            constantBufferAddress = (ulong)triangleConstantBuffers[2].GPUVirtualAddress;
            Unsafe.Write<ulong>((data + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), constantBufferAddress);
            Unsafe.Write<ulong>((data + sizeof(ulong) + (int)D3D12ShaderIdentifierSizeInBytes).ToPointer(), heapStart + (ulong)increment);

            data += (int)shaderTableEntrySize;
            Helpers.CopyMemory(data, shadowHitGroupPtr, (int)D3D12ShaderIdentifierSizeInBytes);

            shaderTable.Unmap(0);
        }

        private static uint align_to(uint _alignment, uint _val)
        {
            return (((_val + _alignment - 1) / _alignment) * _alignment);
        }
    }
}
