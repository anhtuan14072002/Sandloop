using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Sand.Map.Jobs
{
    [BurstCompile]
    public struct ApplySandDotsJob : IJobParallelFor
    {
        public NativeArray<Color32> texturePixels;
        public NativeArray<Color32> pixels;
        public NativeArray<Cell> cells;
        public int width;
        public float density;
        public byte strength;
        public int seed;

        public void Execute(int index)
        {
            Cell cell = cells[index];
            if (cell.hasValue == 0)
                return;

            int x = index % width;
            int y = index / width;
            uint hash = HashPixel(x, y, seed);
            float value = (hash & 0xffff) / 65535f;
            if (value > density)
                return;

            int sign = ((hash >> 16) & 1) == 0 ? -1 : 1;
            int amount = strength * sign;
            Color32 color = texturePixels[index];
            color.r = ClampByte(color.r + amount);
            color.g = ClampByte(color.g + amount);
            color.b = ClampByte(color.b + amount);

            texturePixels[index] = color;
            pixels[index] = color;
            cell.color = color;
            cells[index] = cell;
        }

        private static uint HashPixel(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        private static byte ClampByte(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (byte)value;
        }
    }
}
