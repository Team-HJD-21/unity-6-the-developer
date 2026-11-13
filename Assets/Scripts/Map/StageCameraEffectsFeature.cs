using Map;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class StageCameraEffectsFeature : ScriptableRendererFeature
{
    private StageCameraEffectsPass _pass;

    public override void Create()
    {
        _pass = new StageCameraEffectsPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (!Application.isPlaying || camera.cameraType != CameraType.Game)
            return;

        camera.TryGetComponent(out WeatherShaderApply weather);
        camera.TryGetComponent(out DayNightShaderApply dayNight);
        if (weather != null && weather.isActiveAndEnabled || dayNight != null && dayNight.isActiveAndEnabled)
            renderer.EnqueuePass(_pass);
    }

    private sealed class StageCameraEffectsPass : ScriptableRenderPass
    {
        private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int NextTexture = Shader.PropertyToID("_NextTex");
        private static readonly int Blend = Shader.PropertyToID("_Blend");

        private sealed class TransitionData
        {
            public TextureHandle Previous;
            public TextureHandle Next;
            public Material Material;
            public float BlendAmount;
        }

        public StageCameraEffectsPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
                return;

            Camera camera = frameData.Get<UniversalCameraData>().camera;
            TextureHandle source = resources.activeColorTexture;
            TextureHandle result = source;
            if (camera.TryGetComponent(out WeatherShaderApply weather) && weather.isActiveAndEnabled &&
                weather.TryGetRenderMaterials(out var previous, out var current, out var transition, out float blend))
            {
                result = Blit(graph, source, current, "Current weather");
                if (transition != null)
                {
                    var previousFrame = Blit(graph, source, previous, "Previous weather");
                    result = BlendWeather(graph, previousFrame, result, transition, blend);
                }
            }

            if (camera.TryGetComponent(out DayNightShaderApply dayNight) && dayNight.isActiveAndEnabled)
                result = Blit(graph, result, dayNight.GetRenderMaterial(), "Day and night");

            if (!result.Equals(source))
                graph.AddBlitPass(result, source, Vector2.one, Vector2.zero, passName: "Stage camera effects output");
        }

        private static TextureHandle Blit(RenderGraph graph, TextureHandle source, Material material, string label)
        {
            if (material == null)
                return source;

            TextureHandle output = CreateOutput(graph, source, label);
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, output, material, 0), passName: label);
            return output;
        }

        private static TextureHandle BlendWeather(RenderGraph graph, TextureHandle previous, TextureHandle next,
            Material material, float blend)
        {
            const string label = "Weather transition";
            TextureHandle output = CreateOutput(graph, previous, label);
            using var builder = graph.AddRasterRenderPass<TransitionData>(label, out var pass);
            pass.Previous = previous;
            pass.Next = next;
            pass.Material = material;
            pass.BlendAmount = blend;
            builder.UseTexture(previous);
            if (!next.Equals(previous))
                builder.UseTexture(next);
            builder.SetRenderAttachment(output, 0, AccessFlags.Write);
            builder.SetRenderFunc(static (TransitionData data, RasterGraphContext context) =>
            {
                var properties = context.renderGraphPool.GetTempMaterialPropertyBlock();
                properties.SetTexture(BlitTexture, data.Previous);
                properties.SetTexture(NextTexture, data.Next);
                properties.SetVector(BlitScaleBias, new Vector4(1f, 1f, 0f, 0f));
                properties.SetFloat(Blend, data.BlendAmount);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.Material, 0, MeshTopology.Triangles, 3, 1, properties);
            });
            return output;
        }

        private static TextureHandle CreateOutput(RenderGraph graph, TextureHandle source, string name)
        {
            var descriptor = source.GetDescriptor(graph);
            descriptor.name = name;
            descriptor.clearBuffer = false;
            descriptor.msaaSamples = MSAASamples.None;
            return graph.CreateTexture(descriptor);
        }
    }
}
