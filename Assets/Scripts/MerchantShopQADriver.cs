#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ETAPA 4 - QA em modo play: sobe o jogo real, força o nivel 5 para abrir o
// mercador e valida o redesign da loja (4 cards, sorteio de 2 armas, compra,
// saldo em tempo real e retomada da partida).
public class MerchantShopQADriver : MonoBehaviour
{
    private const string ReportName = "QA_SHOP_PLAYREPORT.txt";
    private const float GlobalTimeout = 150f;

    private static readonly PlayerController.WeaponType[] AllWeapons =
    {
        PlayerController.WeaponType.Standard,
        PlayerController.WeaponType.Dagger,
        PlayerController.WeaponType.Broadsword,
        PlayerController.WeaponType.Rhythmic,
        PlayerController.WeaponType.ShieldSword
    };

    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;
    private float startTime;
    private bool finished;

    private GameManager gm;
    private PlayerController player;

    private void Start()
    {
        startTime = Time.realtimeSinceStartup;
        var legacyQa = FindAnyObjectByType<RuntimeQARunner>();
        if (legacyQa != null)
        {
            legacyQa.StopAllCoroutines();
            legacyQa.enabled = false;
        }
        var enemyQa = FindAnyObjectByType<EnemyPlayQADriver>();
        if (enemyQa != null)
        {
            enemyQa.StopAllCoroutines();
            enemyQa.enabled = false;
        }
        StartCoroutine(Run());
    }

    private void Update()
    {
        if (finished) return;
        if (Time.realtimeSinceStartup - startTime > GlobalTimeout)
        {
            Record(false, "Watchdog", $"suite nao terminou em {GlobalTimeout}s");
            Finish();
        }
    }

    private void Record(bool ok, string name, string details)
    {
        if (ok) passed++;
        else failed++;
        string line = ok ? $"[PASS] {name}: {details}" : $"[FAIL] {name}: {details}";
        results.Add(line);
        if (ok) Debug.Log(line);
        else Debug.LogError(line);
    }

    private bool TimedOut() => Time.realtimeSinceStartup - startTime > GlobalTimeout;

    private IEnumerator Run()
    {
        IEnumerator inner = RunInner();
        while (true)
        {
            object current;
            try
            {
                if (!inner.MoveNext()) yield break;
                current = inner.Current;
            }
            catch (System.Exception e)
            {
                Record(false, "Exception", $"{e.GetType().Name}: {e.Message}");
                Finish();
                yield break;
            }
            yield return current;
        }
    }

    private IEnumerator RunInner()
    {
        gm = GameManager.Instance;
        player = FindAnyObjectByType<PlayerController>();

        if (gm == null || player == null)
        {
            Record(false, "Setup", $"gm={gm != null} player={player != null}");
            Finish();
            yield break;
        }

        gm.StartGame();
        yield return new WaitForSecondsRealtime(0.3f);
        player.SetWeapon(PlayerController.WeaponType.Standard);
        Record(gm.IsGameActive && gm.PlayerLevel == 1, "Setup",
            $"active={gm.IsGameActive} level={gm.PlayerLevel} arma={player.CurrentWeapon}");
        Diag("inicio");

        // ===== Sobe para o nivel 5: o mercador deve abrir e pausar o jogo =====
        LevelUpTo(5);
        float t0 = Time.realtimeSinceStartup;
        while (!gm.IsShopOpen && Time.realtimeSinceStartup - t0 < 3f)
        {
            if (TimedOut()) { Finish(); yield break; }
            yield return null;
        }
        Record(gm.IsShopOpen && gm.PlayerLevel == 5, "Loja_AbreNv5",
            $"open={gm.IsShopOpen} level={gm.PlayerLevel}");
        Record(Time.timeScale == 0f, "Loja_PausaJogo", $"scale={Time.timeScale:F2}");
        Diag("loja-aberta");

        var shop = GameObject.Find("Merchant");
        var card = shop != null ? shop.transform.Find("MerchantCard") : null;
        Record(shop != null && card != null, "Loja_PainelCentral",
            shop != null ? (card != null ? "Merchant + MerchantCard ok" : "sem MerchantCard") : "sem painel Merchant");

        var title = card != null ? card.Find("Title")?.GetComponent<TextMeshProUGUI>() : null;
        Record(title != null && title.text == "MERCADOR", "Loja_Titulo",
            title != null ? $"texto={title.text}" : "titulo nao encontrado");

        var coinsTmp = card != null ? card.Find("ShopCoins")?.GetComponent<TextMeshProUGUI>() : null;
        bool coinsGold = coinsTmp != null
            && Mathf.Abs(coinsTmp.color.r - 1f) < 0.05f
            && Mathf.Abs(coinsTmp.color.g - 0.85f) < 0.07f
            && coinsTmp.color.b < 0.35f;
        Record(coinsTmp != null && coinsTmp.text.StartsWith("MOEDAS:") && coinsGold, "Loja_SaldoDourado",
            coinsTmp != null ? $"texto={coinsTmp.text} cor={coinsTmp.color}" : "saldo nao encontrado");

        var subtitle = card != null ? card.Find("Subtitle")?.GetComponent<TextMeshProUGUI>() : null;
        Record(subtitle != null && !string.IsNullOrWhiteSpace(subtitle.text), "Loja_Subtitulo",
            subtitle != null ? $"texto={subtitle.text}" : "subtitulo nao encontrado");

        int cardCount = 0;
        string cardNames = "";
        if (card != null)
        {
            foreach (Transform c in card)
            {
                if (c.name.StartsWith("Card_"))
                {
                    cardCount++;
                    cardNames = string.IsNullOrEmpty(cardNames) ? c.name : cardNames + " | " + c.name;
                }
            }
        }
        Record(cardCount == 4, "Loja_4Cards", $"cards={cardCount} ({cardNames})");

        // ===== Sorteio: 2 armas distintas, nenhuma igual a equipada =====
        var weapons = GetField(gm, "shopWeapons") as PlayerController.WeaponType[];
        bool rollOk = weapons != null && weapons.Length == 2
            && weapons[0] != weapons[1]
            && weapons[0] != player.CurrentWeapon
            && weapons[1] != player.CurrentWeapon
            && System.Array.IndexOf(AllWeapons, weapons[0]) >= 0
            && System.Array.IndexOf(AllWeapons, weapons[1]) >= 0;
        Record(rollOk, "Loja_2Armas_Sorteio",
            weapons != null
                ? $"{WeaponName(weapons[0])} + {WeaponName(weapons[1])} (equipada: {WeaponName(player.CurrentWeapon)})"
                : "shopWeapons ausente");

        var prices = GetField(gm, "shopPrices") as int[];
        bool pricesOk = prices != null && prices.Length == 4
            && prices[0] == 25 && prices[1] == 25 && prices[2] == 35 && prices[3] == 20;
        Record(pricesOk, "Loja_Precos",
            prices != null ? $"[{string.Join(",", prices)}] (esperado 25,25,35,20)" : "shopPrices ausente");

        var buttons = GetField(gm, "shopButtons") as Button[];
        Record(buttons != null && buttons.Length == 4, "Loja_4Botoes",
            buttons != null ? $"botoes={buttons.Length}" : "shopButtons ausente");

        var exitBtn = card != null ? card.Find("CloseBtn")?.GetComponent<Button>() : null;
        var exitLbl = exitBtn != null ? exitBtn.GetComponentInChildren<TextMeshProUGUI>() : null;
        Record(exitLbl != null && exitLbl.text == "SAIR E CONTINUAR", "Loja_BotaoSair",
            exitLbl != null ? $"texto={exitLbl.text}" : "botao de saida nao encontrado");

        // ===== Pocao desabilitada com HP cheio =====
        if (buttons != null && buttons.Length == 4)
        {
            bool hpFull = player.CurrentHearts >= player.MaxHearts;
            Record(hpFull && !buttons[3].interactable, "Loja_Pocao_HPcheio",
                $"hp={player.CurrentHearts:F1}/{player.MaxHearts} interactable={buttons[3].interactable}");
        }
        else Record(false, "Loja_Pocao_HPcheio", "sem botoes");

        // ===== Compra: da moedas, compra a arma do slot 0 e confere efeitos =====
        int coinsBefore = gm.Coins;
        gm.AddCoin(100);
        Record(gm.Coins >= coinsBefore + 100, "Loja_Enriquece", $"moedas {coinsBefore} -> {gm.Coins}");
        Record(coinsTmp != null && coinsTmp.text == $"MOEDAS: {gm.Coins}", "Loja_Saldo_AposMoedas",
            coinsTmp != null ? $"texto={coinsTmp.text} esperado=MOEDAS: {gm.Coins}" : "saldo nao encontrado");

        if (buttons != null && buttons.Length == 4 && prices != null && weapons != null)
        {
            bool wasEnabled = buttons[0].interactable;
            int coinsPre = gm.Coins;
            PlayerController.WeaponType weaponBefore = player.CurrentWeapon;
            buttons[0].onClick.Invoke();
            yield return null;

            int price0 = prices[0];
            bool coinsSpent = gm.Coins == coinsPre - price0;
            var lbl0 = buttons[0].GetComponentInChildren<TextMeshProUGUI>();
            bool markedBought = !buttons[0].interactable && lbl0 != null && lbl0.text == "COMPRADO";
            var img0 = buttons[0].GetComponent<Image>();
            bool grey = img0 != null && img0.color.r < 0.4f && img0.color.g < 0.4f && img0.color.b < 0.45f;
            Record(wasEnabled && coinsSpent && markedBought && grey, "Loja_Compra",
                $"preco={price0} moedas {coinsPre} -> {gm.Coins} label={(lbl0 != null ? lbl0.text : "?")} " +
                $"interactable={buttons[0].interactable} cor={(img0 != null ? img0.color.ToString() : "?")}");

            Record(coinsTmp != null && coinsTmp.text == $"MOEDAS: {gm.Coins}", "Loja_SaldoAtualizadoNaHora",
                coinsTmp != null ? $"texto={coinsTmp.text} esperado=MOEDAS: {gm.Coins}" : "saldo nao encontrado");

            Record(player.CurrentWeapon == weapons[0] && player.CurrentWeapon != weaponBefore,
                "Loja_ArmaEquipadaAoComprar",
                $"arma {WeaponName(weaponBefore)} -> {WeaponName(player.CurrentWeapon)} " +
                $"(esperado {WeaponName(weapons[0])})");

            Record(!buttons[1].interactable, "Loja_OutraArmaBloqueada",
                $"slot1 interactable={buttons[1].interactable}");
        }
        else Record(false, "Loja_Compra", "botoes/precos/armas indisponiveis");

        // ===== Sair e continuar: fecha a loja e retoma o jogo =====
        if (exitBtn != null)
        {
            exitBtn.onClick.Invoke();
            yield return null;
        }
        bool closed = !gm.IsShopOpen && GameObject.Find("Merchant") == null;
        Record(closed && Time.timeScale == 1f && gm.IsGameActive, "Loja_SairEContinuar",
            $"open={gm.IsShopOpen} scale={Time.timeScale:F2} active={gm.IsGameActive}");

        yield return new WaitForSecondsRealtime(1f);
        Record(gm.IsGameActive && !gm.IsShopOpen && Time.timeScale == 1f && gm.PlayerLevel == 5,
            "Jogo_RetomaAposLoja",
            $"active={gm.IsGameActive} open={gm.IsShopOpen} scale={Time.timeScale:F2} level={gm.PlayerLevel}");
        Diag("fim");

        Finish();
    }

    private void Diag(string phase)
    {
        Debug.Log($"[QA-Shop-Diag] {phase} t={Time.realtimeSinceStartup - startTime:F1}s scale={Time.timeScale:F2} " +
                  $"active={(gm != null && gm.IsGameActive)} shop={(gm != null && gm.IsShopOpen)} " +
                  $"lvl={(gm != null ? gm.PlayerLevel : -1)} coins={(gm != null ? gm.Coins : -1)} " +
                  $"hearts={(player != null ? player.CurrentHearts : -1f):F1} arma={(player != null ? player.CurrentWeapon.ToString() : "?")}");
    }

    private void LevelUpTo(int target)
    {
        int guard = 0;
        while (gm.PlayerLevel < target && guard++ < 16)
        {
            int need = gm.XpToNextLevel - gm.XP;
            gm.AddXp(need > 0 ? need : 1);
        }
    }

    private static string WeaponName(PlayerController.WeaponType weapon)
    {
        switch (weapon)
        {
            case PlayerController.WeaponType.Dagger: return "ADAGA VELOZ";
            case PlayerController.WeaponType.Broadsword: return "LAMINA REAL";
            case PlayerController.WeaponType.Rhythmic: return "ARMA RITMICA";
            case PlayerController.WeaponType.ShieldSword: return "ESPADA E ESCUDO";
            default: return "ESPADA PADRAO";
        }
    }

    private static object GetField(object target, string field)
    {
        FieldInfo fi = typeof(GameManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        return fi?.GetValue(target);
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;

        var sb = new StringBuilder();
        sb.AppendLine("QA MERCHANT SHOP - PLAY MODE (ETAPA 4)");
        sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        foreach (var line in results) sb.AppendLine(line);
        sb.AppendLine($"SUMMARY: PASS={passed} FAIL={failed} TOTAL={passed + failed}");

        string report = Application.dataPath + "/" + ReportName;
        File.WriteAllText(report, sb.ToString());
        if (failed == 0) Debug.Log($"QA SHOP OK: PASS={passed} FAIL={failed}\n{report}");
        else Debug.LogError($"QA SHOP FALHOU: PASS={passed} FAIL={failed}\n{report}");

        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
#endif
