using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private float comboDuration = 3.2f;
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
    public float DifficultyMultiplier => 1.0f + Mathf.Min(3.5f, (PlayerLevel - 1) * 0.45f + GameTime / 80.0f);

    // XP / Nivel: matar inimigos da XP; acumular sobe de nivel.
    public int PlayerLevel { get; private set; } = 1;
    public int XP { get; private set; }
    private int merchantServedLevel;
    public int XpToNextLevel => 35 + (PlayerLevel - 1) * 25;

    // HUD criado em tempo de execucao (nivel, barra de XP, barra de HP, escudo).
    private TextMeshProUGUI levelText;
    private Image xpBarFill;
    private Image hpBarFill;
    private TextMeshProUGUI armorText;

    // Mercador (aparece a cada 5 niveis): painel de loja com 4 itens,
    // cada um compravel apenas 1 vez por aparicao.
    private GameObject shopPanel;
    private TextMeshProUGUI shopCoinsText;
    private Button[] shopButtons;
    private int[] shopPrices;
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
        if (p != null) UpdateArmorHUD(p.HasArmor);
        UpdateLevelHUD();
        UpdateHeartsHUD(p != null ? p.MaxHearts : 5);
        UpdateScoreHUD();
        UpdateCoinsHUD();
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

    private void FloatText(Vector3 pos, string text, Color col, float size)
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

        armorText = RuntimeUIFactory.CreateText("ArmorText", canvas.transform, "", 14f, new Color(0.9f, 0.9f, 0.5f));
        var art = (RectTransform)armorText.transform;
        art.anchorMin = art.anchorMax = new Vector2(0f, 1f);
        art.pivot = new Vector2(0f, 1f);
        art.anchoredPosition = new Vector2(20f, -82f);

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
        UpdateArmorHUD(false);
        UpdateHeartsHUD(5f);
    }

    private void UpdateLevelHUD()
    {
        if (levelText != null) levelText.text = $"NIVEL {PlayerLevel}";
        if (xpBarFill != null)
        {
            xpBarFill.fillAmount = Mathf.Clamp01((float)XP / Mathf.Max(1, XpToNextLevel));
        }
    }

    public void UpdateArmorHUD(bool hasArmor)
    {
        if (armorText == null) return;
        armorText.text = hasArmor ? "ESCUDO ATIVO (+1)" : "ESCUDO: —";
        armorText.color = hasArmor ? new Color(1f, 0.85f, 0.3f) : new Color(0.55f, 0.55f, 0.55f);
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
        }
        if (restoreTime) Time.timeScale = 1f;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        if (go.GetComponent<InputSystemUIInputModule>() == null) go.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildShopUI()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        shopPanel = RuntimeUIFactory.CreateRect("Merchant", canvas.transform, Vector2.zero, new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.75f));
        var prt = (RectTransform)shopPanel.transform;
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        var card = RuntimeUIFactory.CreateRect("MerchantCard", shopPanel.transform, Vector2.zero, new Vector2(620f, 500f), new Color(0.1f, 0.09f, 0.12f, 0.98f));
        card.transform.localScale = Vector3.zero;
        card.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);

        var title = RuntimeUIFactory.CreateText("Title", card.transform, "MERCADOR", 30f, new Color(1f, 0.85f, 0.2f));
        ((RectTransform)title.transform).anchoredPosition = new Vector2(0f, 200f);

        shopCoinsText = RuntimeUIFactory.CreateText("ShopCoins", card.transform, $"MOEDAS: {Coins}", 28f, Color.white);
        ((RectTransform)shopCoinsText.transform).anchoredPosition = new Vector2(0f, 158f);

        Button dagBtn = AddShopItem(card.transform, "ADAGA VELOZ", "- arma curta:\ncavaleiro mais rapido", "25", 116f, BuyDagger);
        Button broadBtn = AddShopItem(card.transform, "LAMINA REAL", "- arma longa:\nalcance maior, dificil ser atingido", "25", 42f, BuyBroadsword);
        Button armorBtn = AddShopItem(card.transform, "ARMADURA", "- escudo de 1 golpe\n(nao regenera; comprar de novo recarrega)", "35", -42f, BuyArmor);
        Button potionBtn = AddShopItem(card.transform, "POCAO DE CURA", "- recupera 50% do HP total", "20", -116f, BuyPotion);

        shopButtons = new[] { dagBtn, broadBtn, armorBtn, potionBtn };
        shopPrices = new[] { 25, 25, 35, 20 };
        shopWeaponChosen = false;

        Button closeBtn = RuntimeUIFactory.CreateButton("CloseBtn", card.transform, "FECHAR", () => CloseMerchant(), new Color(0.65f, 0.2f, 0.2f), Color.white);
        var closeRt = (RectTransform)closeBtn.transform;
        closeRt.anchoredPosition = new Vector2(0f, -190f);
        closeRt.sizeDelta = new Vector2(150f, 44f);
        var closeLbl = closeBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (closeLbl != null)
        {
            closeLbl.fontSize = 22f;
            ((RectTransform)closeLbl.transform).sizeDelta = new Vector2(150f, 44f);
        }

        RefreshShopItems();
    }

    private Button AddShopItem(Transform card, string name, string desc, string price, float yOffset, System.Action onBuy)
    {
        var row = RuntimeUIFactory.CreateRect("Row" + name, card, new Vector2(0f, yOffset), new Vector2(560f, 68f), new Color(0.18f, 0.17f, 0.2f, 0.9f));

        var nameTmp = RuntimeUIFactory.CreateText("Name", row.transform, name, 20f, new Color(1f, 0.9f, 0.3f));
        nameTmp.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        ((RectTransform)nameTmp.transform).anchoredPosition = new Vector2(-120f, 15f);
        ((RectTransform)nameTmp.transform).sizeDelta = new Vector2(300f, 26f);

        var descTmp = RuntimeUIFactory.CreateText("Desc", row.transform, desc, 16f, new Color(0.85f, 0.85f, 0.85f));
        descTmp.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        descTmp.enableWordWrapping = true;
        ((RectTransform)descTmp.transform).anchoredPosition = new Vector2(-110f, -14f);
        ((RectTransform)descTmp.transform).sizeDelta = new Vector2(300f, 36f);

        var priceTmp = RuntimeUIFactory.CreateText("Price", row.transform, price, 26f, Color.white);
        ((RectTransform)priceTmp.transform).anchoredPosition = new Vector2(95f, 0f);

        var btn = RuntimeUIFactory.CreateButton("BuyBtn", row.transform, "COMPRAR", onBuy, new Color(0.2f, 0.55f, 0.25f), Color.white);
        var brt = (RectTransform)btn.transform;
        brt.anchoredPosition = new Vector2(225f, 0f);
        brt.sizeDelta = new Vector2(110f, 46f);
        var buyLbl = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (buyLbl != null)
        {
            buyLbl.fontSize = 20f;
            ((RectTransform)buyLbl.transform).sizeDelta = new Vector2(110f, 46f);
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

    private void BuyDagger()
    {
        if (!TrySpend(25)) return;
        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        cachedPlayer?.SetWeapon(PlayerController.WeaponType.Dagger);
        shopWeaponChosen = true;
        MarkBought(shopButtons, 0);
        RefreshShopItems();
    }

    private void BuyBroadsword()
    {
        if (!TrySpend(25)) return;
        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        cachedPlayer?.SetWeapon(PlayerController.WeaponType.Broadsword);
        shopWeaponChosen = true;
        MarkBought(shopButtons, 1);
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
        buttons[index].interactable = false;
        var img = buttons[index].GetComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.28f);
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
            bool affordable = Coins >= shopPrices[i];
            bool bought = !shopButtons[i].interactable;
            bool blocked = (i == 0 || i == 1) ? shopWeaponChosen : (i == 3 ? !potionNeeded : false);
            bool enabled = affordable && !bought && !blocked;
            shopButtons[i].interactable = enabled;
            if (!bought)
            {
                var img = shopButtons[i].GetComponent<Image>();
                img.color = enabled ? new Color(0.2f, 0.55f, 0.25f) : new Color(0.3f, 0.3f, 0.32f);
            }
        }
    }

    public void UpdateWeaponHUD(PlayerController.WeaponType weapon)
    {
        weaponText.text = (weapon == PlayerController.WeaponType.Broadsword) ? "LAMINA REAL" : "ADAGA CARMESIM";
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
                if (Camera.main != null) Camera.main.transform.position = new Vector3(0f, 0f, -10f);
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
