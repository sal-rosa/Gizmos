namespace Gizmos
{
    using UnityEngine;

    public class SafeAreaScaler : MonoBehaviour
    {
        private Rect _screenSafeArea = new Rect(0, 0, 0, 0);

        public void Update()
        {
            Rect safeArea;
            safeArea = Screen.safeArea;

            if (_screenSafeArea != safeArea)
            {
                _screenSafeArea = safeArea;
                MatchRectTransformToSafeArea();
            }
        }

        private void MatchRectTransformToSafeArea()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();

            Vector2 offsetMin = new Vector2(_screenSafeArea.xMin,
                Screen.height - _screenSafeArea.yMax);

            Vector2 offsetMax = new Vector2(
                _screenSafeArea.xMax - Screen.width,
                -_screenSafeArea.yMin);

            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }
    }
}
