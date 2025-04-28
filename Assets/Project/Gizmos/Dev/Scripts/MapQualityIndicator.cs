namespace Gizmos
{
    using System.Collections.Generic;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.XR.ARSubsystems;

    public class MapQualityIndicator : MonoBehaviour
    {
        public GameObject MapQualityBarPrefab;

        public CircleRenderer CircleRenderer;

        [Range(0, 360)]
        public float Range = 150.0f;

        public float Radius = 0.1f;

        private const float _verticalRange = 150.0f;

        private const float _horizontalRange = 180.0f;

        private const float _qualityThreshold = 0.6f;

        private const float _topviewThreshold = 15.0f;

        private const float _disappearDuration = 0.5f;
        private const float _fadingDuration = 3.0f;
        private const float _barSpacing = 7.5f;
        private const float _circleFadingRange = 10.0f;

        private int _currentQualityState = 0;
        private Camera _mainCamera = null;
        private Vector3 _centerDir;
        private float _fadingTimer = -1.0f;
        private float _disappearTimer = -1.0f;
        private List<MapQualityBar> _mapQualityBars = new List<MapQualityBar>();

        public bool ReachQualityThreshold
        {
            get
            {
                float currentQuality = 0.0f;
                foreach (var bar in _mapQualityBars)
                {
                    currentQuality += bar.Weight;
                }

                return (currentQuality / _mapQualityBars.Count) >= _qualityThreshold;
            }
        }

        public bool ReachTopviewAngle
        {
            get
            {
                var cameraDir = _mainCamera.transform.position - transform.position;
                return Vector3.Angle(cameraDir, Vector3.up) < _topviewThreshold;
            }
        }

        public void UpdateQualityState(int quality)
        {
            _currentQualityState = quality;
        }

        public void DrawIndicator(PlaneAlignment planeAlignment, Camera camera)
        {
            Range = planeAlignment == PlaneAlignment.Vertical ?
                _verticalRange : _horizontalRange;

            _mainCamera = camera;

            _centerDir = planeAlignment == PlaneAlignment.Vertical ?
                transform.TransformVector(Vector3.up) :
                transform.TransformVector(-Vector3.forward);

            DrawBars();
            DrawRing();

            gameObject.SetActive(true);
        }

        public void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Update()
        {
            if (ReachTopviewAngle)
            {
                if (_fadingTimer >= _fadingDuration)
                {
                    return;
                }

                if (_fadingTimer < 0)
                {
                    _fadingTimer = 0.0f;
                }

                _fadingTimer += Time.deltaTime;
                float alpha = Mathf.Clamp(1 - (_fadingTimer / _fadingDuration), 0, 1);
                CircleRenderer.SetAlpha(alpha);
                foreach (var bar in _mapQualityBars)
                {
                    bar.SetAlpha(alpha);
                }

                return;
            }
            else if (_fadingTimer > 0)
            {
                _fadingTimer -= Time.deltaTime;
                float alpha = Mathf.Clamp(1 - (_fadingTimer / _fadingDuration), 0, 1);
                CircleRenderer.SetAlpha(alpha);
                foreach (var bar in _mapQualityBars)
                {
                    bar.SetAlpha(alpha);
                }
            }

            foreach (MapQualityBar bar in _mapQualityBars)
            {
                if (IsLookingAtBar(bar))
                {
                    bar.IsVisited = true;
                    bar.QualityState = _currentQualityState;
                }
            }

            PlayDisappearAnimation();
        }

        private void DrawRing()
        {
            CircleRenderer.DrawArc(_centerDir, Radius, Range + (2 * _circleFadingRange));
        }

        private void DrawBars()
        {
            Vector3 basePos = transform.position;
            Vector3 position = _centerDir * Radius;
            var rotation = Quaternion.AngleAxis(0, Vector3.up);
            var qualityBar =
                Instantiate(MapQualityBarPrefab, basePos + position, rotation, transform);
            _mapQualityBars.Add(qualityBar.GetComponent<MapQualityBar>());

            for (float deltaAngle = _barSpacing; deltaAngle < Range / 2;
                deltaAngle += _barSpacing)
            {
                rotation = Quaternion.AngleAxis(deltaAngle, Vector3.up);
                position = (rotation * _centerDir) * Radius;
                qualityBar =
                    Instantiate(MapQualityBarPrefab, basePos + position, rotation, transform);
                _mapQualityBars.Add(qualityBar.GetComponent<MapQualityBar>());

                rotation = Quaternion.AngleAxis(-deltaAngle, Vector3.up);
                position = (rotation * _centerDir) * Radius;
                qualityBar =
                    Instantiate(MapQualityBarPrefab, basePos + position, rotation, transform);
                _mapQualityBars.Add(qualityBar.GetComponent<MapQualityBar>());
            }
        }

        private bool IsLookingAtBar(MapQualityBar bar)
        {
            var screenPoint = _mainCamera.WorldToViewportPoint(bar.transform.position);
            if (screenPoint.z <= 0 || screenPoint.x <= 0 || screenPoint.x >= 1 ||
                screenPoint.y <= 0 || screenPoint.y >= 1)
            {
                return false;
            }

            float distance = (transform.position - _mainCamera.transform.position).magnitude;
            if (distance <= Radius)
            {
                return false;
            }

            var cameraDir = Vector3.ProjectOnPlane(
                _mainCamera.transform.position - transform.position, Vector3.up);
            var barDir = Vector3.ProjectOnPlane(
                bar.transform.position - transform.position, Vector3.up);
            return Vector3.Angle(cameraDir, barDir) < _barSpacing;
        }

        private void PlayDisappearAnimation()
        {
            if (_disappearTimer < 0.0f && ReachQualityThreshold)
            {
                _disappearTimer = 0.0f;
            }

            if (_disappearTimer >= 0.0f && _disappearTimer < _disappearDuration)
            {
                _disappearTimer += Time.deltaTime;
                float scale =
                    Mathf.Max(0.0f, (_disappearDuration - _disappearTimer) / _disappearDuration);
                transform.localScale = new Vector3(scale, scale, scale);
            }

            if (_disappearTimer >= _disappearDuration)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
