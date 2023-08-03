using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TrueTrace {
    [System.Serializable]
    public class AtmosphereGenerator
    {

        public RenderTexture _TransmittanceLUT;
        public RenderTexture _RayleighTex;
        public RenderTexture _MieTex;
        public RenderTexture MultiScatterTex;

        public RenderTexture DeltaIrradianceTex;
        public RenderTexture IrradianceTex;

        public RenderTexture ScatteringTex;

        public RenderTexture DeltaScatteringTex;

        public RenderTexture DeltaMultiScatterTex;
        public RenderTexture DebugTex;

        public RenderTexture CloudTex1;
        public RenderTexture CloudTex2;

        public ComputeShader Atmosphere;
        private ComputeBuffer rayleigh_densityC;
        private ComputeBuffer mie_densityC;
        private ComputeBuffer absorption_densityC;

        private int SkyViewKernel;

        public struct DensityProfileLayer
        {
            public float width;
            public float exp_term;
            public float exp_scale;
            public float linear_term;
            public float constant_term;
        }

        public Texture2D PermutationTable2D;
        public Texture2D Gradient3D;

        private void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
            where T : struct
        {
            // Do we already have a compute buffer?
            if (buffer != null)
            {
                // If no data or buffer doesn't match the given criteria, release it
                if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
                {
                    buffer.Release();
                    buffer = null;
                }
            }

            if (data.Count != 0)
            {
                // If the buffer has been released or wasn't there to
                // begin with, create it
                if (buffer == null)
                {
                    buffer = new ComputeBuffer(data.Count, stride);
                }
                // Set data on the buffer
                buffer.SetData(data);
            }
        }

        public AtmosphereGenerator(ComputeShader Atmosphere, float BottomRadius, float TopRadius, int MultiScatterIterations)
        {

            this.Atmosphere = Atmosphere;
            List<DensityProfileLayer> rayleigh_density = new List<DensityProfileLayer>();
            List<DensityProfileLayer> mie_density = new List<DensityProfileLayer>();
            List<DensityProfileLayer> absorption_density = new List<DensityProfileLayer>();
            float rayleigh_scale_height = 8000.0f;
            float mie_scale_height = 1200.0f;
            float ozone_scale_height = 15000.0f;
            float ozone_height = 25000.0f;
            float density = 0.001f;
            Vector3 ray_s = new Vector3(5.85f, 13.558f, 33.10f) * density;
            Vector3 ray_a = new Vector3(0.0f, 0.0f, 0.0f);
            Vector3 ray_e = ray_s + ray_a;
            Vector3 mie_s = new Vector3(3.996f, 3.996f, 3.996f) * density;
            Vector3 mie_a = new Vector3(4.4f, 4.4f, 4.4f) * density;
            Vector3 mie_e = mie_s + mie_a;
            Vector3 ozo_s = new Vector3(0.0f, 0.0f, 0.0f);
            Vector3 ozo_a = new Vector3(0.65f, 1.881f, 0.085f) * density;
            Vector3 ozo_e = ozo_s + ozo_a;
            rayleigh_density.Add(new DensityProfileLayer()
            {
                width = 0.0f,
                exp_term = 0.0f,
                exp_scale = 0.0f,
                linear_term = 0.0f,
                constant_term = 0.0f
            });
            rayleigh_density.Add(new DensityProfileLayer()
            {
                width = 0.0f,
                exp_term = 1.0f,
                exp_scale = -0.125f,
                linear_term = 0.0f,
                constant_term = 0.0f
            });
            mie_density.Add(new DensityProfileLayer()
            {
                width = 0.0f,
                exp_term = 0.0f,
                exp_scale = 0.0f,
                linear_term = 0.0f,
                constant_term = 0.0f
            });
            mie_density.Add(new DensityProfileLayer()
            {
                width = 0.0f,
                exp_term = 1.0f,
                exp_scale = -0.833333333333333f,
                linear_term = 0.0f,
                constant_term = 0.0f
            });
            absorption_density.Add(new DensityProfileLayer()
            {
                width = 25,
                exp_term = 0.0f,
                exp_scale = 0.0f,
                linear_term = 0.0666666666666667f,
                constant_term = -0.666666666666667f
            });
            absorption_density.Add(new DensityProfileLayer()
            {
                width = 0.0f,
                exp_term = 0.0f,
                exp_scale = 0.0f,
                linear_term = -0.0666666666666667f,
                constant_term = 2.66666666666667f
            });

            int TransmittanceKernel = Atmosphere.FindKernel("Transmittance_Kernel");
            int SingleScatterKernel = Atmosphere.FindKernel("SingleScatter_Kernel");
            int DirectIrradianceKernel = Atmosphere.FindKernel("DirectIrradiance_Kernel");
            int IndirectIrradianceKernel = Atmosphere.FindKernel("IndirectIrradiance_Kernel");
            int ScatteringDensityKernel = Atmosphere.FindKernel("ScatteringDensity_kernel");
            int MultipleScatteringKernel = Atmosphere.FindKernel("MultiScatter_kernel");
            int FirstCloudKernel = Atmosphere.FindKernel("FirstCloudKernel");
            int SecondCloudKernel = Atmosphere.FindKernel("SecondCloudKernel");

            CreateComputeBuffer(ref rayleigh_densityC, rayleigh_density, 20);
            CreateComputeBuffer(ref mie_densityC, mie_density, 20);
            CreateComputeBuffer(ref absorption_densityC, absorption_density, 20);

            Atmosphere.SetVector("rayleigh_scattering", ray_s);
            Atmosphere.SetVector("solar_irradiance", new Vector3(1.474f, 1.8504f, 1.91198f));
            Atmosphere.SetVector("absorption_extinction", ozo_e);
            Atmosphere.SetVector("mie_extinction", mie_e);
            Atmosphere.SetVector("mie_scattering", mie_s);
            Atmosphere.SetFloat("mu_s_min", -0.207911690817759f);

            Atmosphere.SetBuffer(TransmittanceKernel, "rayleigh_density", rayleigh_densityC);
            Atmosphere.SetBuffer(TransmittanceKernel, "mie_density", mie_densityC);
            Atmosphere.SetBuffer(TransmittanceKernel, "absorption_density", absorption_densityC);
            Atmosphere.SetBuffer(SingleScatterKernel, "rayleigh_density", rayleigh_densityC);
            Atmosphere.SetBuffer(SingleScatterKernel, "mie_density", mie_densityC);
            Atmosphere.SetBuffer(SingleScatterKernel, "absorption_density", absorption_densityC);
            Atmosphere.SetBuffer(ScatteringDensityKernel, "rayleigh_density", rayleigh_densityC);
            Atmosphere.SetBuffer(ScatteringDensityKernel, "mie_density", mie_densityC);
            Atmosphere.SetBuffer(ScatteringDensityKernel, "absorption_density", absorption_densityC);
            Atmosphere.SetFloat("sun_angular_radius", 0.00935f / 2.0f);
            Atmosphere.SetFloat("bottom_radius", BottomRadius);
            Atmosphere.SetFloat("top_radius", TopRadius);

            _TransmittanceLUT = new RenderTexture(256, 64, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            _TransmittanceLUT.enableRandomWrite = true;
            _TransmittanceLUT.Create();
            _RayleighTex = new RenderTexture(256, 128, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            _RayleighTex.volumeDepth = 32;
            _RayleighTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            _RayleighTex.enableRandomWrite = true;
            _RayleighTex.Create();
            _MieTex = new RenderTexture(256, 128, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            _MieTex.volumeDepth = 32;
            _MieTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            _MieTex.enableRandomWrite = true;
            _MieTex.Create();

            DeltaIrradianceTex = new RenderTexture(64, 16, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            DeltaIrradianceTex.enableRandomWrite = true;
            DeltaIrradianceTex.Create();

            IrradianceTex = new RenderTexture(64, 16, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            IrradianceTex.enableRandomWrite = true;
            IrradianceTex.Create();

            ScatteringTex = new RenderTexture(8 * 32, 128, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            ScatteringTex.volumeDepth = 32;
            ScatteringTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            ScatteringTex.enableRandomWrite = true;
            ScatteringTex.Create();

            DebugTex = new RenderTexture(8 * 32, 128, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            DebugTex.enableRandomWrite = true;
            DebugTex.Create();

            DeltaScatteringTex = new RenderTexture(8 * 32, 128, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            DeltaScatteringTex.volumeDepth = 32;
            DeltaScatteringTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            DeltaScatteringTex.enableRandomWrite = true;
            DeltaScatteringTex.Create();

            DeltaMultiScatterTex = new RenderTexture(8 * 32, 128, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            DeltaMultiScatterTex.volumeDepth = 32;
            DeltaMultiScatterTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            DeltaMultiScatterTex.enableRandomWrite = true;
            DeltaMultiScatterTex.Create();

            MultiScatterTex = new RenderTexture(8 * 32, 128, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            MultiScatterTex.volumeDepth = 32;
            MultiScatterTex.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            MultiScatterTex.enableRandomWrite = true;
            MultiScatterTex.Create();

            CloudTex1 = new RenderTexture(128, 128, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            CloudTex1.volumeDepth = 128;
            CloudTex1.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            CloudTex1.enableRandomWrite = true;
            CloudTex1.useMipMap = true;
            CloudTex1.Create();

            CloudTex2 = new RenderTexture(32, 32, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.sRGB);
            CloudTex2.volumeDepth = 32;
            CloudTex2.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            CloudTex2.enableRandomWrite = true;
            CloudTex2.useMipMap = true;
            CloudTex2.Create();

            Atmosphere.SetTexture(SingleScatterKernel, "DebugTex", DebugTex);
            Atmosphere.SetTexture(TransmittanceKernel, "TransmittanceTex", _TransmittanceLUT);
            Atmosphere.Dispatch(TransmittanceKernel, 256, 64, 1);
            Atmosphere.SetTexture(SingleScatterKernel, "TransmittanceTexRead", _TransmittanceLUT);
            Atmosphere.SetTexture(SingleScatterKernel, "RayleighTex", _RayleighTex);
            Atmosphere.SetTexture(SingleScatterKernel, "MieTex", _MieTex);
            Atmosphere.SetTexture(SingleScatterKernel, "ScatteringTex", ScatteringTex);
            Atmosphere.Dispatch(SingleScatterKernel, 256, 128, 32);

            Atmosphere.SetInt("ScatteringOrder", 1);
            int NumScatteringOrder = MultiScatterIterations;
            Atmosphere.SetTexture(DirectIrradianceKernel, "DeltaIrradianceTex", DeltaIrradianceTex);
            Atmosphere.SetTexture(DirectIrradianceKernel, "IrradianceTex", IrradianceTex);
            Atmosphere.SetTexture(DirectIrradianceKernel, "TransmittanceTexRead", _TransmittanceLUT);
            Atmosphere.Dispatch(DirectIrradianceKernel, 64, 16, 1);

            Graphics.CopyTexture(ScatteringTex, MultiScatterTex);
            for (int ScatteringOrder = 2; ScatteringOrder <= NumScatteringOrder; ++ScatteringOrder)
            {
                var TempScatOrder = ScatteringOrder;
                Atmosphere.SetInt("ScatteringOrder", TempScatOrder);
                Atmosphere.SetTexture(ScatteringDensityKernel, "DebugTex", DebugTex);
                Atmosphere.SetTexture(ScatteringDensityKernel, "IrradianceTexRead", DeltaIrradianceTex);
                Atmosphere.SetTexture(ScatteringDensityKernel, "TransmittanceTexRead", _TransmittanceLUT);
                Atmosphere.SetTexture(ScatteringDensityKernel, "RayleighTexRead", _RayleighTex);
                Atmosphere.SetTexture(ScatteringDensityKernel, "MieTexRead", _MieTex);
                Atmosphere.SetTexture(ScatteringDensityKernel, "MultipleScatteringTexRead", DeltaMultiScatterTex);
                Atmosphere.SetTexture(ScatteringDensityKernel, "ScatteringDensityTex", DeltaScatteringTex);
                Atmosphere.Dispatch(ScatteringDensityKernel, 256, 128, 32);

                var TempScatOrder2 = ScatteringOrder - 1;
                Atmosphere.SetInt("ScatteringOrder", TempScatOrder2);
                Atmosphere.SetTexture(IndirectIrradianceKernel, "IrradianceTex", IrradianceTex);
                Atmosphere.SetTexture(IndirectIrradianceKernel, "DeltaIrradianceTex", DeltaIrradianceTex);
                Atmosphere.SetTexture(IndirectIrradianceKernel, "RayleighTexRead", _RayleighTex);
                Atmosphere.SetTexture(IndirectIrradianceKernel, "MieTexRead", _MieTex);
                Atmosphere.SetTexture(IndirectIrradianceKernel, "MultipleScatteringTexRead", DeltaMultiScatterTex);
                Atmosphere.Dispatch(IndirectIrradianceKernel, 64, 16, 1);

                Atmosphere.SetInt("ScatteringOrder", TempScatOrder);
                Atmosphere.SetTexture(MultipleScatteringKernel, "DebugTex", DebugTex);
                Atmosphere.SetTexture(MultipleScatteringKernel, "DeltaMultipleScattering", DeltaMultiScatterTex);
                Atmosphere.SetTexture(MultipleScatteringKernel, "MultiScatterTex", MultiScatterTex);
                Atmosphere.SetTexture(MultipleScatteringKernel, "ScatteringDensityTexRead", DeltaScatteringTex);
                Atmosphere.SetTexture(MultipleScatteringKernel, "TransmittanceTexRead", _TransmittanceLUT);
                Atmosphere.Dispatch(MultipleScatteringKernel, 256, 128, 32);


            }


            // Atmosphere.SetTexture(FirstCloudKernel, "CloudTex1", CloudTex1);
            // Atmosphere.SetInt("u_size", 128);
            // Atmosphere.Dispatch(FirstCloudKernel, 128, 128, 128);


            // Atmosphere.SetTexture(SecondCloudKernel, "CloudTex2", CloudTex2);
            // Atmosphere.SetInt("u_size", 32);
            // Atmosphere.Dispatch(SecondCloudKernel, 32, 32, 32);

            rayleigh_densityC.Release();
            mie_densityC.Release();
            absorption_densityC.Release();

            // Material SkyBoxMaterial;
            // SkyBoxMaterial = RenderSettings.skybox;
            // Texture2D FirstTex = SkyBoxMaterial.GetTexture("_FrontTex") as Texture2D;
            // Skybox = new Cubemap(FirstTex.width, FirstTex.format, false);
            // Skybox.SetPixels(FirstTex.GetPixels(0), CubemapFace.PositiveZ,0);
            // Skybox.SetPixels((SkyBoxMaterial.GetTexture("_BackTex") as Texture2D).GetPixels(0), CubemapFace.NegativeZ,0);
            // Skybox.SetPixels((SkyBoxMaterial.GetTexture("_RightTex") as Texture2D).GetPixels(0), CubemapFace.PositiveX,0);
            // Skybox.SetPixels((SkyBoxMaterial.GetTexture("_LeftTex") as Texture2D).GetPixels(0), CubemapFace.NegativeX,0);
            // Skybox.SetPixels((SkyBoxMaterial.GetTexture("_UpTex") as Texture2D).GetPixels(0), CubemapFace.PositiveY,0);
            // Skybox.SetPixels((SkyBoxMaterial.GetTexture("_DownTex") as Texture2D).GetPixels(0), CubemapFace.NegativeY,0);
        }


    }
}