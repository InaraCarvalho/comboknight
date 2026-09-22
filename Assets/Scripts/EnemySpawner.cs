using System.Collections;
using UnityEngine;
using DG.Tweening;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject slimePrefab;
    [SerializeField] private GameObject batPrefab;
    [SerializeField] private float groundSpawnY = -3.95f;
    [SerializeField] private float minSpawnX = -6.2f;
    [SerializeField] private float maxSpawnX = 6.2f;
    [SerializeField] private float minBatY = -2.2f;
    [SerializeField] private float maxBatY = 1.6f;
    [SerializeField] private float minDistanceFromPlayer = 2.4f;
    [SerializeField] private float warningDuration = 0.9f;

    private float slimeTimer = 1.2f;
    private float batTimer = 1.8f;

    private static Sprite cachedWarningSprite;

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsGameActive) return;

        float diff = GameManager.Instance.DifficultyMultiplier;

        // Slimes: spawn CONTINUO (sem ondas), com intervalo que diminui com o nivel.
        slimeTimer += Time.deltaTime;
        float slimeInterval = Mathf.Max(0.7f, 2.3f / diff);
        if (slimeTimer >= slimeInterval)
        {
            slimeTimer = 0f;
            if (slimePrefab != null) StartCoroutine(WarnThenSpawn(slimePrefab, PickSpot(minSpawnX, maxSpawnX, groundSpawnY, groundSpawnY), false));
        }

        batTimer += Time.deltaTime;
        float batInterval = Mathf.Max(1.1f, 3.4f / diff);
        if (batTimer >= batInterval)
        {
            batTimer = 0f;
            if (batPrefab != null) StartCoroutine(WarnThenSpawn(batPrefab, PickSpot(minSpawnX, maxSpawnX, Random.Range(minBatY, maxBatY), Random.Range(minBatY, maxBatY)), true));
        }
    }

    // Escolhe um ponto aleatorio do cenario, evitando cair em cima do jogador.
    private Vector2 PickSpot(float minX, float maxX, float minY, float maxY)
    {
        Transform player = null;
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;

        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(minX, maxX);
            float y = Random.Range(minY, maxY);
            if (player == null) return new Vector2(x, y);
            float dist = Mathf.Abs(x - player.position.x);
            if (dist > minDistanceFromPlayer) return new Vector2(x, y);
        }

        float fallbackX = (player.position.x >= 0f) ? minX : maxX;
        return new Vector2(fallbackX, Random.Range(minY, maxY));
    }

    // Sinaliza o local antes de o inimigo aparecer, para o jogador nao levar
    // dano de surpresa. O aviso pisca no ponto e entao o inimigo nasce la.
    private IEnumerator WarnThenSpawn(GameObject prefab, Vector2 spot, bool isBat)
    {
        GameObject warning = CreateWarning(spot, isBat);
        float t = warningDuration;
        while (t > 0f)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameActive) break;
            t -= Time.deltaTime;
            yield return null;
        }

        if (warning != null)
        {
            warning.transform.DOKill();
            Destroy(warning);
        }

        if (GameManager.Instance != null && GameManager.Instance.IsGameActive)
        {
            Instantiate(prefab, spot, Quaternion.identity);
        }
    }

    private GameObject CreateWarning(Vector3 spot, bool isBat)
    {
        var go = new GameObject("SpawnWarning");
        go.transform.position = new Vector3(spot.x, (isBat ? spot.y : groundSpawnY), 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetWarningSprite();
        sr.color = isBat ? new Color(1f, 0.25f, 0.25f, 0.9f) : new Color(1f, 0.6f, 0.1f, 0.9f);
        sr.sortingOrder = 20;

        go.transform.localScale = isBat ? new Vector3(1.0f, 1.0f, 1f) : new Vector3(0.9f, 0.28f, 1f);
        go.transform.DOScale(go.transform.localScale * 1.25f, warningDuration)
            .SetEase(Ease.OutQuad);
        sr.DOFade(0.15f, warningDuration * 0.5f).SetLoops(2, LoopType.Yoyo);

        // Pequeno "!" de alerta flutuando sobre o marcador.
        var mark = new GameObject("AlertMark");
        mark.transform.SetParent(go.transform, false);
        mark.transform.localPosition = new Vector3(0f, isBat ? 0.95f : 0.7f, 0f);
        var markSr = mark.AddComponent<SpriteRenderer>();
        markSr.sprite = GetWarningSprite();
        markSr.transform.localScale = new Vector3(isBat ? 0.28f : 0.45f, isBat ? 0.28f : 0.45f, 1f);
        markSr.sortingOrder = 20;
        markSr.DOFade(0.15f, warningDuration * 0.5f).SetLoops(2, LoopType.Yoyo);

        return go;
    }

    // Circulo branco gerado em tempo de execucao (morte ao custo de nenhum asset).
    private static Sprite GetWarningSprite()
    {
        if (cachedWarningSprite != null) return cachedWarningSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float radius = size * 0.5f - 1f;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                px[y * size + x] = (d <= radius) ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        cachedWarningSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedWarningSprite;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}