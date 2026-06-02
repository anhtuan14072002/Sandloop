using System;
using UnityEngine;

namespace Sand.Map
{
    public sealed class RenderMap : IDisposable
    {
        private Map _map;
        public Map Map => _map;
        private RenderTexture _renderTexture;

        public void BuildMap(Texture2D sourceTexture, int mapScale = 1, float sandDotDensity = 0f,
            byte sandDotStrength = 0, int sandDotSeed = 0)
        {
            if (sourceTexture == null) return;
            
            mapScale = Mathf.Max(1, mapScale);
            int width = sourceTexture.width * mapScale;
            int height = sourceTexture.height * mapScale;

            EnsureMap(width, height);
            EnsureRenderTexture(width, height);

            Graphics.Blit(sourceTexture, _renderTexture);
            _map.SetTexturePixelsFromRenderTarget(_renderTexture);
            _map.ApplySandDots(sandDotDensity, sandDotStrength, sandDotSeed);
        }

        public void ClearMap()
        {
            _map?.Clear();
        }

        private void EnsureMap(int width, int height)
        {
            if (_map != null && _map.Width == width && _map.Height == height)
                return;

            _map?.Dispose();
            _map = new Map(width, height);
        }

        private void EnsureRenderTexture(int width, int height)
        {
            if (_renderTexture != null && _renderTexture.width == width && _renderTexture.height == height)
                return;

            ReleaseRenderTexture();
            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            _renderTexture.Create();
        }

        private void ReleaseRenderTexture()
        {
            if (_renderTexture == null)
                return;

            _renderTexture.Release();
            UnityEngine.Object.Destroy(_renderTexture);
            _renderTexture = null;
        }

        public void Dispose()
        {
            _map?.Dispose();
            _map = null;
            ReleaseRenderTexture();
        }
    }
}
