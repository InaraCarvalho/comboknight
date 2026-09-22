using System.Collections;
using UnityEngine;
using DG.Tweening;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [SerializeField] private Sprite slashBroadswordSprite;
    [SerializeField] private Sprite slashDaggerSprite;
    [SerializeField] private Sprite hitSparkSprite;
    [SerializeField] private Sprite dustPuffSprite;
    [SerializeField] private Sprite slimeSplatSprite;
    [SerializeField] private Sprite batPoofSprite;
    [SerializeField] private Sprite coinShineSprite;
    [SerializeField] private Sprite shockwaveSprite;
    [SerializeField] private Sprite comboBurstSprite;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private Tween hitStopTween;

    public void HitStop(float duration = 0.045f)
    {
        hitStopTween?.Kill();
        Time.timeScale = 0f;
        hitStopTween = DOVirtual.DelayedCall(duration, () =>
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameActive && !GameManager.Instance.IsPaused && !GameManager.Instance.IsShopOpen)
                Time.timeScale = 1f;
        }).SetUpdate(true);
    }

    // Cancela um HitStop pendente sem mexer no time scale (usado ao abrir o mercador).
    public void CancelHitStop()
    {
        hitStopTween?.Kill();
        hitStopTween = null;
    }

    public void SpawnHitSpark(Vector3 position)
    {
        if (hitSparkSprite == null) return;
        GameObject go = CreateVfxObject("HitSpark", position, hitSparkSprite, 20);
        go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        go.transform.localScale = Vector3.zero;
        go.transform.DOScale(Vector3.one * 1.2f, 0.08f).SetEase(Ease.OutQuad).OnComplete(() =>
        {
            go.transform.DOScale(Vector3.zero, 0.06f).OnComplete(() => Destroy(go));
        });
    }

    public void SpawnSlashArc(Vector3 playerPos, bool isBroadsword, int facingDir)
    {
        if (isBroadsword)
        {
            if (slashBroadswordSprite == null) return;
            Vector3 offset = new Vector3(facingDir * 0.48f, 0.06f, 0);
            GameObject go = CreateVfxObject("SlashArc_Broadsword", playerPos + offset, slashBroadswordSprite, 18);
            go.transform.rotation = Quaternion.Euler(0, 0, facingDir * 24f);

            Vector3 s = new Vector3(0.38f * facingDir, 0.38f, 1f);
            go.transform.localScale = s * 0.35f;

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            go.transform.DOScale(s * 1.15f, 0.10f).SetEase(Ease.OutQuad);
            go.transform.DORotate(new Vector3(0, 0, -facingDir * 20f), 0.12f).SetEase(Ease.OutCubic);
            sr.DOFade(0f, 0.13f).SetEase(Ease.InQuad).OnComplete(() => Destroy(go));
        }
        else
        {
            if (slashDaggerSprite == null) return;
            Vector3 startOffset = new Vector3(facingDir * 0.28f, 0.02f, 0);
            GameObject go = CreateVfxObject("SlashThrust_Dagger", playerPos + startOffset, slashDaggerSprite, 18);
            go.transform.rotation = Quaternion.identity;

            Vector3 startScale = new Vector3(facingDir * 0.18f, 0.22f, 1f);
            Vector3 targetScale = new Vector3(facingDir * 0.44f, 0.26f, 1f);
            go.transform.localScale = startScale;

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            go.transform.DOMoveX(playerPos.x + facingDir * 0.52f, 0.07f).SetEase(Ease.OutQuad);
            go.transform.DOScale(targetScale, 0.07f).SetEase(Ease.OutQuad);
            sr.DOFade(0f, 0.08f).SetEase(Ease.InQuad).OnComplete(() => Destroy(go));
        }
    }

    public void SpawnJumpDust(Vector3 position)
    {
        if (dustPuffSprite == null) return;
        SpawnDust(position, -1.2f);
        SpawnDust(position, 1.2f);
    }

    public void SpawnLandingDust(Vector3 position)
    {
        if (dustPuffSprite == null) return;
        for (int i = 0; i < 2; i++)
        {
            float vx = (i == 0 ? -1f : 1f) * (1.0f + Random.Range(0.2f, 0.6f));
            SpawnDust(position, vx);
        }
    }

    private void SpawnDust(Vector3 pos, float vx)
    {
        GameObject go = CreateVfxObject("DustPuff", pos + new Vector3(vx * 0.15f, -0.45f, 0), dustPuffSprite, 15);
        go.transform.localScale = Vector3.one * 0.2f;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        
        go.transform.DOMove(go.transform.position + new Vector3(vx * 0.25f, 0.08f, 0), 0.22f).SetEase(Ease.OutQuad);
        go.transform.DOScale(Vector3.one * 0.45f, 0.22f);
        sr.DOFade(0f, 0.22f).OnComplete(() => Destroy(go));
    }

    public void SpawnSlimeSplat(Vector3 position)
    {
        if (slimeSplatSprite == null) return;
        for (int i = 0; i < 2; i++)
        {
            GameObject go = CreateVfxObject("SlimeSplat", position, slimeSplatSprite, 16);
            go.transform.localScale = Vector3.one * Random.Range(0.5f, 0.8f);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();

            float vx = (i == 0 ? -1.5f : 1.5f) * Random.Range(0.8f, 1.3f);
            float vy = Random.Range(1.8f, 3.2f);
            Vector3 target = position + new Vector3(vx * 0.4f, vy * 0.35f, 0);

            go.transform.DOJump(target, 0.35f, 1, 0.25f).SetEase(Ease.OutQuad);
            sr.DOFade(0f, 0.25f).OnComplete(() => Destroy(go));
        }
    }

    public void SpawnBatPoof(Vector3 position)
    {
        if (batPoofSprite == null) return;
        for (int i = 0; i < 2; i++)
        {
            GameObject go = CreateVfxObject("BatPoof", position, batPoofSprite, 16);
            go.transform.localScale = Vector3.one * Random.Range(0.4f, 0.7f);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();

            float angle = i * 180f + Random.Range(-20f, 20f);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Vector3 target = position + dir * Random.Range(0.5f, 0.9f);

            go.transform.DOMove(target, 0.22f).SetEase(Ease.OutQuad);
            go.transform.DOScale(Vector3.one * 1.2f, 0.22f);
            sr.DOFade(0f, 0.22f).OnComplete(() => Destroy(go));
        }
    }

    public void SpawnCoinShine(Vector3 position)
    {
        if (coinShineSprite == null) return;
        GameObject go = CreateVfxObject("CoinShine", position, coinShineSprite, 22);
        go.transform.localScale = Vector3.zero;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();

        go.transform.DOScale(Vector3.one * 1.4f, 0.12f).SetEase(Ease.OutBack);
        go.transform.DORotate(new Vector3(0, 0, 90f), 0.18f);
        sr.DOFade(0f, 0.2f).SetDelay(0.08f).OnComplete(() => Destroy(go));
    }

    public void SpawnShockwave(Vector3 position)
    {
        if (shockwaveSprite == null) return;
        GameObject go = CreateVfxObject("Shockwave", position, shockwaveSprite, 12);
        go.transform.localScale = Vector3.one * 0.2f;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();

        go.transform.DOScale(Vector3.one * 2.8f, 0.28f).SetEase(Ease.OutQuad);
        sr.DOFade(0f, 0.28f).SetEase(Ease.InQuad).OnComplete(() => Destroy(go));
    }

    public void SpawnComboBurst(Vector3 position, int comboCount)
    {
        if (comboBurstSprite != null)
        {
            float scaleMultiplier = Mathf.Clamp(0.85f + (comboCount - 2) * 0.12f, 0.85f, 2.2f);
            GameObject burstObj = CreateVfxObject("ComboBurst", position, comboBurstSprite, 25);
            burstObj.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            burstObj.transform.localScale = Vector3.zero;

            SpriteRenderer burstSr = burstObj.GetComponent<SpriteRenderer>();
            burstObj.transform.DOScale(Vector3.one * scaleMultiplier, 0.12f).SetEase(Ease.OutBack).SetLink(burstObj);
            burstObj.transform.DORotate(new Vector3(0, 0, Random.Range(60f, 150f)), 0.28f, RotateMode.WorldAxisAdd).SetEase(Ease.OutQuad).SetLink(burstObj);
            burstSr.DOFade(0f, 0.20f).SetDelay(0.08f).SetLink(burstObj).OnComplete(() => Destroy(burstObj));
        }

        if (comboCount >= 3)
        {
            SpawnShockwave(position);
        }

        int sparkCount = Mathf.Clamp(3 + comboCount, 4, 12);
        SpawnRadialComboSparks(position, sparkCount);
    }

    private void SpawnRadialComboSparks(Vector3 pos, int count)
    {
        if (hitSparkSprite == null) return;
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + Random.Range(-15f, 15f);
            Vector3 dir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            float dist = Random.Range(0.45f, 1.25f);
            Vector3 target = pos + dir * dist;

            GameObject spark = CreateVfxObject("ComboSpark", pos, hitSparkSprite, 26);
            spark.transform.rotation = Quaternion.Euler(0, 0, angle);
            spark.transform.localScale = Vector3.one * Random.Range(0.6f, 1.0f);

            SpriteRenderer sr = spark.GetComponent<SpriteRenderer>();
            spark.transform.DOMove(target, 0.22f).SetEase(Ease.OutQuad).SetLink(spark);
            spark.transform.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InQuad).SetLink(spark);
            sr.DOFade(0f, 0.22f).SetLink(spark).OnComplete(() => Destroy(spark));
        }
    }

    private GameObject CreateVfxObject(string name, Vector3 pos, Sprite spr, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.sortingOrder = sortingOrder;
        return go;
    }

    private void OnDestroy()
    {
        hitStopTween?.Kill();
    }
}
