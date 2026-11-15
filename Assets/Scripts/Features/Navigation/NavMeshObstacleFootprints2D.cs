// 2D NavMesh를 구울 때 장애물의 그림 대신 발밑 충돌 형상을 사용합니다.
// 바닥은 기존 RenderMeshes 수집을 유지해 윤곽선 Collider 때문에 지면이 빠지지 않게 합니다.
// Not Walkable Modifier가 붙은 나무·돌·터렛 Base만 처리하며 실제 물리 충돌은 바꾸지 않습니다.
using System;
using System.Collections.Generic;
using NavMeshPlus.Components;
using NavMeshPlus.Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace TeamHJD.Game.Navigation
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(NavMeshSurface), typeof(CollectSources2d))]
    public sealed class NavMeshObstacleFootprints2D : NavMeshExtension
    {
        /// <summary>NavMeshPlus의 일반 수집 뒤, 명시된 장애물만 Collider2D 메시로 교체합니다.</summary>
        public override void PostCollectSources(NavMeshSurface surface, List<NavMeshBuildSource> sources,
            NavMeshBuilderState state)
        {
            if (!isActiveAndEnabled) return;
            if (surface.useGeometry != NavMeshCollectGeometry.RenderMeshes)
                throw new InvalidOperationException("발밑 장애물 수집은 Render Meshes 바닥과 함께 사용해야 합니다.");

            var meshes = state.GetExtraState<BuildMeshes>();
            var projection = GetComponent<CollectSources2d>().overrideVector;
            int blockedArea = NavMesh.GetAreaFromName("Not Walkable");
            foreach (var modifier in NavMeshModifier.activeModifiers)
            {
                if (modifier.gameObject.scene != gameObject.scene || modifier.ignoreFromBuild ||
                    !modifier.overrideArea || modifier.area != blockedArea ||
                    !modifier.AffectsAgentType(surface.agentTypeID) ||
                    (surface.layerMask.value & (1 << modifier.gameObject.layer)) == 0) continue;

                var collider = modifier.GetComponent<Collider2D>();
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (collider.compositeOperation != Collider2D.CompositeOperation.None)
                    collider = collider.composite;
                if (collider == null) continue;
                var mesh = collider.CreateMesh(false, false);
                if (mesh == null) throw new InvalidOperationException("장애물 충돌 메시를 만들 수 없습니다: " + modifier.name);
                meshes.Items.Add(mesh);

                // 같은 오브젝트에서 수집된 수관·포신 그림을 제거해 이중 차단하지 않습니다.
                var obstacle = modifier.gameObject;
                sources.RemoveAll(source => source.component != null && source.component.gameObject == obstacle);
                var body = collider.attachedRigidbody;
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Mesh,
                    area = blockedArea,
                    component = collider,
                    sourceObject = mesh,
                    transform = body != null
                        ? Matrix4x4.TRS(Vector3.Scale(body.transform.position, projection), body.transform.rotation, Vector3.one)
                        : Matrix4x4.identity
                });
            }
        }

        // 메시 수명은 NavMeshPlus의 한 번의 Bake 범위에 귀속됩니다.
        public sealed class BuildMeshes : IDisposable
        {
            public readonly List<Mesh> Items = new();
            public void Dispose()
            {
                foreach (var mesh in Items)
                    if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                    else UnityEngine.Object.DestroyImmediate(mesh);
                Items.Clear();
            }
        }
    }
}
