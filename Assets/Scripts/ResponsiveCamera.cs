using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ResponsiveCamera : MonoBehaviour
{
    [SerializeField] private float targetHalfWidth = 3.95f;
    [SerializeField] private float minOrthoSize = 7.0f;
    [SerializeField] private float landscapeTargetHalfWidth = 5.0f;
    [SerializeField] private float landscapeMinOrthoSize = 4.3f;
    [SerializeField] private Transform backgroundTransform;

    // A mesma arte background_arena (640x360, PPU 100 -> 6.4 x 3.6) e usada
    // nas duas orientacoes: atribuida na cena e aproveitada como
    // landscapeBackgroundSprite (antes havia background_arena_portrait, agora
    // removido). Sem a troca generosa a arte nao cobre as laterais em 16:9/18:9.
    [SerializeField] private Sprite landscapeBackgroundSprite;

    // Linha de chao da arte (topo da parede de tijolos), medida da BASE da
    // imagem: 0.8 de 3.6 (tijoleira comeca em y=280 de 360).
    [SerializeField] private float portraitArtFloor = 0.8f;
    [SerializeField] private float landscapeArtFloor = 0.8f;
    [SerializeField] private float backgroundMinScale = 2.25f;
    // Folga de cobertura: garante que a arte nunca deixe uma fresta de 1px de
    // cor de fundo na borda da tela.
    [SerializeField] private float backgroundCoverPadding = 1.02f;

    // Arena: o chao fisico (Ground, Y = -4.50) ocupa uma fracao fixa da altura
    // da tela. groundScreenFraction define quanta altura visivel fica abaixo do
    // topo do chao (0.35 -> 35% da tela, acima fica o restante). Vale em
    // qualquer proporcao: retrato (9:16 / 9:20) e paisagem (16:9 / 18:9 e 4:3).
    [SerializeField] private float groundY = -4.5f;
    [SerializeField] private float groundScreenFraction = 0.35f;
    // Paredes: BoxCollider2D de size 1 com offset +/-0.5 => a face interna da
    // parede fica em +/-halfWidth + 0.5 quando a base do GameObject esta em
    // +/-halfWidth + 0.5 (meia unidade alem da borda da tela).
    [SerializeField] private float wallHalfWidthOffset = 0.5f;

    private Camera cam;
    private int lastWidth;
    private int lastHeight;
    private Transform wallLeft;
    private Transform wallRight;
    private BoxCollider2D groundCollider;
    private PlayerController playerController;
    private EnemySpawner enemySpawner;
    private SpriteRenderer backgroundRenderer;
    private Sprite defaultBackgroundSprite;

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
        AdjustCamera(Screen.width, Screen.height);
    }

    // Resolucao explicita: em runtime vem da tela real; a validacao
    // (Tools/Validate Responsive Arena Layout) usa a sobrecarga para simular
    // 9:16, 9:20, 16:9, 4:3 e 18:9 sem depender do tamanho da Game View.
    public void AdjustCamera(int width, int height)
    {
        if (cam == null) cam = GetComponent<Camera>();
        lastWidth = width;
        lastHeight = height;
        float aspect = (float)width / Mathf.Max(1, height);

        float neededSize;
        bool isLandscape = aspect >= 1f;
        if (isLandscape)
        {
            neededSize = Mathf.Max(landscapeMinOrthoSize, landscapeTargetHalfWidth / aspect);
        }
        else
        {
            neededSize = Mathf.Max(minOrthoSize, targetHalfWidth / aspect);
        }

        cam.orthographicSize = neededSize;
        cam.aspect = aspect;

        // Meia-largura visivel do mundo: base de TODOS os limites da arena
        // (paredes, virada automatica do cavaleiro e spawn dos monstros).
        float halfWidth = cam.orthographicSize * cam.aspect;

        // O topo do chao (groundY) fica em groundScreenFraction da altura da tela
        // acima da borda inferior: a faixa de chao visivel mede
        // 2 * groundScreenFraction * neededSize (0.35 -> 35% em qualquer
        // proporcao). A altura visivel acima do chao e o restante.
        float camPosY = groundY + neededSize * (1f - 2f * groundScreenFraction);
        cam.transform.position = new Vector3(cam.transform.position.x, camPosY, cam.transform.position.z);

        ApplyArenaBounds(halfWidth);
        AdjustBackground(isLandscape, neededSize, halfWidth);
    }

    // Paredes + limites do player e do spawner acompanham a largura visivel,
    // entao ninguem sai da tela em nenhuma proporcao.
    private void ApplyArenaBounds(float halfWidth)
    {
        if (wallLeft == null) wallLeft = GameObject.Find("WallLeft")?.transform;
        if (wallRight == null) wallRight = GameObject.Find("WallRight")?.transform;

        if (wallLeft != null)
        {
            Vector3 p = wallLeft.position;
            wallLeft.position = new Vector3(-halfWidth - wallHalfWidthOffset, p.y, p.z);
        }
        if (wallRight != null)
        {
            Vector3 p = wallRight.position;
            wallRight.position = new Vector3(halfWidth + wallHalfWidthOffset, p.y, p.z);
        }

        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (playerController != null) playerController.SetArenaBounds(halfWidth);

        if (enemySpawner == null) enemySpawner = FindAnyObjectByType<EnemySpawner>();
        if (enemySpawner != null) enemySpawner.SetSpawnBounds(halfWidth);

        // Chao: o BoxCollider2D do Ground nasceu com 16 de largura (±8), mas as
        // paredes seguem a tela (±10 em 18:9) — sem isto o cavaleiro passava do
        // fim do chao nas extremidades e caia. A largura acompanha a arena com
        // folga alem da face interna das paredes; a altura/topo nao mudam.
        if (groundCollider == null)
        {
            var groundGo = GameObject.Find("Ground");
            groundCollider = groundGo != null ? groundGo.GetComponent<BoxCollider2D>() : null;
        }
        if (groundCollider != null)
        {
            Vector2 size = groundCollider.size;
            size.x = 2f * (halfWidth + wallHalfWidthOffset + 1f);
            groundCollider.size = size;
        }
    }

    private void AdjustBackground(bool isLandscape, float neededSize, float halfWidth)
    {
        if (backgroundTransform == null) return;
        if (backgroundRenderer == null) backgroundRenderer = backgroundTransform.GetComponent<SpriteRenderer>();
        if (backgroundRenderer == null || backgroundRenderer.sprite == null) return;
        if (defaultBackgroundSprite == null) defaultBackgroundSprite = backgroundRenderer.sprite;

        // A mesma arte cobre as duas orientacoes (background_arena); como a linha
        // de chao e identica em ambos os casos, o offset nunca muda.
        Sprite art = (isLandscape && landscapeBackgroundSprite != null)
            ? landscapeBackgroundSprite
            : defaultBackgroundSprite;
        backgroundRenderer.sprite = art;

        Bounds artBounds = art.bounds;
        float artWidth = artBounds.size.x;
        float artHeight = artBounds.size.y;
        float artFloor = isLandscape ? landscapeArtFloor : portraitArtFloor;

        // Menor escala que cobre a visao inteira: laterais, a faixa acima do chao
        // (artHeight - artFloor) e a faixa de chao visivel abaixo do topo
        // (artFloor). A fracao da tela abaixo do chao cresce com a camera em
        // proporcoes altas, entao as duas faixas sao calculadas separadamente.
        float scaleByWidth = (2f * halfWidth) * backgroundCoverPadding / artWidth;
        float scaleByHeightAbove = (2f * neededSize * (1f - groundScreenFraction)) * backgroundCoverPadding
                                   / (artHeight - artFloor);
        float scaleByHeightBelow = (2f * neededSize * groundScreenFraction) * backgroundCoverPadding
                                   / artFloor;
        float scale = Mathf.Max(backgroundMinScale,
            Mathf.Max(scaleByWidth, Mathf.Max(scaleByHeightAbove, scaleByHeightBelow)));

        backgroundTransform.localScale = new Vector3(scale, scale, 1f);
        // Ancora a base da arte de modo que a linha de chao da arte caia
        // exatamente em groundY: o cavaleiro continua de pe sobre os tijolos
        // com a camera subindo/descendo em qualquer resolucao.
        float artBottomY = groundY - artFloor * scale;
        float artCenterY = artBottomY - (artBounds.center.y - artHeight * 0.5f) * scale;
        backgroundTransform.position = new Vector3(0f, artCenterY, 0f);
    }
}
