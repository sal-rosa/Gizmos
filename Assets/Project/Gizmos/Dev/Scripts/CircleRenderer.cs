namespace Gizmos
{
    using UnityEngine;

    [RequireComponent(typeof(LineRenderer))]
    public class CircleRenderer : MonoBehaviour
    {

        [Range(4, 720)]
        public int Segment = 72;

        private const string _varColor = "_TintColor";

        private LineRenderer _lineRenderer;

        private float _alpha = 1.0f;

        public void SetAlpha(float alpha)
        {
            _alpha = alpha;
        }

        public void DrawArc(Vector3 centerDir, float radius, float range)
        {
            range = Mathf.Clamp(range, 0.0f, 360.0f);
            if (range == 0.0f)
            {
                gameObject.SetActive(false);
                return;
            }

            int count = (int)(range * Segment / 360.0f);
            float pointSpacing = range / count;

            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.GetComponent<LineRenderer>();
            }

            _lineRenderer.positionCount = count + 1;
            _lineRenderer.useWorldSpace = false;
            for (int i = 0; i < count + 1; ++i)
            {
                float deltaAngle = (-range / 2) + (pointSpacing * i);
                var rotation = Quaternion.AngleAxis(deltaAngle, Vector3.up);
                var position = (rotation * centerDir * radius) + transform.position;
                _lineRenderer.SetPosition(i, transform.InverseTransformPoint(position));
            }

            gameObject.SetActive(true);
        }

        public void Update()
        {
            if (_lineRenderer == null)
            {
                return;
            }

            var renderer = _lineRenderer.GetComponent<Renderer>();
            var color = renderer.material.GetColor(_varColor);
            color.a = _alpha;
            renderer.material.SetColor(_varColor, color);
        }
    }
}
