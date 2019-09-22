// Copyright (c) Henning Thoele.
// Distributed under the MIT license. See the LICENSE file in the project root for more information.

using System;
using Vortice.Direct3D12;
using System.IO;
using Vortice.Dxc;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace D3D12SampleRaytracerSharp
{
    class DxilLibrary
    {
        internal StateSubObject stateSubObject;
        private DxilLibraryDescription libraryDesc;

        internal DxilLibrary(IDxcBlob blob, string[] entryPoints)
        {
            var shaderBytes = Dxc.GetBytesFromBlob(blob);

            ExportDescription[] exportDesc = new ExportDescription[entryPoints.Length];
            for (int i = 0; i < exportDesc.Length; i++)
            {
                exportDesc[i] = new ExportDescription();
                exportDesc[i].Name = entryPoints[i];
                exportDesc[i].Flags = ExportFlags.None;
                exportDesc[i].ExportToRename = null;
            }

            libraryDesc = new DxilLibraryDescription(new ShaderBytecode(shaderBytes), exportDesc);

            stateSubObject = new StateSubObject(libraryDesc);
        }
    }

    struct HitProgram
    {
        public string ExportName;
        public HitGroupDescription Desc;
        public StateSubObject subObject;

        public HitProgram(string ahsExport, string chsExport, string name)
        {
            ExportName = name;
            Desc = new HitGroupDescription();
            Desc.IntersectionShaderImport = null;
            Desc.Type = HitGroupType.Triangles;
            Desc.HitGroupExport = name;
            Desc.AnyHitShaderImport = ahsExport;
            Desc.ClosestHitShaderImport = chsExport;

            subObject = new StateSubObject(Desc);
        }
    };

    struct ExportAssociation
    {
        public StateSubObject SubObject;
        public SubObjectToExportsAssociation Association;

        internal ExportAssociation(string[] ExportNames, StateSubObject subObjectToAssociate)
        {
            Association = new SubObjectToExportsAssociation(subObjectToAssociate, ExportNames);
            SubObject = new StateSubObject(Association);
        }
    }

    struct MyLocalRootSignature
    {
        public LocalRootSignature rootSig;
        public StateSubObject subObject;

        internal MyLocalRootSignature(ID3D12Device5 device, RootSignatureDescription desc)
        {
            rootSig = new LocalRootSignature();
            rootSig.RootSignature = device.CreateRootSignature(desc, RootSignatureVersion.Version1);
            subObject = new StateSubObject(rootSig);
        }
    }

    struct MyGlobalRootSignature
    {
        public GlobalRootSignature rootSig;
        public StateSubObject subObject;

        internal MyGlobalRootSignature(ID3D12Device5 device, RootSignatureDescription desc)
        {
            rootSig = new GlobalRootSignature();
            rootSig.RootSignature = device.CreateRootSignature(desc, RootSignatureVersion.Version1);
            subObject = new StateSubObject(rootSig);
        }
    }

    struct ShaderConfig
    {
        private RaytracingShaderConfig config;
        public StateSubObject subObject;

        internal ShaderConfig(int maxAttributeSize, int maxPayloadSize)
        {
            config = new RaytracingShaderConfig(maxPayloadSize, maxAttributeSize);
            subObject = new StateSubObject(config);
        }
    }

    struct PipelineConfig
    {
        private RaytracingPipelineConfig config;
        public StateSubObject SubObject;

        internal PipelineConfig(int maxRecursion)
        {
            config = new RaytracingPipelineConfig(maxRecursion);
            SubObject = new StateSubObject(config);
        }
    }

    public sealed partial class D3D12GraphicsDevice
    {
        private ID3D12StateObject pipelineState;
        private ID3D12RootSignature emptyRootSig;

        private const string RayGenShader = "rayGen";
        private const string MissShader = "miss";
        private const string TriangleClosestHitShader = "chs";
        private const string PlaneClosestHitShader = "planeChs";
        private const string HitGroupTri = "HitGroup";
        private const string HitGroupPlane = "HitGroupPlane";
        private const string ShadowChsShader = "shadowChs";
        private const string ShadowMissShader = "shadowMiss";
        private const string ShadowHitGroup = "ShadowHitGroup";

        private IDxcBlob CompileLibrary(string filename)
        {
            string content = File.ReadAllText(filename);

            var res = DxcCompiler.Compile(DxcShaderStage.Library, content, "", "", new DxcCompilerOptions()
            {
                ShaderModel = new DxcShaderModel(6, 3)
            });

            Console.WriteLine(res.GetStatus());

            return res.GetResult();
        }

        private DxilLibrary CreateDxilLibrary()
        {
            var res = CompileLibrary("Data\\Shaders.hlsl");
            return new DxilLibrary(res, new string[] { RayGenShader, MissShader, TriangleClosestHitShader,
                PlaneClosestHitShader, ShadowChsShader, ShadowMissShader });
        }

        private void CreateRtPipelineState()
        {
            List<StateSubObject> subObjects = new List<StateSubObject>();

            // Create the DXIL library
            DxilLibrary library = CreateDxilLibrary();
            subObjects.Add(library.stateSubObject); // 0 Library

            // Create the triangle HitProgram
            HitProgram triHitProgram = new HitProgram(null, TriangleClosestHitShader, HitGroupTri);
            subObjects.Add(triHitProgram.subObject); // 1 Triangle Hit Group

            // Create the plane HitProgram
            HitProgram planeHitProgram = new HitProgram(null, PlaneClosestHitShader, HitGroupPlane);
            subObjects.Add(planeHitProgram.subObject); // 2 Plane Hit Group

            // Create the shadow-ray hit group
            HitProgram shadowHitProgram = new HitProgram(null, ShadowChsShader, ShadowHitGroup);
            subObjects.Add(shadowHitProgram.subObject); // 3 Shadow Hit Group

            // Create the ray-gen root-signature and association
            MyLocalRootSignature rgsRootSignature = new MyLocalRootSignature(device, CreateRayGenRootDesc());
            subObjects.Add(rgsRootSignature.subObject); // 4 Ray Gen Root Sig

            int rgsRootIndex = subObjects.Count - 1; // 4
            ExportAssociation rgsRootAssociation = new ExportAssociation(new string[] { RayGenShader }, subObjects[rgsRootIndex]);
            subObjects.Add(rgsRootAssociation.SubObject); // 5 Associate Root Sig to RGS

            // Create the tri hit root-signature and association
            MyLocalRootSignature triHitRootSignature = new MyLocalRootSignature(device, CreateTriangleHitRootDesc());
            subObjects.Add(triHitRootSignature.subObject); // 6 Triangle Hit Root Sig

            int triHitRootIndex = subObjects.Count - 1; // 6
            ExportAssociation triHitRootAssociation = new ExportAssociation(new string[] { TriangleClosestHitShader }, subObjects[triHitRootIndex]);
            subObjects.Add(triHitRootAssociation.SubObject); // 7 Associate Triangle Root Sig to Triangle Hit Group

            // Create the plane hit root-signature and association
            MyLocalRootSignature planeHitRootSignature = new MyLocalRootSignature(device, CreatePlaneHitRootDesc());
            subObjects.Add(planeHitRootSignature.subObject); // 8 Plane Hit Root Sig

            int planeHitRootIndex = subObjects.Count - 1; // 8
            ExportAssociation planeHitRootAssociation = new ExportAssociation(new string[] { HitGroupPlane }, subObjects[planeHitRootIndex]);
            subObjects.Add(planeHitRootAssociation.SubObject); // 9 Associate Plane Hit Root Sig to Plane Hit Group

            // Create the empty root-signature and associate it with the primary miss-shader and the shadow programs
            MyLocalRootSignature emptyRootSignature = new MyLocalRootSignature(device, new RootSignatureDescription(RootSignatureFlags.LocalRootSignature));
            subObjects.Add(emptyRootSignature.subObject); // 10 Empty Root Sig for Plane Hit Group and Miss

            int emptyRootIndex = subObjects.Count - 1; // 10
            ExportAssociation emptyRootAssociation = new ExportAssociation(new string[] { MissShader, ShadowChsShader, ShadowMissShader },
                subObjects[emptyRootIndex]);
            subObjects.Add(emptyRootAssociation.SubObject); // 11 Associate empty root sig to Plane Hit Group and Miss shader

            // Bind the payload size to all programs
            ShaderConfig primaryShaderConfig = new ShaderConfig(Unsafe.SizeOf<float>() * 2, Unsafe.SizeOf<float>() * 3);
            subObjects.Add(primaryShaderConfig.subObject); // 12

            int primaryShaderConfigIndex = subObjects.Count - 1;
            ExportAssociation primaryConfigAssociation = new ExportAssociation(
                new string[] { RayGenShader, MissShader, TriangleClosestHitShader, PlaneClosestHitShader, ShadowMissShader, ShadowChsShader },
                subObjects[primaryShaderConfigIndex]);
            subObjects.Add(primaryConfigAssociation.SubObject); // 13 Associate shader config to all programs

            // Create the pipeline config
            PipelineConfig pconfig = new PipelineConfig(2); // maxRecursionDepth - 1 TraceRay() from the ray-gen, 1 TraceRay() from the primary hit-shader
            subObjects.Add(pconfig.SubObject); // 14

            // Create the global root signature and store the empty signature
            MyGlobalRootSignature root = new MyGlobalRootSignature(device, new RootSignatureDescription());
            emptyRootSig = root.rootSig.RootSignature;
            subObjects.Add(root.subObject); // 15

            // Create the state
            StateObjectDescription desc = new StateObjectDescription(StateObjectType.RaytracingPipeline, subObjects.ToArray());
            pipelineState = device.CreateStateObject(desc);
        }

        private RootSignatureDescription CreateRayGenRootDesc()
        {
            // Create the root-signature
            RootSignatureDescription desc = new RootSignatureDescription(RootSignatureFlags.LocalRootSignature,
                new RootParameter[]
                {
                    new RootParameter(new RootDescriptorTable(new DescriptorRange[]
                    {
                        new DescriptorRange(DescriptorRangeType.UnorderedAccessView, 1, 0, 0, 0),
                        new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0, 0, 1)
                    }), ShaderVisibility.All)
                });
            return desc;
        }

        private RootSignatureDescription CreateTriangleHitRootDesc()
        {
            // Create the root-signature
            RootSignatureDescription desc = new RootSignatureDescription(RootSignatureFlags.LocalRootSignature,
                new RootParameter[]
                {
                    new RootParameter(RootParameterType.ConstantBufferView, new RootDescriptor(0, 0), ShaderVisibility.All),
                    new RootParameter(new RootDescriptorTable(new DescriptorRange[]
                    {
                        new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0, 0, 0)
                    }), ShaderVisibility.All)
                });
            return desc;
        }

        private RootSignatureDescription CreatePlaneHitRootDesc()
        {
            // Create the root-signature
            RootSignatureDescription desc = new RootSignatureDescription(RootSignatureFlags.LocalRootSignature,
                new RootParameter[]
                {
                    new RootParameter(new RootDescriptorTable(new DescriptorRange[]
                    {
                        new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0, 0, 0)
                    }), ShaderVisibility.All)
                });
            return desc;
        }
    }
}
