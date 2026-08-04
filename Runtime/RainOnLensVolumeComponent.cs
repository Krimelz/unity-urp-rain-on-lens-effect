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
        public BoolParameter enabled = new(false, false);
        public BoolParameter debug = new(false, false);
        public FloatParameter intensity = new(0f);
        public FloatParameter disappearingSpeed = new(1f);
        public ClampedFloatParameter lowEdge = new(1f, 0f, 1f);
        public ClampedFloatParameter highEdge = new(1f, 0f, 1f);
        public VolumeParameter<RenderPassEvent> renderPassEvent = new() 
        { 
            value = RenderPassEvent.AfterRenderingPostProcessing 
        };

        public bool IsActive()
        {
            return enabled.value;
        }
    }
}
