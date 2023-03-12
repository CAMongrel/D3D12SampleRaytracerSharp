// Copyright (c) Henning Thoele.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.DirectX.Direct3D;
using static Vortice.Direct3D12.D3D12;
using static Vortice.DXGI.DXGI;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace D3D12SampleRaytracerSharp
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct Matrix3x4
    {
        [FieldOffset(0)]
        public Vector4 Transform1;
        [FieldOffset(16)]
        public Vector4 Transform2;
        [FieldOffset(32)]
        public Vector4 Transform3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MyRaytracingInstanceDescription
    {
        [FieldOffset(0)]
        public Matrix4x4 Transform;
        [FieldOffset(48)]
        public int InstanceID;
        [FieldOffset(51)]
        public byte InstanceMask;
        [FieldOffset(52)]
        public int InstanceContributionToHitGroupIndex;
        [FieldOffset(55)]
        public byte Flags;
        [FieldOffset(56)]
        public long AccelerationStructure;
    }

    internal struct HeapData
    {
        public ID3D12DescriptorHeap Heap;
        public uint usedEntries;
    };

    internal struct FrameObject
    {
        public ID3D12CommandAllocator cmdAllocator;
        public ID3D12Resource swapChainBuffer;
        public CpuDescriptorHandle rtvHandle;
    };

    public sealed partial class D3D12GraphicsDevice
    {
        internal const int rtvHeapSize = 3;
        internal const int DefaultSwapChainBuffers = 3;

        public readonly Window Window;

        private IDXGIFactory4 factory;
        private ID3D12Device5 device;
        private ID3D12CommandQueue commandQueue;
        private ID3D12GraphicsCommandList4 commandList;
        private ID3D12Fence fence;
        private EventWaitHandle fenceEvent;
        private IDXGISwapChain3 swapChain;
        private HeapData rtvHeap;
        private FrameObject[] frameObjects;
        private long fenceValue = 0;

        private long frameCounter = 0;
        private Stopwatch stopwatch;

        private float rotation = 0;

        public D3D12GraphicsDevice(Window window)
        {
            if (!IsSupported())
            {
                throw new InvalidOperationException("Direct3D12 is not supported on current OS");
            }

            Window = window;

            CreateDXGI();
            CreateAccelerationStructure();
            CreateRtPipelineState();
            CreateShaderResources();
            CreateConstantBuffer();
            CreateShaderTable();

            frameCounter = 0;
            stopwatch = new Stopwatch();
            stopwatch.Start();
        }

        private ID3D12Resource CreateBuffer(ID3D12Device5 device, long size, ResourceFlags flags, ResourceStates initState, HeapProperties heapProperties)
        {
            ResourceDescription desc = new ResourceDescription();
            desc.Alignment = 0;
            desc.DepthOrArraySize = 1;
            desc.Dimension = ResourceDimension.Buffer;
            desc.Flags = flags;
            desc.Format = Format.Unknown;
            desc.Width = size;
            desc.Height = 1;
            desc.Layout = TextureLayout.RowMajor;
            desc.MipLevels = 1;
            desc.SampleDescription = new SampleDescription();
            desc.SampleDescription.Count = 1;
            desc.SampleDescription.Quality = 0;

            return device.CreateCommittedResource(heapProperties, HeapFlags.None, desc, initState, null);
        }

        private void CreateDXGI()
        {
            var res = CreateDXGIFactory1<IDXGIFactory4>(out factory);
            device = CreateDevice(factory);
            if (device == null)
            {
                throw new InvalidOperationException("device cannot be null");
            }
            commandQueue = CreateCommandQueue(device);
            swapChain = CreateSwapChain(factory, commandQueue, Format.R8G8B8A8_UNorm);

            rtvHeap.usedEntries = 0;
            rtvHeap.Heap = CreateDescriptorHeap(device, rtvHeapSize, DescriptorHeapType.RenderTargetView, false);

            frameObjects = new FrameObject[DefaultSwapChainBuffers];
            for (int i = 0; i < DefaultSwapChainBuffers; i++)
            {
                frameObjects[i] = new FrameObject();

                frameObjects[i].cmdAllocator = device.CreateCommandAllocator(CommandListType.Direct);
                frameObjects[i].swapChainBuffer = swapChain.GetBuffer<ID3D12Resource>(i);
                frameObjects[i].rtvHandle = CreateRenderTargetView(device, frameObjects[i].swapChainBuffer, rtvHeap.Heap,
                    ref rtvHeap.usedEntries, Format.R8G8B8A8_UNorm_SRgb);
            }

            var cmdList = device.CreateCommandList(0, CommandListType.Direct, frameObjects[0].cmdAllocator, null);
            commandList = cmdList.QueryInterface<ID3D12GraphicsCommandList4>();

            fence = device.CreateFence(0, FenceFlags.None);
            fenceEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
        }

        private CpuDescriptorHandle CreateRenderTargetView(ID3D12Device device, ID3D12Resource swapChainBuffer, ID3D12DescriptorHeap heap, 
            ref uint usedEntries, Format format)
        {
            RenderTargetViewDescription viewDesc = new RenderTargetViewDescription();
            viewDesc.ViewDimension = RenderTargetViewDimension.Texture2D;
            viewDesc.Format = format;
            viewDesc.Texture2D = new Texture2DRenderTargetView();
            viewDesc.Texture2D.MipSlice = 0;

            CpuDescriptorHandle handle = heap.GetCPUDescriptorHandleForHeapStart();
            handle.Ptr += usedEntries * device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);
            usedEntries++;

            device.CreateRenderTargetView(swapChainBuffer, viewDesc, handle);
            return handle;
        }

        private ID3D12DescriptorHeap CreateDescriptorHeap(ID3D12Device device, int count, DescriptorHeapType type, bool shaderVisible)
        {
            DescriptorHeapDescription desc = new DescriptorHeapDescription();
            desc.DescriptorCount = count;
            desc.Flags = shaderVisible ? DescriptorHeapFlags.ShaderVisible : DescriptorHeapFlags.None;
            desc.Type = type;

            return device.CreateDescriptorHeap(desc);
        }

        private IDXGISwapChain3 CreateSwapChain(IDXGIFactory4 factory, ID3D12CommandQueue commandQueue, Format format)
        {
            SwapChainDescription1 desc = new SwapChainDescription1();
            desc.Format = format;
            desc.Width = Window.Width;
            desc.Height = Window.Height;
            desc.BufferCount = DefaultSwapChainBuffers;
            desc.Usage = Usage.RenderTargetOutput;
            desc.SwapEffect = SwapEffect.FlipDiscard;
            desc.SampleDescription = new SampleDescription();
            desc.SampleDescription.Count = 1;

            var swapChain = factory.CreateSwapChainForHwnd(commandQueue, (IntPtr)Window.Handle, desc);

            return swapChain.QueryInterface<IDXGISwapChain3>();
        }

        private ID3D12CommandQueue CreateCommandQueue(ID3D12Device device)
        {
            var desc = new CommandQueueDescription();
            desc.Type = CommandListType.Direct;
            desc.Flags = CommandQueueFlags.None;
            return device.CreateCommandQueue(desc);
        }

        private ID3D12Device5 CreateDevice(IDXGIFactory4 factory4)
        {
            var adapters = factory4.EnumAdapters1();
            for (int i = 0; i < adapters.Length; i++)
            {
                var desc = adapters[i].Description1;
                if (desc.Flags.HasFlag(AdapterFlags.Software))
                {
                    continue;
                }

                var res = D3D12CreateDevice(adapters[i], Vortice.DirectX.Direct3D.FeatureLevel.Level_12_1, out var dev);
                FeatureDataD3D12Options5 opt5 = dev.CheckFeatureSupport<FeatureDataD3D12Options5>(Vortice.Direct3D12.Feature.Options5);
                if (opt5.RaytracingTier != RaytracingTier.Tier1_0 &&
                    opt5.RaytracingTier != (RaytracingTier)11)
                {
                    throw new NotSupportedException("Raytracing not supported");
                }
                return dev.QueryInterface<ID3D12Device5>();
            }
            return null;
        }

        public static bool IsSupported()
        {
            return ID3D12Device.IsSupported(null, Vortice.DirectX.Direct3D.FeatureLevel.Level_11_0);
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        private int BeginFrame()
        {
            commandList.SetDescriptorHeaps(1, new ID3D12DescriptorHeap[] { srvUavHeap });
            return swapChain.GetCurrentBackBufferIndex();
        }

        private void EndFrame(int rtvIndex)
        {
            InsertTransitionResourceBarrier(frameObjects[rtvIndex].swapChainBuffer, ResourceStates.CopyDestination, ResourceStates.Present);
            SubmitCommandList();

            swapChain.Present(0, PresentFlags.None);

            var bufferIndex = swapChain.GetCurrentBackBufferIndex();

            if (fenceValue > DefaultSwapChainBuffers)
            {
                fence.SetEventOnCompletion(fenceValue - DefaultSwapChainBuffers + 1, fenceEvent);
                fenceEvent.WaitOne();
            }

            frameObjects[bufferIndex].cmdAllocator.Reset();
            commandList.Reset(frameObjects[bufferIndex].cmdAllocator, null);
        }

        private void SubmitCommandList()
        {
            commandList.Close();
            commandQueue.ExecuteCommandList(commandList);
            fenceValue++;
            commandQueue.Signal(fence, fenceValue);
        }

        public bool DrawFrame(Action<int, int> draw, [CallerMemberName] string frameName = null)
        {
            int rtvIndex = BeginFrame();

            CreateTopLevelAS(device, commandList, bottomLevelAS, ref tlasSize, true, rotation);
            rotation += 0.0005f;

            // Let's raytrace
            InsertTransitionResourceBarrier(outputResource, ResourceStates.CopySource, ResourceStates.UnorderedAccess);
            DispatchRaysDescription raytraceDesc = new DispatchRaysDescription();
            raytraceDesc.Width = Window.Width;
            raytraceDesc.Height = Window.Height;
            raytraceDesc.Depth = 1;

            // RayGen is the first entry in the shader-table
            raytraceDesc.RayGenerationShaderRecord = new GpuVirtualAddressRange();
            raytraceDesc.RayGenerationShaderRecord.StartAddress = shaderTable.GPUVirtualAddress + 0 * shaderTableEntrySize;
            raytraceDesc.RayGenerationShaderRecord.SizeInBytes = shaderTableEntrySize;

            // Miss is the second entry in the shader-table
            uint missOffset = 1 * shaderTableEntrySize;
            raytraceDesc.MissShaderTable = new GpuVirtualAddressRangeAndStride();
            raytraceDesc.MissShaderTable.StartAddress = shaderTable.GPUVirtualAddress + missOffset;
            raytraceDesc.MissShaderTable.StrideInBytes = shaderTableEntrySize;
            raytraceDesc.MissShaderTable.SizeInBytes = shaderTableEntrySize * 2;   // Only a s single miss-entry

            // Hit is the third entry in the shader-table
            uint hitOffset = 3 * shaderTableEntrySize;
            raytraceDesc.HitGroupTable = new GpuVirtualAddressRangeAndStride();
            raytraceDesc.HitGroupTable.StartAddress = shaderTable.GPUVirtualAddress + hitOffset;
            raytraceDesc.HitGroupTable.StrideInBytes = shaderTableEntrySize;
            raytraceDesc.HitGroupTable.SizeInBytes = shaderTableEntrySize * ((triangleConstantBuffers.Length + 1) * 2);

            // Bind the empty root signature
            commandList.SetComputeRootSignature(emptyRootSig);

            // Dispatch
            commandList.SetPipelineState1(pipelineState);
            commandList.DispatchRays(raytraceDesc);

            // Copy the results to the back-buffer
            InsertTransitionResourceBarriers(outputResource,
                new ResourceStates[]
                {
                    ResourceStates.UnorderedAccess,
                    ResourceStates.Present
                },
                new ResourceStates[]
                {
                    ResourceStates.CopySource,
                    ResourceStates.CopyDestination
                });
            commandList.CopyResource(frameObjects[rtvIndex].swapChainBuffer, outputResource);

            EndFrame(rtvIndex);

            frameCounter++;

            if (stopwatch.ElapsedMilliseconds >= 1000)
            {                
                Console.WriteLine("FPS: " + frameCounter);

                long primaryRayCount = Window.Width * Window.Height;
                long primaryRaysPerSecond = primaryRayCount * frameCounter;
                Console.WriteLine((primaryRaysPerSecond / 1000000) + " MRays/second");

                frameCounter = 0;
                stopwatch.Restart();
            }

            return true;
        }

        private void InsertUAVResourceBarrier(ID3D12Resource uavBuffer)
        {
            var uav = new ResourceUnorderedAccessViewBarrier(uavBuffer);
            ResourceBarrier barrier = new ResourceBarrier(uav);
            commandList.ResourceBarrier(barrier);
        }

        private void InsertTransitionResourceBarrier(ID3D12Resource swapChainBuffer, ResourceStates before, ResourceStates after)
        {
            var transition = new ResourceTransitionBarrier(swapChainBuffer, before, after, ResourceBarrier.AllSubResources);
            ResourceBarrier barrier = new ResourceBarrier(transition, ResourceBarrierFlags.None);
            commandList.ResourceBarrier(barrier);
        }

        private void InsertTransitionResourceBarriers(ID3D12Resource swapChainBuffer, ResourceStates[] before, ResourceStates[] after)
        {
            ResourceBarrier[] barriers = new ResourceBarrier[before.Length];
            for (int i = 0; i < barriers.Length; i++)
            {
                var transition = new ResourceTransitionBarrier(swapChainBuffer, before[i], after[i], ResourceBarrier.AllSubResources);
                barriers[i] = new ResourceBarrier(transition, ResourceBarrierFlags.None);
            }
            commandList.ResourceBarrier(barriers);
        }
    }
}
