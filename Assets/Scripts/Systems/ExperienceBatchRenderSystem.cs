using System;
using System.Runtime.InteropServices;
using LitheEcs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using ZeroAllocSurvival.Components;
using ZeroAllocSurvival.Definitions;

namespace ZeroAllocSurvival.Systems
{
    /// <summary>Renders all experience pickups with one BRG draw command.</summary>
    internal sealed class ExperienceBatchRenderSystem : QueryActionSystem<ExperienceDrop>, IDisposable
    {
        private const int PackedMatrixBytes = 48;
        private const int Float4Bytes = 16;
        private const int ZeroPrefixBytes = 96;
        private readonly int _capacity;
        private readonly Vector3 _scale;
        private readonly float _depthOffset;
        private readonly float _boundsRadius;
        private readonly PackedMatrix[] _objectToWorld;
        private readonly PackedMatrix[] _worldToObject;
        private readonly Vector4[] _boundingSpheres;
        private readonly Vector4[] _colors;
        private readonly ExperienceVisualDefinition _visual;
        private readonly BatchRendererGroup _group;
        private readonly GraphicsBuffer _buffer;
        private readonly BatchID _batchId;
        private readonly BatchMeshID _meshId;
        private readonly BatchMaterialID _materialId;
        private readonly Material _material;
        private readonly Mesh _mesh;
        private readonly uint _objectToWorldOffset;
        private readonly uint _worldToObjectOffset;
        private readonly uint _colorOffset;
        private int _count;

        internal ExperienceBatchRenderSystem(World world, ExperienceVisualDefinition visual, int capacity) : base(world)
        {
            if (visual == null || visual.Sprite == null)
                throw new InvalidOperationException("Experience visual requires a sprite.");

            _capacity = Mathf.Max(1, capacity);
            _visual = visual;
            _scale = visual.Scale;
            _depthOffset = visual.DepthOffset;
            _boundsRadius = visual.Sprite.bounds.extents.magnitude *
                            Mathf.Max(Mathf.Abs(_scale.x), Mathf.Abs(_scale.y));
            _objectToWorld = new PackedMatrix[_capacity];
            _worldToObject = new PackedMatrix[_capacity];
            _boundingSpheres = new Vector4[_capacity];
            _colors = new Vector4[_capacity];
            _mesh = CreateQuad(visual.Sprite);

            var shader = Shader.Find("ZeroAllocSurvival/Experience BRG");
            if (shader == null) throw new InvalidOperationException("Experience BRG shader was not found.");
            _material = new Material(shader) { name = "Experience BRG (Runtime)" };
            _material.mainTexture = visual.Sprite.texture;
            _material.SetColor("_BaseColor", Color.white);

            _objectToWorldOffset = ZeroPrefixBytes;
            _worldToObjectOffset = _objectToWorldOffset + (uint)(PackedMatrixBytes * _capacity);
            _colorOffset = _worldToObjectOffset + (uint)(PackedMatrixBytes * _capacity);
            var totalBytes = (int)_colorOffset + Float4Bytes * _capacity;
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, (totalBytes + 3) / 4, sizeof(int));
            _buffer.SetData(new[] { Matrix4x4.zero });
            _objectToWorld[0] = new PackedMatrix(Matrix4x4.identity);
            _worldToObject[0] = new PackedMatrix(Matrix4x4.identity);
            _buffer.SetData(_objectToWorld, 0, (int)(_objectToWorldOffset / PackedMatrixBytes), 1);
            _buffer.SetData(_worldToObject, 0, (int)(_worldToObjectOffset / PackedMatrixBytes), 1);

            _group = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
            _meshId = _group.RegisterMesh(_mesh);
            _materialId = _group.RegisterMaterial(_material);
            var metadata = new NativeArray<MetadataValue>(3, Allocator.Temp);
            metadata[0] = Metadata("unity_ObjectToWorld", _objectToWorldOffset);
            metadata[1] = Metadata("unity_WorldToObject", _worldToObjectOffset);
            metadata[2] = Metadata("_ExperienceColor", _colorOffset);
            _batchId = _group.AddBatch(metadata, _buffer.bufferHandle);
            metadata.Dispose();
            _group.SetGlobalBounds(new Bounds(Vector3.zero, Vector3.one * 100000f));
        }

        protected override bool OnPreTick()
        {
            _count = 0;
            return true;
        }

        protected override void ForEach(in Entity entity, ref ExperienceDrop pickup)
        {
            if (_count >= _capacity) return;
            var renderPosition = pickup.Position + Vector3.forward * (VisualDepth.Experience + _depthOffset);
            var matrix = Matrix4x4.TRS(renderPosition, Quaternion.identity, _scale);
            _objectToWorld[_count] = new PackedMatrix(matrix);
            _worldToObject[_count] = new PackedMatrix(matrix.inverse);
            _colors[_count] = _visual.ColorForValue(pickup.Value);
            _boundingSpheres[_count] = new Vector4(renderPosition.x, renderPosition.y, renderPosition.z,
                _boundsRadius);
            _count++;
        }

        protected override void OnPostTick()
        {
            if (_count == 0) return;
            _buffer.SetData(_objectToWorld, 0, (int)(_objectToWorldOffset / PackedMatrixBytes), _count);
            _buffer.SetData(_worldToObject, 0, (int)(_worldToObjectOffset / PackedMatrixBytes), _count);
            _buffer.SetData(_colors, 0, (int)(_colorOffset / Float4Bytes), _count);
        }

        public void Dispose()
        {
            _group.Dispose();
            _buffer.Dispose();
            UnityEngine.Object.Destroy(_material);
            UnityEngine.Object.Destroy(_mesh);
        }

        private unsafe JobHandle OnPerformCulling(BatchRendererGroup rendererGroup,
            BatchCullingContext context, BatchCullingOutput output, IntPtr userContext)
        {
            var commands = (BatchCullingOutputDrawCommands*)output.drawCommands.GetUnsafePtr();
            var visibleCount = 0;
            for (var i = 0; i < _count; i++)
                if (IsVisible(_boundingSpheres[i], context.cullingPlanes))
                    visibleCount++;
            if (visibleCount == 0)
            {
                *commands = default;
                return default;
            }

            var alignment = UnsafeUtility.AlignOf<long>();
            commands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<BatchDrawCommand>(), alignment, Allocator.TempJob);
            commands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<BatchDrawRange>(), alignment, Allocator.TempJob);
            commands->visibleInstances = (int*)UnsafeUtility.Malloc(visibleCount * sizeof(int), alignment,
                Allocator.TempJob);
            commands->drawCommandPickingEntityIds = null;
            commands->instanceSortingPositions = null;
            commands->instanceSortingPositionFloatCount = 0;
            commands->drawCommandCount = 1;
            commands->drawRangeCount = 1;
            commands->visibleInstanceCount = visibleCount;
            commands->drawCommands[0] = new BatchDrawCommand
            {
                visibleOffset = 0,
                visibleCount = (uint)visibleCount,
                batchID = _batchId,
                materialID = _materialId,
                meshID = _meshId,
                submeshIndex = 0,
                splitVisibilityMask = 0xff
            };
            commands->drawRanges[0] = new BatchDrawRange
            {
                drawCommandsBegin = 0,
                drawCommandsCount = 1,
                filterSettings = new BatchFilterSettings { renderingLayerMask = uint.MaxValue }
            };
            var visibleIndex = 0;
            for (var i = 0; i < _count; i++)
                if (IsVisible(_boundingSpheres[i], context.cullingPlanes))
                    commands->visibleInstances[visibleIndex++] = i;
            return default;
        }

        private static bool IsVisible(Vector4 sphere, NativeArray<Plane> planes)
        {
            var center = new Vector3(sphere.x, sphere.y, sphere.z);
            for (var i = 0; i < planes.Length; i++)
                if (planes[i].GetDistanceToPoint(center) < -sphere.w)
                    return false;
            return true;
        }

        private static MetadataValue Metadata(string name, uint offset) => new()
        {
            NameID = Shader.PropertyToID(name), Value = 0x80000000u | offset
        };

        private static Mesh CreateQuad(Sprite sprite)
        {
            var min = sprite.bounds.min;
            var max = sprite.bounds.max;
            var mesh = new Mesh { name = "Experience BRG Quad" };
            mesh.vertices = new[]
            {
                new Vector3(min.x, min.y), new Vector3(max.x, min.y),
                new Vector3(max.x, max.y), new Vector3(min.x, max.y)
            };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PackedMatrix
        {
            private readonly float c0x, c0y, c0z;
            private readonly float c1x, c1y, c1z;
            private readonly float c2x, c2y, c2z;
            private readonly float c3x, c3y, c3z;

            internal PackedMatrix(Matrix4x4 m)
            {
                c0x = m.m00;
                c0y = m.m10;
                c0z = m.m20;
                c1x = m.m01;
                c1y = m.m11;
                c1z = m.m21;
                c2x = m.m02;
                c2y = m.m12;
                c2z = m.m22;
                c3x = m.m03;
                c3y = m.m13;
                c3z = m.m23;
            }
        }
    }
}