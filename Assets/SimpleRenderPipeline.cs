using UnityEngine;
using UnityEngine.Rendering;

/*
Small example of a custom SRP that supports 1 directional light with shadows done via a single shadow map (no cascades)
 */
public class SimpleRenderPipeline : RenderPipeline
{
    static readonly ShaderTagId s_OpaquePassTag = new ShaderTagId("ForwardBase"); // Must match the LightMode tag value in the shader for the pass you want to render

    RenderTexture m_ShadowMap;

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        RenderPipeline.BeginFrameRendering(context, cameras);
        foreach(var camera in cameras)
        {
            RenderCamera(ref context, camera);
        }
        RenderPipeline.EndFrameRendering(context, cameras);
    }
    
    private bool RenderShadowMaps(ref ScriptableRenderContext context, ref CullingResults cullingResults, ref Matrix4x4 worldToShadowMatrix)
    {
        if (cullingResults.visibleLights.Length == 0)
            return false;

        int lightIndex = 0; // Just use first light (index 0) for shadows and assume it's a directional light

        bool needToRender = cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(lightIndex, 0, 1, Vector3.forward, 1024, cullingResults.visibleLights[0].light.shadowNearPlane, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData shadowSplitData);

        if (!needToRender)
            return false;

        CommandBuffer cb = new CommandBuffer();
        cb.name = "Set up shadow data";
        cb.SetRenderTarget(m_ShadowMap);
        cb.ClearRenderTarget(true, true, Color.clear);
        cb.SetViewProjectionMatrices(viewMatrix, projMatrix);
        context.ExecuteCommandBuffer(cb);

        ShadowDrawingSettings shadowDrawSettings = new ShadowDrawingSettings(cullingResults, lightIndex);
        shadowDrawSettings.splitData = shadowSplitData;
        
        context.DrawShadows(ref shadowDrawSettings);

        if (SystemInfo.usesReversedZBuffer) {
			projMatrix.m20 = -projMatrix.m20;
			projMatrix.m21 = -projMatrix.m21;
			projMatrix.m22 = -projMatrix.m22;
			projMatrix.m23 = -projMatrix.m23;
		}

        var scaleOffset = Matrix4x4.TRS(
			Vector3.one * 0.5f, Quaternion.identity, Vector3.one * 0.5f
		);

		worldToShadowMatrix =
			scaleOffset * (projMatrix * viewMatrix);

        return true;
    }

    private void RenderCamera(ref ScriptableRenderContext context, Camera camera)
    {
        if (m_ShadowMap == null)
        {
            m_ShadowMap = new RenderTexture(1024, 1024, 24, RenderTextureFormat.Shadowmap);
            m_ShadowMap.name = "Shadow Map";
            m_ShadowMap.Create();
        }

        if(camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters)){
            
            // Start camera render
            RenderPipeline.BeginCameraRendering(context, camera);

            cullingParameters.shadowDistance = 30;

            // Perform culling operations
            CullingResults cullingResults = context.Cull(ref cullingParameters);
            
            // Shadow map rendering
            Matrix4x4 worldToShadowMatrix = Matrix4x4.identity;
            bool didRenderShadowMap = RenderShadowMaps(ref context, ref cullingResults, ref worldToShadowMatrix);

            // Setup camera for rendering
            context.SetupCameraProperties(camera);

            // Clear camera
            CommandBuffer cb_ClearCamera = new CommandBuffer();
            cb_ClearCamera.name = "ClearCamera";
            cb_ClearCamera.SetRenderTarget(camera.targetTexture);
            cb_ClearCamera.ClearRenderTarget(true, true, camera.backgroundColor);
            context.ExecuteCommandBuffer(cb_ClearCamera);

            // Draw opaque objects
            SortingSettings sortSettings = new SortingSettings(camera);
            sortSettings.criteria = SortingCriteria.CommonOpaque;

            FilteringSettings filterSettings = FilteringSettings.defaultValue;
            filterSettings.renderQueueRange = RenderQueueRange.opaque;

            if (didRenderShadowMap)
            {
                CommandBuffer cb = new CommandBuffer();
                cb.name = "Set up shadow shader properties";
                cb.SetGlobalTexture("_ShadowMapTexture", m_ShadowMap);
                cb.SetGlobalMatrix("_WorldToShadowMatrix", worldToShadowMatrix);
                cb.SetGlobalVector("_LightDirection", -cullingResults.visibleLights[0].light.transform.forward); // Direction towards the light
                context.ExecuteCommandBuffer(cb);
            }

            DrawingSettings opaqueDrawSettings = new DrawingSettings(s_OpaquePassTag, sortSettings);
            context.DrawRenderers(cullingResults, ref opaqueDrawSettings, ref filterSettings);

            // Draw skybox
            context.DrawSkybox(camera);

            // Final submission
            context.Submit();

            // End camera render
            RenderPipeline.EndCameraRendering(context, camera);
        }
    }
}