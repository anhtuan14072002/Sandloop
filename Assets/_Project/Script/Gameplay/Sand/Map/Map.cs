using System;
using System.Collections.Generic;
using Sand.Map.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

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

        public bool TryGetRandomTexturePixelColor(out Color32 color)
        {
            color = default;

            if (!_cells.IsCreated || _cells.Length == 0)
            {
                return false;
            }

            int startIndex = Random.Range(0, _cells.Length);

            for (int i = 0; i < _cells.Length; i++)
            {
                int index = (startIndex + i) % _cells.Length;
                Cell cell = _cells[index];

                if (cell.hasValue == 0 || cell.baseColor.a == 0)
                {
                    continue;
                }

                color = cell.baseColor;
                return true;
            }

            return false;
        }

        public bool AbsorbMatchingColor(int centerX, int centerY, int radius, Color32 targetColor, byte tolerance)
        {
            if (!_cells.IsCreated || !_pixels.IsCreated || _cells.Length == 0)
            {
                return false;
            }

            bool absorbed = false;
            int clampedRadius = Mathf.Max(0, radius);
            int minX = Mathf.Max(0, centerX - clampedRadius);
            int maxX = Mathf.Min(m_width - 1, centerX + clampedRadius);
            int minY = Mathf.Max(0, centerY - clampedRadius);
            int maxY = Mathf.Min(m_height - 1, centerY + clampedRadius);
            int radiusSqr = clampedRadius * clampedRadius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;

                    if (dx * dx + dy * dy > radiusSqr)
                    {
                        continue;
                    }

                    int index = Idx(x, y);
                    Cell cell = _cells[index];

                    if (cell.hasValue == 0 || !IsSameColor(cell.baseColor, targetColor, tolerance))
                    {
                        continue;
                    }

                    Color32 emptyColor = new Color32(0, 0, 0, 0);
                    cell.hasValue = 0;
                    cell.color = emptyColor;
                    cell.baseColor = emptyColor;
                    _cells[index] = cell;
                    _pixels[index] = emptyColor;
                    absorbed = true;
                }
            }

            if (!absorbed)
            {
                return false;
            }

            _texture.SetPixelData(_pixels, 0);
            _texture.Apply(false);
            Dirty = true;
            return true;
        }

        public bool AbsorbMatchingColorAbove(
            int centerX,
            int radius,
            Color32 targetColor,
            byte tolerance,
            int maxCells,
            int maxHeightFromBottom,
            int sandTickIterations,
            List<Vector2Int> absorbedCells = null)
        {
            if (!_cells.IsCreated || !_pixels.IsCreated || _cells.Length == 0 || maxCells <= 0)
            {
                return false;
            }

            bool absorbed = false;
            int absorbedCount = 0;
            int clampedRadius = Mathf.Max(0, radius);
            int minX = Mathf.Max(0, centerX - clampedRadius);
            int maxX = Mathf.Min(m_width - 1, centerX + clampedRadius);
            int heightLimit = maxHeightFromBottom <= 0 ? m_height : Mathf.Min(m_height, maxHeightFromBottom);

            for (int y = 0; y < heightLimit; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int distanceFromCenter = Mathf.Abs(x - centerX);

                    if (distanceFromCenter > clampedRadius + y)
                    {
                        continue;
                    }

                    int index = Idx(x, y);
                    Cell cell = _cells[index];

                    if (cell.hasValue == 0 || !IsSameColor(cell.baseColor, targetColor, tolerance))
                    {
                        continue;
                    }

                    Color32 emptyColor = new Color32(0, 0, 0, 0);
                    cell.hasValue = 0;
                    cell.color = emptyColor;
                    cell.baseColor = emptyColor;
                    _cells[index] = cell;
                    _pixels[index] = emptyColor;
                    absorbedCells?.Add(new Vector2Int(x, y));
                    absorbed = true;
                    absorbedCount++;

                    if (absorbedCount >= maxCells)
                    {
                        ApplyAbsorbResult(centerX, minX, maxX, heightLimit, sandTickIterations);
                        return true;
                    }
                }
            }

            if (absorbed)
            {
                ApplyAbsorbResult(centerX, minX, maxX, heightLimit, sandTickIterations);
            }

            return absorbed;
        }

        public bool TickSand(int iterations)
        {
            if (!_cells.IsCreated || !_pixels.IsCreated || _cells.Length == 0 || iterations <= 0)
            {
                return false;
            }

            NativeArray<byte> movedOut = new NativeArray<byte>(1, Allocator.TempJob);
            NativeArray<byte> collidedOut = new NativeArray<byte>(1, Allocator.TempJob);
            NativeArray<byte> blockedOut = new NativeArray<byte>(1, Allocator.TempJob);
            bool moved;

            try
            {
                new SandTickJob
                {
                    width = m_width,
                    height = m_height,
                    iterations = iterations,
                    cells = _cells,
                    movedOut = movedOut,
                    collidedOut = collidedOut,
                    blockedOut = blockedOut
                }.Schedule().Complete();

                moved = movedOut[0] == 1;
            }
            finally
            {
                movedOut.Dispose();
                collidedOut.Dispose();
                blockedOut.Dispose();
            }

            if (!moved)
            {
                return false;
            }

            ApplyCellsToTexture();
            return true;
        }

        private void ApplyAbsorbResult(
            int centerX,
            int voidMinX,
            int voidMaxX,
            int heightLimit,
            int sandTickIterations)
        {
            bool collapsed = CollapseIntoAbsorbedArea(centerX, voidMinX, voidMaxX, heightLimit);

            if (!collapsed && !TickSand(sandTickIterations))
            {
                ApplyCellsToTexture();
            }
        }

        private bool CollapseIntoAbsorbedArea(int centerX, int voidMinX, int voidMaxX, int heightLimit)
        {
            bool moved = false;
            int clampedHeight = Mathf.Clamp(heightLimit, 1, m_height);
            int clampedVoidMinX = Mathf.Clamp(voidMinX, 0, m_width);
            int clampedVoidMaxX = Mathf.Clamp(voidMaxX + 1, clampedVoidMinX + 1, m_width);
            int center = Mathf.Clamp(centerX, 0, m_width - 1);
            List<int> movementOrder = new List<int>(m_width * clampedHeight);

            FillCenterOutward(movementOrder, clampedVoidMinX, clampedVoidMaxX, 0, clampedHeight);
            moved |= Step(movementOrder, true, 2, 2);

            FillLeftToRight(movementOrder, 0, center, 0, clampedHeight / 2);
            moved |= Step(movementOrder, true, 3, 0);

            FillRightToLeft(movementOrder, 0, center, clampedHeight / 2, clampedHeight);
            moved |= Step(movementOrder, false, 3, 0);
            moved |= Step(movementOrder, true, 3, 0);

            FillRightToLeft(movementOrder, center, m_width, 0, clampedHeight / 2);
            moved |= Step(movementOrder, true, 3, 1);

            FillLeftToRight(movementOrder, 0, m_width, clampedHeight / 2, clampedHeight);
            moved |= Step(movementOrder, false, 3, 1);
            moved |= Step(movementOrder, true, 3, 1);

            if (moved)
            {
                ApplyCellsToTexture();
            }

            return moved;
        }

        private void FillCenterOutward(List<int> movementOrder, int minX, int maxX, int minY, int maxY)
        {
            movementOrder.Clear();
            int center = (minX + maxX) / 2;

            for (int y = minY; y < maxY; y++)
            {
                int left = center - 1;
                int right = center;

                while (left >= minX || right < maxX)
                {
                    if (left >= minX)
                    {
                        movementOrder.Add(Idx(left, y));
                        left--;
                    }

                    if (right < maxX)
                    {
                        movementOrder.Add(Idx(right, y));
                        right++;
                    }
                }
            }
        }

        private void FillLeftToRight(List<int> movementOrder, int minX, int maxX, int minY, int maxY)
        {
            movementOrder.Clear();

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    movementOrder.Add(Idx(x, y));
                }
            }
        }

        private void FillRightToLeft(List<int> movementOrder, int minX, int maxX, int minY, int maxY)
        {
            movementOrder.Clear();

            for (int y = minY; y < maxY; y++)
            {
                for (int x = maxX - 1; x >= minX; x--)
                {
                    movementOrder.Add(Idx(x, y));
                }
            }
        }

        private bool Step(List<int> movementOrder, bool diagonalAllowed, int stepCount, int direction)
        {
            bool moved = false;
            int center = m_width / 2;

            for (int i = 0; i < movementOrder.Count; i++)
            {
                int index = movementOrder[i];
                int x = index % m_width;
                int y = index / m_width;
                Cell cell = _cells[index];

                if (cell.hasValue == 0 || cell.isBorder == 1)
                {
                    continue;
                }

                int dx = 0;
                int dy = 0;

                if (CanMoveTo(x, y - 1))
                {
                    dy = -1;
                }
                else if (diagonalAllowed)
                {
                    int resolvedDirection = direction;

                    if (resolvedDirection == 2)
                    {
                        resolvedDirection = x < center ? 1 : 0;
                    }
                    else if (resolvedDirection == 3)
                    {
                        resolvedDirection = x < center ? 0 : 1;
                    }

                    if (resolvedDirection == 0)
                    {
                        if (CanMoveTo(x - 1, y - 1))
                        {
                            dx = -1;
                            dy = -1;
                        }
                        else if (CanMoveTo(x + 1, y - 1))
                        {
                            dx = 1;
                            dy = -1;
                        }
                    }
                    else if (resolvedDirection == 1)
                    {
                        if (CanMoveTo(x + 1, y - 1))
                        {
                            dx = 1;
                            dy = -1;
                        }
                        else if (CanMoveTo(x - 1, y - 1))
                        {
                            dx = -1;
                            dy = -1;
                        }
                    }
                }

                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                int targetX = x;
                int targetY = y;

                for (int step = 1; step <= stepCount; step++)
                {
                    int nextX = x + dx * step;
                    int nextY = y + dy * step;

                    if (!CanMoveTo(nextX, nextY))
                    {
                        break;
                    }

                    targetX = nextX;
                    targetY = nextY;
                }

                if (targetX == x && targetY == y)
                {
                    continue;
                }

                SwapCells(x, y, targetX, targetY);
                moved = true;
            }

            return moved;
        }

        private bool CanMoveTo(int x, int y)
        {
            if (x < 0 || y < 0 || x >= m_width || y >= m_height)
            {
                return false;
            }

            Cell target = _cells[Idx(x, y)];
            return target.hasValue == 0 && target.isBorder == 0;
        }

        private void SwapCells(int x1, int y1, int x2, int y2)
        {
            int index1 = Idx(x1, y1);
            int index2 = Idx(x2, y2);
            Cell cell1 = _cells[index1];
            Cell cell2 = _cells[index2];

            cell1.x = x2;
            cell1.y = y2;
            cell2.x = x1;
            cell2.y = y1;

            _cells[index1] = cell2;
            _cells[index2] = cell1;
        }

        private void ApplyCellsToTexture()
        {
            new SyncPixelsFromCellsJob
            {
                cells = _cells,
                pixels = _pixels
            }.Schedule(_cells.Length, 64).Complete();

            _texture.SetPixelData(_pixels, 0);
            _texture.Apply(false);
            Dirty = true;
        }

        private static bool IsSameColor(Color32 a, Color32 b, byte tolerance)
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance &&
                   a.a > 0;
        }

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
            new UpdateCellsJob
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
        public Color32 baseColor;
    }
}



