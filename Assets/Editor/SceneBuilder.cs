#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Автоматическая сборка демо-сцены: TMP Essentials, шрифт с кириллицей,
/// префабы узлов/рёбер/игрока, Canvas с UI и связка всех менеджеров.
/// Запуск: меню Tools -> "Собрать демо-сцену" или -executeMethod SceneBuilder.BuildAll
/// </summary>
public static class SceneBuilder
{
    const string FontMainPath = "Assets/Fonts/ArialCyrillic Dynamic SDF.asset";
    const string FontFallbackPath = "Assets/Fonts/SegoeUISymbol Dynamic SDF.asset";
    const string PrefabDir = "Assets/Prefabs";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    static TMP_FontAsset s_MainFont;

    [MenuItem("Tools/Собрать демо-сцену")]
    public static void BuildAll()
    {
        try
        {
            Debug.Log("[SceneBuilder] 1/4 Импорт TMP Essentials...");
            ImportTMPEssentials();
            EnsureTMPSettings();

            Debug.Log("[SceneBuilder] 2/4 Создание шрифтов (кириллица + символы)...");
            EnsureFolder("Assets", "Fonts");
            TMP_FontAsset main = CreateDynamicFontAsset("Arial", FontMainPath);
            if (main == null)
            {
                Debug.LogError("[SceneBuilder] Не удалось создать шрифт из системного Arial.");
                return;
            }
            TMP_FontAsset fallback = CreateDynamicFontAsset("Segoe UI Symbol", FontFallbackPath);
            ApplyFontSettings(main, fallback);

            Debug.Log("[SceneBuilder] 3/4 Создание префабов...");
            s_MainFont = main;
            EnsureFolder("Assets", "Prefabs");
            GameObject nodePrefab = BuildNodePrefab();
            GameObject edgePrefab = BuildEdgePrefab();
            GameObject playerPrefab = BuildPlayerPrefab();
            GameObject listItemPrefab = BuildSourceListItemPrefab();

            Debug.Log("[SceneBuilder] 4/4 Сборка сцены...");
            BuildScene(nodePrefab, edgePrefab, playerPrefab, listItemPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] ГОТОВО! Открой Assets/Scenes/SampleScene и нажми Play.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneBuilder] Ошибка сборки: {e}");
            throw;
        }
    }

    // ---------------------------------------------------------------- TMP

    static void ImportTMPEssentials()
    {
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings != null)
        {
            Debug.Log("[SceneBuilder] TMP Settings уже импортированы.");
            return;
        }

        // Реальный путь внутри PackageCache: виртуальный путь "Packages/..."
        // AssetDatabase.ImportPackage в batch-режиме не переваривает
        string unityPackage = null;
        try
        {
            foreach (string dir in Directory.GetDirectories("Library/PackageCache", "com.unity.textmeshpro@*"))
            {
                string candidate = Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
                if (File.Exists(candidate))
                {
                    unityPackage = Path.GetFullPath(candidate);
                    break;
                }
            }
        }
        catch { }

        if (unityPackage != null)
            AssetDatabase.ImportPackage(unityPackage, false);
        else
            Debug.LogWarning("[SceneBuilder] TMP Essential Resources.unitypackage не найден в PackageCache.");

        AssetDatabase.Refresh();

        settings = Resources.Load<TMP_Settings>("TMP Settings");
        Debug.Log(settings != null
            ? "[SceneBuilder] TMP Essentials импортированы."
            : "[SceneBuilder] TMP Essentials не импортировались — создам TMP Settings вручную (страховка).");
    }

    /// <summary> Страховка: если TMP Settings.asset так и нет — создаём пустой.
    /// Рантайм TMP ищет его через Resources.Load("TMP Settings"). </summary>
    static void EnsureTMPSettings()
    {
        if (Resources.Load<TMP_Settings>("TMP Settings") != null) return;

        var settings = ScriptableObject.CreateInstance<TMP_Settings>();
        EnsureFolder("Assets", "TextMesh Pro");
        EnsureFolder("Assets/TextMesh Pro", "Resources");
        AssetDatabase.CreateAsset(settings, "Assets/TextMesh Pro/Resources/TMP Settings.asset");
        Debug.Log("[SceneBuilder] TMP Settings.asset создан вручную.");
    }

    static TMP_FontAsset CreateDynamicFontAsset(string osFontName, string assetPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        // В batch-режиме FontEngine не может загрузить OS-шрифт по имени,
        // поэтому грузим напрямую из файла в C:\Windows\Fonts
        string fontFile = FindFontFile(osFontName);
        if (fontFile == null)
        {
            Debug.LogWarning($"[SceneBuilder] Не найден файл шрифта для {osFontName} в Windows\\Fonts");
            return null;
        }

        // OS-шрифт нужен рантайму для динамической догрузки глифов
        Font osFont = Font.CreateDynamicFontFromOSFont(osFontName, 90);

        TMP_FontAsset fontAsset = CreateFontAssetFromFile(
            fontFile, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, true, osFont);
        if (fontAsset == null)
        {
            Debug.LogWarning($"[SceneBuilder] FontEngine не смог загрузить файл: {fontFile}");
            return null;
        }

        fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(fontAsset, assetPath);
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
    }

    /// <summary> Копия TMP_FontAsset.CreateFontAsset(Font, ...) с загрузкой FontFace из файла.
    /// Приватные/внутренние поля TMP 3.0.7 задаются через SerializedObject. </summary>
    static TMP_FontAsset CreateFontAssetFromFile(string fontFilePath, int samplingPointSize,
        int atlasPadding, GlyphRenderMode renderMode, int atlasWidth, int atlasHeight,
        bool enableMultiAtlasSupport, Font osFont)
    {
        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(fontFilePath, samplingPointSize) != FontEngineError.Success)
            return null;

        TMP_FontAsset fontAsset = ScriptableObject.CreateInstance<TMP_FontAsset>();
        fontAsset.name = Path.GetFileNameWithoutExtension(fontFilePath) + " Dynamic SDF";
        fontAsset.faceInfo = FontEngine.GetFaceInfo();
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.atlasTextures = new Texture2D[1];
        Texture2D texture = new Texture2D(0, 0, TextureFormat.Alpha8, false);
        texture.name = fontAsset.name + " Atlas";
        fontAsset.atlasTextures[0] = texture;
        fontAsset.isMultiAtlasTexturesEnabled = enableMultiAtlasSupport;

        // SDF-материал (как в оригинале для не-bitmap режимов)
        Material tmpMaterial = new Material(Shader.Find("TextMeshPro/Mobile/Distance Field"));
        tmpMaterial.name = fontAsset.name + " Atlas Material";
        tmpMaterial.SetTexture(ShaderUtilities.ID_MainTex, texture);
        tmpMaterial.SetFloat(ShaderUtilities.ID_TextureWidth, atlasWidth);
        tmpMaterial.SetFloat(ShaderUtilities.ID_TextureHeight, atlasHeight);
        tmpMaterial.SetFloat(ShaderUtilities.ID_GradientScale, atlasPadding + 1);
        tmpMaterial.SetFloat(ShaderUtilities.ID_WeightNormal, fontAsset.normalStyle);
        tmpMaterial.SetFloat(ShaderUtilities.ID_WeightBold, fontAsset.boldStyle);
        fontAsset.material = tmpMaterial;

        // Приватные/внутренние поля TMP 3.0.7
        var so = new SerializedObject(fontAsset);
        so.FindProperty("m_Version").stringValue = "1.1.0";
        so.FindProperty("m_AtlasWidth").intValue = atlasWidth;
        so.FindProperty("m_AtlasHeight").intValue = atlasHeight;
        so.FindProperty("m_AtlasPadding").intValue = atlasPadding;
        so.FindProperty("m_AtlasRenderMode").intValue = (int)renderMode;
        so.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = enableMultiAtlasSupport;
        so.FindProperty("m_SourceFontFile").objectReferenceValue = osFont;
        so.FindProperty("m_SourceFontFile_EditorRef").objectReferenceValue = osFont;
        so.FindProperty("m_SourceFontFileGUID").stringValue = "";

        so.ApplyModifiedProperties();

        // Свободные/занятые прямоугольники атласа — через рефлексию (SerializedObject
        // не справляется со списком структур UnityEngine.TextCore.GlyphRect)
        typeof(TMP_FontAsset)
            .GetField("m_FreeGlyphRects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(fontAsset, new List<UnityEngine.TextCore.GlyphRect>
                { new UnityEngine.TextCore.GlyphRect(0, 0, atlasWidth - 1, atlasHeight - 1) });
        typeof(TMP_FontAsset)
            .GetField("m_UsedGlyphRects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(fontAsset, new List<UnityEngine.TextCore.GlyphRect>());

        fontAsset.ReadFontAssetDefinition();
        return fontAsset;
    }

    static string FindFontFile(string osFontName)
    {
        string windir = Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows";
        string fontsDir = Path.Combine(windir, "Fonts");

        string[] candidates;
        switch (osFontName)
        {
            case "Arial":
                candidates = new[] { "arial.ttf", "ARIAL.TTF", "arialbd.ttf" };
                break;
            case "Segoe UI Symbol":
                candidates = new[] { "seguisym.ttf", "SEGUISYM.TTF" };
                break;
            default:
                candidates = new[] { osFontName.Replace(" ", "") + ".ttf" };
                break;
        }

        foreach (string c in candidates)
        {
            string p = Path.Combine(fontsDir, c);
            if (File.Exists(p)) return p;
        }

        // Общий fallback: поиск по началу имени файла
        string search = osFontName.Split(' ')[0].ToLowerInvariant();
        try
        {
            foreach (string f in Directory.GetFiles(fontsDir, "*.ttf"))
                if (Path.GetFileName(f).ToLowerInvariant().StartsWith(search))
                    return f;
        }
        catch { }

        return null;
    }

    static void ApplyFontSettings(TMP_FontAsset main, TMP_FontAsset fallback)
    {
        // В TMP 3.0.7 статические свойства TMP.Settings только для чтения, а поля приватные —
        // назначаем через SerializedObject
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        var so = new SerializedObject(settings);

        var defaultProp = so.FindProperty("m_defaultFontAsset");
        defaultProp.objectReferenceValue = main;

        var fallbackProp = so.FindProperty("m_fallbackFontAssets");
        fallbackProp.ClearArray();
        if (fallback != null)
        {
            fallbackProp.arraySize = 1;
            fallbackProp.GetArrayElementAtIndex(0).objectReferenceValue = fallback;
        }

        so.ApplyModifiedProperties();

        Debug.Log($"[SceneBuilder] Основной шрифт: {main.name}; запасной: {(fallback != null ? fallback.name : "нет")}. Динамический атлас подтянет кириллицу и символы ✓ ✗ ○ ⚠ α автоматически.");
    }

    // ------------------------------------------------------------- Префабы

    static GameObject BuildNodePrefab()
    {
        GameObject root = new GameObject("NodePrefab");

        // Тело узла (сфера радиусом 2)
        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = Vector3.one * 4f;
        var mf = body.AddComponent<MeshFilter>();
        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        var mr = body.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = Color.white;
        mr.sharedMaterial = mat;

        // Коллайдер для кликов (Update() в DemoController делает Physics.Raycast)
        var col = root.AddComponent<SphereCollider>();
        col.radius = 2f;

        var nv = root.AddComponent<NodeVisual>();
        nv.nodeRenderer = mr;
        nv.nameLabel = CreateWorldLabel(root.transform, "NameLabel", new Vector3(0, 3.4f, 0), 44f,
            Color.black);
        nv.valueLabel = CreateWorldLabel(root.transform, "ValueLabel", new Vector3(0, -3.6f, 0), 30f,
            new Color(0.15f, 0.15f, 0.15f));

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/NodePrefab.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static TextMeshPro CreateWorldLabel(Transform parent, string name, Vector3 localPos, float fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font = s_MainFont;
        tmp.text = "";
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.rectTransform.sizeDelta = new Vector2(16, 4);
        tmp.enableWordWrapping = false;
        return tmp;
    }

    static GameObject BuildEdgePrefab()
    {
        GameObject root = new GameObject("EdgePrefab");
        var lr = root.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = 0.3f;
        lr.endWidth = 0.3f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/EdgePrefab.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildPlayerPrefab()
    {
        GameObject root = new GameObject("PlayerPrefab");

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0, 2f, 0);
        body.transform.localScale = Vector3.one * 2f;
        var mf = body.AddComponent<MeshFilter>();
        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("New-Capsule.fbx");
        var mr = body.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.2f, 0.45f, 1f);
        mr.sharedMaterial = mat;

        var col = root.AddComponent<CapsuleCollider>();
        col.radius = 1f;
        col.height = 4f;
        col.center = new Vector3(0, 2f, 0);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/PlayerPrefab.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject BuildSourceListItemPrefab()
    {
        GameObject root = new GameObject("SourceListItemPrefab", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(440, 36);

        var tmp = root.AddComponent<TextMeshProUGUI>();
        tmp.font = s_MainFont;
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.text = "";
        tmp.raycastTarget = false;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/SourceListItemPrefab.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------------------------------------------------------------- Сцена

    static void BuildScene(GameObject nodePrefab, GameObject edgePrefab,
                           GameObject playerPrefab, GameObject listItemPrefab)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Камера: посмотреть на карту радиусом 50 сверху-спереди
        GameObject cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 75f, -62f);
            cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        }

        // Все менеджеры на одном объекте
        GameObject managers = new GameObject("Managers");
        var cityGraph = managers.AddComponent<CityGraph>();
        var pathfinding = managers.AddComponent<PathfindingAlgorithm>();
        pathfinding.cityGraph = cityGraph;
        var spawnValidator = managers.AddComponent<SpawnValidator>();
        spawnValidator.cityGraph = cityGraph;
        spawnValidator.pathfinding = pathfinding;
        var timeCalculator = managers.AddComponent<TimeCalculator>();
        timeCalculator.cityGraph = cityGraph;

        // Линия пройденного пути (поле pathLineRenderer в DemoController)
        GameObject pathLine = new GameObject("PathLine");
        var plr = pathLine.AddComponent<LineRenderer>();
        plr.material = new Material(Shader.Find("Sprites/Default"));
        plr.startWidth = 0.4f;
        plr.endWidth = 0.4f;
        plr.positionCount = 0;

        var demo = managers.AddComponent<DemoController>();
        demo.cityGraph = cityGraph;
        demo.pathfinding = pathfinding;
        demo.spawnValidator = spawnValidator;
        demo.timeCalculator = timeCalculator;
        demo.nodePrefab = nodePrefab;
        demo.edgePrefab = edgePrefab;
        demo.playerPrefab = playerPrefab;
        demo.pathLineRenderer = plr;

        var uiManager = BuildUI(demo);
        demo.uiManager = uiManager;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[SceneBuilder] Сцена сохранена: " + ScenePath);
    }

    static UIManager BuildUI(DemoController demo)
    {
        var uiGo = new GameObject("UIManager");
        var ui = uiGo.AddComponent<UIManager>();

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Информационная панель (слева сверху)
        GameObject infoPanel = CreatePanel(canvasGo.transform, "InfoPanel",
            AnchorPreset.TopLeft, new Vector2(20, -20), new Vector2(470, 250),
            new Color(0f, 0f, 0f, 0.55f));
        ui.remainingTimeText = CreateUiText(infoPanel.transform, "RemainingTimeText",
            AnchorPreset.TopLeft, new Vector2(15, -8), new Vector2(440, 50), 30,
            TextAlignmentOptions.Left, Color.white);
        ui.currentTimeText = CreateUiText(infoPanel.transform, "CurrentTimeText",
            AnchorPreset.TopLeft, new Vector2(15, -63), new Vector2(440, 50), 30,
            TextAlignmentOptions.Left, Color.white);
        ui.weatherText = CreateUiText(infoPanel.transform, "WeatherText",
            AnchorPreset.TopLeft, new Vector2(15, -118), new Vector2(440, 50), 30,
            TextAlignmentOptions.Left, Color.white);
        ui.alphaText = CreateUiText(infoPanel.transform, "AlphaText",
            AnchorPreset.TopLeft, new Vector2(15, -173), new Vector2(440, 50), 30,
            TextAlignmentOptions.Left, Color.white);

        // Статус (внизу по центру)
        ui.statusText = CreateUiText(canvasGo.transform, "StatusText",
            AnchorPreset.BottomCenter, new Vector2(0, 45), new Vector2(1200, 60), 32,
            TextAlignmentOptions.Center, Color.white);

        // Панель предупреждений (сверху по центру)
        GameObject warningPanel = CreatePanel(canvasGo.transform, "WarningPanel",
            AnchorPreset.TopCenter, new Vector2(0, -30), new Vector2(950, 85),
            new Color(0.75f, 0.16f, 0.16f, 0.92f));
        ui.warningPanel = warningPanel;
        ui.warningText = CreateUiText(warningPanel.transform, "WarningText",
            AnchorPreset.TopLeft, new Vector2(20, -15), new Vector2(910, 55), 28,
            TextAlignmentOptions.Center, Color.white);
        warningPanel.SetActive(false);

        // Кнопки (справа сверху)
        ui.generateButton = CreateButton(canvasGo.transform, "GenerateButton", "Сгенерировать",
            new Vector2(-30, -25));
        ui.validateButton = CreateButton(canvasGo.transform, "ValidateButton", "Проверить спавн",
            new Vector2(-30, -100));
        Button finishButton = CreateButton(canvasGo.transform, "FinishButton", "Завершить",
            new Vector2(-30, -175));

        UnityEventTools.AddPersistentListener(ui.generateButton.onClick,
            new UnityAction(demo.OnGenerateClicked));
        UnityEventTools.AddPersistentListener(ui.validateButton.onClick,
            new UnityAction(demo.OnValidateClicked));
        UnityEventTools.AddPersistentListener(finishButton.onClick,
            new UnityAction(demo.OnFinishClicked));

        // Список источников (справа по центру)
        GameObject listGo = new GameObject("SourceList", typeof(RectTransform));
        listGo.transform.SetParent(canvasGo.transform, false);
        var listRect = listGo.GetComponent<RectTransform>();
        ApplyAnchor(listRect, AnchorPreset.MiddleRight);
        listRect.anchoredPosition = new Vector2(-30, 0);
        listRect.sizeDelta = new Vector2(470, 520);
        var layout = listGo.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 6f;
        var fitter = listGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ui.sourceListParent = listGo.transform;
        ui.sourceListItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/SourceListItemPrefab.prefab");

        return ui;
    }

    // ------------------------------------------------------ UI-хелперы

    enum AnchorPreset { TopLeft, TopCenter, TopRight, BottomCenter, MiddleRight, Stretch }

    static void ApplyAnchor(RectTransform rt, AnchorPreset preset)
    {
        switch (preset)
        {
            case AnchorPreset.TopLeft:
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); break;
            case AnchorPreset.TopCenter:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1); break;
            case AnchorPreset.TopRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1); break;
            case AnchorPreset.BottomCenter:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.pivot = new Vector2(0.5f, 0.5f); break;
            case AnchorPreset.MiddleRight:
                rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f); rt.pivot = new Vector2(1, 0.5f); break;
            case AnchorPreset.Stretch:
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; break;
        }
    }

    static GameObject CreatePanel(Transform parent, string name, AnchorPreset anchor,
        Vector2 anchoredPos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        ApplyAnchor(rt, anchor);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    static TextMeshProUGUI CreateUiText(Transform parent, string name, AnchorPreset anchor,
        Vector2 anchoredPos, Vector2 size, float fontSize, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        ApplyAnchor(rt, anchor);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = s_MainFont;
        tmp.text = "";
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        ApplyAnchor(rt, AnchorPreset.TopRight);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(280, 64);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.16f, 0.47f, 0.7f, 1f);

        var btn = go.AddComponent<Button>();

        // Подпись кнопки
        var t = new GameObject("Text", typeof(RectTransform));
        t.transform.SetParent(go.transform, false);
        ApplyAnchor(t.GetComponent<RectTransform>(), AnchorPreset.Stretch);
        var tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.font = s_MainFont;
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return btn;
    }

    // --------------------------------------------------------- Утилиты

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
