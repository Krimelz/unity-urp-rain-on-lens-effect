using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.Codebase.Renderer.RainOnLens
{
    public sealed class RainOnLensRendererFeature : ScriptableRendererFeature
    {
        private static readonly int DisappearingSpeed = Shader.PropertyToID("_DisappearingSpeed");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int HighEdge = Shader.PropertyToID("_HighEdge");
        private static readonly int LowEdge = Shader.PropertyToID("_LowEdge");

        #region FEATURE_FIELDS

        [SerializeField]
        private Material m_Material;
        private RainOnLensPostRenderPass m_FullScreenPass;

        #endregion

        #region FEATURE_METHODS

        public override void Create()
        {
            if (m_Material)
            {
                m_FullScreenPass = new RainOnLensPostRenderPass(name, m_Material);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Material == null || m_FullScreenPass == null)
            {
                return;
            }

            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
            {
                return;
            }

            var volume = VolumeManager.instance.stack?.GetComponent<RainOnLensVolumeComponent>();

            if (volume == null || !volume.IsActive())
            {
                return;
            }

            m_FullScreenPass.renderPassEvent = volume.renderPassEvent.value;
            m_FullScreenPass.ConfigureInput(ScriptableRenderPassInput.None);

            renderer.EnqueuePass(m_FullScreenPass);
        }

        protected override void Dispose(bool disposing)
        {
            m_FullScreenPass?.Dispose();
        }

        #endregion

        private class RainOnLensPostRenderPass : ScriptableRenderPass
        {
            #region PASS_FIELDS

            private Material m_Material;
            private RTHandle m_CopiedColor;

            private static MaterialPropertyBlock s_SharedPropertyBlock = new();

            private static readonly bool kSampleActiveColor = true;
            private static readonly bool kBindDepthStencilAttachment = false;

            private static readonly int kBlitTexturePropertyId = Shader.PropertyToID("_BlitTexture");
            private static readonly int kBlitScaleBiasPropertyId = Shader.PropertyToID("_BlitScaleBias");

            #endregion

            public RainOnLensPostRenderPass(string passName, Material material)
            {
                profilingSampler = new ProfilingSampler(passName);

                m_Material = material;

                requiresIntermediateTexture = kSampleActiveColor;
            }

            #region PASS_SHARED_RENDERING_CODE

            private static void ExecuteCopyColorPass(RasterCommandBuffer cmd, RTHandle sourceTexture)
            {
                Blitter.BlitTexture(cmd, sourceTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
            }

            private static void ExecuteMainPass(RasterCommandBuffer cmd, RTHandle sourceTexture, Material material)
            {
                s_SharedPropertyBlock.Clear();

                if (sourceTexture != null)
                {
                    s_SharedPropertyBlock.SetTexture(kBlitTexturePropertyId, sourceTexture);
                }

                s_SharedPropertyBlock.SetVector(kBlitScaleBiasPropertyId, new Vector4(1, 1, 0, 0));

                var volume = VolumeManager.instance.stack?.GetComponent<RainOnLensVolumeComponent>();

                if (volume != null)
                {
                    s_SharedPropertyBlock.SetFloat(Intensity, volume.intensity.value);
                    s_SharedPropertyBlock.SetFloat(DisappearingSpeed, volume.disappearingSpeed.value);
                    s_SharedPropertyBlock.SetFloat(LowEdge, volume.lowEdge.value);
                    s_SharedPropertyBlock.SetFloat(HighEdge, volume.highEdge.value);

                    if (volume.debug.value)
                    {
                        material.EnableKeyword("_DEBUG");
                    }
                    else
                    {
                        material.DisableKeyword("_DEBUG");
                    }
                }

                cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, s_SharedPropertyBlock);
            }

            private static RenderTextureDescriptor GetCopyPassTextureDescriptor(RenderTextureDescriptor desc)
            {
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;

                return desc;
            }

            #endregion

            #region PASS_NON_RENDER_GRAPH_PATH

            [System.Obsolete("This rendering path works in Compatibility Mode only, which is deprecated. Use the render graph API instead.", false)]
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ResetTarget();

                if (kSampleActiveColor)
                {
                    RenderingUtils.ReAllocateHandleIfNeeded(ref m_CopiedColor, GetCopyPassTextureDescriptor(renderingData.cameraData.cameraTargetDescriptor), name: "_CustomPostPassCopyColor");
                }
            }

            [System.Obsolete("This rendering path works in Compatibility Mode only, which is deprecated. Use the render graph API instead.", false)]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                ref var cameraData = ref renderingData.cameraData;
                var cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, profilingSampler))
                {
                    RasterCommandBuffer rasterCmd = CommandBufferHelpers.GetRasterCommandBuffer(cmd);

                    if (kSampleActiveColor)
                    {
                        CoreUtils.SetRenderTarget(cmd, m_CopiedColor);
                        ExecuteCopyColorPass(rasterCmd, cameraData.renderer.cameraColorTargetHandle);
                    }

                    if (kBindDepthStencilAttachment)
                    {
                        CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle, cameraData.renderer.cameraDepthTargetHandle);
                    }
                    else
                    {
                        CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                    }

                    ExecuteMainPass(rasterCmd, kSampleActiveColor ? m_CopiedColor : null, m_Material);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
            }

            public void Dispose()
            {
                m_CopiedColor?.Release();
            }

            #endregion

            #region PASS_RENDER_GRAPH_PATH

            private class CopyPassData
            {
                public TextureHandle inputTexture;
            }

            private class MainPassData
            {
                public Material material;
                public TextureHandle inputTexture;
            }

            private static void ExecuteCopyColorPass(CopyPassData data, RasterGraphContext context)
            {
                ExecuteCopyColorPass(context.cmd, data.inputTexture);
            }

            private static void ExecuteMainPass(MainPassData data, RasterGraphContext context)
            {
                ExecuteMainPass(context.cmd, data.inputTexture.IsValid() ? data.inputTexture : null, data.material);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourcesData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                using var builder = renderGraph
                    .AddRasterRenderPass<MainPassData>(passName, out var passData, profilingSampler);
                passData.material = m_Material;

                TextureHandle destination;

                if (kSampleActiveColor)
                {
                    var cameraColorDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
                    cameraColorDesc.name = "_CameraColorCustomPostProcessing";
                    cameraColorDesc.clearBuffer = false;

                    destination = renderGraph.CreateTexture(cameraColorDesc);
                    passData.inputTexture = resourcesData.cameraColor;

                    builder.UseTexture(passData.inputTexture, AccessFlags.Read);
                }
                else
                {
                    destination = resourcesData.cameraColor;
                    passData.inputTexture = TextureHandle.nullHandle;
                }

                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                if (kBindDepthStencilAttachment)
                {
                    builder.SetRenderAttachmentDepth(resourcesData.activeDepthTexture, AccessFlags.Write);
                }

                builder.SetRenderFunc((MainPassData data, RasterGraphContext context) => ExecuteMainPass(data, context));

                if (kSampleActiveColor)
                {
                    resourcesData.cameraColor = destination;
                }
            }

            #endregion
        }
    }
}
