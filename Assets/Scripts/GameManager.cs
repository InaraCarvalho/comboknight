using UnityEngine;
using UnityEngine.UI;
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
    public float DifficultyMultiplier => 1.0f + Mathf.Min(2.5f, GameTime / 40.0f);

    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private GameObject floatingTextPrefab;

    [SerializeField] private Image[] heartIcons;
    [SerializeField] private Sprite heartFullSprite;
    [SerializeField] private Sprite heartEmptySprite;
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
    private bool isMuted = false;
    private SimpleAudio cachedAudio;
    private PlayerController cachedPlayer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Debug.unityLogger.logEnabled = false;
        Application.runInBackground = true;
        DOTween.Init();
        HighScore = PlayerPrefs.GetInt("ComboKnight_HighScore", 0);
    }

    private void Start()
    {
        cachedAudio = FindAnyObjectByType<SimpleAudio>();
        cachedPlayer = FindAnyObjectByType<PlayerController>();

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
        UpdateHeartsHUD(5);
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

        if (isCrit || Combo >= 2)
        {
            float freezeDuration = (Combo >= 10) ? 0.085f : (Combo >= 5) ? 0.060f : (Combo >= 3) ? 0.045f : 0.035f;
            VFXManager.Instance?.HitStop(freezeDuration);
        }

        if (cachedPlayer == null) cachedPlayer = FindAnyObjectByType<PlayerController>();
        float daggerBonus = (cachedPlayer != null) ? cachedPlayer.GetCoinBonus() * 0.2f : 0f;
        float dropChance = 0.5f + Mathf.Min(0.4f, Combo * 0.05f) + daggerBonus;

        if (Random.value < dropChance && coinPrefab != null)
        {
            Instantiate(coinPrefab, pos, Quaternion.identity);
        }
    }

    public void ResetCombo()
    {
        Combo = 0;
        comboTimeLeft = 0f;
        if (comboFillBar != null) comboFillBar.fillAmount = 0f;
        if (comboText != null) comboText.text = "COMBO x0";
    }

    public void AddCoin(int amount)
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
    }

    public void UpdateHeartsHUD(int hearts)
    {
        if (heartIcons == null) return;
        for (int i = 0; i < heartIcons.Length; i++)
        {
            if (heartIcons[i] == null) continue;
            bool wasFilled = heartIcons[i].sprite == heartFullSprite;
            heartIcons[i].sprite = (i < hearts) ? heartFullSprite : heartEmptySprite;

            if (wasFilled && i >= hearts)
            {
                heartIcons[i].transform.DOKill();
                heartIcons[i].transform.localScale = Vector3.one;
                heartIcons[i].transform.DOPunchScale(Vector3.one * 0.35f, 0.25f);
            }
        }
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

    public void UpdateWeaponHUD(PlayerController.WeaponType weapon)
    {
        if (weaponText == null) return;
        weaponText.text = (weapon == PlayerController.WeaponType.Broadsword) ? "LAMINA REAL" : "ADAGA CARMESIM";
        weaponText.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f);
    }

    public void GameOver()
    {
        IsGameActive = false;
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
}
