using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZeroAllocSurvival.Editor
{
    internal sealed class SpriteSheetBuilderWindow : EditorWindow
    {
        private readonly List<Texture2D> _frames = new();
        private Vector2 _scroll;
        private int _columns = 4;
        private int _padding;
        private bool _overrideCellSize;
        private Vector2Int _cellSize = new(64, 64);
        private Color _background = Color.clear;
        private bool _createMultipleSprites = true;

        [MenuItem("Tools/Top Down Survival/Sprite Sheet Builder")]
        private static void Open()
        {
            var window = GetWindow<SpriteSheetBuilderWindow>("Sprite Sheet Builder");
            window.minSize = new Vector2(430f, 360f);
            window.AddSelectedTextures(false);
        }

        [MenuItem("Assets/Create Sprite Sheet From Selected Images", true)]
        private static bool ValidateCreateFromSelection() => GetSelectedTextureCount() > 0;

        [MenuItem("Assets/Create Sprite Sheet From Selected Images")]
        private static void CreateFromSelection()
        {
            var window = GetWindow<SpriteSheetBuilderWindow>("Sprite Sheet Builder");
            window.AddSelectedTextures(true);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Frames", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Frames are placed from top-left to right. All cells have the same size; smaller images are centered.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Selected")) AddSelectedTextures(false);
                if (GUILayout.Button("Remove Null")) _frames.RemoveAll(static frame => frame == null);
                if (GUILayout.Button("Clear")) _frames.Clear();
            }

            DrawFrameList();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
            _columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", _columns));
            _padding = Mathf.Max(0, EditorGUILayout.IntField("Padding", _padding));
            _overrideCellSize = EditorGUILayout.Toggle("Override Cell Size", _overrideCellSize);
            if (_overrideCellSize)
            {
                _cellSize = EditorGUILayout.Vector2IntField("Cell Size", _cellSize);
                _cellSize.x = Mathf.Max(1, _cellSize.x);
                _cellSize.y = Mathf.Max(1, _cellSize.y);
            }
            _background = EditorGUILayout.ColorField("Background", _background);
            _createMultipleSprites = EditorGUILayout.Toggle("Create Multiple Sprites", _createMultipleSprites);

            var validCount = CountValidFrames();
            using (new EditorGUI.DisabledScope(validCount == 0))
            {
                if (GUILayout.Button($"Build Sprite Sheet ({validCount} frames)", GUILayout.Height(32f)))
                    Build();
            }
        }

        private void DrawFrameList()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(260f));
            for (var i = 0; i < _frames.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(i.ToString("00"), GUILayout.Width(24f));
                    _frames[i] = (Texture2D)EditorGUILayout.ObjectField(_frames[i], typeof(Texture2D), false);
                    using (new EditorGUI.DisabledScope(i == 0))
                        if (GUILayout.Button("▲", GUILayout.Width(28f))) Swap(i, i - 1);
                    using (new EditorGUI.DisabledScope(i == _frames.Count - 1))
                        if (GUILayout.Button("▼", GUILayout.Width(28f))) Swap(i, i + 1);
                    if (GUILayout.Button("×", GUILayout.Width(28f)))
                    {
                        _frames.RemoveAt(i);
                        i--;
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void AddSelectedTextures(bool replace)
        {
            if (replace) _frames.Clear();
            var selected = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
            Array.Sort(selected, static (left, right) =>
                string.Compare(AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right),
                    StringComparison.OrdinalIgnoreCase));
            foreach (var texture in selected)
                if (texture != null && !_frames.Contains(texture)) _frames.Add(texture);
            Repaint();
        }

        private void Build()
        {
            _frames.RemoveAll(static frame => frame == null);
            if (_frames.Count == 0) return;

            var cellWidth = _overrideCellSize ? _cellSize.x : 1;
            var cellHeight = _overrideCellSize ? _cellSize.y : 1;
            if (!_overrideCellSize)
                foreach (var frame in _frames)
                {
                    cellWidth = Mathf.Max(cellWidth, frame.width);
                    cellHeight = Mathf.Max(cellHeight, frame.height);
                }

            foreach (var frame in _frames)
                if (frame.width > cellWidth || frame.height > cellHeight)
                {
                    EditorUtility.DisplayDialog("Sprite Sheet Builder",
                        $"{frame.name} ({frame.width}x{frame.height}) is larger than the cell ({cellWidth}x{cellHeight}).",
                        "OK");
                    return;
                }

            var columns = Mathf.Min(_columns, _frames.Count);
            var rows = Mathf.CeilToInt(_frames.Count / (float)columns);
            var width = columns * cellWidth + (columns + 1) * _padding;
            var height = rows * cellHeight + (rows + 1) * _padding;
            if (width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
            {
                EditorUtility.DisplayDialog("Sprite Sheet Builder",
                    $"Output {width}x{height} exceeds this device's maximum texture size {SystemInfo.maxTextureSize}.",
                    "OK");
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject("Save Sprite Sheet", "SpriteSheet", "png",
                "Choose the output PNG path.");
            if (string.IsNullOrEmpty(path)) return;

            var output = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = Path.GetFileNameWithoutExtension(path),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            try
            {
                var pixels = new Color32[width * height];
                var background = (Color32)_background;
                Array.Fill(pixels, background);
                for (var index = 0; index < _frames.Count; index++)
                {
                    var source = ReadPixels(_frames[index]);
                    var column = index % columns;
                    var row = index / columns;
                    var x = _padding + column * (cellWidth + _padding) + (cellWidth - _frames[index].width) / 2;
                    var cellBottom = height - _padding - (row + 1) * cellHeight - row * _padding;
                    var y = cellBottom + (cellHeight - _frames[index].height) / 2;
                    for (var sourceY = 0; sourceY < _frames[index].height; sourceY++)
                        Array.Copy(source, sourceY * _frames[index].width, pixels,
                            (y + sourceY) * width + x, _frames[index].width);
                }

                output.SetPixels32(pixels);
                output.Apply(false, false);
                File.WriteAllBytes(Path.GetFullPath(path), output.EncodeToPNG());
            }
            finally
            {
                DestroyImmediate(output);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(path, cellWidth, cellHeight, columns, rows);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private void ConfigureImporter(string path, int cellWidth, int cellHeight, int columns, int rows)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteImportMode = _createMultipleSprites
                ? SpriteImportMode.Multiple
                : SpriteImportMode.Single;
            if (_createMultipleSprites)
            {
                var metadata = new SpriteMetaData[_frames.Count];
                for (var index = 0; index < metadata.Length; index++)
                {
                    var column = index % columns;
                    var row = index / columns;
                    metadata[index] = new SpriteMetaData
                    {
                        name = $"Frame_{index:000}",
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(.5f, .5f),
                        rect = new Rect(
                            _padding + column * (cellWidth + _padding),
                            _padding + (rows - row - 1) * (cellHeight + _padding),
                            cellWidth, cellHeight)
                    };
                }
#pragma warning disable CS0618
                importer.spritesheet = metadata;
#pragma warning restore CS0618
            }
            importer.SaveAndReimport();
        }

        private static Color32[] ReadPixels(Texture2D source)
        {
            var temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
                try
                {
                    readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                    readable.Apply(false, false);
                    return readable.GetPixels32();
                }
                finally
                {
                    DestroyImmediate(readable);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private int CountValidFrames()
        {
            var count = 0;
            foreach (var frame in _frames) if (frame != null) count++;
            return count;
        }

        private void Swap(int left, int right) => (_frames[left], _frames[right]) = (_frames[right], _frames[left]);

        private static int GetSelectedTextureCount() =>
            Selection.GetFiltered<Texture2D>(SelectionMode.Assets).Length;
    }
}
