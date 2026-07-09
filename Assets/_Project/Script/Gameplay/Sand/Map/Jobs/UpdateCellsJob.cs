using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Sand.Map.Jobs
{
    [BurstCompile]
    public struct UpdateCellsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> texturePixels;
        public NativeArray<Color32> pixels;
        public NativeArray<Cell> cells;
        
        public int width;

        public void Execute(int index)
        {
            Color32 color = texturePixels[index];
            int x = index % width;
            int y = index / width;

            pixels[index] = color;
            cells[index] = new Cell
            {
                x = x,
                y = y,
                hasValue = color.a > 0 ? (byte)1 : (byte)0,
                isBorder = 0,
                color = color,
                baseColor = color
            };
        }
    }
}
