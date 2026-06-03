using System.Collections.Generic;
using UnityEngine;

namespace Sand.Map
{
    public class InitMap : MonoBehaviour
    {
        [SerializeField] private Texture2D m_sourceTexture;
        [SerializeField] private MeshRenderer targetMesh;
        [SerializeField, Range(1, 4)] private int mapScale = 2;
        [SerializeField, Range(0f, 1f)] private float sandDotDensity = 0.18f;
        [SerializeField, Range(0, 80)] private int sandDotStrength = 22;
        [SerializeField] private int sandDotSeed = 12345;
        
        private RenderMap _renderMap;
        public Map Map => _renderMap?.Map;

        private void Awake()
        {
            _renderMap = new RenderMap();
            BuildMap();
        }
        
        public void BuildMap()
        {
            if (m_sourceTexture == null) return;

            EnsureRenderMap();
            _renderMap.ClearMap();
            ConfigureTextureForSharpSampling(m_sourceTexture);
            _renderMap.BuildMap(m_sourceTexture, mapScale, sandDotDensity, (byte)sandDotStrength, sandDotSeed);
            BindMapTexture();
        }

        public void BuildMap(Texture2D texture)
        {
            if (texture == null) return;

            EnsureRenderMap();
            m_sourceTexture = texture;
            _renderMap.ClearMap();
            ConfigureTextureForSharpSampling(texture);
            _renderMap.BuildMap(texture, mapScale, sandDotDensity, (byte)sandDotStrength, sandDotSeed);
            BindMapTexture();
        }
       
        private void BindMapTexture()
        {
            Texture2D texture = _renderMap?.Map?.Texture;
            if (texture == null || targetMesh == null)
                return;

            ConfigureTextureForSharpSampling(texture);
            targetMesh.material.mainTexture = texture;
        }

        private static void ConfigureTextureForSharpSampling(Texture2D texture)
        {
            if (texture == null)
                return;

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 0;
        }

        private void EnsureRenderMap()
        {
            _renderMap ??= new RenderMap();
        }

        public void ClearMap()
        {
            if (_renderMap == null)
                return;

            _renderMap?.ClearMap();
            BindMapTexture();
        }

        public bool TickSand(int iterations)
        {
            return _renderMap?.Map?.TickSand(iterations) == true;
        }

        public bool AbsorbMatchingColorAtWorldPosition(
            Vector3 worldPosition,
            Color32 color,
            int radius,
            byte tolerance)
        {
            if (!TryWorldToMapPosition(worldPosition, out int x, out int y))
            {
                return false;
            }

            return _renderMap?.Map?.AbsorbMatchingColor(x, y, radius, color, tolerance) == true;
        }

        public bool AbsorbMatchingColorAboveWorldPosition(
            Vector3 worldPosition,
            Color32 color,
            int radius,
            byte tolerance,
            int maxCells,
            int maxHeightFromBottom,
            int sandTickIterations,
            List<Vector3> absorbedWorldPositions = null,
            float maxWorldDistanceToMap = 0f)
        {
            if (maxWorldDistanceToMap > 0f && !IsInsideMapAbsorbArea(worldPosition, maxWorldDistanceToMap))
            {
                return false;
            }

            if (!TryWorldToMapX(worldPosition, out int x))
            {
                return false;
            }

            List<Vector2Int> absorbedCells = absorbedWorldPositions != null ? new List<Vector2Int>() : null;
            bool absorbed = _renderMap?.Map?.AbsorbMatchingColorAbove(
                x,
                radius,
                color,
                tolerance,
                maxCells,
                maxHeightFromBottom,
                sandTickIterations,
                absorbedCells) == true;

            if (absorbed && absorbedCells != null)
            {
                absorbedWorldPositions.Clear();

                foreach (var cell in absorbedCells)
                {
                    if (TryMapToWorldPosition(cell.x, cell.y, out Vector3 sourcePosition))
                    {
                        absorbedWorldPositions.Add(sourcePosition);
                    }
                }
            }

            return absorbed;
        }

        public bool TryGetMapAbsorbArea(
            Vector3 worldPosition,
            float maxWorldDistance,
            out Vector3 closestWorldPosition,
            out bool insideArea)
        {
            closestWorldPosition = default;
            insideArea = false;

            MeshFilter meshFilter = targetMesh != null ? targetMesh.GetComponent<MeshFilter>() : null;

            if (targetMesh == null || meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            Vector3 localPosition = targetMesh.transform.InverseTransformPoint(worldPosition);
            Bounds localBounds = meshFilter.sharedMesh.bounds;
            int verticalAxis = localBounds.size.y >= localBounds.size.z ? 1 : 2;
            int depthAxis = verticalAxis == 1 ? 2 : 1;

            float localX = localPosition.x;
            float localVertical = verticalAxis == 1 ? localPosition.y : localPosition.z;
            float localDepth = depthAxis == 1 ? localPosition.y : localPosition.z;
            float verticalMin = verticalAxis == 1 ? localBounds.min.y : localBounds.min.z;
            float verticalMax = verticalAxis == 1 ? localBounds.max.y : localBounds.max.z;
            float depthMin = depthAxis == 1 ? localBounds.min.y : localBounds.min.z;
            float depthMax = depthAxis == 1 ? localBounds.max.y : localBounds.max.z;
            float clampedX = Mathf.Clamp(localX, localBounds.min.x, localBounds.max.x);
            float clampedVertical = Mathf.Clamp(localVertical, verticalMin, verticalMax);
            float clampedDepth = Mathf.Clamp(localDepth, depthMin, depthMax);
            Vector3 closestLocalPosition = localPosition;

            closestLocalPosition.x = clampedX;

            if (verticalAxis == 1)
            {
                closestLocalPosition.y = clampedVertical;
            }
            else
            {
                closestLocalPosition.z = clampedVertical;
            }

            if (depthAxis == 1)
            {
                closestLocalPosition.y = clampedDepth;
            }
            else
            {
                closestLocalPosition.z = clampedDepth;
            }

            bool insideX = localX >= localBounds.min.x && localX <= localBounds.max.x;
            bool insideVertical = localVertical >= verticalMin && localVertical <= verticalMax;
            bool insideDepth = localDepth >= depthMin && localDepth <= depthMax;
            float distanceToClosest = Vector3.Distance(localPosition, closestLocalPosition);

            closestWorldPosition = targetMesh.transform.TransformPoint(closestLocalPosition);
            insideArea = insideX && insideVertical && insideDepth && distanceToClosest <= maxWorldDistance;
            return true;
        }

        private bool IsInsideMapAbsorbArea(Vector3 worldPosition, float maxWorldDistance)
        {
            return TryGetMapAbsorbArea(worldPosition, maxWorldDistance, out _, out bool insideArea) && insideArea;
        }

        private bool TryMapToWorldPosition(int x, int y, out Vector3 worldPosition)
        {
            worldPosition = default;

            Map map = _renderMap?.Map;
            MeshFilter meshFilter = targetMesh != null ? targetMesh.GetComponent<MeshFilter>() : null;

            if (map == null || targetMesh == null || meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            Bounds localBounds = meshFilter.sharedMesh.bounds;
            int verticalAxis = localBounds.size.y >= localBounds.size.z ? 1 : 2;
            float normalizedX = map.Width <= 1 ? 0f : x / (float)(map.Width - 1);
            float normalizedY = map.Height <= 1 ? 0f : y / (float)(map.Height - 1);
            Vector3 localPosition = localBounds.center;

            localPosition.x = Mathf.Lerp(localBounds.min.x, localBounds.max.x, normalizedX);

            if (verticalAxis == 1)
            {
                localPosition.y = Mathf.Lerp(localBounds.min.y, localBounds.max.y, normalizedY);
            }
            else
            {
                localPosition.z = Mathf.Lerp(localBounds.min.z, localBounds.max.z, normalizedY);
            }

            worldPosition = targetMesh.transform.TransformPoint(localPosition);
            return true;
        }

        private bool TryWorldToMapX(Vector3 worldPosition, out int x)
        {
            x = 0;

            Map map = _renderMap?.Map;
            MeshFilter meshFilter = targetMesh != null ? targetMesh.GetComponent<MeshFilter>() : null;

            if (map == null || targetMesh == null || meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            Vector3 localPosition = targetMesh.transform.InverseTransformPoint(worldPosition);
            Bounds localBounds = meshFilter.sharedMesh.bounds;

            if (localBounds.size.x <= 0f)
            {
                return false;
            }

            float normalizedX = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localPosition.x);

            if (normalizedX < 0f || normalizedX > 1f)
            {
                return false;
            }

            x = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (map.Width - 1)), 0, map.Width - 1);
            return true;
        }

        private bool TryWorldToMapPosition(Vector3 worldPosition, out int x, out int y)
        {
            x = 0;
            y = 0;

            Map map = _renderMap?.Map;
            MeshFilter meshFilter = targetMesh != null ? targetMesh.GetComponent<MeshFilter>() : null;

            if (map == null || targetMesh == null || meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            Vector3 localPosition = targetMesh.transform.InverseTransformPoint(worldPosition);
            Bounds localBounds = meshFilter.sharedMesh.bounds;
            int verticalAxis = localBounds.size.y >= localBounds.size.z ? 1 : 2;
            float localVerticalPosition = verticalAxis == 1 ? localPosition.y : localPosition.z;
            float verticalMin = verticalAxis == 1 ? localBounds.min.y : localBounds.min.z;
            float verticalSize = verticalAxis == 1 ? localBounds.size.y : localBounds.size.z;

            if (localBounds.size.x <= 0f || verticalSize <= 0f)
            {
                return false;
            }

            float normalizedX = Mathf.InverseLerp(localBounds.min.x, localBounds.max.x, localPosition.x);
            float normalizedY = Mathf.InverseLerp(verticalMin, verticalMin + verticalSize, localVerticalPosition);

            if (normalizedX < 0f || normalizedX > 1f || normalizedY < 0f || normalizedY > 1f)
            {
                return false;
            }

            x = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (map.Width - 1)), 0, map.Width - 1);
            y = Mathf.Clamp(Mathf.RoundToInt(normalizedY * (map.Height - 1)), 0, map.Height - 1);
            return true;
        }

        private void OnDestroy()
        {
            _renderMap?.Dispose();
            _renderMap = null;
        }
    }
}
