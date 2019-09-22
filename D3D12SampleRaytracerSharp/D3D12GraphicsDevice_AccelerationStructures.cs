// Copyright (c) Henning Thoele.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using Vortice.DirectX.Direct3D;
using SharpGen.Runtime;
using static Vortice.Direct3D12.D3D12;
using static Vortice.DXGI.DXGI;
using Vortice.Mathematics;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace D3D12SampleRaytracerSharp
{
    internal struct AccelerationStructureBuffers
    {
        public ID3D12Resource Scratch;
        public ID3D12Resource Result;
        public ID3D12Resource InstanceDesc;    // Used only for top-level AS
    };

    public sealed partial class D3D12GraphicsDevice
    {
        private ID3D12Resource[] vertexBuffers;
        private ID3D12Resource topLevelAS;
        private ID3D12Resource[] bottomLevelAS;
        private long tlasSize = 0;

        private AccelerationStructureBuffers buffers;

        private void CreateAccelerationStructure()
        {
            vertexBuffers = new ID3D12Resource[2];

            vertexBuffers[0] = CreateTriangle(device);
            vertexBuffers[1] = CreatePlane(device);

            var vertexCounts = new int[2];
            vertexCounts[0] = 3;
            vertexCounts[1] = 6;

            AccelerationStructureBuffers[] bottomLevelBuffers = new AccelerationStructureBuffers[2];
            bottomLevelBuffers[0] = CreateBottomLevelAS(device, commandList, vertexBuffers, vertexCounts, 2);
            bottomLevelBuffers[1] = CreateBottomLevelAS(device, commandList, vertexBuffers, vertexCounts, 1);

            bottomLevelAS = new ID3D12Resource[2];
            bottomLevelAS[0] = bottomLevelBuffers[0].Result;
            bottomLevelAS[1] = bottomLevelBuffers[1].Result;

            CreateTopLevelAS(device, commandList, bottomLevelAS, ref tlasSize, false, 0);

            SubmitCommandList();
            fence.SetEventOnCompletion(fenceValue, fenceEvent);
            fenceEvent.WaitOne();
            var bufferIndex = swapChain.GetCurrentBackBufferIndex();
            commandList.Reset(frameObjects[0].cmdAllocator, null);

            topLevelAS = buffers.Result;
        }

        private void CreateTopLevelAS(ID3D12Device5 device, ID3D12GraphicsCommandList4 commandList, 
            ID3D12Resource[] bottomLevelAS, ref long tlasSize, bool isUpdate, float rotate)
        {
            int instanceCount = 3;

            BuildRaytracingAccelerationStructureInputs inputs = new BuildRaytracingAccelerationStructureInputs();
            inputs.Layout = ElementsLayout.Array;
            inputs.Flags = RaytracingAccelerationStructureBuildFlags.AllowUpdate;
            inputs.DescriptorsCount = instanceCount;
            inputs.Type = RaytracingAccelerationStructureType.TopLevel;

            var info = device.GetRaytracingAccelerationStructurePrebuildInfo(inputs);

            if (isUpdate == false)
            {
                HeapProperties properties = new HeapProperties(HeapType.Default, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);
                HeapProperties upload_properties = new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);

                long descSize = (long)Unsafe.SizeOf<MyRaytracingInstanceDescription>();

                buffers = new AccelerationStructureBuffers();
                buffers.Scratch = CreateBuffer(device, info.ScratchDataSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.UnorderedAccess, properties);
                buffers.Result = CreateBuffer(device, info.ResultDataMaxSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.RaytracingAccelerationStructure, properties);
                buffers.InstanceDesc = CreateBuffer(device, descSize * instanceCount, ResourceFlags.None, ResourceStates.GenericRead, upload_properties);

                tlasSize = info.ResultDataMaxSizeInBytes;
            }
            else
            {
                InsertUAVResourceBarrier(buffers.Result);
            }

            int instanceContr = 0;
            MyRaytracingInstanceDescription[] instanceDescs = new MyRaytracingInstanceDescription[instanceCount];
            for (int i = 0; i < instanceCount; i++)
            {
                instanceDescs[i] = new MyRaytracingInstanceDescription();

                float xPos = (i - 1) * 1.5f;
                float yPos = 0.0f;
                float zPos = 0.0f;
                instanceDescs[i].Transform = Matrix4x4.Identity;
                if (i != 1)
                {
                    instanceDescs[i].Transform *= Matrix4x4.CreateRotationY(rotate);
                    if (i == 2)
                    {
                        zPos = -0.5f;
                    }
                }
                instanceDescs[i].Transform *= Matrix4x4.CreateTranslation(xPos, yPos, zPos);

                instanceDescs[i].Transform = Matrix4x4.Transpose(instanceDescs[i].Transform);
                instanceDescs[i].InstanceID = i;
                instanceDescs[i].InstanceContributionToHitGroupIndex = instanceContr;
                instanceDescs[i].Flags = (byte)RaytracingInstanceFlags.None;
                instanceDescs[i].InstanceMask = 0xFF;
                if (i == 1)
                {
                    instanceDescs[i].AccelerationStructure = bottomLevelAS[0].GPUVirtualAddress;
                    instanceContr += 4;
                }
                else
                {
                    instanceDescs[i].AccelerationStructure = bottomLevelAS[1].GPUVirtualAddress;
                    instanceContr += 2;
                }
            }

            IntPtr data = buffers.InstanceDesc.Map(0, null);
            Helpers.CopyMemory<MyRaytracingInstanceDescription>(data, new ReadOnlySpan<MyRaytracingInstanceDescription>(
                instanceDescs));
            buffers.InstanceDesc.Unmap(0, null);

            BuildRaytracingAccelerationStructureDescription asDesc = new BuildRaytracingAccelerationStructureDescription();
            asDesc.Inputs = inputs;
            asDesc.Inputs.InstanceDescriptions = buffers.InstanceDesc.GPUVirtualAddress;
            asDesc.DestinationAccelerationStructureData = buffers.Result.GPUVirtualAddress;
            asDesc.ScratchAccelerationStructureData = buffers.Scratch.GPUVirtualAddress;

            if (isUpdate)
            {
                asDesc.Inputs.Flags |= RaytracingAccelerationStructureBuildFlags.PerformUpdate;
                asDesc.SourceAccelerationStructureData = buffers.Result.GPUVirtualAddress;
            }

            commandList.BuildRaytracingAccelerationStructure(asDesc);

            InsertUAVResourceBarrier(buffers.Result);
        }

        private AccelerationStructureBuffers CreateBottomLevelAS(ID3D12Device5 device, ID3D12GraphicsCommandList4 commandList, 
            ID3D12Resource[] vertexBuffers, int[] vertexCounts, int geometryCount)
        {
            RaytracingGeometryDescription[] descs = new RaytracingGeometryDescription[geometryCount];
            for (int i = 0; i < descs.Length; i++)
            {
                descs[i] = new RaytracingGeometryDescription();
                descs[i].Type = RaytracingGeometryType.Triangles;
                descs[i].Triangles = new RaytracingGeometryTrianglesDescription();
                descs[i].Triangles.VertexBuffer = new GpuVirtualAddressAndStride();
                descs[i].Triangles.VertexBuffer.StartAddress = vertexBuffers[i].GPUVirtualAddress;
                descs[i].Triangles.VertexBuffer.StrideInBytes = 3 * 4;
                descs[i].Triangles.VertexFormat = Format.R32G32B32_Float;
                descs[i].Triangles.VertexCount = vertexCounts[i];
                descs[i].Flags = RaytracingGeometryFlags.Opaque;
            }

            BuildRaytracingAccelerationStructureInputs inputs = new BuildRaytracingAccelerationStructureInputs();
            inputs.Layout = ElementsLayout.Array;
            inputs.Flags = RaytracingAccelerationStructureBuildFlags.None;
            inputs.DescriptorsCount = geometryCount;
            inputs.GeometryDescriptions = descs;
            inputs.Type = RaytracingAccelerationStructureType.BottomLevel;

            RaytracingAccelerationStructurePrebuildInfo info = device.GetRaytracingAccelerationStructurePrebuildInfo(inputs);

            HeapProperties properties = new HeapProperties(HeapType.Default, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);

            AccelerationStructureBuffers buffers = new AccelerationStructureBuffers();
            buffers.Scratch = CreateBuffer(device, info.ScratchDataSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.UnorderedAccess, properties);
            buffers.Result = CreateBuffer(device, info.ResultDataMaxSizeInBytes, ResourceFlags.AllowUnorderedAccess, ResourceStates.RaytracingAccelerationStructure, properties);

            BuildRaytracingAccelerationStructureDescription asDesc = new BuildRaytracingAccelerationStructureDescription();
            asDesc.Inputs = inputs;
            asDesc.DestinationAccelerationStructureData = buffers.Result.GPUVirtualAddress;
            asDesc.ScratchAccelerationStructureData = buffers.Scratch.GPUVirtualAddress;

            commandList.BuildRaytracingAccelerationStructure(asDesc);

            commandList.ResourceBarrier(ResourceBarrier.BarrierUnorderedAccessView(buffers.Result));

            return buffers;
        }

        private ID3D12Resource CreateTriangle(ID3D12Device5 device)
        {
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(      0,     1, 0),
                new Vector3( 0.866f, -0.5f, 0),
                new Vector3(-0.866f, -0.5f, 0)
            };

            var res = CreateBuffer(device, vertices.Length * (3 * 4), ResourceFlags.None, ResourceStates.GenericRead, 
                new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0));

            IntPtr data = res.Map(0, null);
            Helpers.CopyMemory<Vector3>(data, new ReadOnlySpan<Vector3>(vertices));
            res.Unmap(0, null);
            return res;
        }

        private ID3D12Resource CreatePlane(ID3D12Device5 device)
        {
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-100, -1,  -2),
                new Vector3( 100, -1,  100),
                new Vector3(-100, -1,  100),

                new Vector3(-100, -1,  -2),
                new Vector3( 100, -1,  -2),
                new Vector3( 100, -1,  100),
            };

            var res = CreateBuffer(device, vertices.Length * (3 * 4), ResourceFlags.None, ResourceStates.GenericRead,
                new HeapProperties(HeapType.Upload, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0));

            IntPtr data = res.Map(0, null);
            Helpers.CopyMemory<Vector3>(data, new ReadOnlySpan<Vector3>(vertices));
            res.Unmap(0, null);
            return res;
        }
    }
}
