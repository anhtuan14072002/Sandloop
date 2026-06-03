using Sand.Map;
using UnityEngine;

namespace Sand.Bottle
{
    public class SpawnBottle : MonoBehaviour
    {
        [SerializeField] private GameObject _bottle;
        [SerializeField] private Transform _bottleSpawn;
        [SerializeField] private BoardControl _boardControl;
        
        [SerializeField] private InitMap _initMap;

        private void Awake()
        {
            if (_boardControl == null)
            {
                _boardControl = GetComponent<BoardControl>() ?? GetComponentInParent<BoardControl>();
            }

            if (_initMap == null)
            {
                _initMap = GetComponent<InitMap>() ?? GetComponentInParent<InitMap>();
            }
        }
        
        public void InstantiateBottle()
        {
            if (_bottle == null) return;

            var spawnPosition = _bottleSpawn != null ? _bottleSpawn.position : transform.position;
            var bottleObject = Instantiate(_bottle, spawnPosition, Quaternion.identity);
            var bottle = bottleObject.GetComponent<Bottle>();

            if (bottle == null)
                bottle = bottleObject.AddComponent<Bottle>();

            if (_initMap != null && _initMap.Map != null &&
                _initMap.Map.TryGetRandomTexturePixelColor(out Color32 bottleColor))
            {
                bottle.SetColor(bottleColor);
            }

            _boardControl?.AddBottle(bottle);
        }
    }
}
