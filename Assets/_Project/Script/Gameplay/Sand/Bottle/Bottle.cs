using UnityEngine;

namespace Sand.Bottle
{
    public class Bottle : MonoBehaviour
    {
        private Color32 m_color;
        public Color32 Color => m_color;

        public void SetColor(Color color)
        {
            m_color = color;

            foreach (var spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                spriteRenderer.color = color;
            }

            foreach (var meshRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (meshRenderer is SpriteRenderer)
                {
                    continue;
                }

                meshRenderer.material.color = color;
            }
        }
    }
}
