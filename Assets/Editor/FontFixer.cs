#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Автоматический ремонт после batch-сборки:
/// 1) привязывает исходные шрифтовые файлы (arial.ttf, seguisym.ttf) к TMP-ассетам —
///    без этого динамическая догрузка кириллицы не работает ("character not found");
/// 2) назначает префабу EdgePrefab постоянный материал (иначе LineRenderer розовый).
/// Запускается сам при перекомпиляции скриптов, а также через меню Tools -> "Починить шрифты".
/// </summary>
[InitializeOnLoad]
public static class FontFixer
{
    const string ArialFontPath = "Assets/Fonts/arial.ttf";
    const string SeguisymFontPath = "Assets/Fonts/seguisym.ttf";
    const string ArialAssetPath = "Assets/Fonts/ArialCyrillic Dynamic SDF.asset";
    const string SeguisymAssetPath = "Assets/Fonts/SegoeUISymbol Dynamic SDF.asset";
    const string EdgePrefabPath = "Assets/Prefabs/EdgePrefab.prefab";
    const string EdgeMaterialPath = "Assets/Prefabs/EdgeMaterial.mat";
    const string FixerFlag = "FontFixer.DidFix";

    static FontFixer()
    {
        EditorApplication.delayCall += RunFix;
    }

    [MenuItem("Tools/Починить шрифты")]
    public static void RunFromMenu()
    {
        SessionState.SetBool(FixerFlag, false);
        RunFix();
    }

    static void RunFix()
    {
        if (SessionState.GetBool(FixerFlag, false)) return;

        Font arialFont = AssetDatabase.LoadAssetAtPath<Font>(ArialFontPath);
        if (arialFont == null)
        {
            Debug.LogWarning("[FontFixer] arial.ttf ещё не импортировался. Подожди пару секунд и запусти Tools -> Починить шрифты.");
            return;
        }
        Font symbolFont = AssetDatabase.LoadAssetAtPath<Font>(SeguisymFontPath);

        bool changed = false;
        changed |= LinkSourceFont(ArialAssetPath, arialFont);
        if (symbolFont != null)
            changed |= LinkSourceFont(SeguisymAssetPath, symbolFont);

        changed |= FixEdgePrefab();

        SessionState.SetBool(FixerFlag, true);
        AssetDatabase.SaveAssets();

        if (changed)
            Debug.Log("[FontFixer] ГОТОВО! Шрифты перелинкованы, материал рёбер исправлен. Жми Play.");
        else
            Debug.Log("[FontFixer] Всё уже в порядке.");
    }

    /// <summary> Привязка исходного Font-ассета к динамическому TMP_FontAsset. </summary>
    static bool LinkSourceFont(string fontAssetPath, Font sourceFont)
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
        if (fontAsset == null)
        {
            Debug.LogWarning("[FontFixer] Не найден ассет: " + fontAssetPath);
            return false;
        }

        var so = new SerializedObject(fontAsset);
        var sourceProp = so.FindProperty("m_SourceFontFile");
        var editorProp = so.FindProperty("m_SourceFontFile_EditorRef");
        var guidProp = so.FindProperty("m_SourceFontFileGUID");

        if (editorProp.objectReferenceValue == sourceFont) return false; // уже привязан

        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sourceFont));
        sourceProp.objectReferenceValue = sourceFont;
        editorProp.objectReferenceValue = sourceFont;
        guidProp.stringValue = guid;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fontAsset);
        Debug.Log($"[FontFixer] {fontAsset.name}: привязан исходный шрифт {sourceFont.name}");
        return true;
    }

    /// <summary> Постоянный материал для LineRenderer рёбер (в batch он не сохранился). </summary>
    static bool FixEdgePrefab()
    {
        Material edgeMat = AssetDatabase.LoadAssetAtPath<Material>(EdgeMaterialPath);
        if (edgeMat == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[FontFixer] Шейдер Sprites/Default не найден!");
                return false;
            }
            edgeMat = new Material(shader);
            AssetDatabase.CreateAsset(edgeMat, EdgeMaterialPath);
            Debug.Log("[FontFixer] Создан материал EdgeMaterial.mat");
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(EdgePrefabPath);
        try
        {
            var lr = prefabRoot.GetComponent<LineRenderer>();
            if (lr == null)
            {
                Debug.LogWarning("[FontFixer] В EdgePrefab нет LineRenderer!");
                return false;
            }
            if (lr.sharedMaterial == edgeMat) return false;

            lr.sharedMaterial = edgeMat;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, EdgePrefabPath);
            Debug.Log("[FontFixer] EdgePrefab: материал исправлен");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
#endif
