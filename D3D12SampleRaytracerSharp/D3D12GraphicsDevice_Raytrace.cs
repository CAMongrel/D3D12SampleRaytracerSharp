// Copyright (c) Henning Thoele.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using Vortice.Direct3D12;
using System.IO;
using Vortice.Dxc;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.DXGI;

namespace D3D12SampleRaytracerSharp
{
    public sealed partial class D3D12GraphicsDevice
    {
        private ID3D12Resource outputResource;
        private ID3D12DescriptorHeap srvUavHeap;

        private void CreateShaderResources()
        {
            HeapProperties heapProperties = new HeapProperties(HeapType.Default, CpuPageProperty.Unknown, MemoryPool.Unknown, 0, 0);

            // Create the output resource. The dimensions and format should match the swap-chain
            ResourceDescription resDesc = new ResourceDescription();
            resDesc.Alignment = 0;
            resDesc.DepthOrArraySize = 1;
            resDesc.Dimension = ResourceDimension.Texture2D;
            resDesc.Flags = ResourceFlags.AllowUnorderedAccess;
            resDesc.Format = Format.R8G8B8A8_UNorm;
            resDesc.Width = Window.Width;
            resDesc.Height = Window.Height;
            resDesc.Layout = TextureLayout.Unknown;
            resDesc.MipLevels = 1;
            resDesc.SampleDescription = new SampleDescription();
            resDesc.SampleDescription.Count = 1;

            outputResource = device.CreateCommittedResource(heapProperties, HeapFlags.None, resDesc, ResourceStates.CopySource, null);

            // Create an SRV/UAV descriptor heap. Need 2 entries - 1 SRV for the scene and 1 UAV for the output
            srvUavHeap = CreateDescriptorHeap(device, 2, DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, true);

            // Create the UAV. Based on the root signature we created it should be the first entry
            UnorderedAccessViewDescription uavDesc = new UnorderedAccessViewDescription();
            uavDesc.ViewDimension = UnorderedAccessViewDimension.Texture2D;
            device.CreateUnorderedAccessView(outputResource, null, uavDesc, srvUavHeap.GetCPUDescriptorHandleForHeapStart());

            // Create the TLAS SRV right after the UAV. Note that we are using a different SRV desc here
            ShaderResourceViewDescription srvDesc = new ShaderResourceViewDescription();
            srvDesc.ViewDimension = ShaderResourceViewDimension.RaytracingAccelerationStructure;
            srvDesc.Shader4ComponentMapping = 5768; // D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING
            srvDesc.RaytracingAccelerationStructure = new RaytracingAccelerationStructureShaderResourceView();
            srvDesc.RaytracingAccelerationStructure.Location = topLevelAS.GPUVirtualAddress;

            CpuDescriptorHandle srvHandle = srvUavHeap.GetCPUDescriptorHandleForHeapStart();
            srvHandle.Ptr += device.GetDescriptorHandleIncrementSize(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView);
            device.CreateShaderResourceView(null, srvDesc, srvHandle);
        }
    }
}

/*
 * #define D3D12_SHADER_COMPONENT_MAPPING_MASK 0x7 
#define D3D12_SHADER_COMPONENT_MAPPING_SHIFT 3 
#define D3D12_SHADER_COMPONENT_MAPPING_ALWAYS_SET_BIT_AVOIDING_ZEROMEM_MISTAKES (1<<(D3D12_SHADER_COMPONENT_MAPPING_SHIFT*4)) 
#define D3D12_ENCODE_SHADER_4_COMPONENT_MAPPING(Src0,Src1,Src2,Src3) ((((Src0)&D3D12_SHADER_COMPONENT_MAPPING_MASK)| \
                                                                (((Src1)&D3D12_SHADER_COMPONENT_MAPPING_MASK)<<D3D12_SHADER_COMPONENT_MAPPING_SHIFT)| \
                                                                (((Src2)&D3D12_SHADER_COMPONENT_MAPPING_MASK)<<(D3D12_SHADER_COMPONENT_MAPPING_SHIFT*2))| \
                                                                (((Src3)&D3D12_SHADER_COMPONENT_MAPPING_MASK)<<(D3D12_SHADER_COMPONENT_MAPPING_SHIFT*3))| \
                                                                D3D12_SHADER_COMPONENT_MAPPING_ALWAYS_SET_BIT_AVOIDING_ZEROMEM_MISTAKES))
#define D3D12_DECODE_SHADER_4_COMPONENT_MAPPING(ComponentToExtract,Mapping) ((D3D12_SHADER_COMPONENT_MAPPING)(Mapping >> (D3D12_SHADER_COMPONENT_MAPPING_SHIFT*ComponentToExtract) & D3D12_SHADER_COMPONENT_MAPPING_MASK))
#define D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING D3D12_ENCODE_SHADER_4_COMPONENT_MAPPING(0,1,2,3) 
*/
