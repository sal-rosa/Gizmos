using UnityEngine;

public class Anchor : MonoBehaviour
{
    public Vector3 rotationSpeed = new Vector3(0, 1, 0);
    public Vector3 startRotation = new Vector3(-90, 0, 0);

    void Start()
    {
        transform.rotation = Quaternion.Euler(startRotation);
    }

    void Update()
    {
        transform.Rotate(rotationSpeed * 5f * Time.deltaTime, Space.World);
    }
}
