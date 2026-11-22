
using UnityEngine;
using System.Collections;

public class CameraIntro : MonoBehaviour
{
    [Header("Camera Positions")]
    public Transform cameraStart;
    public Transform cameraEnd;

    [Header("Animation Settings")]
    public float transitionDuration = 4f;

    private void Start()
    {
        if (cameraStart == null || cameraEnd == null)
        {
            Debug.LogError("Camera positions are not assigned!");
            return;
        }

        transform.position = cameraStart.position;
        transform.rotation = cameraStart.rotation;

        StartCoroutine(CameraTransition());
    }

    IEnumerator CameraTransition()
    {
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / transitionDuration);

            // Smooth camera movement
            t = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(
                cameraStart.position,
                cameraEnd.position,
                t
            );

            transform.rotation = Quaternion.Slerp(
                cameraStart.rotation,
                cameraEnd.rotation,
                t
            );

            yield return null;
        }

        transform.position = cameraEnd.position;
        transform.rotation = cameraEnd.rotation;
    }
}
