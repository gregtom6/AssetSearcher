using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AssetSearcher : EditorWindow
{
    static UnityEngine.Object newObject = null;
    static Dictionary<FieldInfo, ShouldISearchForItAndString> fieldsAndTheirValuesToSearchFor = new Dictionary<FieldInfo, ShouldISearchForItAndString>();
    List<UnityObjectAndString> valueOfFields = new List<UnityObjectAndString>();
    List<bool> shouldISearchForBools = new List<bool>();
    System.Type previousType;

    static List<SearchResult> searchResults = new List<SearchResult>();

    HashSet<string> scenesToSearch = new HashSet<string>();

    bool wasThereSearchAndNoBrowsingAfterThat;

    int countBeforeSearching;

    string searchedSceneName;

    Vector2 verticalScrollPos = Vector2.zero;

    string[] sceneGuids;

    Dictionary<Type, Action<GameObject, string>> searchingMethods = new Dictionary<Type, Action<GameObject, string>>()
    {
        { typeof(MonoScript), SearchMonoScriptInGO},
        { typeof(AnimationClip), SearchAnimationClipInGO },
        {typeof(AnimatorController), SearchAnimatorInGO },
        {typeof(AudioClip), SearchAudioClipInGO },
        {typeof(Texture2D), SearchTexture2DInGO},
        {typeof(Material), SearchMaterialInGO},
        {typeof(Shader), SearchShaderInGO},
        {typeof(GameObject), SearchPrefabInGO },
        {typeof(ScriptableObject), SearchScriptableObjectInGO }
    };

    [MenuItem("MoonStudios/AssetSearcher")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(AssetSearcher));
    }

    void OnGUI()
    {
        UnityEngine.Object prevObject = newObject;
        newObject = EditorGUILayout.ObjectField("", newObject, typeof(UnityEngine.Object), true);

        if (prevObject != newObject)
        {
            searchResults.Clear();
            wasThereSearchAndNoBrowsingAfterThat = false;
        }

        if (newObject is MonoScript)
        {
            MonoScript monoScript = (MonoScript)newObject;
            System.Type monoBehaviourType = monoScript.GetClass();

            if (monoBehaviourType != previousType)
            {
                valueOfFields.Clear();
                shouldISearchForBools.Clear();
                fieldsAndTheirValuesToSearchFor.Clear();
            }

            if (monoBehaviourType != null)
            {
                for (int i = 0; i < monoBehaviourType.GetFields().Length; i++)
                {
                    valueOfFields.Add(null);
                    shouldISearchForBools.Add(false);
                    if (!fieldsAndTheirValuesToSearchFor.ContainsKey(monoBehaviourType.GetFields()[i]))
                        fieldsAndTheirValuesToSearchFor.Add(monoBehaviourType.GetFields()[i], new ShouldISearchForItAndString()
                        {
                            shouldISearchForIt = false,
                            value = "",
                        });
                }

                for (int i = 0; i < monoBehaviourType.GetFields().Length; i++)
                {
                    FieldInfo field = monoBehaviourType.GetFields()[i];
                    GUILayout.BeginHorizontal();
                    shouldISearchForBools[i] = EditorGUILayout.Toggle("search for it? ", shouldISearchForBools[i]);
                    EditorGUILayout.LabelField(field.Name);
                    EditorGUILayout.LabelField(field.FieldType.ToString());

                    if (valueOfFields[i] == null)
                    {
                        valueOfFields[i] = new UnityObjectAndString();
                    }

                    if (field.FieldType.IsValueType || field.FieldType == typeof(string))
                    {
                        valueOfFields[i].value = EditorGUILayout.TextField(valueOfFields[i].value.ToString());
                    }
                    else
                    {
                        valueOfFields[i].obj = EditorGUILayout.ObjectField(valueOfFields[i].obj, field.FieldType, true);
                    }
                    fieldsAndTheirValuesToSearchFor[field] = new ShouldISearchForItAndString()
                    {
                        shouldISearchForIt = shouldISearchForBools[i],
                        value = valueOfFields[i].value,
                        obj = valueOfFields[i].obj,
                    };

                    GUILayout.EndHorizontal();
                }
            }

            previousType = monoBehaviourType;
        }

        if (GUILayout.Button("search in project file assets"))
        {
            SearchResult searchResult = searchResults.Where(x => x.scene == EditorSceneManager.GetSceneByName("")).FirstOrDefault();
            if (searchResult != null)
                searchResults.RemoveAt(searchResults.IndexOf(searchResult));

            wasThereSearchAndNoBrowsingAfterThat = true;

            countBeforeSearching = searchResults.Count;
            searchedSceneName = "";
            if (newObject == null) { return; }



            SearchingInProjectAssets();
        }

        if (sceneGuids == null || sceneGuids.Length == 0)
        {
            sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
            for (int i = 0; i < sceneGuids.Length; i++)
                scenesToSearch.Add(AssetDatabase.GUIDToAssetPath(sceneGuids[i]));
        }

        foreach (string scenePath in scenesToSearch)
        {
            string sceneName = scenePath.Split('/').Last();
            if (GUILayout.Button("search in " + sceneName))
            {
                Scene scene = EditorSceneManager.GetSceneByName(sceneName);

                if (!scene.isLoaded)
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                SearchResult searchResult = searchResults.Where(x => x.scene == scene).FirstOrDefault();
                if (searchResult != null)
                    searchResults.RemoveAt(searchResults.IndexOf(searchResult));

                wasThereSearchAndNoBrowsingAfterThat = true;

                searchedSceneName = sceneName;
                countBeforeSearching = searchResults.Count;
                if (newObject == null) { return; }

                SearchingInScenes(scene, scenePath);
            }
        }

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("");
        GUILayout.EndHorizontal();

        if (GUILayout.Button("clear results and unload all opened scenes"))
        {
            searchResults.Clear();

            int countLoaded = SceneManager.sceneCount;
            Scene[] loadedScenes = new Scene[countLoaded];
            for (int i = 0; i < countLoaded; i++)
            {
                loadedScenes[i] = SceneManager.GetSceneAt(i);
            }

            foreach (Scene scene in loadedScenes)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        searchResults = searchResults.OrderBy(x => x.scene.name).ToList();
        searchResults = searchResults.OrderBy(x => x.scene.name != null ? 0 : 1).ToList();

        int indexOfFirstProjectFileResult = searchResults.IndexOf(searchResults.Where(x => x.scene.name == null).FirstOrDefault());

        if (searchResults.Count > 0)
        {
            EditorGUILayout.BeginVertical();
            verticalScrollPos = EditorGUILayout.BeginScrollView(verticalScrollPos);
        }

        for (int i = 0; i < searchResults.Count; i++)
        {
            if (i == 0 && searchResults.Any(x => x.scene.name != null))
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("--- Scene references ---");
                GUILayout.EndHorizontal();
            }

            if (i == indexOfFirstProjectFileResult)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("--- Project asset references ---");
                GUILayout.EndHorizontal();
            }

            for (int j = 0; j < searchResults[i].gameObjects.Count; j++)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(searchResults[i].paths[j] + ((searchResults[i].scene.name == null && searchResults[i].gameObjects[j] != null) ? " - " + AssetDatabase.GetAssetPath(searchResults[i].gameObjects[j]) : ""));
                if (searchResults[i].gameObjects[j] != null)
                    EditorGUILayout.ObjectField(searchResults[i].gameObjects[j], typeof(GameObject), true);
                else if (j < searchResults[i].scriptableObjects.Count && searchResults[i].scriptableObjects[j] != null)
                    EditorGUILayout.ObjectField(searchResults[i].scriptableObjects[j], typeof(ScriptableObject), true);
                else if (j < searchResults[i].materials.Count && searchResults[i].materials[j] != null)
                    EditorGUILayout.ObjectField(searchResults[i].materials[j], typeof(Material), true);
                else if (j < searchResults[i].animators.Count && searchResults[i].animators[j] != null)
                    EditorGUILayout.ObjectField(searchResults[i].animators[j], typeof(RuntimeAnimatorController), true);
                GUILayout.EndHorizontal();
            }
        }

        if (countBeforeSearching == searchResults.Count && wasThereSearchAndNoBrowsingAfterThat)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (searchedSceneName != "")
                EditorGUILayout.LabelField("no result in scene " + searchedSceneName);
            else
                EditorGUILayout.LabelField("no result in project files");
            GUILayout.EndHorizontal();
        }

        if (searchResults.Count > 0)
        {
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }

    void SearchingInScenes(Scene scene, string scenePath)
    {
        GameObject[] rootGameObjects = scene.GetRootGameObjects();

        for (int i = 0; i < rootGameObjects.Length; i++)
        {
            SearchInsideGameObject(rootGameObjects[i], scene.name);
        }
    }

    void SearchingInProjectAssets()
    {
        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();

        foreach (string path in allAssetPaths)
        {
            System.Type type = AssetDatabase.GetMainAssetTypeAtPath(path);

            if (type != typeof(SceneAsset))
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is GameObject)
                    {
                        SearchInsideGameObject((GameObject)assets[i], "", path);
                    }
                    else if (assets[i] is ScriptableObject)
                    {
                        SearchAnyInSC((ScriptableObject)assets[i], "", path);
                    }
                    else if (assets[i] is Material)
                    {
                        SearchInsideMaterialAsset((Material)assets[i], path);
                    }
                    else if (assets[i] is RuntimeAnimatorController)
                    {
                        SearchInsideAnimatorAsset((RuntimeAnimatorController)assets[i], path);
                    }
                }
            }
        }
    }

    Action<GameObject, string> GetBackSearchingMethodBasedOnType(GameObject go, string sceneName)
    {
        if (newObject is ScriptableObject)
        {
            if (searchingMethods.ContainsKey(typeof(ScriptableObject)))
                return searchingMethods[typeof(ScriptableObject)];
        }
        else if (searchingMethods.ContainsKey(newObject.GetType()))
            return searchingMethods[newObject.GetType()];

        return null;
    }

    void SearchInsideGameObject(GameObject go, string sceneName = null, string path = null)
    {
        if (go == null) { return; }

        Action<GameObject, string> searchMethod = GetBackSearchingMethodBasedOnType(go, sceneName);

        if (searchMethod == null)
        {
            SearchAnyInGO(go, sceneName);
        }
        else
        {
            searchMethod?.Invoke(go, sceneName);
        }

        int childCount = go.transform.childCount;

        for (int i = 0; i < childCount; i++)
        {
            SearchInsideGameObject(go.transform.GetChild(i).gameObject, sceneName);
        }
    }

    static void SearchMonoScriptInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c != null && c is MonoBehaviour)
            {
                MonoBehaviour monoBehaviour = (MonoBehaviour)c;
                MonoScript script = MonoScript.FromMonoBehaviour(monoBehaviour);
                if (script == newObject)
                {
                    AddNewElementToResult(monoBehaviour, sceneName, go, c);
                }
                else
                {
                    SerializedObject so = new SerializedObject(c);
                    SerializedProperty iterator = so.GetIterator();

                    while (iterator.Next(true))
                    {
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            if (iterator.objectReferenceValue != null && iterator.objectReferenceValue is MonoBehaviour)
                            {
                                script = MonoScript.FromMonoBehaviour((MonoBehaviour)iterator.objectReferenceValue);
                                if (script == newObject)
                                {
                                    AddNewElementToResult((MonoBehaviour)iterator.objectReferenceValue, sceneName, go, c);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    static void SearchAnimationClipInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c != null && c is Animator)
            {
                Animator animator = (Animator)c;
                RuntimeAnimatorController animatorController = null;
                if (animator != null)
                    animatorController = animator.runtimeAnimatorController;
                if (animatorController != null && animatorController.animationClips != null && animatorController.animationClips.Contains((AnimationClip)newObject))
                {
                    InsertNewResultFromAScene(go, sceneName, c);
                }
            }
            else
            {
                SearchAny(go, sceneName, c);
            }
        }
    }

    static void SearchAnimatorInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];

            if (c != null && c is Animator)
            {
                Animator animator = (Animator)c;
                RuntimeAnimatorController animatorController = animator.runtimeAnimatorController;

                if (animatorController == (AnimatorController)newObject)
                {
                    InsertNewResultFromAScene(go, sceneName, c);
                }
            }
            else
            {
                SearchAny(go, sceneName, c);
            }
        }
    }



    static void SearchAudioClipInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];

            if (c != null && c is AudioSource)
            {
                AudioSource audioSource = (AudioSource)c;

                if (audioSource.clip == (AudioClip)newObject)
                {
                    InsertNewResultFromAScene(go, sceneName, c);
                }
            }
            else
            {
                SearchAny(go, sceneName, c);
            }
        }
    }

    static void SearchTexture2DInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];

            if (c != null && c is Animator)
            {
                Animator animator = (Animator)c;
                RuntimeAnimatorController animatorController = null;
                if (animator != null)
                    animatorController = animator.runtimeAnimatorController;

                if (animatorController != null && animatorController.animationClips != null)
                    for (int j = 0; j < animatorController.animationClips.Length; j++)
                    {
                        EditorCurveBinding[] objectCurves = AnimationUtility.GetObjectReferenceCurveBindings(animatorController.animationClips[j]);
                        for (int k = 0; k < objectCurves.Length; k++)
                        {
                            ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(animatorController.animationClips[j], objectCurves[k]);

                            for (int l = 0; l < keyframes.Length; l++)
                            {
                                if (TextureFromSprite((Sprite)keyframes[l].value) == newObject)
                                {
                                    InsertNewResultFromAScene(go, sceneName, c);
                                }
                            }
                        }
                    }
            }
            else
            {
                SearchAny(go, sceneName, c);
            }
        }
    }

    void SearchInsideMaterialAsset(Material mat, string path)
    {
        if (mat == null) { return; }

        if (mat.shader == newObject)
        {
            if (!searchResults.Any(x => x.scene == EditorSceneManager.GetSceneByName("")))
            {
                searchResults.Add(new SearchResult()
                {
                    scene = EditorSceneManager.GetSceneByName(""),
                    gameObjects = new List<GameObject>(),
                });

                AddNewAssetElementToResult(mat, path);
            }
            else
            {
                AddNewAssetElementToResult(mat, path);
            }
        }
    }

    void SearchInsideAnimatorAsset(RuntimeAnimatorController runtimeAnimatorController, string path)
    {
        if (runtimeAnimatorController == null || runtimeAnimatorController.animationClips == null) { return; }

        if (newObject is AnimationClip && runtimeAnimatorController.animationClips.Contains((AnimationClip)newObject))
        {
            if (!searchResults.Any(x => x.scene == EditorSceneManager.GetSceneByName("")))
            {
                searchResults.Add(new SearchResult()
                {
                    scene = EditorSceneManager.GetSceneByName(""),
                    gameObjects = new List<GameObject>(),
                });

                AddNewAssetElementToResult(runtimeAnimatorController, path);
            }
            else
            {
                AddNewAssetElementToResult(runtimeAnimatorController, path);
            }

        }
        else
        {
            for (int j = 0; j < runtimeAnimatorController.animationClips.Length; j++)
            {
                EditorCurveBinding[] objectCurves = AnimationUtility.GetObjectReferenceCurveBindings(runtimeAnimatorController.animationClips[j]);
                for (int k = 0; k < objectCurves.Length; k++)
                {
                    ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(runtimeAnimatorController.animationClips[j], objectCurves[k]);

                    for (int l = 0; l < keyframes.Length; l++)
                    {
                        if (TextureFromSprite((Sprite)keyframes[l].value) == newObject)
                        {
                            if (!searchResults.Any(x => x.scene == EditorSceneManager.GetSceneByName("")))
                            {
                                searchResults.Add(new SearchResult()
                                {
                                    scene = EditorSceneManager.GetSceneByName(""),
                                    gameObjects = new List<GameObject>(),
                                });

                                AddNewAssetElementToResult(runtimeAnimatorController, path);
                            }
                            else
                            {
                                AddNewAssetElementToResult(runtimeAnimatorController, path);
                            }
                        }
                    }
                }
            }
        }
    }

    static void SearchMaterialInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];

            if (c != null)
            {
                SerializedObject so = new SerializedObject(c);
                SerializedProperty iterator = so.GetIterator();

                while (iterator.Next(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (iterator.objectReferenceValue is Material && iterator.objectReferenceValue == newObject)
                        {
                            InsertNewResultFromAScene(go, sceneName, c);
                        }
                    }
                }
            }
        }
    }

    static void SearchShaderInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];

            if (c != null)
            {
                SerializedObject so = new SerializedObject(c);
                SerializedProperty iterator = so.GetIterator();

                while (iterator.Next(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (IsItInsideMaterial(iterator) || IsItTheSearchedShader(iterator))
                        {
                            InsertNewResultFromAScene(go, sceneName, c);
                        }
                    }
                }
            }
        }
    }

    static void SearchPrefabInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        GameObject prefabObj = PrefabUtility.GetCorrespondingObjectFromSource(go);
        GameObject prefabObject = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);

        if (prefabObject == newObject || prefabObj == newObject)
        {
            InsertNewResultFromAScene(go, sceneName, null);
        }
        else
        {
            if (go == newObject && (PrefabUtility.GetPrefabAssetType(newObject) == PrefabAssetType.Regular || PrefabUtility.GetPrefabAssetType(newObject) == PrefabAssetType.Variant))
            {
                InsertNewResultFromAScene(go, sceneName, null);
            }
            else
            {
                Component[] components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component c = components[i];

                    if (c != null)
                    {
                        SerializedObject so = new SerializedObject(c);
                        SerializedProperty iterator = so.GetIterator();

                        while (iterator.Next(true))
                        {
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                if (iterator.objectReferenceValue is GameObject && iterator.objectReferenceValue == newObject)
                                {
                                    InsertNewResultFromAScene(go, sceneName, c);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    static void SearchScriptableObjectInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];

            if (c != null)
            {
                SerializedObject so = new SerializedObject(c);
                SerializedProperty iterator = so.GetIterator();

                while (iterator.Next(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (iterator.objectReferenceValue is ScriptableObject && iterator.objectReferenceValue == newObject)
                        {
                            InsertNewResultFromAScene(go, sceneName, c);
                        }
                    }
                }
            }
        }
    }

    static void SearchAny(GameObject go, string sceneName, Component c)
    {
        if (c == null || go == null) { return; }

        SerializedObject so = new SerializedObject(c);
        SerializedProperty iterator = so.GetIterator();

        while (iterator.Next(true))
        {
            if (iterator.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (iterator.objectReferenceValue == newObject)
                {
                    InsertNewResultFromAScene(go, sceneName, c);
                }
            }
        }
    }

    void SearchAnyInGO(GameObject go, string sceneName = null)
    {
        if (go == null) { return; }

        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            SearchAny(go, sceneName, c);
        }
    }

    void SearchAnyInSC(ScriptableObject sr, string sceneName = null, string path = null)
    {
        if (sr == null) { return; }

        SerializedObject so = new SerializedObject(sr);
        SerializedProperty iterator = so.GetIterator();

        while (iterator.Next(true))
        {
            if (iterator.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (iterator.objectReferenceValue == newObject)
                {
                    if (!searchResults.Any(x => x.scene == EditorSceneManager.GetSceneByName(sceneName)))
                    {
                        searchResults.Add(new SearchResult()
                        {
                            scene = EditorSceneManager.GetSceneByName(sceneName),
                            gameObjects = new List<GameObject>(),
                        });

                        AddNewAssetElementToResult(sr, path);
                    }
                    else
                    {
                        AddNewAssetElementToResult(sr, path);
                    }
                }
            }
        }
    }

    static bool IsItInsideMaterial(SerializedProperty iterator)
    {
        return iterator.objectReferenceValue is Material && ((Material)iterator.objectReferenceValue).shader == newObject;
    }

    static bool IsItTheSearchedShader(SerializedProperty iterator)
    {
        return iterator.objectReferenceValue is Shader && (Shader)iterator.objectReferenceValue == newObject;
    }

    public static Texture2D TextureFromSprite(Sprite sprite)
    {
        if (sprite.rect.width != sprite.texture.width)
        {
            Texture2D newText = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
            Color[] newColors = sprite.texture.GetPixels((int)sprite.textureRect.x,
                                                         (int)sprite.textureRect.y,
                                                         (int)sprite.textureRect.width,
                                                         (int)sprite.textureRect.height);
            newText.SetPixels(newColors);
            newText.Apply();
            return newText;
        }
        else
            return sprite.texture;
    }

    static void AddNewElementToResult(MonoBehaviour monoBehaviour, string sceneName, GameObject go, Component c)
    {
        if (monoBehaviour == null || go == null) { return; }

        List<bool> fieldValueCheckResults = new List<bool>();

        foreach (FieldInfo fieldInfo in monoBehaviour.GetType().GetFields())
        {
            if (fieldsAndTheirValuesToSearchFor[fieldInfo].shouldISearchForIt)
            {
                if (fieldInfo.FieldType.IsValueType || fieldInfo.FieldType == typeof(string))
                    fieldValueCheckResults.Add(fieldInfo.GetValue(monoBehaviour).ToString() == fieldsAndTheirValuesToSearchFor[fieldInfo].value.ToString());
                else
                {
                    if (fieldsAndTheirValuesToSearchFor[fieldInfo].obj != null)
                        fieldValueCheckResults.Add(fieldInfo.GetValue(monoBehaviour).ToString() == fieldsAndTheirValuesToSearchFor[fieldInfo].obj.ToString());
                    else
                        fieldValueCheckResults.Add(fieldInfo.GetValue(monoBehaviour) == null);
                }
            }
        }


        bool shouldAdd = true;

        for (int i = 0; i < fieldValueCheckResults.Count; i++)
        {
            if (!fieldValueCheckResults[i])
                shouldAdd = false;
        }

        if (shouldAdd)
        {
            if (!searchResults.Any(x => x.scene == EditorSceneManager.GetSceneByName(sceneName)))
            {
                searchResults.Add(new SearchResult()
                {
                    scene = EditorSceneManager.GetSceneByName(sceneName),
                    gameObjects = new List<GameObject>(),
                });
            }

            AddNewElementToResult(sceneName, go, c);
        }
    }

    static void InsertNewResultFromAScene(GameObject go, string sceneName, Component c)
    {
        if (go == null) { return; }

        if (!searchResults.Any(x => x.scene == EditorSceneManager.GetSceneByName(sceneName)))
        {
            searchResults.Add(new SearchResult()
            {
                scene = EditorSceneManager.GetSceneByName(sceneName),
                gameObjects = new List<GameObject>(),
            });

            AddNewElementToResult(sceneName, go, c);
        }
        else
        {
            AddNewElementToResult(sceneName, go, c);
        }
    }

    static void AddNewElementToResult(string sceneName, GameObject go, Component c)
    {
        if (go == null) { return; }

        SearchResult searchResult = searchResults.Where(x => x.scene == EditorSceneManager.GetSceneByName(sceneName)).FirstOrDefault();
        if (!searchResult.paths.Contains(sceneName + GetGameObjectPath(go.transform, c)))
        {
            searchResult.paths.Add(sceneName + GetGameObjectPath(go.transform, c));
            searchResult.gameObjects.Add(go);
            searchResult.scriptableObjects.Add(null);
            searchResult.materials.Add(null);
            searchResult.animators.Add(null);
        }
    }

    void AddNewAssetElementToResult(UnityEngine.Object assetElement, string path)
    {
        if (assetElement == null) { return; }

        SearchResult searchResult = searchResults.Where(x => x.scene == EditorSceneManager.GetSceneByName("")).FirstOrDefault();
        if (!searchResult.paths.Contains(path))
        {
            searchResult.paths.Add(path);
            searchResult.gameObjects.Add(null);
            searchResult.scriptableObjects.Add(assetElement is ScriptableObject ? (ScriptableObject)assetElement : null);
            searchResult.materials.Add(assetElement is Material ? (Material)assetElement : null);
            searchResult.animators.Add(assetElement is RuntimeAnimatorController ? (RuntimeAnimatorController)assetElement : null);
        }
    }

    private static string GetGameObjectPath(Transform transform, Component c)
    {
        string path = "/" + transform.name;
        while (transform.transform.parent != null)
        {
            transform = transform.parent;
            path = "/" + transform.name + path;
        }
        return path + (c != null ? " - " + c.ToString() : "");
    }
}

public class UnityObjectAndString
{
    public UnityEngine.Object obj;
    public string value = "";
}

public class ShouldISearchForItAndString
{
    public bool shouldISearchForIt;
    public UnityEngine.Object obj;
    public object value;
}

public class SearchResult
{
    public Scene scene;
    public List<string> paths = new List<string>();
    public List<GameObject> gameObjects = new List<GameObject>();
    public List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();
    public List<Material> materials = new List<Material>();
    public List<RuntimeAnimatorController> animators = new List<RuntimeAnimatorController>();
}