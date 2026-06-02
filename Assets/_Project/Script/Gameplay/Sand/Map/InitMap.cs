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

        private void OnDestroy()
        {
            _renderMap?.Dispose();
            _renderMap = null;
        }
    }
}
