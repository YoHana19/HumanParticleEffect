using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenRTBlitRenderPassFeature : ScriptableRendererFeature
{
    class ScreenRTBlitRenderPass : ScriptableRenderPass
    {
        private RenderTargetIdentifier _currentTarget;
        private RenderTexture _screenBuffer;

        public void Setup(RenderTargetIdentifier target, RenderTexture buffer)
        {
            _currentTarget = target;
            _screenBuffer = buffer;
        }
        
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        { 
            var cmd = CommandBufferPool.Get(nameof(ScreenRTBlitRenderPass));
            cmd.Blit(_currentTarget, _screenBuffer);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
        }
    }

    ScreenRTBlitRenderPass _scriptablePass;

    public override void Create()
    {
        _scriptablePass = new ScreenRTBlitRenderPass();

        _scriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var currentCamera = renderingData.cameraData.camera;
        if (currentCamera != null && currentCamera.cameraType == CameraType.Game)
        {
            var humanParticle = currentCamera.GetComponent<HumanParticle>();
            if (humanParticle == null) return;
            if (humanParticle.LatestCameraFeedBuffer == null) return;
            _scriptablePass.Setup(renderer.cameraColorTarget, humanParticle.LatestCameraFeedBuffer);
            renderer.EnqueuePass(_scriptablePass);
        }
    }
}
