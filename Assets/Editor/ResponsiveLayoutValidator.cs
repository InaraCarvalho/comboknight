using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ETAPA 1 - Validacao da responsividade da arena/camera.
// Simula as proporcoes de tela alvo sobre a cena real e confere:
//  * paredes posicionadas pela meia-largura visivel;
//  * cavaleiro invertendo o passo ANTES de chegar na borda da tela;
//  * chao cobrindo a arena (nao cai nas extremidades laterais);
//  * spawns dos monstros dentro do campo de visao;
//  * chao (Ground, Y=-4.50) ocupando a fracao da tela (groundScreenFraction);
//  * troca da arte de fundo pela da orientacao atual;
//  * background cobrindo a visao inteira com a linha de chao em groundY.
// Os campos lidos por reflection: AdjustCamera grava direto nos campos do
// MonoBehaviour (autoTurnX, minSpawnX...) e o SerializedObject, em modo
// edicao, le a copia nativa ainda nao atualizada (leitura velha).
public static class ResponsiveLayoutValidator
{
    private const float Epsilon = 0.002f;
    private const string ScenePath = "Assets/Scenes/MainScene.unity";
    private const string ReportPath = "Assets/QA_RESPONSIVE_REPORT.txt";

    private struct ResolutionCase
    {
        public int width;
        public int height;
        public string label;

        public ResolutionCase(int width, int height, string label)
        {
            this.width = width;
            this.height = height;
            this.label = label;
        }
    }

    private static readonly ResolutionCase[] Cases =
    {
        new ResolutionCase(720, 1280, "Retrato 9:16"),
        new ResolutionCase(1080, 2400, "Retrato 9:20"),
        new ResolutionCase(1280, 720, "Paisagem 16:9"),
        new ResolutionCase(2048, 1536, "Paisagem iPad 5th 4:3"),
        new ResolutionCase(2400, 1080, "Paisagem 18:9"),
    };

    [MenuItem("Tools/Validate Responsive Arena Layout")]
    public static void RunFromMenu()
    {
        Run();
    }

    // Entrada para batch mode:
    // Unity.exe -batchmode -projectPath <proj> -executeMethod ResponsiveLayoutValidator.RunFromCommandLine -logFile <log>
    public static void RunFromCommandLine()
    {
        bool ok = Run();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Run()
    {
        var results = new List<string>();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[ResponsiveQA] Nao foi possivel abrir " + ScenePath);
            return false;
        }

        var resp = Object.FindAnyObjectByType<ResponsiveCamera>();
        Camera cam = resp != null ? resp.GetComponent<Camera>() : Object.FindAnyObjectByType<Camera>();
        var wallLeft = GameObject.Find("WallLeft");
        var wallRight = GameObject.Find("WallRight");
        var ground = GameObject.Find("Ground");
        var groundCol = ground != null ? ground.GetComponent<BoxCollider2D>() : null;
        var player = Object.FindAnyObjectByType<PlayerController>();
        var spawner = Object.FindAnyObjectByType<EnemySpawner>();

        if (cam == null || resp == null || wallLeft == null || wallRight == null || groundCol == null || player == null || spawner == null)
        {
            Debug.LogError("[ResponsiveQA] Cena incompleta: camera=" + (cam != null) +
                           " walls=" + (wallLeft != null && wallRight != null) +
                           " ground=" + (groundCol != null) +
                           " player=" + (player != null) + " spawner=" + (spawner != null));
            return false;
        }

        float groundY = GetFloat(resp, "groundY");
        float groundScreenFraction = GetFloat(resp, "groundScreenFraction");
        float wallHalfWidthOffset = GetFloat(resp, "wallHalfWidthOffset");
        float portraitArtFloor = GetFloat(resp, "portraitArtFloor");
        float landscapeArtFloor = GetFloat(resp, "landscapeArtFloor");
        var background = GetObject<Transform>(resp, "backgroundTransform");
        var landscapeSprite = GetObject<Sprite>(resp, "landscapeBackgroundSprite");
        var bgRenderer = background != null ? background.GetComponent<SpriteRenderer>() : null;

        // Guarda os valores originais para devolver a cena ao estado inicial.
        Vector3 originalCamPos = cam.transform.position;
        float originalOrthoSize = cam.orthographicSize;
        Vector3 originalWallLeft = wallLeft.transform.position;
        Vector3 originalWallRight = wallRight.transform.position;
        Vector2 originalGroundSize = groundCol.size;
        Vector3 originalBgPos = background != null ? background.position : Vector3.zero;
        Vector3 originalBgScale = background != null ? background.localScale : Vector3.one;
        Sprite originalSprite = bgRenderer != null ? bgRenderer.sprite : null;
        float originalAutoTurnX = GetFloat(player, "autoTurnX");
        float originalMinSpawnX = GetFloat(spawner, "minSpawnX");
        float originalMaxSpawnX = GetFloat(spawner, "maxSpawnX");

        int passed = 0;
        int failed = 0;

        foreach (var c in Cases)
        {
            resp.AdjustCamera(c.width, c.height);

            float aspect = (float)c.width / c.height;
            bool isLandscape = aspect >= 1f;
            float halfWidth = cam.orthographicSize * cam.aspect;
            float camY = cam.transform.position.y;
            float viewBottom = camY - cam.orthographicSize;
            float viewTop = camY + cam.orthographicSize;

            Check(results, ref passed, ref failed, c, "Aspect sincronizado",
                Mathf.Abs(cam.aspect - aspect) < Epsilon,
                $"cam.aspect={cam.aspect:F4} esperado={aspect:F4}");

            Check(results, ref passed, ref failed, c, "Paredes na borda visivel",
                Mathf.Abs(wallLeft.transform.position.x - (-halfWidth - wallHalfWidthOffset)) < Epsilon &&
                Mathf.Abs(wallRight.transform.position.x - (halfWidth + wallHalfWidthOffset)) < Epsilon &&
                wallRight.transform.position.x >= halfWidth && wallLeft.transform.position.x <= -halfWidth,
                $"WallLeft.x={wallLeft.transform.position.x:F3} WallRight.x={wallRight.transform.position.x:F3} halfWidth={halfWidth:F3}");

            float autoTurnX = GetFloat(player, "autoTurnX");
            Check(results, ref passed, ref failed, c, "Rastro da arena (autoTurnX) sincronizado",
                Mathf.Abs(autoTurnX - (halfWidth - 0.6f)) < Epsilon && autoTurnX < halfWidth,
                $"autoTurnX={autoTurnX:F3} (meia-largura={halfWidth:F3}, folga {halfWidth - autoTurnX:F2})");

            // O sprite do cavaleiro (com a espada) e bem maior que o collider do
            // corpo: o clamp lateral precisa de folga para o personagem INTEIRO
            // ficar dentro da tela quando encosta nas bordas da arena. Mede os
            // sprites REAIS de runtime (madfeira do PlayerController), nao o
            // idle serializado na cena (53x48), que nao representa o jogo.
            var runtimeBg = GetObject<Sprite>(player, "broadswordSprite");
            var runtimeDagger = GetObject<Sprite>(player, "daggerSprite");
            Sprite runtimeSprite = runtimeBg != null ? runtimeBg : runtimeDagger;
            if (runtimeSprite != null)
            {
                float visualHalf = Mathf.Max(
                    runtimeBg != null ? runtimeBg.bounds.extents.x : 0f,
                    runtimeDagger != null ? runtimeDagger.bounds.extents.x : 0f) * Mathf.Abs(player.transform.lossyScale.x);
                float clampRoom = halfWidth - visualHalf;
                Check(results, ref passed, ref failed, c, "Sprite do cavaleiro cabe na tela (clamp lateral)",
                    clampRoom > 0.1f,
                    $"meia-largura visual={visualHalf:F3}, folga do clamp={clampRoom:F3} vs visivel +/-{halfWidth:F3}");
            }

            // O chao precisa cobrir ate alem da face interna das paredes, senao
            // o cavaleiro cai no vao quando vai pras extremidades laterais.
            float groundHalfW = groundCol.size.x * 0.5f;
            float wallFaceX = halfWidth + wallHalfWidthOffset;
            Check(results, ref passed, ref failed, c, "Chao cobre a arena (nao cai nas extremidades)",
                groundCol.size.x > 0f && groundHalfW >= wallFaceX && ground.transform.position.y - 6f + groundCol.size.y * 0.5f >= groundY - Epsilon,
                $"chao cobre +-{groundHalfW:F3} (precisa ate +-{wallFaceX:F3}), topo={ground.transform.position.y + groundCol.offset.y + groundCol.size.y * 0.5f:F3} (groundY={groundY:F2})");

            float minSpawnX = GetFloat(spawner, "minSpawnX");
            float maxSpawnX = GetFloat(spawner, "maxSpawnX");
            Check(results, ref passed, ref failed, c, "Spawns dentro da tela",
                Mathf.Abs(minSpawnX - (-halfWidth + 0.8f)) < Epsilon &&
                Mathf.Abs(maxSpawnX - (halfWidth - 0.8f)) < Epsilon &&
                minSpawnX > -halfWidth && maxSpawnX < halfWidth,
                $"minSpawnX={minSpawnX:F3} maxSpawnX={maxSpawnX:F3} (visivel +/-{halfWidth:F3})");

            Check(results, ref passed, ref failed, c, "Chao ocupa a fracao da tela",
                Mathf.Abs(camY - (groundY + cam.orthographicSize * (1f - 2f * groundScreenFraction))) < Epsilon &&
                viewBottom < groundY &&
                Mathf.Abs((groundY - viewBottom) / (2f * cam.orthographicSize) - groundScreenFraction) < 0.01f,
                $"base da tela={viewBottom:F3}, topo do chao={groundY:F3} ({(groundY - viewBottom) / (2f * cam.orthographicSize):P0} da tela embaixo do topo)");

            Sprite expectedSprite = isLandscape ? landscapeSprite : originalSprite;
            Check(results, ref passed, ref failed, c, "Arte da orientacao correta",
                expectedSprite != null && bgRenderer != null && bgRenderer.sprite == expectedSprite,
                $"sprite={(bgRenderer != null && bgRenderer.sprite != null ? bgRenderer.sprite.name : "null")}" +
                $" esperado={(expectedSprite != null ? expectedSprite.name : "NULL (landscapeBackgroundSprite nao atribuido)")}");

            if (background != null && bgRenderer != null && bgRenderer.sprite != null)
            {
                Vector3 ls = background.lossyScale;
                Bounds lb = bgRenderer.sprite.bounds;
                float artHalfW = lb.size.x * Mathf.Abs(ls.x) * 0.5f;
                float artCenterY = background.position.y + lb.center.y * ls.y;
                float artHalfH = lb.size.y * Mathf.Abs(ls.y) * 0.5f;
                float artBottom = artCenterY - artHalfH;
                float artTop = artCenterY + artHalfH;
                float artFloor = isLandscape ? landscapeArtFloor : portraitArtFloor;
                float artFloorY = artBottom + artFloor * Mathf.Abs(ls.y);

                bool covered = artTop >= viewTop - Epsilon &&
                               artBottom <= viewBottom + Epsilon &&
                               artHalfW >= halfWidth - Epsilon &&
                               Mathf.Abs(artFloorY - groundY) < 0.05f;
                string bgDetails = $"arte [{artBottom:F2}..{artTop:F2}] x +-{artHalfW:F2}" +
                                   $" vs visao [{viewBottom:F2}..{viewTop:F2}] x +-{halfWidth:F2}" +
                                   $", linha do chao da arte={artFloorY:F3}";

                Check(results, ref passed, ref failed, c, "Background cobre a visao (sem vao preto)",
                    covered, bgDetails);
            }
        }

        // Devolve a cena ao estado original (nao salva nada).
        cam.transform.position = originalCamPos;
        cam.orthographicSize = originalOrthoSize;
        cam.ResetAspect();
        wallLeft.transform.position = originalWallLeft;
        wallRight.transform.position = originalWallRight;
        groundCol.size = originalGroundSize;
        if (background != null)
        {
            background.position = originalBgPos;
            background.localScale = originalBgScale;
        }
        if (bgRenderer != null && originalSprite != null) bgRenderer.sprite = originalSprite;
        SetFloat(player, "autoTurnX", originalAutoTurnX);
        SetFloat(spawner, "minSpawnX", originalMinSpawnX);
        SetFloat(spawner, "maxSpawnX", originalMaxSpawnX);

        var sb = new StringBuilder();
        sb.AppendLine("=== COMBO KNIGHT RESPONSIVE ARENA QA (ETAPA 1) ===");
        foreach (var line in results) sb.AppendLine(line);
        sb.AppendLine($"-------------------------------------------------");
        sb.AppendLine($"SUMMARY: Total={passed + failed} | Passed={passed} | Failed={failed}");
        string report = sb.ToString();

        File.WriteAllText(Application.dataPath + "/QA_RESPONSIVE_REPORT.txt", report);
        if (failed == 0) Debug.Log(report);
        else Debug.LogError(report);

        return failed == 0;
    }

    private static void Check(List<string> results, ref int passed, ref int failed,
        ResolutionCase c, string testName, bool ok, string details)
    {
        string mark = ok ? "[PASS]" : "[FAIL]";
        results.Add($"{mark} {c.width}x{c.height} ({c.label}) {testName}: {details}");
        if (ok) passed++;
        else failed++;
    }

    private static FieldInfo FindField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static float GetFloat(object target, string name)
    {
        FieldInfo fi = FindField(target, name);
        return fi != null && fi.FieldType == typeof(float) ? (float)fi.GetValue(target) : float.NaN;
    }

    private static void SetFloat(object target, string name, float value)
    {
        FieldInfo fi = FindField(target, name);
        if (fi != null && fi.FieldType == typeof(float)) fi.SetValue(target, value);
    }

    private static T GetObject<T>(object target, string name) where T : class
    {
        FieldInfo fi = FindField(target, name);
        return fi != null ? fi.GetValue(target) as T : null;
    }
}
