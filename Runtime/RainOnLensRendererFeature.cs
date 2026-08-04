using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RainOnLens
{
    public sealed partial class RainOnLensRendererFeature : ScriptableRendererFeature
    {
        private static readonly int DisappearingSpeed = Shader.PropertyToID("_DisappearingSpeed");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int HighEdge = Shader.PropertyToID("_HighEdge");
        private static readonly int LowEdge = Shader.PropertyToID("_LowEdge");

        private Material m_Material;
        private RainOnLensPostRenderPass m_FullScreenPass;

        public override void Create()
        {
            CoreUtils.Destroy(m_Material);
            m_Material = CoreUtils.CreateEngineMaterial("Shader Graphs/RainOnLens");

            if (!m_Material)
            {
                Debug.LogError("Cannot create material!");
                return;
            }

            m_FullScreenPass = new RainOnLensPostRenderPass(name, m_Material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Material == null || m_FullScreenPass == null)
            {
                return;
            }

            if (renderingData.cameraData.camera.cameraType is not (CameraType.Game or CameraType.SceneView))
            {
                return;
            }

#if UNITY_EDITOR
            if (renderingData.cameraData.isSceneViewCamera)
            {
                var sceneView = UnityEditor.SceneView.currentDrawingSceneView;
                if (sceneView != null && !sceneView.sceneViewState.showImageEffects)
                {
                    return;
                }
            }
#endif

            var volume = VolumeManager.instance.stack?.GetComponent<RainOnLensVolumeComponent>();

            if (volume == null || !volume.IsActive())
            {
                return;
            }

            m_FullScreenPass.renderPassEvent = volume.RenderPassEvent.value;
            m_FullScreenPass.ConfigureInput(ScriptableRenderPassInput.None);

            renderer.EnqueuePass(m_FullScreenPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
        }
    }
}
