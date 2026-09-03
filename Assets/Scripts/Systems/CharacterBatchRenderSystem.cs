using System;
using System.Runtime.InteropServices;
using System.Threading;
using LitheEcs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Services;

namespace ZeroAllocSurvival.Systems
{
    internal sealed class CharacterBatchRenderSystem :
        ParallelActionSystem<BatchVisual, PhysicsPosition, CharacterAnimationState, CharacterVisualFeedback, CollisionRadius>,
        IDisposable
    {
        private const int PackedMatrixBytes = 48;
        private const int Float4Bytes = 16;
        private const int ZeroPrefixBytes = 96;
        private readonly PackedMatrix[] _objectToWorld;
        private readonly PackedMatrix[] _worldToObject;
        private readonly Vector4[] _visualData;
        private readonly Vector4[] _boundingSpheres;
        private readonly int[] _instanceBatchIds;
        private readonly Vector3[] _visualOffsets;
        private readonly float[] _boundsRadii;
        private readonly BatchRendererGroup _group;
        private readonly GraphicsBuffer _buffer;
        private readonly BatchID[] _batchIds;
        private readonly BatchMeshID[] _meshIds;
        private readonly BatchMaterialID[] _materialIds;
        private readonly Mesh[] _meshes;
        private readonly Material[] _materials;
        private readonly uint _objectToWorldOffset;
        private readonly uint _worldToObjectOffset;
        private readonly uint _visualDataOffset;
        private int _count;

        internal CharacterBatchRenderSystem(World world, CharacterVisualRegistry visuals, int capacity)
            : base(world, capacity)
        {
            _objectToWorld = new PackedMatrix[capacity];
            _worldToObject = new PackedMatrix[capacity];
            _visualData = new Vector4[capacity];
            _boundingSpheres = new Vector4[capacity];
            _instanceBatchIds = new int[capacity];
            _visualOffsets = new Vector3[visuals.Count];
            _boundsRadii = new float[visuals.Count];
            _batchIds = new BatchID[visuals.Count];
            _meshIds = new BatchMeshID[visuals.Count];
            _materialIds = new BatchMaterialID[visuals.Count];
            _meshes = new Mesh[visuals.Count];
            _materials = new Material[visuals.Count];

            _objectToWorldOffset = ZeroPrefixBytes;
            _worldToObjectOffset = _objectToWorldOffset + (uint)(PackedMatrixBytes * capacity);
            _visualDataOffset = _worldToObjectOffset + (uint)(PackedMatrixBytes * capacity);
            var totalBytes = (int)_visualDataOffset + Float4Bytes * capacity;
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, (totalBytes + 3) / 4, sizeof(int));
            _buffer.SetData(new[] { Matrix4x4.zero });
            _group = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);

            var shader = Shader.Find("ZeroAllocSurvival/Character BRG");
            if (shader == null) throw new InvalidOperationException("Character BRG shader was not found.");
            for (var i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i].Visual;
                _visualOffsets[i] = visual.VisualOffset;
                var mesh = CreateQuad(visual.VisualScale, visual.name);
                var material = new Material(shader) { name = $"{visual.name} BRG (Runtime)" };
                material.mainTexture = visual.AtlasTexture;
                material.SetVector("_AtlasSize", new Vector4(visual.AtlasColumns, visual.AtlasRows,
                    1f / visual.AtlasColumns, 1f / visual.AtlasRows));
                _meshes[i] = mesh;
                _materials[i] = material;
                _boundsRadii[i] = mesh.bounds.extents.magnitude;
                _meshIds[i] = _group.RegisterMesh(mesh);
                _materialIds[i] = _group.RegisterMaterial(material);
                var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
                metadata[0] = Metadata("unity_ObjectToWorld", _objectToWorldOffset);
                metadata[1] = Metadata("unity_WorldToObject", _worldToObjectOffset);
                metadata[2] = Metadata("_VisualData", _visualDataOffset);
                _batchIds[i] = _group.AddBatch(metadata, _buffer.bufferHandle);
                metadata.Dispose();
            }
            _group.SetGlobalBounds(new Bounds(Vector3.zero, Vector3.one * 100000f));
        }

        protected override bool OnPreTick() { _count = 0; return true; }

        protected override void OnPostTick()
        {
            if (_count == 0) return;
            _buffer.SetData(_objectToWorld, 0, (int)(_objectToWorldOffset / PackedMatrixBytes), _count);
            _buffer.SetData(_worldToObject, 0, (int)(_worldToObjectOffset / PackedMatrixBytes), _count);
            _buffer.SetData(_visualData, 0, (int)(_visualDataOffset / Float4Bytes), _count);
        }

        protected override void ForEach(Span<BatchVisual> batchVisual, Span<PhysicsPosition> positions, Span<CharacterAnimationState> animations,
            Span<CharacterVisualFeedback> feedbacks, Span<CollisionRadius> radii, EntityRange entities)
        {
            var destination = entities.Offset;
            for (var i = 0; i < entities.Length; i++)
            {
                Write(destination + i, entities[i], batchVisual[i], positions[i], animations[i], feedbacks[i], radii[i]);
            }
            Interlocked.Add(ref _count, entities.Length);
        }

        private void Write(int index, in Entity entity, in BatchVisual visual, in PhysicsPosition position,
            in CharacterAnimationState animation, in CharacterVisualFeedback feedback, in CollisionRadius radius)
        {
            var frame = animation.CurrentFrame + ClipStart(animation);
            var column = frame % Mathf.Max(1, animation.Columns);
            var row = frame / Mathf.Max(1, animation.Columns);
            var atlasFrame = (animation.AtlasRowOffset + row) * Mathf.Max(1, animation.AtlasColumns) +
                             animation.AtlasColumnOffset + column;
            var diameter = Mathf.Max(.01f, radius.Value * 2f);
            var renderPosition = position.Value + _visualOffsets[visual.BatchId] * diameter;
            renderPosition.z = visual.Depth;
            if (visual.SortByY != 0)
                renderPosition.z += Mathf.Clamp(renderPosition.y * .001f, -.04f, .04f) +
                                    (entity.Id.GetHashCode() & 1023) * 1e-7f;
            var matrix = Matrix4x4.TRS(renderPosition, Quaternion.identity,
                new Vector3(diameter, diameter, 1f));
            _objectToWorld[index] = new PackedMatrix(matrix);
            _worldToObject[index] = new PackedMatrix(matrix.inverse);
            _visualData[index] = new Vector4(atlasFrame, animation.AppliedFacingLeft,
                feedback.AppliedEmission, feedback is { AppliedFade: <= 0f, HasAppliedEffect: 0 }
                    ? 1f : feedback.AppliedFade);
            _boundingSpheres[index] = new Vector4(renderPosition.x, renderPosition.y, renderPosition.z,
                diameter * _boundsRadii[visual.BatchId]);
            _instanceBatchIds[index] = visual.BatchId;
        }

        public void Dispose()
        {
            _group.Dispose();
            _buffer.Dispose();
            for (var i = 0; i < _materials.Length; i++)
            {
                UnityEngine.Object.Destroy(_materials[i]);
                UnityEngine.Object.Destroy(_meshes[i]);
            }
        }

        private unsafe JobHandle OnPerformCulling(BatchRendererGroup rendererGroup,
            BatchCullingContext context, BatchCullingOutput output, IntPtr userContext)
        {
            var commands = (BatchCullingOutputDrawCommands*)output.drawCommands.GetUnsafePtr();
            var visibleCount = 0;
            var commandCount = 0;
            for (var batch = 0; batch < _batchIds.Length; batch++)
            {
                var batchVisible = 0;
                for (var i = 0; i < _count; i++)
                    if (_instanceBatchIds[i] == batch && IsVisible(_boundingSpheres[i], context.cullingPlanes))
                        batchVisible++;
                visibleCount += batchVisible;
                if (batchVisible != 0) commandCount++;
            }
            if (visibleCount == 0) { *commands = default; return default; }

            var alignment = UnsafeUtility.AlignOf<long>();
            commands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(
                commandCount * UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment, Allocator.TempJob);
            commands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
            commands->visibleInstances = (int*)UnsafeUtility.Malloc(
                visibleCount * sizeof(int), alignment, Allocator.TempJob);
            commands->drawCommandPickingEntityIds = null;
            commands->instanceSortingPositions = null;
            commands->instanceSortingPositionFloatCount = 0;
            commands->drawCommandCount = commandCount;
            commands->drawRangeCount = 1;
            commands->visibleInstanceCount = visibleCount;

            var commandIndex = 0;
            var visibleOffset = 0;
            for (var batch = 0; batch < _batchIds.Length; batch++)
            {
                var start = visibleOffset;
                for (var i = 0; i < _count; i++)
                    if (_instanceBatchIds[i] == batch && IsVisible(_boundingSpheres[i], context.cullingPlanes))
                        commands->visibleInstances[visibleOffset++] = i;
                var count = visibleOffset - start;
                if (count == 0) continue;
                commands->drawCommands[commandIndex++] = new BatchDrawCommand
                {
                    visibleOffset = (uint)start, visibleCount = (uint)count, batchID = _batchIds[batch],
                    materialID = _materialIds[batch], meshID = _meshIds[batch], submeshIndex = 0,
                    splitVisibilityMask = 0xff, flags = BatchDrawCommandFlags.None, sortingPosition = 0
                };
            }
            commands->drawRanges[0] = new BatchDrawRange
            {
                drawCommandsBegin = 0, drawCommandsCount = (uint)commandCount,
                filterSettings = new BatchFilterSettings { renderingLayerMask = uint.MaxValue }
            };
            return default;
        }

        private static bool IsVisible(Vector4 sphere, NativeArray<Plane> planes)
        {
            var center = new Vector3(sphere.x, sphere.y, sphere.z);
            for (var i = 0; i < planes.Length; i++)
                if (planes[i].GetDistanceToPoint(center) < -sphere.w) return false;
            return true;
        }

        private static byte ClipStart(in CharacterAnimationState value) => value.AppliedClip switch
        {
            CharacterAnimationClip.Walk => value.WalkStart,
            CharacterAnimationClip.Dead => value.DeadStart,
            _ => value.IdleStart
        };

        private static MetadataValue Metadata(string name, uint offset) => new()
        {
            NameID = Shader.PropertyToID(name), Value = 0x80000000u | offset
        };

        private static Mesh CreateQuad(Vector3 scale, string name)
        {
            var half = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) * .5f;
            var mesh = new Mesh { name = $"{name} BRG Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-half.x, -half.y), new Vector3(half.x, -half.y),
                new Vector3(half.x, half.y), new Vector3(-half.x, half.y)
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PackedMatrix
        {
            private readonly float c0x, c0y, c0z, c1x, c1y, c1z;
            private readonly float c2x, c2y, c2z, c3x, c3y, c3z;
            internal PackedMatrix(Matrix4x4 m)
            {
                c0x = m.m00; c0y = m.m10; c0z = m.m20;
                c1x = m.m01; c1y = m.m11; c1z = m.m21;
                c2x = m.m02; c2y = m.m12; c2z = m.m22;
                c3x = m.m03; c3y = m.m13; c3z = m.m23;
            }
        }
    }
}
