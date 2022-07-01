using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace kTools.Motion
{
    sealed class MotionBlurRenderPass : ScriptableRenderPass
    {
#region Fields
        const string kMotionBlurShader = "Hidden/kMotion/MotionBlur";
        const string kProfilingTag = "Motion Blur";

        static readonly string[] s_ShaderTags = new string[]
        {
            "UniversalForward",
            "LightweightForward",
        };

        Material m_Material;
        MotionBlur m_MotionBlur;
#endregion

#region Constructors
        internal MotionBlurRenderPass()
        {
            // Set data
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }
#endregion

#region Setup
        internal void Setup(MotionBlur motionBlur)
        {
            // Set data
            m_MotionBlur = motionBlur;
            m_Material = new Material(Shader.Find(kMotionBlurShader));
        }
#endregion

#region Execution
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Get data
            var camera = renderingData.cameraData.camera;

            // Never draw in Preview
            if (camera.cameraType == CameraType.Preview)
                return;

            // Profiling command
            CommandBuffer cmd = CommandBufferPool.Get(kProfilingTag);
            using (new ProfilingScope(cmd, new ProfilingSampler(kProfilingTag)))
            {
                // Set Material properties from VolumeComponent
                m_Material.SetFloat("_Intensity", m_MotionBlur.intensity.value);

                // RenderTexture
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.colorFormat = RenderTextureFormat.DefaultHDR;
                descriptor.depthBufferBits = 16;

                var renderTexture = RenderTexture.GetTemporary(descriptor);

                // Blits
                var passIndex = (int)m_MotionBlur.quality.value;

                cmd.SetGlobalTexture("_MainTex", renderingData.cameraData.renderer.cameraColorTarget);
                cmd.SetGlobalVector("_MainTex_TexelSize", new Vector4(0, 0, renderTexture.width, renderTexture.height));
                cmd.SetRenderTarget(new RenderTargetIdentifier(renderTexture, 0, CubemapFace.Unknown, -1));
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, m_Material, 0, passIndex);

                cmd.SetGlobalTexture("_MainTex", renderTexture);
                cmd.SetGlobalVector("_MainTex_TexelSize", new Vector4(0, 0, renderTexture.width, renderTexture.height));
                cmd.SetRenderTarget(new RenderTargetIdentifier(renderingData.cameraData.renderer.cameraColorTarget, 0, CubemapFace.Unknown, -1));
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, m_Material, 0, passIndex);

                //cmd.Blit(renderingData.cameraData.renderer.cameraColorTarget, renderTexture, m_Material, passIndex);
                //cmd.Blit(renderTexture, renderingData.cameraData.renderer.cameraColorTarget, m_Material, passIndex);

                ExecuteCommand(context, cmd);

                RenderTexture.ReleaseTemporary(renderTexture);
            }
            ExecuteCommand(context, cmd);
        }
#endregion

#region CommandBufer
        void ExecuteCommand(ScriptableRenderContext context, CommandBuffer cmd)
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
#endregion
    }
}