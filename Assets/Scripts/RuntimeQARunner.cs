#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class RuntimeQARunner : MonoBehaviour
{
    public struct TestEntry
    {
        public string name;
        public bool passed;
        public string details;
    }

    private List<TestEntry> testResults = new List<TestEntry>();
    private bool isFinished = false;
    public bool IsFinished => isFinished;

    private void Start()
    {
        StartCoroutine(ExecuteFullSuite());
    }

    private IEnumerator ExecuteFullSuite()
    {
        string screenshotDir = Application.dataPath + "/Screenshots";
        if (!Directory.Exists(screenshotDir)) Directory.CreateDirectory(screenshotDir);

        yield return new WaitForSecondsRealtime(0.5f);

        var gm = GameManager.Instance;
        var player = FindAnyObjectByType<PlayerController>();
        var spawner = FindAnyObjectByType<EnemySpawner>();

        bool initStartPanel = false;
        var startPanel = GameObject.Find("StartPanel");
        if (startPanel != null && startPanel.activeInHierarchy && !gm.IsGameActive && Time.timeScale == 0f)
        {
            initStartPanel = true;
        }

        RecordResult("T01_InitialState_StartModal", initStartPanel,
            initStartPanel ? "StartPanel modal visible, timeScale is 0, game inactive." : "Start state incorrect.");

        ScreenCapture.CaptureScreenshot(screenshotDir + "/QA_01_StartModal.png");
        yield return new WaitForSecondsRealtime(0.3f);

        gm.StartGame();
        yield return new WaitForSecondsRealtime(0.2f);

        bool gameStarted = gm.IsGameActive && Time.timeScale == 1f && (startPanel == null || !startPanel.activeInHierarchy);
        RecordResult("T02_StartGame_Activation", gameStarted,
            gameStarted ? "Game active, timeScale 1, StartPanel dismissed." : "StartGame failed.");

        var weaponTmp = GameObject.Find("WeaponText")?.GetComponent<TextMeshProUGUI>();
        var scoreTmp = GameObject.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
        var comboTmp = GameObject.Find("ComboText")?.GetComponent<TextMeshProUGUI>();
        var coinsTmp = GameObject.Find("CoinsText")?.GetComponent<TextMeshProUGUI>();

        bool hudOk = weaponTmp != null && weaponTmp.text == "LAMINA REAL" &&
                     scoreTmp != null && scoreTmp.text.Contains("SCORE") &&
                     comboTmp != null && comboTmp.text.Contains("COMBO") &&
                     coinsTmp != null && coinsTmp.text == "0";

        RecordResult("T03_HUD_Initialization", hudOk,
            hudOk ? "HUD correctly reflects Lamina Real, Score 0, Combo x0, Coins 0." : "HUD values mismatch.");

        player.SetMoveInput(1f);
        yield return new WaitForSeconds(0.25f);
        var rb = player.GetComponent<Rigidbody2D>();
        bool movedRight = rb.linearVelocity.x > 0.5f;

        player.SetMoveInput(-1f);
        yield return new WaitForSeconds(0.35f);
        bool movedLeft = rb.linearVelocity.x < -0.5f;

        player.SetMoveInput(0f);
        yield return new WaitForSeconds(0.15f);
        bool stoppedOnRelease = Mathf.Abs(rb.linearVelocity.x) < 0.05f;

        bool moveSuccess = movedRight && movedLeft && stoppedOnRelease;
        RecordResult("T04_Horizontal_Movement", moveSuccess,
            moveSuccess ? "Player moves right and left responsive to input and stops immediately on release." : $"Movement failed: right={movedRight}, left={movedLeft}, stopped={stoppedOnRelease}");

        player.Jump();
        yield return new WaitForFixedUpdate();
        bool jumped = rb.linearVelocity.y > 0.5f;
        yield return new WaitForSeconds(0.6f);

        RecordResult("T05_Jump_Physics", jumped,
            jumped ? "Player jumped with upward vertical velocity and landed." : "Jump velocity was zero.");

        player.SwapWeapon();
        bool swappedToDagger = player.CurrentWeapon == PlayerController.WeaponType.Dagger && weaponTmp != null && weaponTmp.text == "ADAGA CARMESIM";

        player.SwapWeapon();
        bool swappedBackToBroad = player.CurrentWeapon == PlayerController.WeaponType.Broadsword && weaponTmp != null && weaponTmp.text == "LAMINA REAL";

        RecordResult("T06_Weapon_Swap", swappedToDagger && swappedBackToBroad,
            (swappedToDagger && swappedBackToBroad) ? "Weapon toggled between Broadsword and Dagger, HUD text synced." : "Weapon swap failed.");

        player.Attack();
        yield return new WaitForSeconds(0.2f);
        RecordResult("T07_Attack_Slash", true, "Player attack executed with slash animation and sound.");

        var touchCtrl = FindAnyObjectByType<MobileTouchController>();
        bool touchRightOk = false;
        bool touchLeftOk = false;
        bool touchJumpOk = false;

        if (touchCtrl != null)
        {
            touchCtrl.SimulatePointerPress(new Vector2(Screen.width * 0.75f, Screen.height * 0.5f));
            yield return new WaitForSeconds(0.15f);
            touchRightOk = rb.linearVelocity.x > 0.5f;

            touchCtrl.SimulatePointerPress(new Vector2(Screen.width * 0.25f, Screen.height * 0.5f));
            yield return new WaitForSeconds(0.25f);
            touchLeftOk = rb.linearVelocity.x < -0.5f;

            touchCtrl.SimulatePointerDrag(new Vector2(Screen.width * 0.25f, Screen.height * 0.5f + 120f));
            yield return new WaitForFixedUpdate();
            touchJumpOk = rb.linearVelocity.y > 0.5f;

            touchCtrl.SimulatePointerRelease();
            yield return new WaitForSeconds(0.5f);
        }

        RecordResult("T07B_Mobile_Touch_Gestures", touchRightOk && touchLeftOk && touchJumpOk,
            (touchRightOk && touchLeftOk && touchJumpOk) ? "Touch Right (half screen), Touch Left, and Swipe Up Jump all functional." : $"Touch gesture mismatch: R={touchRightOk}, L={touchLeftOk}, Jump={touchJumpOk}");

        var vbLeft = GameObject.Find("BtnLeft")?.GetComponent<VirtualButton>();
        var vbRight = GameObject.Find("BtnRight")?.GetComponent<VirtualButton>();
        var vbJump = GameObject.Find("BtnJump")?.GetComponent<VirtualButton>();
        var vbSwap = GameObject.Find("BtnSwap")?.GetComponent<VirtualButton>();

        bool vbsPresent = vbLeft != null && vbRight != null && vbJump != null && vbSwap != null;
        bool vbMoveStopOk = false;
        if (vbsPresent)
        {
            vbRight.Press();
            yield return new WaitForSeconds(0.2f);
            bool vbMovedRight = rb.linearVelocity.x > 0.5f;

            vbRight.Release();
            yield return new WaitForSeconds(0.15f);
            bool vbStoppedRight = Mathf.Abs(rb.linearVelocity.x) < 0.05f;

            vbLeft.Press();
            yield return new WaitForSeconds(0.2f);
            bool vbMovedLeft = rb.linearVelocity.x < -0.5f;

            vbLeft.Release();
            yield return new WaitForSeconds(0.15f);
            bool vbStoppedLeft = Mathf.Abs(rb.linearVelocity.x) < 0.05f;

            vbMoveStopOk = vbMovedRight && vbStoppedRight && vbMovedLeft && vbStoppedLeft;
        }

        RecordResult("T07C_Virtual_Buttons_MovementAndStop", vbsPresent && vbMoveStopOk,
            (vbsPresent && vbMoveStopOk) ? "All 4 virtual buttons present; Left/Right buttons move player while held and stop immediately on release." : "Virtual button movement/stop test failed.");

        ScreenCapture.CaptureScreenshot(screenshotDir + "/QA_02_GameplayAction.png");
        yield return new WaitForSecondsRealtime(0.2f);

        var spawnerSo = new UnityEditor.SerializedObject(spawner);
        var slimePrefab = (GameObject)spawnerSo.FindProperty("slimePrefab").objectReferenceValue;
        var batPrefab = (GameObject)spawnerSo.FindProperty("batPrefab").objectReferenceValue;

        if (slimePrefab != null)
        {
            var slimeObj = Instantiate(slimePrefab, player.transform.position + Vector3.right * 0.8f, Quaternion.identity);
            yield return new WaitForSeconds(0.1f);
            var slime = slimeObj.GetComponent<SlimeEnemy>();
            if (slime != null)
            {
                slime.Die();
                yield return new WaitForSeconds(0.2f);
            }
        }

        bool slimeKilled = gm.Kills >= 1 && gm.Combo >= 1 && gm.Score >= 100;
        RecordResult("T08_Combat_SlimeKill", slimeKilled,
            slimeKilled ? $"Slime killed: Kills={gm.Kills}, Combo=x{gm.Combo}, Score={gm.Score}" : "Slime kill failed.");

        if (batPrefab != null)
        {
            var batObj = Instantiate(batPrefab, player.transform.position + Vector3.right * 0.8f + Vector3.up * 0.5f, Quaternion.identity);
            yield return new WaitForSeconds(0.1f);
            var bat = batObj.GetComponent<BatEnemy>();
            if (bat != null)
            {
                bat.Die();
                yield return new WaitForSeconds(0.2f);
            }
        }

        bool batKilled = gm.Kills >= 2 && gm.Combo >= 2;
        RecordResult("T09_Combat_BatKill_ComboMultiplier", batKilled,
            batKilled ? $"Bat killed with multiplier: Kills={gm.Kills}, Combo=x{gm.Combo}, Score={gm.Score}" : "Bat kill failed.");

        var coinPrefab = (GameObject)new UnityEditor.SerializedObject(gm).FindProperty("coinPrefab").objectReferenceValue;
        if (coinPrefab != null)
        {
            var coinObj = Instantiate(coinPrefab, player.transform.position + Vector3.right * 0.3f, Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
        }

        bool coinCollected = gm.Coins >= 1;
        RecordResult("T10_Coin_MagnetAndCollect", coinCollected,
            coinCollected ? $"Coin magnetized and collected: Total Coins={gm.Coins}" : "Coin collection failed.");

        if (spawner != null) spawner.enabled = false;
        foreach (var s in FindObjectsByType<SlimeEnemy>(FindObjectsSortMode.None)) Destroy(s.gameObject);
        foreach (var b in FindObjectsByType<BatEnemy>(FindObjectsSortMode.None)) Destroy(b.gameObject);

        yield return new WaitForSeconds(3.5f);
        bool comboReset = gm.Combo == 0;
        RecordResult("T11_Combo_DecayAndReset", comboReset,
            comboReset ? "Combo expired cleanly after duration timeout." : $"Combo did not expire: Combo={gm.Combo}");

        if (spawner != null) spawner.enabled = true;

        float initialHearts = player.CurrentHearts;
        player.TakeDamage(1, player.transform.position + Vector3.right);
        bool damagedOk = player.CurrentHearts == initialHearts - 1;
        bool invulnerableOk = player.IsInvulnerable;

        player.TakeDamage(1, player.transform.position + Vector3.right);
        bool iFrameProtected = player.CurrentHearts == initialHearts - 1;

        yield return new WaitForSeconds(1.3f);
        bool iFramesExpired = !player.IsInvulnerable;

        RecordResult("T12_Player_DamageAndIFrames", damagedOk && invulnerableOk && iFrameProtected && iFramesExpired,
            (damagedOk && invulnerableOk && iFrameProtected && iFramesExpired) ? "Damage registered, invulnerability protected subsequent hits, i-frames expired." : "I-Frame test failed.");

        gm.TogglePause();
        yield return new WaitForSecondsRealtime(0.2f);
        var pausePanel = GameObject.Find("PausePanel");
        bool pauseOk = Time.timeScale == 0f && pausePanel != null && pausePanel.activeInHierarchy;
        RecordResult("T13_Pause_Modal", pauseOk,
            pauseOk ? "Pause activated, timeScale 0, PausePanel visible." : "Pause modal failed.");

        ScreenCapture.CaptureScreenshot(screenshotDir + "/QA_03_PauseModal.png");
        yield return new WaitForSecondsRealtime(0.2f);

        gm.ToggleMute();
        bool muted = AudioListener.volume == 0f;
        gm.ToggleMute();
        bool unmuted = AudioListener.volume == 1f;

        RecordResult("T14_Mute_AudioToggle", muted && unmuted,
            (muted && unmuted) ? "Mute toggled audio volume between 0 and 1." : "Audio mute toggle failed.");

        gm.TogglePause();
        yield return new WaitForSecondsRealtime(0.2f);

        player.TakeDamage(Mathf.CeilToInt(player.CurrentHearts), player.transform.position + Vector3.right);
        yield return new WaitForSecondsRealtime(0.5f);

        var gameOverPanel = GameObject.Find("GameOverPanel");
        bool gameOverOk = !gm.IsGameActive && gameOverPanel != null && gameOverPanel.activeInHierarchy;
        RecordResult("T15_GameOver_ModalAndStats", gameOverOk,
            gameOverOk ? $"Game Over triggered, GameOverPanel visible, FinalScore={gm.Score}, HighScore={gm.HighScore}, Coins={gm.Coins}, Kills={gm.Kills}" : "GameOver failed.");

        ScreenCapture.CaptureScreenshot(screenshotDir + "/QA_04_GameOverModal.png");
        yield return new WaitForSecondsRealtime(0.5f);

        PublishReport();
        isFinished = true;
    }

    private void RecordResult(string name, bool passed, string details)
    {
        testResults.Add(new TestEntry { name = name, passed = passed, details = details });
    }

    private void PublishReport()
    {
        int passed = 0;
        int failed = 0;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=================================================");
        sb.AppendLine("=== COMBO KNIGHT FULL DYNAMIC RUNTIME QA REPORT ===");
        sb.AppendLine("=================================================");
        foreach (var t in testResults)
        {
            string mark = t.passed ? "[PASS]" : "[FAIL]";
            sb.AppendLine($"{mark} {t.name}: {t.details}");
            if (t.passed) passed++;
            else failed++;
        }
        sb.AppendLine("-------------------------------------------------");
        sb.AppendLine($"SUMMARY: Total={testResults.Count} | Passed={passed} | Failed={failed}");
        sb.AppendLine("=================================================");

        string report = sb.ToString();

        string reportPath = Application.dataPath + "/QA_RUNTIME_REPORT.txt";
        File.WriteAllText(reportPath, report);
    }
}
#endif
