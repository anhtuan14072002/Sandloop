using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Sand.Map.Jobs
{
    [BurstCompile]
    public struct ClearMapJob : IJobParallelFor
    {
        public NativeArray<Color32> pixels;
        public NativeArray<Cell> cells;
        public int width;

        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;
            Color32 color = new Color32(0, 0, 0, 0);

            pixels[index] = color;
            cells[index] = new Cell
            {
                x = x,
                y = y,
                hasValue = 0,
                isBorder = 0,
                color = color
            };
        }
    }
}
