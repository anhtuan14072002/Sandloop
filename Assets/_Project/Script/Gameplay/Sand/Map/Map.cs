using System;
using Sand.Map.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sand.Map
{
    public class Map : IDisposable
    {
        private Texture2D _texture;
        public Texture2D Texture => _texture;
        
        private bool _ownsTexture;
        private bool m_isMovePause;
        
        private NativeArray<Cell> _cells;
        public NativeArray<Cell> Cells => _cells;
        
        public bool IsMovePause
        {
            get => m_isMovePause;
            set => m_isMovePause = value;
        }

        public bool _dirty = true;

        public bool Dirty
        {
            get => _dirty;
            set => _dirty = value;
        }

        private bool _isGameOver;
        private int m_width, m_height;
        public int Width => m_width;
        public int Height => m_height;

        private int Idx(int x, int y) => y * m_width + x;
        private NativeArray<Color32> _pixels;

        public Cell GetCell(int x, int y) => _cells[Idx(x, y)];

        public Map(int width, int height, Texture2D outputTexture = null)
        {
            m_width = width;
            m_height = height;

            _cells = new NativeArray<Cell>(m_width * m_height, Allocator.Persistent);

            if (outputTexture != null && outputTexture.width == m_width && outputTexture.height == m_height &&
                outputTexture.isReadable)
            {
                _texture = outputTexture;
                _ownsTexture = false;
            }
            else
            {
                _texture = new Texture2D(m_width, m_height, TextureFormat.RGBA32, false);
                _texture.filterMode = FilterMode.Point;
                _texture.wrapMode = TextureWrapMode.Clamp;
                _ownsTexture = true;
            }

            _texture.filterMode = FilterMode.Point;
            _texture.wrapMode = TextureWrapMode.Clamp;
            _pixels = new NativeArray<Color32>(m_width * m_height, Allocator.Persistent);
            new ClearMapJob
            {
                pixels = _pixels,
                cells = _cells,
                width = m_width
            }.Schedule(_cells.Length, 64).Complete();
        }

        public void SetTexturePixelsFromRenderTarget(RenderTexture renderTexture)
        {
            if (renderTexture == null) return;
            
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = renderTexture;
                _texture.ReadPixels(new Rect(0.0f, 0.0f, m_width, m_height), 0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            ScheduleUpdateCellsFromTexturePixelsJob();
            _texture.Apply(false);
            Dirty = true;
        }

        public void Clear()
        {
            new ClearMapJob
            {
                pixels = _pixels,
                cells = _cells,
                width = m_width
            }.Schedule(_cells.Length, 64).Complete();

            _texture.SetPixelData(_pixels, 0);
            _texture.Apply(false);
            Dirty = true;
        }

        public void ApplySandDots(float density, byte strength, int seed)
        {
            if (density <= 0f || strength == 0)
                return;

            density = Mathf.Clamp01(density);
            NativeArray<Color32> texturePixels = _texture.GetPixelData<Color32>(0);
            new ApplySandDotsJob
            {
                texturePixels = texturePixels,
                pixels = _pixels,
                cells = _cells,
                width = m_width,
                density = density,
                strength = strength,
                seed = seed
            }.Schedule(texturePixels.Length, 64).Complete();

            _texture.Apply(false);
            Dirty = true;
        }

        private void ScheduleUpdateCellsFromTexturePixelsJob()
        {
            NativeArray<Color32> texturePixels = _texture.GetPixelData<Color32>(0);
            new UpdateCellsFromTexturePixelsJob
            {
                texturePixels = texturePixels,
                pixels = _pixels,
                cells = _cells,
                width = m_width
            }.Schedule(texturePixels.Length, 64).Complete();
        }

        public void Dispose()
        {
            if (_ownsTexture && _texture != null)
                Object.Destroy(_texture);
            if (_cells.IsCreated)
                _cells.Dispose();
            if (_pixels.IsCreated)
                _pixels.Dispose();
        }
    }

    public struct Cell
    {
        public int x, y;
        public byte hasValue;
        public byte isBorder;
        public Color32 color;
    }
}
