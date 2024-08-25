using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CommonVars;

namespace TrueTrace {
    [System.Serializable]
    public class Denoiser
    {
        public bool Initialized = false;
        private ComputeShader Bloom;
        private ComputeShader AutoExpose;
        private ComputeShader TAA;
        private ComputeShader Upscaler;
        private ComputeShader ToneMapper;
        private ComputeShader TAAU;
        private ComputeShader Sharpen;
        bool BloomInitialized = false;
        bool TAAInitialized = false;
        bool UpscalerInitialized = false;
        bool TAAUInitialized = false;
        bool SharpenInitialized = false;


        public RenderTexture _TAAPrev;
        public RenderTexture Intermediate;
        public RenderTexture SuperIntermediate;
        private RenderTexture UpScalerLightingDataTexture;

        private RenderTexture TempTexTAA;
        private RenderTexture TempTexTAA2;


        private RenderTexture SharpenTex;




        private RenderTexture TAAA;
        private RenderTexture TAAB;
        public RenderTexture[] BloomSamplesDown;
        public RenderTexture BloomIntermediate;

        private int ToneMapLuminanceKernel;
        private int ToneMapExposureWeightKernel;
        private int ToneMapBlendKernel;
        private int ToneMapBlendLapLaceKernel;
        private int ToneMapCombineKernel;




        public ComputeBuffer ExposureBuffer;

        private int ScreenWidth;
        private int ScreenHeight;

        private Camera _camera;
        private Matrix4x4 PrevViewProjection;

        private int threadGroupsX;
        private int threadGroupsY;

        private int threadGroupsX2;
        private int threadGroupsY2;

        private int BloomDownsampleKernel;
        private int BloomLowPassKernel;
        private int BloomUpsampleKernel;

        private int ComputeHistogramKernel;
        private int CalcAverageKernel;
        private int ToneMapKernel;

        private int AutoExposeKernel;
        private int AutoExposeFinalizeKernel;

        private int TAAKernel;
        private int TAAFinalizeKernel;
        private int TAAPrepareKernel;

        private int TAAUKernel;
        private int TAAUCopyKernel;




        private int UpsampleKernel;

        private int SourceWidth;
        private int SourceHeight;
        private int[] BloomWidths;
        private int[] BloomHeights;

        private void InitRenderTexture(bool Force = false)
        {
            if(Force) {
                    TAAA.ReleaseSafe();
                    TAAB.ReleaseSafe();
                    _TAAPrev.ReleaseSafe();
                    TempTexTAA.ReleaseSafe();
                    TempTexTAA2.ReleaseSafe();
                    SharpenTex.ReleaseSafe();
                BloomInitialized = false;
                TAAInitialized = false;
                UpscalerInitialized = false;
                SharpenInitialized = false;
            }
            
        }
        public void ClearAll() {
            _TAAPrev.ReleaseSafe();
            Intermediate.ReleaseSafe();
            SuperIntermediate.ReleaseSafe();
            UpScalerLightingDataTexture.ReleaseSafe();

            TempTexTAA.ReleaseSafe();
            TempTexTAA2.ReleaseSafe();

            TAAA.ReleaseSafe();
            TAAB.ReleaseSafe();

            SharpenTex.ReleaseSafe();

            ExposureBuffer.ReleaseSafe();
            BloomIntermediate.ReleaseSafe();
            if(BloomSamplesDown != null) for(int i = 0; i < BloomSamplesDown.Length; i++) BloomSamplesDown[i].ReleaseSafe();

        }

        void OnApplicationQuit()
        {
            ClearAll();
        }

        public void init(int SourceWidth, int SourceHeight)
        {
            this.SourceWidth = SourceWidth;
            this.SourceHeight = SourceHeight;

            _camera = RayTracingMaster._camera;
            if (AutoExpose == null) { AutoExpose = Resources.Load<ComputeShader>("PostProcess/Compute/AutoExpose"); }
            if (Bloom == null) { Bloom = Resources.Load<ComputeShader>("PostProcess/Compute/Bloom"); }
            if (TAA == null) { TAA = Resources.Load<ComputeShader>("PostProcess/Compute/TAA"); }
            if (Upscaler == null) { Upscaler = Resources.Load<ComputeShader>("PostProcess/Compute/Upscaler"); }
            if (ToneMapper == null) { ToneMapper = Resources.Load<ComputeShader>("PostProcess/Compute/ToneMap"); }
            if (TAAU == null) { TAAU = Resources.Load<ComputeShader>("PostProcess/Compute/TAAU"); }
            if (Sharpen == null) { Sharpen = Resources.Load<ComputeShader>("PostProcess/Compute/Sharpen"); }


            TAAUKernel = TAAU.FindKernel("TAAU");
            TAAUCopyKernel = TAAU.FindKernel("Copy");


            BloomDownsampleKernel = Bloom.FindKernel("Downsample");
            BloomLowPassKernel = Bloom.FindKernel("LowPass");
            BloomUpsampleKernel = Bloom.FindKernel("Upsample");

            TAAKernel = TAA.FindKernel("kernel_taa");
            TAAFinalizeKernel = TAA.FindKernel("kernel_taa_finalize");
            TAAPrepareKernel = TAA.FindKernel("kernel_taa_prepare");

            UpsampleKernel = Upscaler.FindKernel("kernel_upsample");

            AutoExposeKernel = AutoExpose.FindKernel("AutoExpose");
            AutoExposeFinalizeKernel = AutoExpose.FindKernel("AutoExposeFinalize");
            List<float> TestBuffer = new List<float>();
            TestBuffer.Add(1);
            ExposureBuffer?.Release(); ExposureBuffer = new ComputeBuffer(1, sizeof(float)); ExposureBuffer.SetData(TestBuffer);


            Bloom.SetInt("screen_width", Screen.width);
            Bloom.SetInt("screen_height", Screen.height);

            Sharpen.SetInt("screen_width", Screen.width);
            Sharpen.SetInt("screen_height", Screen.height);

            AutoExpose.SetInt("screen_width", Screen.width);
            AutoExpose.SetInt("screen_height", Screen.height);
            AutoExpose.SetBuffer(AutoExposeKernel, "A", ExposureBuffer);
            AutoExpose.SetBuffer(AutoExposeFinalizeKernel, "A", ExposureBuffer);

            TAA.SetInt("screen_width", Screen.width);
            TAA.SetInt("screen_height", Screen.height);


            threadGroupsX = Mathf.CeilToInt(SourceWidth / 16.0f);
            threadGroupsY = Mathf.CeilToInt(SourceHeight / 16.0f);

            threadGroupsX2 = Mathf.CeilToInt(Screen.width / 16.0f);
            threadGroupsY2 = Mathf.CeilToInt(Screen.height / 16.0f);

            BloomInitialized = false;
            TAAInitialized = false;
            UpscalerInitialized = false;
            SharpenInitialized = false;
            InitRenderTexture();
            Initialized = true;
        }

        public void Reinit(int SourceWidth, int SourceHeight)
        {
            this.SourceWidth = SourceWidth;
            this.SourceHeight = SourceHeight;
            _camera = RayTracingMaster._camera;

            List<float> TestBuffer = new List<float>();
            TestBuffer.Add(1);
            ExposureBuffer?.Release(); ExposureBuffer = new ComputeBuffer(1, sizeof(float)); ExposureBuffer.SetData(TestBuffer);

            Bloom.SetInt("screen_width", Screen.width);
            Bloom.SetInt("screen_height", Screen.height);
            AutoExpose.SetInt("screen_width", Screen.width);
            AutoExpose.SetInt("screen_height", Screen.height);
            AutoExpose.SetBuffer(AutoExposeKernel, "A", ExposureBuffer);
            AutoExpose.SetBuffer(AutoExposeFinalizeKernel, "A", ExposureBuffer);

            TAA.SetInt("screen_width", Screen.width);
            TAA.SetInt("screen_height", Screen.height);

            threadGroupsX = Mathf.CeilToInt(SourceWidth / 16.0f);
            threadGroupsY = Mathf.CeilToInt(SourceHeight / 16.0f);

            threadGroupsX2 = Mathf.CeilToInt(Screen.width / 16.0f);
            threadGroupsY2 = Mathf.CeilToInt(Screen.height / 16.0f);


            InitRenderTexture(true);
        }

        public void ValidateInit(bool BloomInit, bool TAAInit, bool IsUpscaling, bool UseTAAU, bool SharpenInit) {
            if(!BloomInit) {
                if(BloomInitialized) {
                    BloomIntermediate.ReleaseSafe();
                    if(BloomSamplesDown != null) for(int i = 0; i < BloomSamplesDown.Length; i++) BloomSamplesDown[i].ReleaseSafe();
                    BloomInitialized = false;
                }
            }
            if(!TAAInit) {
                if(TAAInitialized) {
                    _TAAPrev.ReleaseSafe();
                    TempTexTAA.ReleaseSafe();
                    TempTexTAA2.ReleaseSafe();
                    TAAInitialized = false;
                }
            }
            if(!SharpenInit) {
                if(SharpenInitialized) {
                    SharpenTex.ReleaseSafe();
                    SharpenInitialized = false;
                }
            }
            if(!IsUpscaling) {
                if(!UseTAAU) {
                    if(UpscalerInitialized) {
                        UpScalerLightingDataTexture.ReleaseSafe();
                        UpscalerInitialized = false;
                    }
                } else {
                    if(TAAUInitialized) {
                        TAAA.ReleaseSafe();
                        TAAB.ReleaseSafe();
                        TAAUInitialized = false;
                    }
                }
            }
        }


        private void InitBloom() {
            BloomSamplesDown = new RenderTexture[8];
            int BloomWidth = Screen.width / 2;
            int BloomHeight = Screen.height / 2;
            BloomWidths = new int[8];
            BloomHeights = new int[8];
            for (int i = 0; i < 8; i++)
            {
                CommonFunctions.CreateRenderTexture(ref BloomSamplesDown[i], BloomWidth, BloomHeight, CommonFunctions.RTHalf4);
                BloomWidths[i] = BloomWidth;
                BloomHeights[i] = BloomHeight;
                BloomWidth /= 2;
                BloomHeight /= 2;
            }
            CommonFunctions.CreateRenderTexture(ref BloomIntermediate, Screen.width, Screen.height, CommonFunctions.RTFull4);
            BloomInitialized = true;
        }
        public void ExecuteBloom(ref RenderTexture _converged, float BloomStrength, CommandBuffer cmd)
        {//need to fix this so it doesnt create new textures every time
            if(!BloomInitialized) InitBloom();
            if (Bloom == null) { Bloom = Resources.Load<ComputeShader>("PostProcess/Compute/Bloom"); }

            Bloom.SetInt("screen_width", Screen.width);
            Bloom.SetInt("screen_height", Screen.height);
            cmd.BeginSample("Bloom");
            Bloom.SetFloat("strength", BloomStrength);
            cmd.SetComputeIntParam(Bloom, "screen_width", Screen.width);
            cmd.SetComputeIntParam(Bloom, "screen_height", Screen.height);
            cmd.SetComputeIntParam(Bloom, "TargetWidth", BloomWidths[0]);
            cmd.SetComputeIntParam(Bloom, "TargetHeight", BloomHeights[0]);
            cmd.SetComputeTextureParam(Bloom, BloomLowPassKernel, "InputTex", _converged);
            cmd.SetComputeTextureParam(Bloom, BloomLowPassKernel, "OutputTex", BloomSamplesDown[0]);
            cmd.DispatchCompute(Bloom, BloomLowPassKernel, (int)Mathf.Ceil(BloomWidths[0] / 16.0f), (int)Mathf.Ceil(BloomHeights[0] / 16.0f), 1);
            for (int i = 1; i < 8; i++)
            {
                // Debug.Log(BloomWidths[i]);
                cmd.SetComputeIntParam(Bloom, "TargetWidth", BloomWidths[i]);
                cmd.SetComputeIntParam(Bloom, "TargetHeight", BloomHeights[i]);
                cmd.SetComputeIntParam(Bloom, "screen_width", BloomWidths[i - 1]);
                cmd.SetComputeIntParam(Bloom, "screen_height", BloomHeights[i - 1]);
                cmd.SetComputeTextureParam(Bloom, BloomDownsampleKernel, "InputTex", BloomSamplesDown[i - 1]);
                cmd.SetComputeTextureParam(Bloom, BloomDownsampleKernel, "OutputTex", BloomSamplesDown[i]);
                cmd.DispatchCompute(Bloom, BloomDownsampleKernel, (int)Mathf.Ceil(BloomWidths[i - 1] / 16.0f), (int)Mathf.Ceil(BloomHeights[i - 1] / 16.0f), 1);
            }
            Bloom.SetBool("IsFinal", false);

            for (int i = 7; i > 0; i--)
            {
                cmd.SetComputeIntParam(Bloom, "TargetWidth", BloomWidths[i - 1]);
                cmd.SetComputeIntParam(Bloom, "TargetHeight", BloomHeights[i - 1]);
                cmd.SetComputeIntParam(Bloom, "screen_width", BloomWidths[i]);
                cmd.SetComputeIntParam(Bloom, "screen_height", BloomHeights[i]);
                cmd.SetComputeTextureParam(Bloom, BloomUpsampleKernel, "InputTex", BloomSamplesDown[i]);
                cmd.SetComputeTextureParam(Bloom, BloomUpsampleKernel, "OutputTex", BloomSamplesDown[i - 1]);
                // cmd.SetComputeTextureParam(Bloom, BloomUpsampleKernel, "OrigTex", BloomSamplesDown[i - 1]);

                cmd.DispatchCompute(Bloom, BloomUpsampleKernel, (int)Mathf.Ceil(BloomWidths[i - 1] / 16.0f), (int)Mathf.Ceil(BloomHeights[i - 1] / 16.0f), 1);
            }
            cmd.Blit(_converged, BloomIntermediate);
            cmd.SetComputeIntParam(Bloom, "TargetWidth", Screen.width);
            cmd.SetComputeIntParam(Bloom, "TargetHeight", Screen.height);
            cmd.SetComputeIntParam(Bloom, "screen_width", BloomWidths[0]);
            cmd.SetComputeIntParam(Bloom, "screen_height", BloomHeights[0]);
            Bloom.SetBool("IsFinal", true);
            cmd.SetComputeTextureParam(Bloom, BloomUpsampleKernel, "OrigTex", BloomIntermediate);
            cmd.SetComputeTextureParam(Bloom, BloomUpsampleKernel, "InputTex", BloomSamplesDown[0]);
            cmd.SetComputeTextureParam(Bloom, BloomUpsampleKernel, "OutputTex", _converged);
            cmd.DispatchCompute(Bloom, BloomUpsampleKernel, (int)Mathf.Ceil(Screen.width / 16.0f), (int)Mathf.Ceil(Screen.height / 16.0f), 1);
            cmd.EndSample("Bloom");



        }


        public void ExecuteAutoExpose(ref RenderTexture _converged, float Exposure, CommandBuffer cmd, bool ExposureAuto)
        {//need to fix this so it doesnt create new textures every time
            cmd.BeginSample("Auto Exposure");
            if(ExposureBuffer == null) {
                List<float> TestBuffer = new List<float>();
                TestBuffer.Add(1);
                ExposureBuffer.ReleaseSafe(); ExposureBuffer = new ComputeBuffer(1, sizeof(float)); ExposureBuffer.SetData(TestBuffer);
            }
            if (AutoExpose == null) { 
                AutoExpose = Resources.Load<ComputeShader>("PostProcess/Compute/AutoExpose");
                AutoExposeKernel = AutoExpose.FindKernel("AutoExpose");
                AutoExposeFinalizeKernel = AutoExpose.FindKernel("AutoExposeFinalize");
            }
            AutoExpose.SetInt("screen_width", Screen.width);
            AutoExpose.SetInt("screen_height", Screen.height);
            AutoExpose.SetBuffer(AutoExposeKernel, "A", ExposureBuffer);
            AutoExpose.SetBuffer(AutoExposeFinalizeKernel, "A", ExposureBuffer);
            cmd.SetComputeTextureParam(AutoExpose, AutoExposeKernel, "InTex", _converged);
            AutoExpose.SetFloat("Exposure", Exposure);
            AutoExpose.SetBool("ExposureAuto", ExposureAuto);
            AutoExpose.SetFloat("frame_time", Time.deltaTime);
            cmd.DispatchCompute(AutoExpose, AutoExposeKernel, 1, 1, 1);
            cmd.SetComputeTextureParam(AutoExpose, AutoExposeFinalizeKernel, "OutTex", _converged);
            cmd.DispatchCompute(AutoExpose, AutoExposeFinalizeKernel, threadGroupsX2, threadGroupsY2, 1);
            cmd.EndSample("Auto Exposure");


        }
        private void InitializeTAA() {
            CommonFunctions.CreateRenderTexture(ref TempTexTAA, Screen.width, Screen.height, CommonFunctions.RTHalf4);
            CommonFunctions.CreateRenderTexture(ref TempTexTAA2, Screen.width, Screen.height, CommonFunctions.RTHalf4);
            CommonFunctions.CreateRenderTexture(ref _TAAPrev, Screen.width, Screen.height, CommonFunctions.RTHalf4);
            TAAInitialized = true;
        }
        public void ExecuteTAA(ref RenderTexture _Final, int CurrentSamples, CommandBuffer cmd)
        {//need to fix this so it doesnt create new textures every time

            if(!TAAInitialized) InitializeTAA();
            cmd.SetComputeIntParam(TAA,"Samples_Accumulated", CurrentSamples);

            TAA.SetFloat("FarPlane", _camera.farClipPlane);
            TAA.SetTextureFromGlobal(TAAPrepareKernel, "MotionVectors", "_CameraMotionVectorsTexture");
            TAA.SetTextureFromGlobal(TAAPrepareKernel, "DepthTex", "_CameraDepthTexture");
            cmd.SetComputeTextureParam(TAA, TAAPrepareKernel, "ColorIn", _Final);
            cmd.SetComputeTextureParam(TAA, TAAPrepareKernel, "ColorOut", TempTexTAA);
            cmd.BeginSample("TAA Prepare Kernel");
            cmd.DispatchCompute(TAA, TAAPrepareKernel, threadGroupsX2, threadGroupsY2, 1);
            cmd.EndSample("TAA Prepare Kernel");


            cmd.SetComputeTextureParam(TAA, TAAKernel, "ColorIn", TempTexTAA);
            TAA.SetTextureFromGlobal(TAAKernel, "MotionVectors", "_CameraMotionVectorsTexture");
            cmd.SetComputeTextureParam(TAA, TAAKernel, "TAAPrev", _TAAPrev);
            cmd.SetComputeTextureParam(TAA, TAAKernel, "TAAPrevRead", _TAAPrev);
            cmd.SetComputeTextureParam(TAA, TAAKernel, "ColorOut", TempTexTAA2);
            cmd.BeginSample("TAA Main Kernel");
            cmd.DispatchCompute(TAA, TAAKernel, threadGroupsX2, threadGroupsY2, 1);
            cmd.EndSample("TAA Main Kernel");

            cmd.SetComputeTextureParam(TAA, TAAFinalizeKernel, "TAAPrev", _TAAPrev);
            cmd.SetComputeTextureParam(TAA, TAAFinalizeKernel, "ColorOut", _Final);
            cmd.SetComputeTextureParam(TAA, TAAFinalizeKernel, "ColorIn", TempTexTAA2);
            cmd.BeginSample("TAA Finalize Kernel");
            cmd.DispatchCompute(TAA, TAAFinalizeKernel, threadGroupsX2, threadGroupsY2, 1);
            cmd.EndSample("TAA Finalize Kernel");
        }

        Matrix4x4 PreviousCameraMatrix;
        Matrix4x4 PreviousCameraInverseMatrix;
        Matrix4x4 PrevProjInv;
        private void InitializeUpsampler() {
            CommonFunctions.CreateRenderTexture(ref UpScalerLightingDataTexture, Screen.width, Screen.height, CommonFunctions.RTHalf4);
            UpscalerInitialized = true;
        }
        public void ExecuteUpsample(ref RenderTexture Input, ref RenderTexture Output, int curframe, int cursample, CommandBuffer cmd, RenderTexture ScreenSpaceInfo)
        {//need to fix this so it doesnt create new textures every time
            if(!UpscalerInitialized) InitializeUpsampler();
            cmd.SetComputeIntParam(Upscaler,"curframe", curframe);
            cmd.SetComputeIntParam(Upscaler,"cursam", cursample);
            cmd.SetComputeIntParam(Upscaler,"source_width", Input.width);
            cmd.SetComputeIntParam(Upscaler,"source_height", Input.height);
            cmd.SetComputeIntParam(Upscaler,"target_width", Output.width);
            cmd.SetComputeIntParam(Upscaler,"target_height", Output.height);
            Upscaler.SetMatrix("CamToWorld", _camera.cameraToWorldMatrix);
            Upscaler.SetMatrix("CamInvProj", _camera.projectionMatrix.inverse);
            Upscaler.SetMatrix("_PrevCameraToWorld", PreviousCameraMatrix);
            Upscaler.SetMatrix("_PrevCameraInverseProjection", PreviousCameraInverseMatrix);
            Upscaler.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
            Upscaler.SetMatrix("_PrevCameraInverseProjection", PrevProjInv);
            Upscaler.SetVector("Forward", _camera.transform.forward);
            Upscaler.SetVector("CamPos", _camera.transform.position);
            Upscaler.SetMatrix("ViewProjectionMatrix", _camera.projectionMatrix * _camera.worldToCameraMatrix);
            Upscaler.SetFloat("FarPlane", _camera.farClipPlane);
            Upscaler.SetInt("CurFrame", curframe);
            Upscaler.SetTextureFromGlobal(UpsampleKernel, "Albedo", "_CameraGBufferTexture0");
            Upscaler.SetTextureFromGlobal(UpsampleKernel, "Albedo2", "_CameraGBufferTexture1");
            Upscaler.SetTextureFromGlobal(UpsampleKernel, "DepthTex", "_CameraDepthTexture");
            Upscaler.SetTextureFromGlobal(UpsampleKernel, "NormalTex", "_CameraGBufferTexture2");
            Upscaler.SetTextureFromGlobal(UpsampleKernel, "MotionVectors", "_CameraMotionVectorsTexture");
            cmd.SetComputeTextureParam(Upscaler, UpsampleKernel, "SmallerGBuffer", ScreenSpaceInfo);
            cmd.SetComputeTextureParam(Upscaler, UpsampleKernel, "Input", Input);
            cmd.SetComputeTextureParam(Upscaler, UpsampleKernel, "Output", UpScalerLightingDataTexture);
            cmd.SetComputeTextureParam(Upscaler, UpsampleKernel, "FinalOutput", Output);
            cmd.BeginSample("Upsample Main Kernel");
            cmd.DispatchCompute(Upscaler, UpsampleKernel, threadGroupsX2, threadGroupsY2, 1);
            cmd.EndSample("Upsample Main Kernel");


            Upscaler.SetTextureFromGlobal(UpsampleKernel + 1, "Albedo", "_CameraGBufferTexture0");
            Upscaler.SetTextureFromGlobal(UpsampleKernel + 1, "Albedo2", "_CameraGBufferTexture1");
            cmd.SetComputeTextureParam(Upscaler, UpsampleKernel + 1, "Input", UpScalerLightingDataTexture);
            cmd.SetComputeTextureParam(Upscaler, UpsampleKernel + 1, "FinalOutput", Output);
            cmd.BeginSample("Upsample Blur Kernel");
            cmd.DispatchCompute(Upscaler, UpsampleKernel + 1, threadGroupsX2, threadGroupsY2, 1);
            cmd.EndSample("Upsample Blur Kernel");
            

            PreviousCameraMatrix = _camera.cameraToWorldMatrix;
            PreviousCameraInverseMatrix = _camera.projectionMatrix.inverse;
            PrevProjInv = _camera.projectionMatrix.inverse;
        }


        public void ExecuteToneMap(ref RenderTexture Output, CommandBuffer cmd, ref Texture3D LUT, ref Texture3D LUT2, int ToneMapSelection)
        {//need to fix this so it doesnt create new textures every time
            cmd.BeginSample("ToneMap");
            cmd.SetComputeIntParam(ToneMapper,"ToneMapSelection", ToneMapSelection);
            cmd.SetComputeIntParam(ToneMapper,"screen_width", Output.width);
            cmd.SetComputeIntParam(ToneMapper,"screen_height", Output.height);
            cmd.SetComputeTextureParam(ToneMapper, 0, "Result", Output);
            cmd.SetComputeTextureParam(ToneMapper, 0, "LUT", ToneMapSelection == 5 ? LUT2 : LUT);
            cmd.DispatchCompute(ToneMapper, 0, threadGroupsX2, threadGroupsY2, 1);
            cmd.EndSample("ToneMap");
        }
        private void InitializeTAAU() {
            CommonFunctions.CreateRenderTexture(ref TAAA, Screen.width, Screen.height, CommonFunctions.RTHalf4);
            CommonFunctions.CreateRenderTexture(ref TAAB, Screen.width, Screen.height, CommonFunctions.RTHalf4);
            TAAUInitialized = true;
        }
        public void ExecuteTAAU(ref RenderTexture Output, ref RenderTexture Input, CommandBuffer cmd, int CurFrame)
        {//need to fix this so it doesnt create new textures every time
            if(!TAAUInitialized) InitializeTAAU();
            cmd.BeginSample("TAAU");
            bool IsEven = CurFrame % 2 == 0;
            cmd.SetComputeIntParam(TAAU,"source_width", SourceWidth);
            cmd.SetComputeIntParam(TAAU,"source_height", SourceHeight);
            cmd.SetComputeIntParam(TAAU,"target_width", Output.width);
            cmd.SetComputeIntParam(TAAU,"target_height", Output.height);
            cmd.SetComputeIntParam(TAAU,"CurFrame", CurFrame);
            cmd.SetComputeTextureParam(TAAU, TAAUKernel, "IMG_ASVGF_TAA_A", IsEven ? TAAA : TAAB);
            cmd.SetComputeTextureParam(TAAU, TAAUKernel, "TEX_ASVGF_TAA_B", !IsEven ? TAAA : TAAB);
            cmd.SetComputeTextureParam(TAAU, TAAUKernel, "TEX_FLAT_COLOR", Input);
            cmd.SetComputeTextureParam(TAAU, TAAUKernel, "IMG_TAA_OUTPUT", Output);
            TAAU.SetTextureFromGlobal(TAAUKernel, "Albedo", "_CameraGBufferTexture0");
            TAAU.SetTextureFromGlobal(TAAUKernel, "Albedo2", "_CameraGBufferTexture1");
            TAAU.SetTextureFromGlobal(TAAUKernel, "TEX_FLAT_MOTION", "_CameraMotionVectorsTexture");
            cmd.DispatchCompute(TAAU, TAAUKernel, threadGroupsX2, threadGroupsY2, 1);
            cmd.EndSample("TAAU");
        }


        private void InitializeSharpen() {
            CommonFunctions.CreateRenderTexture(ref SharpenTex, Screen.width, Screen.height, CommonFunctions.RTFull4);
            SharpenInitialized = true;
        }
        public void ExecuteSharpen(ref RenderTexture Output, float Sharpness, CommandBuffer cmd) {
            if(!SharpenInitialized) InitializeSharpen();
            if (Sharpen == null) { Sharpen = Resources.Load<ComputeShader>("PostProcess/Compute/Sharpen"); }
            Sharpen.SetInt("screen_width", Screen.width);
            Sharpen.SetInt("screen_height", Screen.height);
            cmd.CopyTexture(Output, 0, 0, SharpenTex, 0, 0);
            cmd.SetComputeTextureParam(Sharpen, 0, "Input", SharpenTex);
            cmd.SetComputeTextureParam(Sharpen, 0, "Result", Output);
            Sharpen.SetFloat("Sharpness", Sharpness);
            cmd.DispatchCompute(Sharpen, 0, threadGroupsX2, threadGroupsY2, 1);

        }

    }
}

