using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // Janela de combo (ETAPA 2): 3.2s no padrao; a Arma Ritmica estende para
    // 4.5s avisando via SetComboDuration (chamado pelo PlayerController).
    public const float DefaultComboDuration = 3.2f;
    public const float ExtendedComboDuration = 4.5f;
    [SerializeField] private float comboDuration = DefaultComboDuration;
    public int Score { get; private set; }
    public int HighScore { get; private set; }
    public int Coins { get; private set; }
    public int Kills { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }
    private float comboTimeLeft;

    public float GameTime { get; private set; }
    // Dificuldade sobe conforme o NIVEL do jogador (e um pouco com o tempo),
    // tornando o jogo mais dificil a cada level up.
    public float DifficultyMultiplier =>
        1.0f + Mathf.Min(3.0f, (PlayerLevel - 1) * 0.35f + GameTime / 120.0f);

    // XP / Nivel: matar inimigos da XP; acumular sobe de nivel.
    public int PlayerLevel { get; private set; } = 1;
    public int XP { get; private set; }
    private int merchantServedLevel;
    public int XpToNextLevel => 55 + (PlayerLevel - 1) * 35;

    // HUD criado em tempo de execucao (nivel, barra de XP, barra de HP, escudo).
    private TextMeshProUGUI levelText;
    private Image xpBarFill;
    private TextMeshProUGUI xpNumericText;
    private Image hpBarFill;
    private TextMeshProUGUI armorText;
    private GameObject armorBarRoot;
    private Image armorBarFill;

    // Mercador (aparece a cada 5 niveis): painel de loja com 4 cards
    // (2 armas sorteadas + armadura + pocao), cada um compravel 1 vez.
    private GameObject shopPanel;
    private TextMeshProUGUI shopCoinsText;
    private Button[] shopButtons;
    private int[] shopPrices;
    private bool[] shopBought;
    private PlayerController.WeaponType[] shopWeapons;
    private bool shopWeaponChosen;

    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private GameObject floatingTextPrefab;

    [SerializeField] private Image[] heartIcons;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private Image comboFillBar;
    [SerializeField] private TextMeshProUGUI weaponText;

    [SerializeField] private GameObject startPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI finalHighScoreText;
    [SerializeField] private TextMeshProUGUI newRecordNoticeText;
    [SerializeField] private TextMeshProUGUI finalMaxComboText;
    [SerializeField] private TextMeshProUGUI finalCoinsText;
    [SerializeField] private TextMeshProUGUI finalKillsText;
    [SerializeField] private TextMeshProUGUI muteButtonText;

    private static bool startDirectlyOnReload;

    public enum SoundType { Jump, Slash, Kill, Coin, Hurt, Swap, Combo }
    public bool IsGameActive { get; private set; }
    public bool IsPaused => pausePanel != null && pausePanel.activeSelf;
    public bool IsShopOpen => shopPanel != null;
    private bool isMuted = false;
    private SimpleAudio cachedAudio;
    private PlayerController cachedPlayer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Debug.unityLogger.logEnabled = Application.isEditor || Debug.isDebugBuild;
        Application.runInBackground = true;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        DOTween.Init();
        HighScore = PlayerPrefs.GetInt("ComboKnight_HighScore", 0);
    }

    private void Start()
    {
        cachedAudio = FindAnyObjectByType<SimpleAudio>();
        cachedPlayer = FindAnyObjectByType<PlayerController>();
        BuildRuntimeHud();

        if (startDirectlyOnReload)
        {
            startDirectlyOnReload = false;
            if (startPanel != null) startPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            StartGame();
        }
        else
        {
            Time.timeScale = 0f;
            if (startPanel != null) ShowModal(startPanel);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }

        UpdateMuteButtonText();
    }

    private void Update()
    {
        if (!IsGameActive) return;

        GameTime += Time.deltaTime;

        if (Combo > 0)
        {
            comboTimeLeft -= Time.deltaTime;
            if (comboFillBar != null)
            {
                comboFillBar.fillAmount = Mathf.Max(0f, comboTimeLeft / comboDuration);
                comboFillBar.color = (comboTimeLeft < 1.0f && Mathf.Sin(Time.time * 25f) > 0f) ? Color.red : Color.white;
            }
            if (comboText != null) comboText.text = $"COMBO x{Combo}";

            if (comboTimeLeft <= 0f) ResetCombo();
        }
        else
        {
            if (comboFillBar != null)
            {
                comboFillBar.fillAmount = 0f;
                comboFillBar.color = Color.white;
            }
            if (comboText != null) comboText.text = "COMBO x0";
        }
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        IsGameActive = true;
        if (startPanel != null) startPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        var p = FindAnyObjectByType<PlayerController>();
        p?.ResetHealth();
        // (Mercador) novo jogo comeca do nivel 1 e sem loja aberta.
        PlayerLevel = 1;
        XP = 0;
        merchantServedLevel = 0;
        if (p != null)
            UpdateArmorHUD(p.CurrentArmor, p.MaxArmor);
        UpdateLevelHUD();
        UpdateHeartsHUD(p != null ? p.MaxHearts : 5);
        UpdateScoreHUD();
        UpdateCoinsHUD();
        // HUD da arma acompanha a arma atual desde o inicio da partida.
        if (p != null) UpdateWeaponHUD(p.CurrentWeapon);
    }

    public void RegisterKill(int baseScore, Vector3 pos, bool isBat)
    {
        Kills++;
        Combo++;
        if (Combo > MaxCombo) MaxCombo = Combo;
        comboTimeLeft = comboDuration;

        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();

        bool isCrit = (!isBat && cachedPlayer != null && cachedPlayer.CurrentWeapon == PlayerController.WeaponType.Dagger)
                   || (isBat && cachedPlayer != null && cachedPlayer.CurrentWeapon == PlayerController.WeaponType.Broadsword);

        int weaponMultiplier = isCrit ? 2 : 1;
        int earnedScore = baseScore * weaponMultiplier * Mathf.Max(1, Combo);
        Score += earnedScore;
        UpdateScoreHUD();

        float comboShakeIntensity = (Combo >= 10) ? 0.65f : (Combo >= 5) ? 0.45f : (isCrit ? 0.35f : ((Combo >= 2) ? 0.28f : 0.16f));
        float comboShakeDuration = (Combo >= 10) ? 0.35f : (Combo >= 5) ? 0.25f : 0.16f;
        TriggerScreenShake(comboShakeDuration, comboShakeIntensity);

        if (comboText != null)
        {
            comboText.transform.DOKill();
            comboText.transform.localScale = Vector3.one;
            comboText.transform.rotation = Quaternion.identity;
            float punch = (Combo >= 10) ? 0.7f : (Combo >= 5) ? 0.5f : 0.35f;
            comboText.transform.DOPunchScale(Vector3.one * punch, 0.22f, 10, 1f).SetLink(comboText.gameObject);
            if (Combo >= 5)
            {
                float rotKick = (Combo % 2 == 0) ? 10f : -10f;
                comboText.transform.DOPunchRotation(new Vector3(0, 0, rotKick), 0.22f, 6, 0.6f).SetLink(comboText.gameObject);
            }
        }

        if (comboFillBar != null && comboFillBar.transform.parent != null)
        {
            comboFillBar.transform.parent.DOKill();
            comboFillBar.transform.parent.localScale = Vector3.one;
            comboFillBar.transform.parent.DOPunchScale(new Vector3(0.18f, 0.3f, 0f), 0.2f, 8, 0.6f).SetLink(comboFillBar.transform.parent.gameObject);
        }

        if (floatingTextPrefab != null)
        {
            GameObject popup = Instantiate(floatingTextPrefab, pos + Vector3.up * 0.5f, Quaternion.identity);
            Color col = isCrit ? new Color(1f, 0.85f, 0.1f) : ((Combo >= 10) ? new Color(1f, 0.15f, 0.6f) : (Combo >= 5) ? new Color(1f, 0.65f, 0f) : (Combo >= 2) ? new Color(1f, 0.9f, 0.1f) : Color.white);
            string prefix = isCrit ? (isBat ? "CORTE AEREO 2x! " : "PERFURAR 2x! ") : "";
            string text = (Combo >= 10) ? $"{prefix}MEGA x{Combo}! +{earnedScore}" : (Combo >= 5) ? $"{prefix}COMBO x{Combo}! +{earnedScore}" : (isCrit ? $"{prefix}+{earnedScore}" : $"+{earnedScore}");
            popup.GetComponent<FloatingText>()?.Setup(text, col, isCrit ? 4.8f : 4f);
        }

        float risingPitch = 1f + Mathf.Min(0.6f, Combo * 0.05f);
        if (isCrit || Combo >= 2)
        {
            PlaySound(SoundType.Combo, risingPitch * (isCrit ? 1.08f : 1f));
        }
        else
        {
            PlaySound(SoundType.Kill, risingPitch);
        }

        VFXManager.Instance?.SpawnComboBurst(pos, Combo);

        if (isCrit || Combo >= 3) VibrateOnce();

        if (isCrit || Combo >= 2)
        {
            float freezeDuration = (Combo >= 10) ? 0.085f : (Combo >= 5) ? 0.060f : (Combo >= 3) ? 0.045f : 0.035f;
            VFXManager.Instance?.HitStop(freezeDuration);
        }

        // MOEDAS: cada moeda coletada tambem CURA, por porcentagem (4%-10% do HP).
        // MOEDAS: todo abate mostra moedinhas caindo no chao. Nem todas curam:
        // a moeda de CURA e 75% rara (25% de chance) e cura 4%-10% do HP.
        int coinCount = 1;
        if (Combo >= 3 && Random.value < 0.45f) coinCount++;
        if (Combo >= 6 && Random.value < 0.45f) coinCount++;
        coinCount = Mathf.Min(coinCount, 3);
        if (coinPrefab != null)
        {
            for (int i = 0; i < coinCount; i++)
            {
                // Moeda nasce no ponto do abate, um pouco acima do chao para nao
                // entrar no piso; cai e fica visivel no chao.
                Vector2 spawnPos = (Vector2)pos + Vector2.up * 0.35f;
                if (spawnPos.y < -3.55f) spawnPos.y = -3.55f;
                var coinObj = Instantiate(coinPrefab, spawnPos, Quaternion.identity);
                var coinCmp = coinObj.GetComponent<Coin>();
                if (coinCmp != null) coinCmp.Configure(Random.value < 0.25f);
            }
        }
    }

    public void ResetCombo()
    {
        Combo = 0;
        comboTimeLeft = 0f;
        if (comboFillBar != null) comboFillBar.fillAmount = 0f;
        if (comboText != null) comboText.text = "COMBO x0";
    }

    public void AddCoin(int amount, bool heals = false)
    {
        Coins += amount;
        Score += 50;
        UpdateCoinsHUD();
        UpdateScoreHUD();
        if (shopCoinsText != null) RuntimeUIFactory.SetText(shopCoinsText, $"MOEDAS: {Coins}");
        // Com a loja aberta, novas moedas podem habilitar itens que antes
        // estavam inalcancaveis: reavalia os botoes na hora.
        if (shopPanel != null) RefreshShopItems();
        if (coinsText != null)
        {
            coinsText.transform.DOKill();
            coinsText.transform.localScale = Vector3.one;
            coinsText.transform.DOPunchScale(Vector3.one * 0.35f, 0.2f, 8, 0.5f);
        }
        PlaySound(SoundType.Coin);

        // Apenas a moeda de CURA (rara) recupera HP, por porcentagem (4%-10%).
        if (heals)
        {
            if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
            cachedPlayer?.HealFraction(Random.Range(0.04f, 0.10f));
        }
    }

    // Barra de HP em porcentagem (substitui os icones de coracao).
    public void UpdateHeartsHUD(float hearts)
    {
        if (hpBarFill == null) return;
        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        float max = (cachedPlayer != null) ? cachedPlayer.MaxHearts : 5f;
        float ratio = Mathf.Clamp01(hearts / Mathf.Max(1f, max));
        hpBarFill.fillAmount = ratio;
        hpBarFill.color = ratio > 0.5f ? new Color(0.35f, 0.9f, 0.4f)
            : ratio > 0.25f ? new Color(0.95f, 0.8f, 0.25f)
            : new Color(0.9f, 0.3f, 0.3f);
        hpBarFill.transform.DOKill();
        hpBarFill.transform.localScale = Vector3.one;
        hpBarFill.transform.DOPunchScale(Vector3.one * 0.12f, 0.2f);
    }

    private void UpdateScoreHUD()
    {
        if (scoreText != null)
        {
            scoreText.text = $"SCORE {Score:D6}";
            scoreText.transform.DOKill();
            scoreText.transform.localScale = Vector3.one;
            scoreText.transform.DOPunchScale(Vector3.one * 0.12f, 0.14f, 5, 0.5f);
        }
    }

    private void UpdateCoinsHUD()
    {
        if (coinsText != null) coinsText.text = Coins.ToString();
    }

    // ===== XP / Nivel =====
    public void AddXp(int amount)
    {
        if (!IsGameActive || amount <= 0) return;
        XP += amount;
        while (XP >= XpToNextLevel)
        {
            XP -= XpToNextLevel;
            PlayerLevel++;
            OnLevelUp();
        }
        UpdateLevelHUD();
    }

    private void OnLevelUp()
    {
        PlaySound(SoundType.Combo, 1.5f);
        TriggerScreenShake(0.3f, 0.22f);
        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        if (cachedPlayer != null)
            FloatText(cachedPlayer.transform.position + Vector3.up * 1.2f, $"NIVEL {PlayerLevel}!", new Color(1f, 0.85f, 0.2f), 5.2f);

        // Mercador aparece a cada 5 niveis.
        if (PlayerLevel % 5 == 0 && PlayerLevel > merchantServedLevel)
        {
            merchantServedLevel = PlayerLevel;
            OpenMerchant();
        }
    }

    public void FloatText(Vector3 pos, string text, Color col, float size)
    {
        if (floatingTextPrefab == null) return;
        var go = Instantiate(floatingTextPrefab, pos, Quaternion.identity);
        go.GetComponent<FloatingText>()?.Setup(text, col, size);
    }

    // ===== HUD criado em tempo de execucao (nivel, XP, escudo) =====
    private void BuildRuntimeHud()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        levelText = RuntimeUIFactory.CreateText("LevelText", canvas.transform, "NIVEL " + PlayerLevel, 16f, new Color(1f, 0.85f, 0.2f));
        var lrt = (RectTransform)levelText.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = new Vector2(0f, -38f);

        var bg = RuntimeUIFactory.CreateRect("XpBar", canvas.transform, new Vector2(0f, -56f), new Vector2(200f, 8f), new Color(0.12f, 0.12f, 0.14f, 0.9f));
        var brt = (RectTransform)bg.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -56f);

        var fillGo = RuntimeUIFactory.CreateRect("XpFill", bg.transform, Vector2.zero, new Vector2(200f, 8f), new Color(0.3f, 0.85f, 1f));
        xpBarFill = fillGo.GetComponent<Image>();

        // Numero de XP centralizado sobre a barra: branco, negrito e contorno
        // preto fino para garantir contraste em cima do preenchimento azul.
        xpNumericText = RuntimeUIFactory.CreateText("XpNumericText", bg.transform, "", 11f, Color.white);
        xpNumericText.fontStyle = FontStyles.Bold;
        xpNumericText.outlineWidth = 0.12f;
        xpNumericText.outlineColor = Color.black;
        xpNumericText.enableWordWrapping = false;
        var xnt = (RectTransform)xpNumericText.transform;
        xnt.anchorMin = xnt.anchorMax = new Vector2(0.5f, 0.5f);
        xnt.pivot = new Vector2(0.5f, 0.5f);
        xnt.anchoredPosition = Vector2.zero;
        xnt.sizeDelta = new Vector2(200f, 14f);

        armorText = RuntimeUIFactory.CreateText("ArmorText", canvas.transform, "", 12f, new Color(0.302f, 0.651f, 1f));
        var art = (RectTransform)armorText.transform;
        art.anchorMin = art.anchorMax = new Vector2(0f, 1f);
        art.pivot = new Vector2(0f, 1f);
        art.anchoredPosition = new Vector2(20f, -60f);

        // Barra de ARMADURA (ETAPA 6): logo abaixo da barra de HP, 240x8,
        // fundo cinza escuro e fill azul #4da6ff; some quando nao ha armadura.
        armorBarRoot = RuntimeUIFactory.CreateRect("ArmorBar", canvas.transform, new Vector2(20f, -48f), new Vector2(240f, 8f), new Color(0.12f, 0.12f, 0.14f, 0.9f));
        var abrt = (RectTransform)armorBarRoot.transform;
        abrt.anchorMin = abrt.anchorMax = new Vector2(0f, 1f);
        abrt.pivot = new Vector2(0f, 1f);
        abrt.anchoredPosition = new Vector2(20f, -48f);

        var armorFillGo = RuntimeUIFactory.CreateRect("ArmorFill", armorBarRoot.transform, Vector2.zero, new Vector2(240f, 8f), new Color(0.302f, 0.651f, 1f));
        var aft = (RectTransform)armorFillGo.transform;
        aft.anchorMin = Vector2.zero;
        aft.anchorMax = Vector2.one;
        aft.offsetMin = Vector2.zero;
        aft.offsetMax = Vector2.zero;
        armorBarFill = armorFillGo.GetComponent<Image>();
        armorBarFill.type = Image.Type.Filled;
        armorBarFill.fillMethod = Image.FillMethod.Horizontal;

        // Barra de HP (substitui os coracoes): esconde os icones do cenario e
        // cria uma barra preenchida em cima, atualizada por porcentagem.
        if (heartIcons != null)
        {
            foreach (var h in heartIcons)
                if (h != null) h.gameObject.SetActive(false);
        }

        var hpBg = RuntimeUIFactory.CreateRect("HpBar", canvas.transform, new Vector2(20f, -26f), new Vector2(240f, 18f), new Color(0.12f, 0.12f, 0.14f, 0.9f));
        var hrt = (RectTransform)hpBg.transform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
        hrt.pivot = new Vector2(0f, 1f);
        hrt.anchoredPosition = new Vector2(20f, -26f);

        var hpFillGo = RuntimeUIFactory.CreateRect("HpFill", hpBg.transform, Vector2.zero, new Vector2(240f, 18f), new Color(0.35f, 0.9f, 0.4f));
        var hfrt = (RectTransform)hpFillGo.transform;
        hfrt.anchorMin = new Vector2(0f, 1f);
        hfrt.anchorMax = new Vector2(1f, 1f);
        hfrt.pivot = new Vector2(0f, 1f);
        hfrt.offsetMin = new Vector2(0f, -18f);
        hfrt.offsetMax = Vector2.zero;
        hpBarFill = hpFillGo.GetComponent<Image>();
        hpBarFill.type = Image.Type.Filled;
        hpBarFill.fillMethod = Image.FillMethod.Horizontal;

        UpdateLevelHUD();
        UpdateArmorHUD(0f, 100f);
        UpdateHeartsHUD(5f);
    }

    private void UpdateLevelHUD()
    {
        if (levelText != null) levelText.text = $"NIVEL {PlayerLevel}";
        if (xpBarFill != null)
        {
            xpBarFill.fillAmount = (float)XP / XpToNextLevel;
        }
        if (xpNumericText != null)
        {
            int remaining = Mathf.Max(0, XpToNextLevel - XP);
            xpNumericText.text = $"{XP} / {XpToNextLevel} XP  (-{remaining})";
        }
    }

    // Barra de armadura (ETAPA 6): fill azul proporcionail ao sobrevida,
    // barra e texto visiveis apenas enquanto houver armadura.
    public void UpdateArmorHUD(float currentArmor, float maxArmor)
    {
        float ratio = maxArmor > 0f ? Mathf.Clamp01(currentArmor / maxArmor) : 0f;
        bool active = currentArmor > 0f;

        if (armorBarFill != null) armorBarFill.fillAmount = ratio;
        if (armorBarRoot != null) armorBarRoot.SetActive(active);
        if (armorText != null)
        {
            armorText.gameObject.SetActive(active);
            if (active)
            {
                armorText.text = $"ARMADURA {Mathf.CeilToInt(currentArmor)}/{Mathf.CeilToInt(maxArmor)}";
                armorText.color = new Color(0.302f, 0.651f, 1f);
            }
        }
    }

    // ===== Mercador (aparece a cada 5 niveis) =====
    private void OpenMerchant()
    {
        if (!IsGameActive || shopPanel != null) return;
        // Cancela HitStop pendente para que ele nao restaure o tempo com a loja aberta.
        VFXManager.Instance?.CancelHitStop();
        // Pausa ANTES de abrir a loja, para o jogo nunca continuar rodando.
        Time.timeScale = 0f;
        EnsureEventSystem();
        BuildShopUI();
        PlaySound(SoundType.Combo, 1.2f);
    }

    private void CloseMerchant(bool restoreTime = true)
    {
        if (shopPanel != null)
        {
            DOTween.Kill(shopPanel.transform);
            Destroy(shopPanel);
            shopPanel = null;
            shopButtons = null;
            shopPrices = null;
            shopBought = null;
            shopWeapons = null;
        }
        if (restoreTime) Time.timeScale = 1f;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        if (go.GetComponent<InputSystemUIInputModule>() == null) go.AddComponent<InputSystemUIInputModule>();
    }

    private static readonly PlayerController.WeaponType[] AllShopWeapons =
    {
        PlayerController.WeaponType.Standard,
        PlayerController.WeaponType.Dagger,
        PlayerController.WeaponType.Broadsword,
        PlayerController.WeaponType.Rhythmic,
        PlayerController.WeaponType.ShieldSword
    };

    private void BuildShopUI()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // Fundo escuro cobrindo a tela inteira, com fade-in (o jogo ja esta
        // pausado; os tweens usam SetUpdate para rodarem com timeScale 0).
        shopPanel = RuntimeUIFactory.CreateRect("Merchant", canvas.transform, Vector2.zero, new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0f));
        var prt = (RectTransform)shopPanel.transform;
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        var bgImg = shopPanel.GetComponent<Image>();
        bgImg.DOFade(0.85f, 0.35f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(shopPanel);

        // Card central estilizado (entra com scale-out).
        var card = RuntimeUIFactory.CreateRect("MerchantCard", shopPanel.transform, Vector2.zero, new Vector2(700f, 600f), new Color(0.1f, 0.09f, 0.13f, 0.98f));
        card.transform.localScale = Vector3.zero;
        card.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(shopPanel);

        RuntimeUIFactory.CreateRect("CardAccent", card.transform, new Vector2(0f, 297f), new Vector2(700f, 6f), new Color(1f, 0.85f, 0.2f));

        var title = RuntimeUIFactory.CreateText("Title", card.transform, "MERCADOR", 34f, new Color(1f, 0.94f, 0.78f));
        ((RectTransform)title.transform).anchoredPosition = new Vector2(0f, 252f);

        shopCoinsText = RuntimeUIFactory.CreateText("ShopCoins", card.transform, $"MOEDAS: {Coins}", 26f, new Color(1f, 0.85f, 0.2f));
        ((RectTransform)shopCoinsText.transform).anchoredPosition = new Vector2(0f, 210f);

        var subtitle = RuntimeUIFactory.CreateText("Subtitle", card.transform,
            "4 ofertas exclusivas desta visita — cada item pode ser comprado uma vez só.", 15f, new Color(0.72f, 0.72f, 0.78f));
        subtitle.enableWordWrapping = true;
        ((RectTransform)subtitle.transform).anchoredPosition = new Vector2(0f, 176f);
        ((RectTransform)subtitle.transform).sizeDelta = new Vector2(640f, 24f);

        // Sorteio: 2 das 5 armas, nunca a que o jogador ja esta equipando.
        shopWeapons = RollTwoWeapons();
        var weapon0 = shopWeapons[0];
        var weapon1 = shopWeapons[1];
        Color weaponAccent = new Color(0.45f, 0.65f, 1f);
        Color armorAccent = new Color(1f, 0.85f, 0.2f);
        Color potionAccent = new Color(0.4f, 0.95f, 0.55f);

        // Grid 2x2 de cards: 2 armas + armadura + pocao.
        Button w0Btn = AddShopCard(card.transform, new Vector2(-160f, 45f),
            WeaponDisplayName(weapon0), WeaponDescription(weapon0), 25, weaponAccent, () => BuyWeaponSlot(0));
        Button w1Btn = AddShopCard(card.transform, new Vector2(160f, 45f),
            WeaponDisplayName(weapon1), WeaponDescription(weapon1), 25, weaponAccent, () => BuyWeaponSlot(1));
        Button armorBtn = AddShopCard(card.transform, new Vector2(-160f, -115f),
            "ARMADURA", "Barra azul de 100 que absorve dano\nantes do HP (recompra recarrega)", 35, armorAccent, BuyArmor);
        Button potionBtn = AddShopCard(card.transform, new Vector2(160f, -115f),
            "POÇÃO DE CURA", "Recupera 50% do HP total", 20, potionAccent, BuyPotion);

        shopButtons = new[] { w0Btn, w1Btn, armorBtn, potionBtn };
        shopPrices = new[] { 25, 25, 35, 20 };
        shopBought = new bool[4];
        shopWeaponChosen = false;

        Button exitBtn = RuntimeUIFactory.CreateButton("CloseBtn", card.transform, "SAIR E CONTINUAR", () => CloseMerchant(), new Color(0.7f, 0.25f, 0.25f), Color.white);
        var closeRt = (RectTransform)exitBtn.transform;
        closeRt.anchoredPosition = new Vector2(0f, -255f);
        closeRt.sizeDelta = new Vector2(260f, 48f);
        var closeLbl = exitBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (closeLbl != null)
        {
            closeLbl.fontSize = 20f;
            ((RectTransform)closeLbl.transform).sizeDelta = new Vector2(260f, 48f);
        }

        RefreshShopItems();
    }

    // Sorteia 2 armas distintas das 5 possiveis, excluindo a equipada agora.
    private PlayerController.WeaponType[] RollTwoWeapons()
    {
        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        var pool = new List<PlayerController.WeaponType>(AllShopWeapons);
        if (cachedPlayer != null) pool.Remove(cachedPlayer.CurrentWeapon);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }
        return new[] { pool[0], pool[1] };
    }

    private static string WeaponDisplayName(PlayerController.WeaponType weapon)
    {
        switch (weapon)
        {
            case PlayerController.WeaponType.Dagger: return "ADAGA VELOZ";
            case PlayerController.WeaponType.Broadsword: return "LÂMINA REAL";
            case PlayerController.WeaponType.Rhythmic: return "ARMA RÍTMICA";
            case PlayerController.WeaponType.ShieldSword: return "ESPADA E ESCUDO";
            default: return "ESPADA PADRÃO";
        }
    }

    private static string WeaponDescription(PlayerController.WeaponType weapon)
    {
        switch (weapon)
        {
            case PlayerController.WeaponType.Dagger:
                return "Rápida: velocidade 6.6, pulo +15%\ncrítico 2x, +1 moeda por abate";
            case PlayerController.WeaponType.Broadsword:
                return "Alcance maior (hitbox longa)\ncrítico 2x contra morcegos";
            case PlayerController.WeaponType.Rhythmic:
                return "Janela de combo estendida:\na janela de combo dura 4.5s";
            case PlayerController.WeaponType.ShieldSword:
                return "Defensiva: escudo bloqueia\nprojéteis frontais inimigos";
            default:
                return "Equilibrada: velocidade 5.0\nhitbox e alcance padrões";
        }
    }

    // Card de item da loja: nome, descricao de atributos, preco com icone de
    // moeda e botao de comprar. Retorna o botao para o painel gerenciar.
    private Button AddShopCard(Transform card, Vector2 pos, string name, string desc, int price, Color accent, System.Action onBuy)
    {
        var row = RuntimeUIFactory.CreateRect("Card_" + name, card, pos, new Vector2(300f, 150f), new Color(0.16f, 0.15f, 0.19f, 0.97f));
        RuntimeUIFactory.CreateRect("Accent", row.transform, new Vector2(0f, 72f), new Vector2(300f, 6f), accent);

        var nameTmp = RuntimeUIFactory.CreateText("Name", row.transform, name, 20f, new Color(1f, 0.92f, 0.45f));
        nameTmp.alignment = TextAlignmentOptions.MidlineLeft;
        var nrT = (RectTransform)nameTmp.transform;
        nrT.pivot = new Vector2(0f, 0.5f);
        nrT.anchoredPosition = new Vector2(-140f, 48f);
        nrT.sizeDelta = new Vector2(280f, 26f);

        var descTmp = RuntimeUIFactory.CreateText("Desc", row.transform, desc, 14f, new Color(0.78f, 0.78f, 0.85f));
        descTmp.alignment = TextAlignmentOptions.MidlineLeft;
        descTmp.enableWordWrapping = true;
        var drT = (RectTransform)descTmp.transform;
        drT.pivot = new Vector2(0f, 0.5f);
        drT.anchoredPosition = new Vector2(-140f, 0f);
        drT.sizeDelta = new Vector2(280f, 48f);

        var coinIcon = RuntimeUIFactory.CreateRect("CoinIcon", row.transform, new Vector2(-114f, -52f), new Vector2(16f, 16f), new Color(1f, 0.85f, 0.2f));
        coinIcon.GetComponent<Image>().sprite = RuntimeUIFactory.CircleSprite;

        var priceTmp = RuntimeUIFactory.CreateText("Price", row.transform, price.ToString(), 22f, new Color(1f, 0.85f, 0.2f));
        priceTmp.alignment = TextAlignmentOptions.MidlineLeft;
        var prT = (RectTransform)priceTmp.transform;
        prT.pivot = new Vector2(0f, 0.5f);
        prT.anchoredPosition = new Vector2(-102f, -52f);
        prT.sizeDelta = new Vector2(60f, 26f);

        var btn = RuntimeUIFactory.CreateButton("BuyBtn", row.transform, "COMPRAR", onBuy, new Color(0.2f, 0.55f, 0.25f), Color.white);
        var brt = (RectTransform)btn.transform;
        brt.anchoredPosition = new Vector2(80f, -52f);
        brt.sizeDelta = new Vector2(110f, 40f);
        var buyLbl = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (buyLbl != null)
        {
            buyLbl.fontSize = 16f;
            ((RectTransform)buyLbl.transform).sizeDelta = new Vector2(110f, 40f);
        }
        return btn;
    }

    private bool TrySpend(int price)
    {
        if (Coins < price) return false;
        Coins -= price;
        UpdateCoinsHUD();
        if (shopCoinsText != null) RuntimeUIFactory.SetText(shopCoinsText, $"MOEDAS: {Coins}");
        PlaySound(SoundType.Coin);
        return true;
    }

    // Compra uma das armas sorteadas (slot 0 ou 1): paga, equipa, trava a
    // troca de arma e desativa o outro card de arma.
    private void BuyWeaponSlot(int slot)
    {
        if (shopWeapons == null || slot < 0 || slot >= shopWeapons.Length) return;
        if (shopPrices == null || slot >= shopPrices.Length) return;
        if (!TrySpend(shopPrices[slot])) return;
        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        cachedPlayer?.SetWeapon(shopWeapons[slot]);
        shopWeaponChosen = true;
        MarkBought(shopButtons, slot);
        RefreshShopItems();
    }

    private void BuyArmor()
    {
        if (!TrySpend(35)) return;
        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        cachedPlayer?.GrantArmor();
        MarkBought(shopButtons, 2);
        RefreshShopItems();
    }

    private void BuyPotion()
    {
        if (!TrySpend(20)) return;
        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        if (cachedPlayer != null) cachedPlayer.HealFraction(0.5f);
        MarkBought(shopButtons, 3);
        RefreshShopItems();
    }

    private void MarkBought(Button[] buttons, int index)
    {
        if (buttons == null || index < 0 || index >= buttons.Length) return;
        if (shopBought != null && index < shopBought.Length) shopBought[index] = true;
        buttons[index].interactable = false;
        var img = buttons[index].GetComponent<Image>();
        if (img != null) img.color = new Color(0.25f, 0.25f, 0.28f);
        var lbl = buttons[index].GetComponentInChildren<TextMeshProUGUI>();
        if (lbl != null) lbl.text = "COMPRADO";
    }

    // Atualiza quais botoes podem ser comprados (dinheiro, item ja comprado,
    // arma unica ja escolhida, poco inutil se o HP estiver cheio).
    private void RefreshShopItems()
    {
        if (shopButtons == null || shopPrices == null) return;
        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        bool potionNeeded = cachedPlayer == null || cachedPlayer.CurrentHearts < cachedPlayer.MaxHearts;

        for (int i = 0; i < shopButtons.Length; i++)
        {
            bool bought = shopBought != null && i < shopBought.Length && shopBought[i];
            bool affordable = Coins >= shopPrices[i];
            bool blocked = (i == 0 || i == 1) ? shopWeaponChosen : (i == 3 ? !potionNeeded : false);
            bool enabled = affordable && !bought && !blocked;
            shopButtons[i].interactable = enabled;
            if (!bought)
            {
                var img = shopButtons[i].GetComponent<Image>();
                if (img != null) img.color = enabled ? new Color(0.2f, 0.55f, 0.25f) : new Color(0.3f, 0.3f, 0.32f);
            }
        }
    }

    // Permite que a Arma Ritmica (ou qualquer origem) ajuste a janela de
    // combo: 4.5s com a Arma Ritmica ativa, 3.2s nas demais armas.
    public void SetComboDuration(float duration)
    {
        comboDuration = duration;
    }

    public void UpdateWeaponHUD(PlayerController.WeaponType weapon)
    {
        if (weaponText == null) return;
        weaponText.text = WeaponDisplayName(weapon);
        weaponText.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f);
    }

    public void GameOver()
    {
        IsGameActive = false;
        CloseMerchant(false);
        Time.timeScale = 0f;

        bool isNewRecord = Score > HighScore;
        if (isNewRecord)
        {
            HighScore = Score;
            PlayerPrefs.SetInt("ComboKnight_HighScore", HighScore);
            PlayerPrefs.Save();
        }

        if (finalScoreText != null) finalScoreText.text = Score.ToString();
        if (finalHighScoreText != null) finalHighScoreText.text = HighScore.ToString();
        if (newRecordNoticeText != null) newRecordNoticeText.gameObject.SetActive(isNewRecord);

        if (finalMaxComboText != null) finalMaxComboText.text = $"x{MaxCombo}";
        if (finalCoinsText != null) finalCoinsText.text = Coins.ToString();
        if (finalKillsText != null) finalKillsText.text = Kills.ToString();
        if (gameOverPanel != null) ShowModal(gameOverPanel);
    }

    public void TogglePause()
    {
        if (pausePanel == null) return;
        bool isPaused = pausePanel.activeSelf;
        if (!isPaused) ShowModal(pausePanel);
        else pausePanel.SetActive(false);
        Time.timeScale = isPaused ? 1f : 0f;
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        AudioListener.volume = isMuted ? 0f : 1f;
        UpdateMuteButtonText();
    }

    private void UpdateMuteButtonText()
    {
        if (muteButtonText != null) muteButtonText.text = isMuted ? "SOM: MUDO" : "SOM: ATIVO";
    }

    private void ShowModal(GameObject panel)
    {
        panel.SetActive(true);
        panel.transform.localScale = Vector3.zero;
        panel.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void RestartGame()
    {
        startDirectlyOnReload = true;
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public void TriggerScreenShake(float duration, float magnitude)
    {
        if (Camera.main != null)
        {
            Camera.main.DOKill();
            Camera.main.DOShakePosition(duration, magnitude, 14, 90f, true).SetUpdate(true).OnComplete(() =>
            {
                // Restaura o enquadramento ancorado da ResponsiveCamera (o chao
                // continua alinhado a base da tela) em vez de voltar a (0,0,-10),
                // que deslocava a cena verticalmente a cada golpe.
                if (Camera.main != null)
                {
                    var responsive = FindAnyObjectByType<ResponsiveCamera>();
                    if (responsive != null) responsive.AdjustCamera();
                    else Camera.main.transform.position = new Vector3(0f, 0f, -10f);
                }
            });
        }
    }

    public void PlaySound(SoundType type, float pitch = 1f)
    {
        if (cachedAudio == null) cachedAudio = FindAnyObjectByType<SimpleAudio>();
        cachedAudio?.PlaySFX(type, pitch);
    }

    public static void VibrateOnce()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
