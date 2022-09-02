using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthOnly : ScriptableRendererFeature
{
	public RenderTexture rt;
	public class CustomRenderPass : ScriptableRenderPass
	{
		RenderQueueType renderQueueType = RenderQueueType.Opaque;
		FilteringSettings m_FilteringSettings;
		List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>() { new ShaderTagId("DepthOnly") };

		RenderTexture rt;
		//ProfilingSampler m_ProfilingSampler;
		static Camera camera;
		public static void SetCam(Camera camera)
		{
			CustomRenderPass.camera = camera;
		}
		public CustomRenderPass(RenderTexture rt)
		{
			this.rt = rt;
			base.profilingSampler = new ProfilingSampler("De");
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			ConfigureClear(ClearFlag.All, Color.clear);
			ConfigureTarget(new RenderTargetIdentifier(rt));
			
		}
		// This method is called before executing the render pass.
		// It can be used to configure render targets and their clear state. Also to create temporary render target textures.
		// When empty this render pass will render to the active camera render target.
		// You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
		// The render pipeline will ensure target setup and clearing happens in a performant manner.
		public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
		{
		}

		// Here you can implement the rendering logic.
		// Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
		// https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
		// You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			if (camera == null)
				return;

			CommandBuffer cmd = CommandBufferPool.Get("water");
			//using (new ProfilingScope(cmd, profilingSampler))
			{
				SortingCriteria sortingCriteria = (renderQueueType == RenderQueueType.Transparent)
				? SortingCriteria.CommonTransparent
				: renderingData.cameraData.defaultOpaqueSortFlags;
				RenderQueueRange renderQueueRange = (renderQueueType == RenderQueueType.Transparent)
	? RenderQueueRange.transparent
	: RenderQueueRange.opaque;



				m_FilteringSettings = new FilteringSettings(renderQueueRange, -1);
				
				RenderStateBlock renderStateBlock=new RenderStateBlock(RenderStateMask.Everything);

				//DrawingSettings drawingSettings =
				//	CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);

				var sortingSettings = new SortingSettings(camera);
				DrawingSettings drawingSettings =
				new DrawingSettings(new ShaderTagId("DepthOnly"), sortingSettings);
				cmd.SetViewProjectionMatrices(
						camera.worldToCameraMatrix,
						camera.projectionMatrix);
				camera.TryGetCullingParameters(out var cullingParameters);
				var cullingResults = context.Cull(ref cullingParameters);



				context.ExecuteCommandBuffer(cmd); 
				cmd.Clear();
				context.DrawRenderers(cullingResults, ref drawingSettings, ref m_FilteringSettings,ref renderStateBlock);
			}
			CommandBufferPool.Release(cmd);
		}

		// Cleanup any allocated resources that were created during the execution of this render pass.
		public override void OnCameraCleanup(CommandBuffer cmd)
		{
		}
	}

	CustomRenderPass m_ScriptablePass;

	/// <inheritdoc/>
	public override void Create()
	{
		m_ScriptablePass = new CustomRenderPass(rt);

		// Configures where the render pass should be injected.
		m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
	}

	// Here you can inject one or multiple render passes in the renderer.
	// This method is called when setting up the renderer once per-camera.
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		renderer.EnqueuePass(m_ScriptablePass);
	}
}


