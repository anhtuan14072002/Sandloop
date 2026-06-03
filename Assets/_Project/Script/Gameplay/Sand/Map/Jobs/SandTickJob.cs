using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Sand.Map.Jobs
{
    [BurstCompile]
    public struct SandTickJob : IJob
    {
        public int width;
        public int height;
        public int iterations;

        public NativeArray<Cell> cells;

        public NativeArray<byte> movedOut;
        public NativeArray<byte> collidedOut;
        public NativeArray<byte> blockedOut;

        public void Execute()
        {
            bool moved = false;
            bool collided = false;
            bool blocked = false;

            for (int it = 0; it < iterations; it++)
            {
                bool movedThisIter = false;

                for (int y = 1; y < height; y++)
                {
                    if ((y & 1) == 0)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (ProcessCell(x, y, ref collided, ref blocked)) movedThisIter = true;
                        }
                    }
                    else
                    {
                        for (int x = width - 1; x >= 0; x--)
                        {
                            if (ProcessCell(x, y, ref collided, ref blocked)) movedThisIter = true;
                        }
                    }
                }

                if (movedThisIter) moved = true;
                else break;
            }

            movedOut[0] = (byte)(moved ? 1 : 0);
            collidedOut[0] = (byte)(collided ? 1 : 0);
            blockedOut[0] = (byte)(blocked ? 1 : 0);
        }

        private bool ProcessCell(int x, int y, ref bool collided, ref bool blocked)
        {
            int idx = Idx(x, y);
            Cell c = cells[idx];
            if (c.hasValue != 1 || c.isBorder == 1) return false;

            if (!In(x, y - 1)) return false;
            Cell below = cells[Idx(x, y - 1)];

            if (below.hasValue == 0 && below.isBorder == 0)
            {
                Swap(x, y, x, y - 1);
                return true;
            }
            if (below.hasValue == 1)
            {
                bool canMoveLeft = CanMove(x - 1, y - 1);
                bool canMoveRight = CanMove(x + 1, y - 1);
                
                if (canMoveLeft)
                {
                    collided = true;
                    Swap(x, y, x - 1, y - 1);
                    return true;
                    /*bool hasLeftNeighbor = In(x - 1, y) && cells[Idx(x - 1, y)].hasValue == 1;
                    if (!hasLeftNeighbor)
                    {
                        collided = true;
                        Swap(x, y, x - 1, y - 1);
                        return true;
                    }*/
                }

                if (canMoveRight)
                {
                    collided = true;
                    Swap(x, y, x + 1, y - 1);
                    return true;
                    /*bool hasRightNeighbor = In(x + 1, y) && cells[Idx(x + 1, y)].hasValue == 1;
                    if (!hasRightNeighbor)
                    {
                        collided = true;
                        Swap(x, y, x + 1, y - 1);
                        return true;
                    }*/
                }
                blocked = true;
            }

            return false;
        }

        private int Idx(int x, int y) => y * width + x;

        private bool In(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        private bool CanMove(int toX, int toY)
        {
            if (!In(toX, toY)) return false;
            var to = cells[Idx(toX, toY)];
            return to.hasValue == 0 && to.isBorder == 0;
        }

        private void Swap(int x1, int y1, int x2, int y2)
        {
            int i1 = Idx(x1, y1);
            int i2 = Idx(x2, y2);
            if (i1 == i2) return;

            Cell c1 = cells[i1];
            Cell c2 = cells[i2];

            var tmp = c1;

            c1.x = c2.x;
            c1.y = c2.y;
            c2.x = tmp.x;
            c2.y = tmp.y;

            cells[i1] = c2;
            cells[i2] = c1;
        }
    }
}