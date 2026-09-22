using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ResponsiveCamera : MonoBehaviour
{
    [SerializeField] private float targetHalfWidth = 3.95f;
    [SerializeField] private float minOrthoSize = 7.0f;
    [SerializeField] private float landscapeTargetHalfWidth = 5.0f;
    [SerializeField] private float landscapeMinOrthoSize = 4.3f;
    [SerializeField] private Transform backgroundTransform;

    // Arte do fundo: background_arena (640x360, PPU 100 -> 6.4 x 3.6 no mundo).
    [SerializeField] private float backgroundSpriteWidth = 6.4f;
    [SerializeField] private float backgroundSpriteHeight = 3.6f;
    // Paisagem: aumenta a arte um pouco alem do cenario (corta so o topo, onde
    // ficam predios/ceu) e desce o centro para os predios nao encostarem no
    // topo da tela. Ajustaveis no Inspector para bater com a arte.
    [SerializeField] private float landscapeBackgroundOverScale = 1.15f;
    [SerializeField] private float landscapeBackgroundY = -0.75f;

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

        float neededSize;
        if (aspect >= 1f)
        {
            neededSize = Mathf.Max(landscapeMinOrthoSize, landscapeTargetHalfWidth / aspect);
        }
        else
        {
            neededSize = Mathf.Max(minOrthoSize, targetHalfWidth / aspect);
        }

        cam.orthographicSize = neededSize;

        if (backgroundTransform != null)
        {
            float camHeight = cam.orthographicSize * 2f;
            float camWidth = camHeight * aspect;
            if (aspect >= 1f)
            {
                // Paisagem: cobre largura E altura com a arte e ainda aumenta
                // um pouco (overScale) para o topo nao esbarrar na borda. O
                // centro fica abaixo do meio, entao os predios (banda de cima)
                // aparecem mais para os lados/inferiores, e nao "pro alto".
                float coverScale = Mathf.Max((camWidth * 1.05f) / backgroundSpriteWidth,
                                             (camHeight * 1.02f) / backgroundSpriteHeight);
                float finalScale = Mathf.Max(2.25f, coverScale) * landscapeBackgroundOverScale;
                backgroundTransform.localScale = new Vector3(finalScale, finalScale, 1f);
                backgroundTransform.position = new Vector3(0f, landscapeBackgroundY, 0f);
            }
            else
            {
                float scaleByWidth = (camWidth * 1.05f) / backgroundSpriteWidth;
                float scaleByHeight = (camHeight * 1.02f) / 10.0f;
                float finalScale = Mathf.Max(2.25f, Mathf.Max(scaleByWidth, scaleByHeight));
                backgroundTransform.localScale = new Vector3(finalScale, finalScale, 1f);
                backgroundTransform.position = new Vector3(0f, -1.95f, 0f);
            }
        }
    }
}
