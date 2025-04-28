namespace Gizmos
{
    using UnityEngine;
    using UnityEngine.UI;

    public class DoubleLabelsItem : MonoBehaviour
    {
        public Text FirstLabel;

        public Text SecondLabel;

        public void SetLabels(string first, string second)
        {
            if (FirstLabel != null)
            {
                FirstLabel.text = first;
            }

            if (SecondLabel != null)
            {
                SecondLabel.text = second;
            }
        }
    }
}
