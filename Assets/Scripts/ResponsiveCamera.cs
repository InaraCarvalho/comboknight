using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ResponsiveCamera : MonoBehaviour
{
    [SerializeField] private float targetHalfWidth = 3.95f;
    [SerializeField] private float minOrthoSize = 7.0f;
    [SerializeField] private Transform backgroundTransform;

    private Camera cam;
    private int lastWidth;
    private int lastHeight;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        AdjustCamera();
    }

    private void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            AdjustCamera();
        }
    }

    public void AdjustCamera()
    {
        if (cam == null) cam = GetComponent<Camera>();
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        float aspect = (float)lastWidth / Mathf.Max(1, lastHeight);

        float neededSize = targetHalfWidth / aspect;
        cam.orthographicSize = Mathf.Max(minOrthoSize, neededSize);

        if (backgroundTransform != null)
        {
            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * aspect;
            float scaleByWidth = (camWidth * 1.05f) / 3.6f;
            float scaleByHeight = (camHeight * 1.02f) / 10.0f;
            float finalScale = Mathf.Max(2.25f, Mathf.Max(scaleByWidth, scaleByHeight));
            backgroundTransform.localScale = new Vector3(finalScale, finalScale, 1f);
            backgroundTransform.position = new Vector3(0f, -1.95f, 0f);
        }
    }
}
