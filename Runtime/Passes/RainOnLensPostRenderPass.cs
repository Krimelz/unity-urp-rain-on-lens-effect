using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace RainOnLens
{
    public class RainOnLensPostRenderPass : ScriptableRenderPass
    {
        private class PassData
        {
            public Material material;
            public TextureHandle inputTexture;
        }

        private Material m_Material;

        private static MaterialPropertyBlock s_SharedPropertyBlock = new();

        private static readonly bool kSampleActiveColor = true;
        private static readonly bool kBindDepthStencilAttachment = false;

        private static readonly int DisappearingSpeed = Shader.PropertyToID("_DisappearingSpeed");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int HighEdge = Shader.PropertyToID("_HighEdge");
        private static readonly int LowEdge = Shader.PropertyToID("_LowEdge");

        private static readonly int kBlitTexturePropertyId = Shader.PropertyToID("_BlitTexture");
        private static readonly int kBlitScaleBiasPropertyId = Shader.PropertyToID("_BlitScaleBias");

        public RainOnLensPostRenderPass(string passName, Material material)
        {
            profilingSampler = new ProfilingSampler(passName);

            m_Material = material;

            requiresIntermediateTexture = kSampleActiveColor;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourcesData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            using var builder = renderGraph
                .AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler);
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

            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecuteMainPass(data, context));

            if (kSampleActiveColor)
            {
                resourcesData.cameraColor = destination;
            }
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
                s_SharedPropertyBlock.SetFloat(Intensity, volume.Intensity.value);
                s_SharedPropertyBlock.SetFloat(DisappearingSpeed, volume.DisappearingSpeed.value);
                s_SharedPropertyBlock.SetFloat(LowEdge, volume.LowEdge.value);
                s_SharedPropertyBlock.SetFloat(HighEdge, volume.HighEdge.value);

                if (volume.Debug.value)
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

        private static void ExecuteMainPass(PassData data, RasterGraphContext context)
        {
            ExecuteMainPass(context.cmd, data.inputTexture.IsValid() ? data.inputTexture : null, data.material);
        }
    }
}
