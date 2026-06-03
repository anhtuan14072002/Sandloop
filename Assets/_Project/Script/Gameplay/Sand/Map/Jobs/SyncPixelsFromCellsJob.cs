using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Sand.Map.Jobs
{
    [BurstCompile]
    public struct SyncPixelsFromCellsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Cell> cells;
        public NativeArray<Color32> pixels;

        public void Execute(int index)
        {
            Cell cell = cells[index];
            pixels[index] = cell.hasValue == 1 ? cell.color : new Color32(0, 0, 0, 0);
        }
    }
}
