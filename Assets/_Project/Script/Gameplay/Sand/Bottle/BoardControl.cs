using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sand.Map;
using UnityEngine;

namespace Sand.Bottle
{
    public class BoardControl : MonoBehaviour
    {
        [SerializeField] private Transform m_boardA;
        [SerializeField] private Transform m_boardB;

        [SerializeField] private float m_moveSpeed = 1f;
        [SerializeField] private float m_disableDuration = 0.1f;
        [SerializeField] private InitMap m_initMap;
        [SerializeField] private int m_absorbRadius = 3;
        [SerializeField] private int m_absorbCellsPerFrame = 8;
        [SerializeField] private int m_absorbHeightFromBottom = 64;
        [SerializeField] private int m_sandTickIterationsAfterAbsorb = 0;
        [SerializeField] private int m_sandTickIterationsPerFrame = 2;
        [SerializeField] private byte m_colorTolerance = 8;
        [SerializeField] private float m_maxAbsorbDistanceToMap = 0.25f;
        [SerializeField] private bool m_enableAbsorbVfx = true;
        [SerializeField] private int m_vfxParticlesPerAbsorb = 3;
        [SerializeField] private float m_vfxStartHeight = 0.75f;
        [SerializeField] private float m_vfxStartRadius = 0.25f;
        [SerializeField] private float m_vfxDuration = 0.25f;
        [SerializeField] private float m_vfxDurationRandom = 0.35f;
        [SerializeField] private float m_vfxDestinationRadius = 0.12f;
        [SerializeField] private float m_vfxParticleScale = 0.04f;
        [SerializeField] private float m_vfxParticleScaleRandom = 0.08f;
        [SerializeField] private GameObject m_vfxPrefab;

        [SerializeField] private List<Bottle> m_bottles = new();

        private readonly Dictionary<Bottle, CancellationTokenSource> m_runningBottles = new();
        private readonly List<Vector3> m_absorbedWorldPositions = new();

        private void Awake()
        {
            if (m_initMap == null)
            {
                m_initMap = GetComponent<InitMap>() ?? GetComponentInParent<InitMap>();
            }
        }

        private void Update()
        {
            StartAllBottles();
        }

        private void OnDisable()
        {
            var runningTokenSources = new List<CancellationTokenSource>(m_runningBottles.Values);

            foreach (var runningTokenSource in runningTokenSources)
            {
                runningTokenSource.Cancel();
            }

            m_runningBottles.Clear();
        }

        private void StartAllBottles()
        {
            if (m_boardA == null || m_boardB == null)
            {
                return;
            }

            foreach (var bottle in m_bottles)
            {
                StartBottle(bottle);
            }
        }

        public void AddBottle(Bottle bottle)
        {
            if (bottle == null || m_boardA == null || m_boardB == null)
            {
                return;
            }

            if (!m_bottles.Contains(bottle))
            {
                m_bottles.Add(bottle);
            }

            StartBottle(bottle);
        }

        private void StartBottle(Bottle bottle)
        {
            if (bottle == null || m_runningBottles.ContainsKey(bottle))
            {
                return;
            }

            var destroyToken = this.GetCancellationTokenOnDestroy();
            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
            m_runningBottles.Add(bottle, cancellationTokenSource);

            MoveBottleLoopAsync(bottle, cancellationTokenSource, cancellationTokenSource.Token).Forget();
        }

        private async UniTaskVoid MoveBottleLoopAsync(Bottle bottle,
            CancellationTokenSource cancellationTokenSource,
            CancellationToken cancellationToken)
        {
            try
            {
                var bottleTransform = bottle.transform;

                while (bottle != null && !cancellationToken.IsCancellationRequested)
                {
                    await MoveToAsync(bottle, m_boardB.position, cancellationToken);

                    if (bottle == null || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    bottle.gameObject.SetActive(false);
                    bottleTransform.position = m_boardA.position;

                    if (m_disableDuration > 0f)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(m_disableDuration), cancellationToken: cancellationToken);
                    }

                    if (bottle == null || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    bottle.gameObject.SetActive(true);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (bottle != null && m_runningBottles.TryGetValue(bottle, out var currentTokenSource) &&
                    currentTokenSource == cancellationTokenSource)
                {
                    m_runningBottles.Remove(bottle);
                }

                cancellationTokenSource.Dispose();
            }
        }

        private async UniTask MoveToAsync(Bottle bottle, Vector3 position, CancellationToken cancellationToken)
        {
            Transform target = bottle != null ? bottle.transform : null;
            var moveSpeed = Mathf.Max(0.01f, m_moveSpeed);

            while (target != null && Vector3.Distance(target.position, position) > 0.01f)
            {
                cancellationToken.ThrowIfCancellationRequested();
                target.position = Vector3.MoveTowards(target.position, position, moveSpeed * Time.deltaTime);
                AbsorbSand(bottle, cancellationToken);
                await UniTask.Yield(cancellationToken);
            }

            if (target != null)
            {
                target.position = position;
                AbsorbSand(bottle, cancellationToken);
            }
        }

        private void AbsorbSand(Bottle bottle, CancellationToken cancellationToken)
        {
            if (bottle == null || m_initMap == null)
            {
                return;
            }

            bool absorbed = m_initMap.AbsorbMatchingColorAboveWorldPosition(
                bottle.transform.position,
                bottle.Color,
                m_absorbRadius,
                m_colorTolerance,
                m_absorbCellsPerFrame,
                m_absorbHeightFromBottom,
                m_sandTickIterationsAfterAbsorb,
                m_absorbedWorldPositions,
                m_maxAbsorbDistanceToMap);

            if (absorbed && m_enableAbsorbVfx)
            {
                PlayAbsorbVfxAsync(bottle, new List<Vector3>(m_absorbedWorldPositions), cancellationToken).Forget();
            }

            if (m_sandTickIterationsPerFrame > 0)
            {
                m_initMap.TickSand(m_sandTickIterationsPerFrame);
            }
        }

        private async UniTaskVoid PlayAbsorbVfxAsync(
            Bottle bottle,
            IReadOnlyList<Vector3> sourcePositions,
            CancellationToken cancellationToken)
        {
            try
            {
                int particleCount = Mathf.Min(sourcePositions.Count, Mathf.Max(1, m_vfxParticlesPerAbsorb));

                for (int i = 0; i < particleCount; i++)
                {
                    if (bottle == null || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    FlySandParticleAsync(bottle, sourcePositions[i], cancellationToken).Forget();
                    await UniTask.Yield(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTaskVoid FlySandParticleAsync(
            Bottle bottle,
            Vector3 sourcePosition,
            CancellationToken cancellationToken)
        {
            GameObject particle = m_vfxPrefab != null
                ? Instantiate(m_vfxPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            particle.name = "Sand Absorb VFX";

            foreach (var particleCollider in particle.GetComponentsInChildren<Collider>(true))
            {
                Destroy(particleCollider);
            }

            foreach (var particleRenderer in particle.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in particleRenderer.materials)
                {
                    material.color = bottle.Color;
                }
            }

            Vector3 startPosition = sourcePosition;

            particle.transform.position = startPosition;
            particle.transform.rotation = UnityEngine.Random.rotation;
            float startScale = Mathf.Max(0.001f, m_vfxParticleScale + UnityEngine.Random.Range(0f, m_vfxParticleScaleRandom));
            particle.transform.localScale = Vector3.one * startScale;

            try
            {
                float elapsed = 0f;
                float duration = Mathf.Max(0.01f, m_vfxDuration + UnityEngine.Random.Range(0f, m_vfxDurationRandom));
                Vector3 destinationOffset = new Vector3(
                    UnityEngine.Random.Range(-m_vfxDestinationRadius, m_vfxDestinationRadius),
                    UnityEngine.Random.Range(0f, m_vfxDestinationRadius),
                    UnityEngine.Random.Range(-m_vfxDestinationRadius, m_vfxDestinationRadius));
                Vector3 controlPoint = startPosition + Vector3.up * UnityEngine.Random.Range(0.1f, 0.35f);

                while (elapsed < duration)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (bottle == null)
                    {
                        return;
                    }

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float easedT = 1f - (1f - t) * (1f - t);
                    Vector3 destination = bottle.transform.position + destinationOffset;
                    Vector3 a = Vector3.Lerp(startPosition, controlPoint, easedT);
                    Vector3 b = Vector3.Lerp(controlPoint, destination, easedT);
                    particle.transform.position = Vector3.Lerp(a, b, easedT);
                    particle.transform.Rotate(
                        UnityEngine.Random.Range(120f, 360f) * Time.deltaTime,
                        UnityEngine.Random.Range(120f, 360f) * Time.deltaTime,
                        UnityEngine.Random.Range(120f, 360f) * Time.deltaTime,
                        Space.Self);
                    particle.transform.localScale = Vector3.one * Mathf.Lerp(startScale, 0.001f, t);
                    await UniTask.Yield(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (particle != null)
                {
                    foreach (var particleRenderer in particle.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var material in particleRenderer.materials)
                        {
                            Destroy(material);
                        }
                    }

                    Destroy(particle);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (m_initMap == null)
            {
                m_initMap = GetComponent<InitMap>() ?? GetComponentInParent<InitMap>();
            }

            if (m_initMap == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, m_maxAbsorbDistanceToMap));

            foreach (var bottle in m_bottles)
            {
                if (bottle == null)
                {
                    continue;
                }

                if (!m_initMap.TryGetMapAbsorbArea(
                        bottle.transform.position,
                        m_maxAbsorbDistanceToMap,
                        out Vector3 closestWorldPosition,
                        out bool insideArea))
                {
                    continue;
                }

                Gizmos.color = insideArea ? Color.green : Color.red;
                Gizmos.DrawSphere(bottle.transform.position, 0.05f);
                Gizmos.DrawWireSphere(closestWorldPosition, Mathf.Max(0.01f, m_maxAbsorbDistanceToMap));
                Gizmos.DrawLine(bottle.transform.position, closestWorldPosition);
            }
        }
    }
}
