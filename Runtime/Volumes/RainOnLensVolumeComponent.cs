using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RainOnLens
{
    [Serializable]
    [DisplayInfo(name = "Rain On Lens")]
    [VolumeComponentMenu("Custom/Rain On Lens")]
    [VolumeRequiresRendererFeatures(typeof(RainOnLensRendererFeature))]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class RainOnLensVolumeComponent : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter Enabled = new(false, false);
        public BoolParameter Debug = new(false, false);
        public FloatParameter Intensity = new(4f);
        public FloatParameter DisappearingSpeed = new(1f);
        public ClampedFloatParameter LowEdge = new(0f, 0f, 1f);
        public ClampedFloatParameter HighEdge = new(1f, 0f, 1f);
        public VolumeParameter<RenderPassEvent> RenderPassEvent = new() 
        { 
            value = UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingPostProcessing 
        };

        public bool IsActive()
        {
            return Enabled.value;
        }
    }
}
