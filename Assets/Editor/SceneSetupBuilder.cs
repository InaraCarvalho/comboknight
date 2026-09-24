using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using TMPro;
using System.IO;

public static class SceneSetupBuilder
{
    public static void Build()
    {
        string prefabsDir = "Assets/Prefabs";
        if (!Directory.Exists(prefabsDir)) Directory.CreateDirectory(prefabsDir);

        Sprite knightBroadswordSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/knight_broadsword.png");
        Sprite knightDaggerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/knight_dagger.png");
        Sprite knightIdleBroadswordSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/knight_idle_broadsword.png");
        Sprite knightIdleDaggerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/knight_idle_dagger.png");
        Sprite slimeIdleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/slime_idle.png");
        Sprite slimeHopSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/slime_hop.png");
        Sprite batFlyUpSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/bat_fly_up.png");
        Sprite batFlyDownSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/bat_fly_down.png");
        Sprite runnerIdleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Runner_Idle.png");
        Sprite runnerRunSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Runner_run.png");
        Sprite archerIdleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Archer_Idle.png");
        Sprite archerPrepareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Archer_Prepare.png");
        Sprite archerShootSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Archer_Shoot.png");
        Sprite arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Arrow.png");
        Sprite coinGoldSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/coin_gold.png");
        Sprite heartFullSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/heart_full.png");
        Sprite bgLandscapeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/background_arena.png");
        Sprite btnLeftSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_btn_left.png");
        Sprite btnRightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_btn_right.png");
        Sprite btnJumpSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_btn_jump.png");
        Sprite btnSwapSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_btn_swap.png");
        Sprite btnPauseSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_btn_pause.png");
        Sprite clusterBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_thumb_cluster_bg.png");

        Sprite frameGothicSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_frame_gothic.png");
        Sprite cardDarkSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_card_dark.png");
        Sprite btnGoldSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_btn_gold.png");
        Sprite btnBlueSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_btn_blue.png");
        Sprite btnGreenSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_btn_green.png");
        Sprite comboBarFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_combo_bar_frame.png");
        Sprite comboBarFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_combo_bar_fill.png");
        Sprite weaponPlateSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_weapon_plate.png");
        Sprite hudHeaderBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ui_hud_header_bg.png");
        Material hitFlashMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/mat_hit_flash.mat");

        var font = TMP_Settings.defaultFontAsset;
        if (font != null && font.material != null)
        {
            font.material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.0f);
            font.material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.06f);
            font.material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.95f));
            EditorUtility.SetDirty(font.material);
        }

        GameObject floatingTextObj = new GameObject("FloatingText");
        var tmp = floatingTextObj.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 5f;
        tmp.characterSpacing = 2f;
        tmp.color = Color.yellow;
        tmp.sortingOrder = 30;
        var ft = floatingTextObj.AddComponent<FloatingText>();
        var ftSerialized = new SerializedObject(ft);
        ftSerialized.FindProperty("textMesh").objectReferenceValue = tmp;
        ftSerialized.ApplyModifiedProperties();
        GameObject floatingTextPrefab = PrefabUtility.SaveAsPrefabAsset(floatingTextObj, "Assets/Prefabs/FloatingTextPrefab.prefab");
        Object.DestroyImmediate(floatingTextObj);

        GameObject coinObj = new GameObject("Coin");
        coinObj.tag = "Coin";
        coinObj.transform.localScale = new Vector3(2.0f, 2.0f, 1f);
        var coinSr = coinObj.AddComponent<SpriteRenderer>();
        coinSr.sprite = coinGoldSprite;
        coinSr.sortingOrder = 10;
        var coinRb = coinObj.AddComponent<Rigidbody2D>();
        coinRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var triggerCol = coinObj.AddComponent<CircleCollider2D>();
        triggerCol.isTrigger = true;
        triggerCol.radius = 0.22f;
        var bounceCol = coinObj.AddComponent<CircleCollider2D>();
        bounceCol.isTrigger = false;
        bounceCol.radius = 0.15f;
        var coinComp = coinObj.AddComponent<Coin>();
        GameObject coinPrefab = PrefabUtility.SaveAsPrefabAsset(coinObj, "Assets/Prefabs/CoinPrefab.prefab");
        Object.DestroyImmediate(coinObj);

        GameObject slimeObj = new GameObject("Slime");
        slimeObj.tag = "Enemy";
        slimeObj.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
        var slimeSr = slimeObj.AddComponent<SpriteRenderer>();
        slimeSr.sprite = slimeIdleSprite;
        slimeSr.sortingOrder = 4;
        var slimeRb = slimeObj.AddComponent<Rigidbody2D>();
        slimeRb.gravityScale = 0f;
        slimeRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var slimeCol = slimeObj.AddComponent<BoxCollider2D>();
        slimeCol.size = new Vector2(0.44f, 0.32f);
        slimeCol.offset = new Vector2(0f, 0.02f);
        var slimeComp = slimeObj.AddComponent<SlimeEnemy>();
        var slimeSerialized = new SerializedObject(slimeComp);
        slimeSerialized.FindProperty("idleSprite").objectReferenceValue = slimeIdleSprite;
        slimeSerialized.FindProperty("hopSprite").objectReferenceValue = slimeHopSprite;
        slimeSerialized.ApplyModifiedProperties();
        var slimeFlash = slimeObj.AddComponent<HitFlash>();
        var slimeFlashSerialized = new SerializedObject(slimeFlash);
        slimeFlashSerialized.FindProperty("flashMaterial").objectReferenceValue = hitFlashMat;
        slimeFlashSerialized.ApplyModifiedProperties();
        GameObject slimePrefab = PrefabUtility.SaveAsPrefabAsset(slimeObj, "Assets/Prefabs/SlimePrefab.prefab");
        Object.DestroyImmediate(slimeObj);

        GameObject batObj = new GameObject("Bat");
        batObj.tag = "Enemy";
        batObj.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        var batSr = batObj.AddComponent<SpriteRenderer>();
        batSr.sprite = batFlyUpSprite;
        batSr.sortingOrder = 4;
        var batRb = batObj.AddComponent<Rigidbody2D>();
        batRb.gravityScale = 0f;
        batRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        var batCol = batObj.AddComponent<CircleCollider2D>();
        batCol.radius = 0.15f;
        var batComp = batObj.AddComponent<BatEnemy>();
        var batSerialized = new SerializedObject(batComp);
        batSerialized.FindProperty("flyUpSprite").objectReferenceValue = batFlyUpSprite;
        batSerialized.FindProperty("flyDownSprite").objectReferenceValue = batFlyDownSprite;
        batSerialized.ApplyModifiedProperties();
        var batFlash = batObj.AddComponent<HitFlash>();
        var batFlashSerialized = new SerializedObject(batFlash);
        batFlashSerialized.FindProperty("flashMaterial").objectReferenceValue = hitFlashMat;
        batFlashSerialized.ApplyModifiedProperties();
        GameObject batPrefab = PrefabUtility.SaveAsPrefabAsset(batObj, "Assets/Prefabs/BatPrefab.prefab");
        Object.DestroyImmediate(batObj);

        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        var cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 7.0f;
        ColorUtility.TryParseHtmlString("#060814", out Color bgColor);
        cam.backgroundColor = bgColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        camObj.transform.position = new Vector3(0f, 0f, -10f);
        camObj.AddComponent<AudioListener>();

        GameObject bgObj = new GameObject("Background");
        var bgSr = bgObj.AddComponent<SpriteRenderer>();
        bgSr.sprite = bgLandscapeSprite;
        bgSr.sortingOrder = -10;
        bgObj.transform.localScale = new Vector3(2.25f, 2.25f, 1f);
        bgObj.transform.position = new Vector3(0f, -2.25f, 0f);

        var respCam = camObj.AddComponent<ResponsiveCamera>();
        var respSerialized = new SerializedObject(respCam);
        respSerialized.FindProperty("targetHalfWidth").floatValue = 3.95f;
        respSerialized.FindProperty("minOrthoSize").floatValue = 7.0f;
        respSerialized.FindProperty("groundY").floatValue = -4.5f;
        respSerialized.FindProperty("groundScreenFraction").floatValue = 0.35f;
        respSerialized.FindProperty("backgroundTransform").objectReferenceValue = bgObj.transform;
        respSerialized.FindProperty("landscapeBackgroundSprite").objectReferenceValue = bgLandscapeSprite;
        respSerialized.ApplyModifiedProperties();

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0) groundLayer = 0;

        var noFrictionMat = new PhysicsMaterial2D("Frictionless") { friction = 0f, bounciness = 0f };

        GameObject groundObj = new GameObject("Ground");
        groundObj.tag = "Ground";
        groundObj.layer = groundLayer;
        groundObj.transform.position = new Vector3(0f, -4.5f, 0f);
        // A tijoleira usa o sub-sprite Ground_0 (pivo topo-centro) direto no
        // objeto do chao, sem filho: o topo do tijolo coincide com o topo do
        // colisor em -4.5 e acompanha qualquer ajuste manual de altura.
        groundObj.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
        var groundSr = groundObj.AddComponent<SpriteRenderer>();
        groundSr.sprite = LoadSubSprite("Assets/Sprites/Ground.png", "Ground_0");
        groundSr.sortingOrder = 1;
        var groundCol = groundObj.AddComponent<BoxCollider2D>();
        // Largura generosa (±20): o ResponsiveCamera ajusta em runtime para a
        // arena visivel; o valor daqui cobre qualquer resolucao mesmo antes.
        groundCol.size = new Vector2(40f, 12f);
        groundCol.offset = new Vector2(0f, -6f);
        groundCol.sharedMaterial = noFrictionMat;

        GameObject wallLeft = new GameObject("WallLeft");
        wallLeft.transform.position = new Vector3(-3.75f, 0f, 0f);
        var wlCol = wallLeft.AddComponent<BoxCollider2D>();
        wlCol.size = new Vector2(1f, 30f);
        wlCol.offset = new Vector2(-0.5f, 0f);
        wlCol.sharedMaterial = noFrictionMat;

        GameObject wallRight = new GameObject("WallRight");
        wallRight.transform.position = new Vector3(3.75f, 0f, 0f);
        var wrCol = wallRight.AddComponent<BoxCollider2D>();
        wrCol.size = new Vector2(1f, 30f);
        wrCol.offset = new Vector2(0.5f, 0f);
        wrCol.sharedMaterial = noFrictionMat;

        GameObject playerObj = new GameObject("Player");
        playerObj.tag = "Player";
        // Spawn exatamente com os pes na superficie do chao (topo do solo em
        // -4.5) para o cavaleiro nao "cair" nem afundar no primeiro frame.
        playerObj.transform.position = new Vector3(0f, -3.80425f, 0f);
        playerObj.transform.localScale = new Vector3(2.53f, 2.53f, 1f);
        var playerRb = playerObj.AddComponent<Rigidbody2D>();
        playerRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        playerRb.interpolation = RigidbodyInterpolation2D.Interpolate;
        playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerRb.gravityScale = 3.0f;

        var playerCol = playerObj.AddComponent<CapsuleCollider2D>();
        playerCol.size = new Vector2(0.38f, 0.65f);
        playerCol.offset = new Vector2(0f, 0.05f);
        playerCol.direction = CapsuleDirection2D.Vertical;
        playerCol.sharedMaterial = noFrictionMat;

        var playerSr = playerObj.AddComponent<SpriteRenderer>();
        playerSr.sprite = knightIdleBroadswordSprite != null ? knightIdleBroadswordSprite : knightBroadswordSprite;
        playerSr.sortingOrder = 5;

        GameObject groundCheckObj = new GameObject("GroundCheck");
        groundCheckObj.transform.SetParent(playerObj.transform);
        groundCheckObj.transform.localPosition = new Vector3(0f, -0.32f, 0f);

        GameObject weaponTransformObj = new GameObject("WeaponTransform");
        weaponTransformObj.transform.SetParent(playerObj.transform);
        weaponTransformObj.transform.localPosition = Vector3.zero;

        GameObject swordColObj = new GameObject("SwordCollider");
        swordColObj.tag = "Sword";
        swordColObj.transform.SetParent(weaponTransformObj.transform);
        swordColObj.transform.localPosition = Vector3.zero;
        var swordCol = swordColObj.AddComponent<BoxCollider2D>();
        swordCol.isTrigger = true;
        swordCol.size = new Vector2(0.50f, 0.60f);
        swordCol.offset = new Vector2(0.40f, 0.15f);

        var playerCtrl = playerObj.AddComponent<PlayerController>();
        var pcSerialized = new SerializedObject(playerCtrl);
        pcSerialized.FindProperty("groundCheck").objectReferenceValue = groundCheckObj.transform;
        pcSerialized.FindProperty("groundLayer").intValue = 1 << groundLayer;
        pcSerialized.FindProperty("swordCollider").objectReferenceValue = swordCol;
        pcSerialized.FindProperty("bodyRenderer").objectReferenceValue = playerSr;
        pcSerialized.FindProperty("weaponTransform").objectReferenceValue = weaponTransformObj.transform;
        pcSerialized.FindProperty("broadswordSprite").objectReferenceValue = knightBroadswordSprite;
        pcSerialized.FindProperty("daggerSprite").objectReferenceValue = knightDaggerSprite;
        pcSerialized.FindProperty("broadswordIdleSprite").objectReferenceValue = knightIdleBroadswordSprite;
        pcSerialized.FindProperty("daggerIdleSprite").objectReferenceValue = knightIdleDaggerSprite;
        pcSerialized.ApplyModifiedProperties();

        var playerFlash = playerObj.AddComponent<HitFlash>();
        var playerFlashSerialized = new SerializedObject(playerFlash);
        playerFlashSerialized.FindProperty("flashMaterial").objectReferenceValue = hitFlashMat;
        playerFlashSerialized.ApplyModifiedProperties();

        GameObject touchCtrlObj = new GameObject("MobileTouchController");
        var touchCtrl = touchCtrlObj.AddComponent<MobileTouchController>();
        var tcSerialized = new SerializedObject(touchCtrl);
        tcSerialized.FindProperty("player").objectReferenceValue = playerCtrl;
        tcSerialized.ApplyModifiedProperties();

        GameObject spawnerObj = new GameObject("EnemySpawner");
        var spawner = spawnerObj.AddComponent<EnemySpawner>();
        var spawnerSerialized = new SerializedObject(spawner);
        spawnerSerialized.FindProperty("slimePrefab").objectReferenceValue = slimePrefab;
        spawnerSerialized.FindProperty("batPrefab").objectReferenceValue = batPrefab;
        spawnerSerialized.FindProperty("slimeSpawnY").floatValue = -4.07f;
        spawnerSerialized.FindProperty("chargerSpawnY").floatValue = -3.70f;
        spawnerSerialized.FindProperty("minBatY").floatValue = -2.0f;
        spawnerSerialized.FindProperty("maxBatY").floatValue = -0.5f;
        spawnerSerialized.FindProperty("minDistanceFromPlayer").floatValue = 2.4f;
        spawnerSerialized.FindProperty("warningDuration").floatValue = 0.9f;
        spawnerSerialized.FindProperty("chargerIdleSprite").objectReferenceValue = runnerIdleSprite;
        spawnerSerialized.FindProperty("chargerRunSprite").objectReferenceValue = runnerRunSprite;
        spawnerSerialized.FindProperty("shooterIdleSprite").objectReferenceValue = archerIdleSprite;
        spawnerSerialized.FindProperty("shooterPrepareSprite").objectReferenceValue = archerPrepareSprite;
        spawnerSerialized.FindProperty("shooterShootSprite").objectReferenceValue = archerShootSprite;
        spawnerSerialized.FindProperty("projectileSprite").objectReferenceValue = arrowSprite;
        spawnerSerialized.ApplyModifiedProperties();

        GameObject audioObj = new GameObject("AudioManager");
        var audioComp = audioObj.AddComponent<SimpleAudio>();
        var audioSerialized = new SerializedObject(audioComp);
        audioSerialized.FindProperty("jumpClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/jump.wav");
        audioSerialized.FindProperty("slashClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/slash.wav");
        audioSerialized.FindProperty("killClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/kill.wav");
        audioSerialized.FindProperty("coinClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/coin.wav");
        audioSerialized.FindProperty("hurtClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/hurt.wav");
        audioSerialized.FindProperty("swapClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/swap.wav");
        audioSerialized.FindProperty("comboClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/combo.wav");
        audioSerialized.FindProperty("bgmClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bgm_quest_arena.mp3");
        audioSerialized.ApplyModifiedProperties();

        GameObject vfxObj = new GameObject("VFXManager");
        var vfxMgr = vfxObj.AddComponent<VFXManager>();
        var vfxSerialized = new SerializedObject(vfxMgr);
        vfxSerialized.FindProperty("slashBroadswordSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/vfx_slash_arc_broadsword.png");
        vfxSerialized.FindProperty("slashDaggerSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/vfx_slash_arc_dagger.png");
        vfxSerialized.FindProperty("hitSparkSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/vfx_hit_spark.png");
        vfxSerialized.FindProperty("dustPuffSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/vfx_dust_puff.png");
        vfxSerialized.FindProperty("slimeSplatSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/vfx_slime_splat.png");
        vfxSerialized.FindProperty("batPoofSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/vfx_bat_poof.png");
        vfxSerialized.FindProperty("coinShineSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/vfx_coin_shine.png");
        vfxSerialized.FindProperty("shockwaveSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/vfx_shockwave_ring.png");
        vfxSerialized.FindProperty("comboBurstSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/VFX/vfx_combo_burst.png");
        vfxSerialized.ApplyModifiedProperties();

        GameObject botObj = new GameObject("GameplayBot");
        botObj.AddComponent<GameplayBot>();

        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        GameObject canvasObj = new GameObject("Canvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720f, 1280f);
        scaler.matchWidthOrHeight = 0f;
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject hudObj = new GameObject("HUD");
        hudObj.transform.SetParent(canvasObj.transform, false);
        var hudRt = hudObj.AddComponent<RectTransform>();
        hudRt.anchorMin = Vector2.zero;
        hudRt.anchorMax = Vector2.one;
        hudRt.offsetMin = Vector2.zero;
        hudRt.offsetMax = Vector2.zero;

        GameObject hudHeaderObj = new GameObject("HudHeaderBg");
        hudHeaderObj.transform.SetParent(hudObj.transform, false);
        var hhRt = hudHeaderObj.AddComponent<RectTransform>();
        hhRt.anchorMin = new Vector2(0f, 1f);
        hhRt.anchorMax = new Vector2(1f, 1f);
        hhRt.pivot = new Vector2(0.5f, 1f);
        hhRt.anchoredPosition = Vector2.zero;
        hhRt.sizeDelta = new Vector2(0f, 240f);
        var hhImg = hudHeaderObj.AddComponent<Image>();
        hhImg.sprite = hudHeaderBgSprite;

        GameObject heartsContainer = new GameObject("HeartsContainer");
        heartsContainer.transform.SetParent(hudObj.transform, false);
        var heartsRt = heartsContainer.AddComponent<RectTransform>();
        heartsRt.anchorMin = new Vector2(0f, 1f);
        heartsRt.anchorMax = new Vector2(0f, 1f);
        heartsRt.pivot = new Vector2(0f, 1f);
        heartsRt.anchoredPosition = new Vector2(20f, -24f);
        heartsRt.sizeDelta = new Vector2(190f, 40f);
        var hlg = heartsContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        Image[] heartImages = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            GameObject h = new GameObject($"Heart_{i}");
            h.transform.SetParent(heartsContainer.transform, false);
            var hImg = h.AddComponent<Image>();
            hImg.sprite = heartFullSprite;
            var hRt = h.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(34f, 34f);
            heartImages[i] = hImg;
        }

        GameObject pauseBtnObj = new GameObject("PauseButton");
        pauseBtnObj.transform.SetParent(hudObj.transform, false);
        var pbRt = pauseBtnObj.AddComponent<RectTransform>();
        pbRt.anchorMin = new Vector2(1f, 1f);
        pbRt.anchorMax = new Vector2(1f, 1f);
        pbRt.pivot = new Vector2(1f, 1f);
        pbRt.anchoredPosition = new Vector2(-60f, -22f);
        pbRt.sizeDelta = new Vector2(82f, 82f);
        var pbImg = pauseBtnObj.AddComponent<Image>();
        pbImg.sprite = btnPauseSprite;
        var vbPause = pauseBtnObj.AddComponent<VirtualButton>();
        var vbPauseSerialized = new SerializedObject(vbPause);
        vbPauseSerialized.FindProperty("action").enumValueIndex = (int)VirtualButton.ButtonAction.Pause;
        vbPauseSerialized.ApplyModifiedProperties();

        GameObject scoreObj = new GameObject("ScoreText");
        scoreObj.transform.SetParent(hudObj.transform, false);
        var scoreRt = scoreObj.AddComponent<RectTransform>();
        scoreRt.anchorMin = new Vector2(0.5f, 1f);
        scoreRt.anchorMax = new Vector2(0.5f, 1f);
        scoreRt.pivot = new Vector2(0.5f, 1f);
        scoreRt.anchoredPosition = new Vector2(0f, -22f);
        scoreRt.sizeDelta = new Vector2(300f, 38f);
        var scoreTmp = scoreObj.AddComponent<TextMeshProUGUI>();
        scoreTmp.alignment = TextAlignmentOptions.Center;
        scoreTmp.fontSize = 28f;
        scoreTmp.characterSpacing = 2f;
        scoreTmp.enableWordWrapping = false;
        scoreTmp.fontStyle = FontStyles.Bold;
        scoreTmp.color = new Color(0.94f, 0.96f, 1f);
        scoreTmp.text = "SCORE 000000";

        GameObject coinsObj = new GameObject("CoinsContainer");
        coinsObj.transform.SetParent(hudObj.transform, false);
        var coinsRt = coinsObj.AddComponent<RectTransform>();
        coinsRt.anchorMin = new Vector2(1f, 1f);
        coinsRt.anchorMax = new Vector2(1f, 1f);
        coinsRt.pivot = new Vector2(1f, 1f);
        coinsRt.anchoredPosition = new Vector2(-24f, -130f);
        coinsRt.sizeDelta = new Vector2(180f, 52f);

        GameObject coinIconObj = new GameObject("CoinIcon");
        coinIconObj.transform.SetParent(coinsObj.transform, false);
        var ciRt = coinIconObj.AddComponent<RectTransform>();
        ciRt.anchorMin = new Vector2(0f, 0.5f);
        ciRt.anchorMax = new Vector2(0f, 0.5f);
        ciRt.pivot = new Vector2(0f, 0.5f);
        ciRt.anchoredPosition = new Vector2(8f, 0f);
        ciRt.sizeDelta = new Vector2(36f, 36f);
        var ciImg = coinIconObj.AddComponent<Image>();
        ciImg.sprite = coinGoldSprite;
        ciImg.preserveAspect = true;

        GameObject coinsTextObj = new GameObject("CoinsText");
        coinsTextObj.transform.SetParent(coinsObj.transform, false);
        var ctRt = coinsTextObj.AddComponent<RectTransform>();
        ctRt.anchorMin = new Vector2(0f, 0f);
        ctRt.anchorMax = new Vector2(1f, 1f);
        ctRt.offsetMin = new Vector2(50f, 0f);
        ctRt.offsetMax = Vector2.zero;
        var coinsTmp = coinsTextObj.AddComponent<TextMeshProUGUI>();
        coinsTmp.alignment = TextAlignmentOptions.MidlineLeft;
        coinsTmp.fontSize = 32f;
        coinsTmp.characterSpacing = 2f;
        coinsTmp.enableWordWrapping = false;
        coinsTmp.fontStyle = FontStyles.Bold;
        coinsTmp.color = new Color(1f, 0.85f, 0.2f);
        coinsTmp.text = "0";

        GameObject weaponPlateObj = new GameObject("WeaponPlate");
        weaponPlateObj.transform.SetParent(hudObj.transform, false);
        var wpRt = weaponPlateObj.AddComponent<RectTransform>();
        wpRt.anchorMin = new Vector2(0f, 1f);
        wpRt.anchorMax = new Vector2(0f, 1f);
        wpRt.pivot = new Vector2(0f, 1f);
        wpRt.anchoredPosition = new Vector2(20f, -74f);
        wpRt.sizeDelta = new Vector2(190f, 38f);
        var wpImg = weaponPlateObj.AddComponent<Image>();
        wpImg.sprite = weaponPlateSprite;
        wpImg.type = Image.Type.Sliced;

        GameObject weaponObj = new GameObject("WeaponText");
        weaponObj.transform.SetParent(weaponPlateObj.transform, false);
        var wRt = weaponObj.AddComponent<RectTransform>();
        wRt.anchorMin = Vector2.zero;
        wRt.anchorMax = Vector2.one;
        wRt.offsetMin = new Vector2(8f, 0f);
        wRt.offsetMax = new Vector2(-8f, 0f);
        var weaponTmp = weaponObj.AddComponent<TextMeshProUGUI>();
        weaponTmp.alignment = TextAlignmentOptions.Center;
        weaponTmp.fontSize = 17f;
        weaponTmp.characterSpacing = 2f;
        weaponTmp.enableWordWrapping = false;
        weaponTmp.fontStyle = FontStyles.Bold;
        weaponTmp.color = new Color(0.4f, 0.9f, 1f);
        weaponTmp.text = "ESPADA PADRÃO";

        GameObject comboObj = new GameObject("ComboContainer");
        comboObj.transform.SetParent(hudObj.transform, false);
        var cbRt = comboObj.AddComponent<RectTransform>();
        cbRt.anchorMin = new Vector2(0.5f, 1f);
        cbRt.anchorMax = new Vector2(0.5f, 1f);
        cbRt.pivot = new Vector2(0.5f, 1f);
        cbRt.anchoredPosition = new Vector2(0f, -74f);
        cbRt.sizeDelta = new Vector2(240f, 44f);

        GameObject comboTextObj = new GameObject("ComboText");
        comboTextObj.transform.SetParent(comboObj.transform, false);
        var cbtRt = comboTextObj.AddComponent<RectTransform>();
        cbtRt.anchorMin = new Vector2(0f, 0.45f);
        cbtRt.anchorMax = new Vector2(1f, 1f);
        cbtRt.offsetMin = Vector2.zero;
        cbtRt.offsetMax = Vector2.zero;
        var comboTmp = comboTextObj.AddComponent<TextMeshProUGUI>();
        comboTmp.alignment = TextAlignmentOptions.Center;
        comboTmp.fontSize = 22f;
        comboTmp.characterSpacing = 2f;
        comboTmp.enableWordWrapping = false;
        comboTmp.fontStyle = FontStyles.Bold;
        comboTmp.color = new Color(1f, 0.82f, 0.05f);
        comboTmp.text = "COMBO x0";

        GameObject comboBarBg = new GameObject("ComboBarBg");
        comboBarBg.transform.SetParent(comboObj.transform, false);
        var cbbgRt = comboBarBg.AddComponent<RectTransform>();
        cbbgRt.anchorMin = new Vector2(0.08f, 0f);
        cbbgRt.anchorMax = new Vector2(0.92f, 0.38f);
        cbbgRt.offsetMin = Vector2.zero;
        cbbgRt.offsetMax = Vector2.zero;
        var cbbgImg = comboBarBg.AddComponent<Image>();
        cbbgImg.sprite = comboBarFrameSprite;
        cbbgImg.type = Image.Type.Sliced;

        GameObject comboFillObj = new GameObject("ComboFill");
        comboFillObj.transform.SetParent(comboBarBg.transform, false);
        var cfRt = comboFillObj.AddComponent<RectTransform>();
        cfRt.anchorMin = Vector2.zero;
        cfRt.anchorMax = Vector2.one;
        cfRt.offsetMin = new Vector2(3f, 2f);
        cfRt.offsetMax = new Vector2(-3f, -2f);
        var comboFillImg = comboFillObj.AddComponent<Image>();
        comboFillImg.sprite = comboBarFillSprite;
        comboFillImg.type = Image.Type.Filled;
        comboFillImg.fillMethod = Image.FillMethod.Horizontal;
        comboFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        comboFillImg.fillAmount = 0f;

        GameObject controlsObj = new GameObject("TouchControls");
        controlsObj.transform.SetParent(canvasObj.transform, false);
        var ctlsRt = controlsObj.AddComponent<RectTransform>();
        ctlsRt.anchorMin = Vector2.zero;
        ctlsRt.anchorMax = Vector2.one;
        ctlsRt.offsetMin = Vector2.zero;
        ctlsRt.offsetMax = Vector2.zero;

        GameObject leftCluster = new GameObject("LeftThumbCluster");
        leftCluster.transform.SetParent(controlsObj.transform, false);
        var lcRt = leftCluster.AddComponent<RectTransform>();
        lcRt.anchorMin = new Vector2(0f, 0f);
        lcRt.anchorMax = new Vector2(0f, 0f);
        lcRt.pivot = new Vector2(0f, 0f);
        lcRt.anchoredPosition = new Vector2(24f, 28f);
        lcRt.sizeDelta = new Vector2(400f, 180f);
        var lcBg = leftCluster.AddComponent<Image>();
        lcBg.sprite = clusterBgSprite;
        lcBg.type = Image.Type.Sliced;

        GameObject btnLeftObj = new GameObject("BtnLeft");
        btnLeftObj.transform.SetParent(leftCluster.transform, false);
        var blRt = btnLeftObj.AddComponent<RectTransform>();
        blRt.anchorMin = new Vector2(0f, 0.5f);
        blRt.anchorMax = new Vector2(0f, 0.5f);
        blRt.pivot = new Vector2(0f, 0.5f);
        blRt.anchoredPosition = new Vector2(26f, 0f);
        blRt.sizeDelta = new Vector2(160f, 160f);
        var blImg = btnLeftObj.AddComponent<Image>();
        blImg.sprite = btnLeftSprite;
        var blVb = btnLeftObj.AddComponent<VirtualButton>();
        var blVbSerialized = new SerializedObject(blVb);
        blVbSerialized.FindProperty("action").enumValueIndex = (int)VirtualButton.ButtonAction.Left;
        blVbSerialized.ApplyModifiedProperties();

        GameObject btnRightObj = new GameObject("BtnRight");
        btnRightObj.transform.SetParent(leftCluster.transform, false);
        var brRt = btnRightObj.AddComponent<RectTransform>();
        brRt.anchorMin = new Vector2(1f, 0.5f);
        brRt.anchorMax = new Vector2(1f, 0.5f);
        brRt.pivot = new Vector2(1f, 0.5f);
        brRt.anchoredPosition = new Vector2(-26f, 0f);
        brRt.sizeDelta = new Vector2(160f, 160f);
        var brImg = btnRightObj.AddComponent<Image>();
        brImg.sprite = btnRightSprite;
        var brVb = btnRightObj.AddComponent<VirtualButton>();
        var brVbSerialized = new SerializedObject(brVb);
        brVbSerialized.FindProperty("action").enumValueIndex = (int)VirtualButton.ButtonAction.Right;
        brVbSerialized.ApplyModifiedProperties();

        GameObject rightCluster = new GameObject("RightThumbCluster");
        rightCluster.transform.SetParent(controlsObj.transform, false);
        var rcRt = rightCluster.AddComponent<RectTransform>();
        rcRt.anchorMin = new Vector2(1f, 0f);
        rcRt.anchorMax = new Vector2(1f, 0f);
        rcRt.pivot = new Vector2(1f, 0f);
        rcRt.anchoredPosition = new Vector2(-24f, 28f);
        rcRt.sizeDelta = new Vector2(210f, 170f);
        var rcBg = rightCluster.AddComponent<Image>();
        rcBg.sprite = clusterBgSprite;
        rcBg.type = Image.Type.Sliced;

        GameObject btnJumpObj = new GameObject("BtnJump");
        btnJumpObj.transform.SetParent(rightCluster.transform, false);
        var bjRt = btnJumpObj.AddComponent<RectTransform>();
        bjRt.anchorMin = new Vector2(1f, 0.5f);
        bjRt.anchorMax = new Vector2(1f, 0.5f);
        bjRt.pivot = new Vector2(1f, 0.5f);
        bjRt.anchoredPosition = new Vector2(-18f, 0f);
        bjRt.sizeDelta = new Vector2(176f, 118f);
        var bjImg = btnJumpObj.AddComponent<Image>();
        bjImg.sprite = btnJumpSprite;
        var bjVb = btnJumpObj.AddComponent<VirtualButton>();
        var bjVbSerialized = new SerializedObject(bjVb);
        bjVbSerialized.FindProperty("action").enumValueIndex = (int)VirtualButton.ButtonAction.Jump;
        bjVbSerialized.ApplyModifiedProperties();

        GameObject startPanel = CreateModalPanel(canvasObj.transform, "StartPanel", frameGothicSprite, 650f, 680f);
        var startBox = startPanel.transform.Find("ModalBox");
        AddModalHeader(startBox.gameObject, "COMBO KNIGHT", "2D PIXEL ARCADE ACTION");
        AddModalBodyCard(startBox.gameObject, cardDarkSprite,
            "<size=22><color=#FFD700><b>OBJETIVO</b></color></size>\n" +
            "Sobreviva na arena lunar medieval e encadeie abates rapidos para multiplicar seus pontos!",
            "<size=22><color=#FFD700><b>CONTROLES TOUCH</b></color></size>\n" +
            "\u2022 <color=#38BDF8><b>Esquerda:</b></color> Botao Esquerda\n" +
            "\u2022 <color=#38BDF8><b>Direita:</b></color> Botao Direita\n" +
            "\u2022 <color=#C084FC><b>Deslize p/ Cima:</b></color> Pular\n" +
            "\u2022 <color=#F43F5E><b>Ataque:</b></color> Automatico (ou duplo toque)",
            "<size=22><color=#FFD700><b>ENERGIA DE ARMAS</b></color></size>\n" +
            "\u2022 <color=#F43F5E><b>Adaga:</b></color> 2x Dano/Pontos em <b>Slimes</b>\n" +
            "\u2022 <color=#38BDF8><b>Espada (Corte Vertical):</b></color> 2x Dano/Pontos em <b>Morcegos</b>");
        var startBtn = AddModalButton(startBox.gameObject, "INICIAR BATALHA", btnGoldSprite, -285f, 480f, 90f);

        GameObject pausePanel = CreateModalPanel(canvasObj.transform, "PausePanel", frameGothicSprite, 560f, 580f);
        var pauseBox = pausePanel.transform.Find("ModalBox");
        AddModalHeader(pauseBox.gameObject, "PAUSA", "JOGO EM ESPERA");
        var resumeBtn = AddModalButton(pauseBox.gameObject, "CONTINUAR", btnBlueSprite, 60f, 420f, 75f);
        var restartPauseBtn = AddModalButton(pauseBox.gameObject, "REINICIAR", btnGoldSprite, -40f, 420f, 75f);
        var muteBtn = AddModalButton(pauseBox.gameObject, "SOM: ATIVO", btnGreenSprite, -140f, 420f, 75f);
        var muteTmp = muteBtn.GetComponentInChildren<TextMeshProUGUI>();

        GameObject gameOverPanel = CreateModalPanel(canvasObj.transform, "GameOverPanel", frameGothicSprite, 640f, 840f);
        var goBox = gameOverPanel.transform.Find("ModalBox");
        AddModalHeader(goBox.gameObject, "FIM DE JOGO", "SEUS CORAÇÕES SE ESGOTARAM");

        GameObject goCard = new GameObject("StatsCard");
        goCard.transform.SetParent(goBox, false);
        var gcRt = goCard.AddComponent<RectTransform>();
        gcRt.anchorMin = new Vector2(0.5f, 0.5f);
        gcRt.anchorMax = new Vector2(0.5f, 0.5f);
        gcRt.pivot = new Vector2(0.5f, 0.5f);
        gcRt.anchoredPosition = new Vector2(0f, 45f);
        gcRt.sizeDelta = new Vector2(560f, 380f);
        var gcImg = goCard.AddComponent<Image>();
        gcImg.sprite = cardDarkSprite;
        gcImg.type = Image.Type.Sliced;

        var finalScoreTmp = CreateStatLine(goCard.transform, "PONTUAÇÃO:", "0", 125f, Color.white);
        var finalHighScoreTmp = CreateStatLine(goCard.transform, "RECORDE:", "0", 75f, new Color(1f, 0.85f, 0.2f));

        GameObject recordNoticeObj = new GameObject("RecordNotice");
        recordNoticeObj.transform.SetParent(goCard.transform, false);
        var rnRt = recordNoticeObj.AddComponent<RectTransform>();
        rnRt.anchoredPosition = new Vector2(0f, 25f);
        rnRt.sizeDelta = new Vector2(500f, 35f);
        var rnTmp = recordNoticeObj.AddComponent<TextMeshProUGUI>();
        rnTmp.alignment = TextAlignmentOptions.Center;
        rnTmp.fontSize = 26f;
        rnTmp.characterSpacing = 2f;
        rnTmp.fontStyle = FontStyles.Bold;
        rnTmp.color = new Color(0.95f, 0.35f, 0.95f);
        rnTmp.text = "NOVO RECORDE PESSOAL!";
        recordNoticeObj.SetActive(false);

        var finalMaxComboTmp = CreateStatLine(goCard.transform, "MAIOR COMBO:", "x0", -25f, new Color(1f, 0.6f, 0.1f));
        var finalCoinsTmp = CreateStatLine(goCard.transform, "MOEDAS:", "0", -75f, new Color(1f, 0.85f, 0.2f));
        var finalKillsTmp = CreateStatLine(goCard.transform, "ABATES:", "0", -125f, new Color(0.4f, 0.9f, 1f));

        var retryBtn = AddModalButton(goBox.gameObject, "TENTAR NOVAMENTE", btnGoldSprite, -320f, 480f, 85f);

        GameObject gmObj = new GameObject("GameManager");
        var gm = gmObj.AddComponent<GameManager>();
        var gmSerialized = new SerializedObject(gm);
        gmSerialized.FindProperty("coinPrefab").objectReferenceValue = coinPrefab;
        gmSerialized.FindProperty("floatingTextPrefab").objectReferenceValue = floatingTextPrefab;
        var hiProp = gmSerialized.FindProperty("heartIcons");
        hiProp.arraySize = 5;
        for (int i = 0; i < 5; i++) hiProp.GetArrayElementAtIndex(i).objectReferenceValue = heartImages[i];
        gmSerialized.FindProperty("scoreText").objectReferenceValue = scoreTmp;
        gmSerialized.FindProperty("coinsText").objectReferenceValue = coinsTmp;
        gmSerialized.FindProperty("comboText").objectReferenceValue = comboTmp;
        gmSerialized.FindProperty("comboFillBar").objectReferenceValue = comboFillImg;
        gmSerialized.FindProperty("weaponText").objectReferenceValue = weaponTmp;

        gmSerialized.FindProperty("startPanel").objectReferenceValue = startPanel;
        gmSerialized.FindProperty("pausePanel").objectReferenceValue = pausePanel;
        gmSerialized.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        gmSerialized.FindProperty("finalScoreText").objectReferenceValue = finalScoreTmp;
        gmSerialized.FindProperty("finalHighScoreText").objectReferenceValue = finalHighScoreTmp;
        gmSerialized.FindProperty("newRecordNoticeText").objectReferenceValue = rnTmp;
        gmSerialized.FindProperty("finalMaxComboText").objectReferenceValue = finalMaxComboTmp;
        gmSerialized.FindProperty("finalCoinsText").objectReferenceValue = finalCoinsTmp;
        gmSerialized.FindProperty("finalKillsText").objectReferenceValue = finalKillsTmp;
        gmSerialized.FindProperty("muteButtonText").objectReferenceValue = muteTmp;
        gmSerialized.ApplyModifiedProperties();

        pausePanel.SetActive(false);
        gameOverPanel.SetActive(false);

        UnityEventTools.AddPersistentListener(startBtn.onClick, gm.StartGame);
        UnityEventTools.AddPersistentListener(resumeBtn.onClick, gm.TogglePause);
        UnityEventTools.AddPersistentListener(restartPauseBtn.onClick, gm.RestartGame);
        UnityEventTools.AddPersistentListener(muteBtn.onClick, gm.ToggleMute);
        UnityEventTools.AddPersistentListener(retryBtn.onClick, gm.RestartGame);

        string scenePath = "Assets/Scenes/MainScene.unity";
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);

        UnityEditor.EditorBuildSettingsScene[] scenes = new UnityEditor.EditorBuildSettingsScene[]
        {
            new UnityEditor.EditorBuildSettingsScene(scenePath, true)
        };
        UnityEditor.EditorBuildSettings.scenes = scenes;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject CreateModalPanel(Transform parent, string name, Sprite frameSprite, float width, float height)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.03f, 0.06f, 0.88f);

        GameObject box = new GameObject("ModalBox");
        box.transform.SetParent(panel.transform, false);
        var boxRt = box.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.sizeDelta = new Vector2(width, height);
        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = frameSprite;
        boxImg.type = Image.Type.Sliced;

        return panel;
    }

    private static void AddModalHeader(GameObject parent, string title, string subtitle)
    {
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(parent.transform, false);
        var tRt = titleObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.5f, 1f);
        tRt.anchorMax = new Vector2(0.5f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -32f);
        tRt.sizeDelta = new Vector2(580f, 60f);
        var tTmp = titleObj.AddComponent<TextMeshProUGUI>();
        tTmp.alignment = TextAlignmentOptions.Center;
        tTmp.fontSize = 44f;
        tTmp.characterSpacing = 2.5f;
        tTmp.fontStyle = FontStyles.Bold;
        tTmp.color = new Color(0.98f, 0.80f, 0.08f);
        tTmp.text = title;

        if (!string.IsNullOrEmpty(subtitle))
        {
            GameObject subObj = new GameObject("Subtitle");
            subObj.transform.SetParent(parent.transform, false);
            var sRt = subObj.AddComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.5f, 1f);
            sRt.anchorMax = new Vector2(0.5f, 1f);
            sRt.pivot = new Vector2(0.5f, 1f);
            sRt.anchoredPosition = new Vector2(0f, -92f);
            sRt.sizeDelta = new Vector2(540f, 35f);
            var sTmp = subObj.AddComponent<TextMeshProUGUI>();
            sTmp.alignment = TextAlignmentOptions.Center;
            sTmp.fontSize = 20f;
            sTmp.characterSpacing = 1.5f;
            sTmp.color = new Color(0.58f, 0.64f, 0.72f);
            sTmp.text = subtitle;
        }
    }

    private static void AddModalBodyCard(GameObject parent, Sprite cardSprite, params string[] columns)
    {
        GameObject cardObj = new GameObject("InstructionsCard");
        cardObj.transform.SetParent(parent.transform, false);
        var cRt = cardObj.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0.5f);
        cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(0f, 20f);
        cRt.sizeDelta = new Vector2(626f, 300f);
        var cImg = cardObj.AddComponent<Image>();
        cImg.sprite = cardSprite;
        cImg.type = Image.Type.Sliced;

        int count = columns.Length;
        float colWidth = 188f;
        float gap = 12f;
        float startX = -((count - 1) * (colWidth + gap)) / 2f;
        for (int i = 0; i < count; i++)
        {
            GameObject colObj = new GameObject("Column" + (i + 1));
            colObj.transform.SetParent(cardObj.transform, false);
            var colRt = colObj.AddComponent<RectTransform>();
            colRt.anchorMin = new Vector2(0.5f, 0.5f);
            colRt.anchorMax = new Vector2(0.5f, 0.5f);
            colRt.pivot = new Vector2(0.5f, 0.5f);
            float x = startX + i * (colWidth + gap);
            colRt.anchoredPosition = new Vector2(x, 0f);
            colRt.sizeDelta = new Vector2(colWidth, 270f);
            var colTmp = colObj.AddComponent<TextMeshProUGUI>();
            colTmp.alignment = TextAlignmentOptions.TopLeft;
            colTmp.fontSize = 17f;
            colTmp.characterSpacing = 0.5f;
            colTmp.lineSpacing = 7f;
            colTmp.color = new Color(0.95f, 0.96f, 1f);
            colTmp.richText = true;
            colTmp.enableWordWrapping = true;
            colTmp.text = columns[i];
        }
    }

    private static Button AddModalButton(GameObject parent, string label, Sprite btnSprite, float yPos, float width, float height)
    {
        GameObject btnObj = new GameObject("Btn_" + label.Replace(" ", ""));
        btnObj.transform.SetParent(parent.transform, false);
        var rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, yPos);
        rt.sizeDelta = new Vector2(width, height);

        var img = btnObj.AddComponent<Image>();
        img.sprite = btnSprite;
        img.type = Image.Type.Sliced;
        var btn = btnObj.AddComponent<Button>();

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        var tRt = txtObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;
        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28f;
        tmp.characterSpacing = 2f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.text = label;

        return btn;
    }

    private static TextMeshProUGUI CreateStatLine(Transform parent, string label, string defaultValue, float yOffset, Color valColor)
    {
        GameObject row = new GameObject("Row_" + label.Replace(":", ""));
        row.transform.SetParent(parent, false);
        var rt = row.AddComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(480f, 40f);

        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(row.transform, false);
        var lRt = lblObj.AddComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(0.55f, 1f);
        lRt.offsetMin = Vector2.zero;
        lRt.offsetMax = Vector2.zero;
        var lTmp = lblObj.AddComponent<TextMeshProUGUI>();
        lTmp.alignment = TextAlignmentOptions.MidlineLeft;
        lTmp.fontSize = 24f;
        lTmp.characterSpacing = 1.5f;
        lTmp.color = new Color(0.65f, 0.72f, 0.82f);
        lTmp.text = label;

        GameObject valObj = new GameObject("Value");
        valObj.transform.SetParent(row.transform, false);
        var vRt = valObj.AddComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0.55f, 0f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.offsetMin = Vector2.zero;
        vRt.offsetMax = Vector2.zero;
        var vTmp = valObj.AddComponent<TextMeshProUGUI>();
        vTmp.alignment = TextAlignmentOptions.MidlineRight;
        vTmp.fontSize = 26f;
        vTmp.characterSpacing = 1.5f;
        vTmp.fontStyle = FontStyles.Bold;
        vTmp.color = valColor;
        vTmp.text = defaultValue;

        return vTmp;
    }

    // Carrega um sub-sprite (multiple spritesheet) pelo nome, e.g. Ground_0.
    private static Sprite LoadSubSprite(string assetPath, string name)
    {
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (sub is Sprite s && s.name == name) return s;
        }
        return null;
    }
}
