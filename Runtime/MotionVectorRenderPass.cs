using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace kTools.Motion
{
    sealed class MotionVectorRenderPass : ScriptableRenderPass
    {
#region Fields
        const string kCameraShader = "Hidden/kMotion/CameraMotionVectors";
        const string kObjectShader = "Hidden/kMotion/ObjectMotionVectors";
        const string kPreviousViewProjectionMatrix = "_PrevViewProjMatrix";
        const string kMotionVectorTexture = "_MotionVectorTexture";
        const string kProfilingTag = "Motion Vectors";

        const string kPerObjectMotionIntensity = "_kMotionPerObjectFac";
        const string kCameraMotionIntensity = "_kMotionCameraFac";

        // We only render materials that export a motion vector pass
        static readonly string[] s_ShaderTags = new string[]
        {
            "MotionVectors",
            "kMotionVectors"
        };

        RenderTargetHandle m_MotionVectorHandle;
        Material m_CameraMaterial;
        Material m_ObjectMaterial;
        MotionData m_MotionData;

        MotionBlur m_MotionBlur;
#endregion

#region Constructors
        internal MotionVectorRenderPass()
        {
            // Set data
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }
#endregion

#region State
        internal void Setup(MotionData motionData, MotionBlur motionBlur)
        {
            // Set data
            m_MotionBlur = motionBlur;
            m_MotionData = motionData;
            m_CameraMaterial = new Material(Shader.Find(kCameraShader));
            m_ObjectMaterial = new Material(Shader.Find(kObjectShader));
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // Configure Render Target
            m_MotionVectorHandle.Init(kMotionVectorTexture);

            // We need to slightly tweak the container for the data we're holding
            var descriptor = cameraTextureDescriptor;
            descriptor.colorFormat = RenderTextureFormat.RGHalf;

            cmd.GetTemporaryRT(m_MotionVectorHandle.id, descriptor, FilterMode.Point);
            ConfigureTarget(m_MotionVectorHandle.Identifier(), m_MotionVectorHandle.Identifier());
            cmd.SetRenderTarget(m_MotionVectorHandle.Identifier(), m_MotionVectorHandle.Identifier());
                
            // zCubed: You have to clear here because of the temporary RT :)
            cmd.ClearRenderTarget(true, true, Color.black, 1.0f);
        }
#endregion

#region Execution
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Get data
            var camera = renderingData.cameraData.camera;

            // Never draw in previews or reflections
            if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
                return;

            // Profiling command
            CommandBuffer cmd = CommandBufferPool.Get(kProfilingTag);
            using (new ProfilingScope(cmd, new ProfilingSampler(kProfilingTag)))
            {
                ExecuteCommand(context, cmd);

                // Shader uniforms
                Shader.SetGlobalMatrix(kPreviousViewProjectionMatrix, m_MotionData.previousViewProjectionMatrix);
                Shader.SetGlobalFloat(kPerObjectMotionIntensity, m_MotionBlur.perObjectBlurIntensity.value);
                Shader.SetGlobalFloat(kCameraMotionIntensity, m_MotionBlur.cameraBlurIntensity.value);

                // These flags are still required in SRP or the engine won't compute previous model matrices...
                // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
                camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                // Drawing
                DrawCameraMotionVectors(context, cmd, camera);
                DrawObjectMotionVectors(context, ref renderingData, cmd, camera);
            }
            ExecuteCommand(context, cmd);
        }

        DrawingSettings GetDrawingSettings(ref RenderingData renderingData)
        {
            // Drawing Settings
            var camera = renderingData.cameraData.camera;
            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(ShaderTagId.none, sortingSettings)
            {
                perObjectData = PerObjectData.MotionVectors,
                enableDynamicBatching = renderingData.supportsDynamicBatching,
                enableInstancing = true,
            };

            // Shader Tags
            for (int i = 0; i < s_ShaderTags.Length; ++i)
            {
                drawingSettings.SetShaderPassName(i, new ShaderTagId(s_ShaderTags[i]));
            }
            
            // Material
            //drawingSettings.fallbackMaterial = m_ObjectMaterial;
            //drawingSettings.overrideMaterialPassIndex = 0;
            return drawingSettings;
        }

        void DrawCameraMotionVectors(ScriptableRenderContext context, CommandBuffer cmd, Camera camera)
        {
            // Draw fullscreen quad
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, m_CameraMaterial, 0, 0);
            ExecuteCommand(context, cmd);
        }

        void DrawObjectMotionVectors(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, Camera camera)
        {
            // Get CullingParameters
            var cullingParameters = new ScriptableCullingParameters();
            if (!camera.TryGetCullingParameters(out cullingParameters))
                return;

            // Culling Results
            var cullingResults = context.Cull(ref cullingParameters);

            var drawingSettings = GetDrawingSettings(ref renderingData);
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, camera.cullingMask);
            var renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            
            // Draw Renderers
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
        }
#endregion

#region Cleanup
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            
            // Reset Render Target
            if (m_MotionVectorHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(m_MotionVectorHandle.id);
                m_MotionVectorHandle = RenderTargetHandle.CameraTarget;
            }
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
