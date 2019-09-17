using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "SimpleRenderPipelineAsset")]
public class SimpleRenderPipelineAsset : RenderPipelineAsset
{
    protected override RenderPipeline CreatePipeline() {
        return new SimpleRenderPipeline();
    }
}