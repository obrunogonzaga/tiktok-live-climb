using UnityEngine;

[DefaultExecutionOrder(200)]
public sealed class ClimbAtmosphereFollow : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform[] elements;
    [SerializeField] private Vector3[] offsets;
    [SerializeField] private Quaternion[] rotations;

    public void Configure(Transform camera, Transform[] atmosphere)
    {
        cameraTransform = camera; elements = atmosphere;
        offsets = new Vector3[elements.Length]; rotations = new Quaternion[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            offsets[i] = camera.InverseTransformPoint(elements[i].position);
            rotations[i] = Quaternion.Inverse(camera.rotation) * elements[i].rotation;
        }
    }

    private void LateUpdate()
    {
        if (cameraTransform == null || elements == null) return;
        for (int i = 0; i < elements.Length; i++)
            if (elements[i] != null) elements[i].SetPositionAndRotation(cameraTransform.TransformPoint(offsets[i]), cameraTransform.rotation * rotations[i]);
    }
}
