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
    List<string> valueOfFields = new List<string>();
    List<bool> shouldISearchForBools = new List<bool>();
    System.Type previousType;

    static List<SearchResult> searchResults = new List<SearchResult>();

    HashSet<string> scenesToSearch = new HashSet<string>();

    bool wasThereSearchAndNoBrowsingAfterThat;

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

            for (int i = 0; i < monoBehaviourType.GetFields().Length; i++)
            {
                valueOfFields.Add("");
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
                valueOfFields[i] = EditorGUILayout.TextField(field.Name + " " + field.FieldType.ToString() + " ", valueOfFields[i]);
                fieldsAndTheirValuesToSearchFor[field] = new ShouldISearchForItAndString()
                {
                    shouldISearchForIt = shouldISearchForBools[i],
                    value = valueOfFields[i],
                };
                GUILayout.EndHorizontal();
            }


            previousType = monoBehaviourType;
        }

        if (GUILayout.Button("search"))
        {
            searchResults.Clear();
            Search();
            wasThereSearchAndNoBrowsingAfterThat = true;
        }

        int indexOfFirstProjectFileResult = searchResults.IndexOf(searchResults.Where(x => x.scene.name == null).FirstOrDefault());


        for (int i = 0; i < searchResults.Count; i++)
        {
            if (i == 0 && searchResults[i].scene.name != null)
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
                else if (searchResults[i].scriptableObjects[j] != null)
                    EditorGUILayout.ObjectField(searchResults[i].scriptableObjects[j], typeof(ScriptableObject), true);
                else if (searchResults[i].materials[j] != null)
                    EditorGUILayout.ObjectField(searchResults[i].materials[j], typeof(Material), true);
                else if (searchResults[i].animators[j]!=null)
                    EditorGUILayout.ObjectField(searchResults[i].animators[j], typeof(RuntimeAnimatorController), true);
                GUILayout.EndHorizontal();
            }
        }

        if (searchResults.Count == 0 && wasThereSearchAndNoBrowsingAfterThat)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("no result");
            GUILayout.EndHorizontal();
        }

        /*
        if(searchResults.Count > 0)
        {
            foreach (string scenePath in scenesToSearch)
                EditorSceneManager.CloseScene(EditorSceneManager.GetSceneByPath(scenePath), true);
        }
        */

    }

    void Search()
    {
        if (newObject == null) { return; }

        SearchingInScenes();

        SearchingInProjectAssets();
    }

    void SearchingInScenes()
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
        for (int i = 0; i < sceneGuids.Length; i++)
            scenesToSearch.Add(AssetDatabase.GUIDToAssetPath(sceneGuids[i]));

        foreach (string scenePath in scenesToSearch)
        {
            Scene scene = EditorSceneManager.GetSceneByPath(scenePath);

            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            GameObject[] rootGameObjects = scene.GetRootGameObjects();

            for (int i = 0; i < rootGameObjects.Length; i++)
            {
                SearchInsideGameObject(rootGameObjects[i], scene.name);
            }
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
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c is MonoBehaviour)
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
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c is Animator)
            {
                Animator animator = (Animator)c;
                RuntimeAnimatorController animatorController = animator.runtimeAnimatorController;
                if (animatorController.animationClips.Contains((AnimationClip)newObject))
                {
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
            }
            else
            {
                SearchAny(go, sceneName, c);
            }
        }
    }

    static void SearchAnimatorInGO(GameObject go, string sceneName = null)
    {
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c is Animator)
            {
                Animator animator = (Animator)c;
                RuntimeAnimatorController animatorController = animator.runtimeAnimatorController;

                if (animatorController == (AnimatorController)newObject)
                {
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
            }
            else
            {
                SearchAny(go, sceneName, c);
            }
        }
    }

    static void SearchAudioClipInGO(GameObject go, string sceneName = null)
    {
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c is AudioSource)
            {
                AudioSource audioSource = (AudioSource)c;

                if (audioSource.clip == (AudioClip)newObject)
                {
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
            }
            else
            {
                SearchAny(go, sceneName, c);
            }
        }
    }

    static void SearchTexture2DInGO(GameObject go, string sceneName = null)
    {
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c is Animator)
            {
                Animator animator = (Animator)c;
                RuntimeAnimatorController animatorController = animator.runtimeAnimatorController;
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
        if (mat.shader == newObject)
        {
            if (!searchResults.Any(x => x.scene == EditorSceneManager.GetSceneByName("")))
            {
                searchResults.Add(new SearchResult()
                {
                    scene = EditorSceneManager.GetSceneByName(""),
                    gameObjects = new List<GameObject>(),
                });

                AddNewElementToResult(mat, path);
            }
            else
            {
                AddNewElementToResult(mat, path);
            }
        }
    }

    void SearchInsideAnimatorAsset(RuntimeAnimatorController runtimeAnimatorController, string path)
    {
        if (runtimeAnimatorController.animationClips.Contains((AnimationClip)newObject))
        {
            if (!searchResults.Any(x => x.scene == EditorSceneManager.GetSceneByName("")))
            {
                searchResults.Add(new SearchResult()
                {
                    scene = EditorSceneManager.GetSceneByName(""),
                    gameObjects = new List<GameObject>(),
                });

                AddNewElementToResult(runtimeAnimatorController, path);
            }
            else
            {
                AddNewElementToResult(runtimeAnimatorController, path);
            }

        }
    }

    static void SearchMaterialInGO(GameObject go, string sceneName = null)
    {
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            SerializedObject so = new SerializedObject(c);
            SerializedProperty iterator = so.GetIterator();

            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (iterator.objectReferenceValue is Material && iterator.objectReferenceValue == newObject)
                    {
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
                }
            }
        }
    }

    static void SearchShaderInGO(GameObject go, string sceneName = null)
    {
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            SerializedObject so = new SerializedObject(c);
            SerializedProperty iterator = so.GetIterator();

            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (IsItInsideMaterial(iterator) || IsItTheSearchedShader(iterator))
                    {
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
                }
            }
        }
    }

    static void SearchPrefabInGO(GameObject go, string sceneName = null)
    {
        GameObject prefabObject = PrefabUtility.GetCorrespondingObjectFromSource(go);

        if (prefabObject == newObject)
        {
            if (!searchResults.Any(x => x.scene == EditorSceneManager.GetSceneByName(sceneName)))
            {
                searchResults.Add(new SearchResult()
                {
                    scene = EditorSceneManager.GetSceneByName(sceneName),
                    gameObjects = new List<GameObject>(),
                });

                AddNewElementToResult(sceneName, go, null);
            }
            else
            {
                AddNewElementToResult(sceneName, go, null);
            }
        }
        else
        {
            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                SerializedObject so = new SerializedObject(c);
                SerializedProperty iterator = so.GetIterator();

                while (iterator.Next(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (iterator.objectReferenceValue is GameObject && iterator.objectReferenceValue == newObject)
                        {
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
                    }
                }
            }
        }
    }

    static void SearchScriptableObjectInGO(GameObject go, string sceneName = null)
    {
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            SerializedObject so = new SerializedObject(c);
            SerializedProperty iterator = so.GetIterator();

            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (iterator.objectReferenceValue is ScriptableObject && iterator.objectReferenceValue == newObject)
                    {
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
                }
            }
        }
    }

    static void SearchAny(GameObject go, string sceneName, Component c)
    {
        SerializedObject so = new SerializedObject(c);
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

                        AddNewElementToResult(sceneName, go, c);
                    }
                    else
                    {
                        AddNewElementToResult(sceneName, go, c);
                    }
                }
            }
        }
    }

    void SearchAnyInGO(GameObject go, string sceneName = null)
    {
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            SearchAny(go, sceneName, c);
        }
    }

    void SearchAnyInSC(ScriptableObject sr, string sceneName = null, string path = null)
    {
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

                        AddNewElementToResult(sr, path);
                    }
                    else
                    {
                        AddNewElementToResult(sr, path);
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
        List<bool> valueChecks = new List<bool>();

        foreach (FieldInfo fieldInfo in monoBehaviour.GetType().GetFields())
        {
            if (fieldsAndTheirValuesToSearchFor[fieldInfo].shouldISearchForIt)
                valueChecks.Add(fieldInfo.GetValue(monoBehaviour).ToString() == fieldsAndTheirValuesToSearchFor[fieldInfo].value.ToString());
        }


        bool shouldAdd = true;

        for (int i = 0; i < valueChecks.Count; i++)
        {
            if (!valueChecks[i])
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

            SearchResult searchResult = searchResults.Where(x => x.scene == EditorSceneManager.GetSceneByName(sceneName)).FirstOrDefault();
            if (!searchResult.paths.Contains(sceneName + GetGameObjectPath(go.transform, c)))
            {
                searchResult.paths.Add(sceneName + GetGameObjectPath(go.transform, c));
                searchResult.gameObjects.Add(go);
            }
        }
    }

    static void AddNewElementToResult(string sceneName, GameObject go, Component c)
    {
        SearchResult searchResult = searchResults.Where(x => x.scene == EditorSceneManager.GetSceneByName(sceneName)).FirstOrDefault();
        if (!searchResult.paths.Contains(sceneName + GetGameObjectPath(go.transform, c)))
        {
            searchResult.paths.Add(sceneName + GetGameObjectPath(go.transform, c));
            searchResult.gameObjects.Add(go);
        }
    }

    void AddNewElementToResult(ScriptableObject scriptableObject, string path)
    {
        SearchResult searchResult = searchResults.Where(x => x.scene == EditorSceneManager.GetSceneByName("")).FirstOrDefault();
        if (!searchResult.paths.Contains(path))
        {
            searchResult.paths.Add(path);
            searchResult.gameObjects.Add(null);
            searchResult.scriptableObjects.Add(scriptableObject);
        }
    }

    void AddNewElementToResult(Material material, string path)
    {
        SearchResult searchResult = searchResults.Where(x => x.scene == EditorSceneManager.GetSceneByName("")).FirstOrDefault();
        if (!searchResult.paths.Contains(path))
        {
            searchResult.paths.Add(path);
            searchResult.gameObjects.Add(null);
            searchResult.scriptableObjects.Add(null);
            searchResult.materials.Add(material);
        }
    }

    void AddNewElementToResult(RuntimeAnimatorController animator, string path)
    {
        SearchResult searchResult = searchResults.Where(x => x.scene == EditorSceneManager.GetSceneByName("")).FirstOrDefault();
        if (!searchResult.paths.Contains(path))
        {
            searchResult.paths.Add(path);
            searchResult.gameObjects.Add(null);
            searchResult.scriptableObjects.Add(null);
            searchResult.materials.Add(null);
            searchResult.animators.Add(animator);
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

public class ShouldISearchForItAndString
{
    public bool shouldISearchForIt;
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