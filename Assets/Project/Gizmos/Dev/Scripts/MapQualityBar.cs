namespace Gizmos
{
    using UnityEngine;

    public class MapQualityBar : MonoBehaviour
    {
        public Animator Animator;

        public Renderer Renderer;

   
        public Color InitialColor = Color.white;

        public Color LowQualityColor = Color.red;

        public Color MediumQualityColor = Color.yellow;

        public Color HighQualityColor = Color.green;

        private const string _varColor = "_Color";

        private static readonly int _paramQuality = Animator.StringToHash("Quality");
        private static readonly int _paramIsVisited = Animator.StringToHash("IsVisited");
        private static readonly int _paramColorCurve = Animator.StringToHash("ColorCurve");

        private static readonly int _stateLow = Animator.StringToHash("Base Layer.Low");
        private static readonly int _stateMedium = Animator.StringToHash("Base Layer.Medium");
        private static readonly int _stateHigh = Animator.StringToHash("Base Layer.High");

        private bool _isVisited = false;
        private int _state = 0;
        private float _alpha = 1.0f;

        public bool IsVisited
        {
            get
            {
                return _isVisited;
            }

            set
            {
                _isVisited = value;
                Animator.SetBool(_paramIsVisited, _isVisited);
            }
        }

        public int QualityState
        {
            get
            {
                return _state;
            }

            set
            {
                _state = value;
                Animator.SetInteger(_paramQuality, _state);
            }
        }

        public float Weight
        {
            get
            {
                if (IsVisited)
                {
                    switch (_state)
                    {
                        case 0: return 0.1f; 
                        case 1: return 0.5f; 
                        case 2: return 1.0f; 
                        default: return 0.0f;
                    }
                }
                else
                {
                    return 0.0f;
                }
            }
        }

        public void SetAlpha(float alpha)
        {
            _alpha = alpha;
        }

        public void Update()
        {
            var stateInfo = Animator.GetCurrentAnimatorStateInfo(0);
            float colorCurve = Animator.GetFloat(_paramColorCurve);
            Color color = InitialColor;
            if (stateInfo.fullPathHash == _stateLow)
            {
                color = Color.Lerp(InitialColor, LowQualityColor, colorCurve);
            }
            else if (stateInfo.fullPathHash == _stateMedium)
            {
                color = Color.Lerp(LowQualityColor, MediumQualityColor, colorCurve);
            }
            else if (stateInfo.fullPathHash == _stateHigh)
            {
                color = Color.Lerp(MediumQualityColor, HighQualityColor, colorCurve);
            }

            color.a = _alpha;
            Renderer.material.SetColor(_varColor, color);
        }
    }
}
