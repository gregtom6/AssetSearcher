using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class AssetSearcher : EditorWindow
{
    UnityEngine.Object newObject = null;
    Dictionary<FieldInfo, ShouldISearchForItAndString> fieldsAndTheirValuesToSearchFor = new Dictionary<FieldInfo, ShouldISearchForItAndString>();
    List<string> valueOfFields = new List<string>();
    List<bool> shouldISearchForBools = new List<bool>();
    System.Type previousType;

    [MenuItem("MoonStudios/AssetSearcher")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(AssetSearcher));
    }

    void OnGUI()
    {
        newObject = EditorGUILayout.ObjectField("", newObject, typeof(UnityEngine.Object), true);

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
            Search();
        }
    }

    void Search()
    {

    }
}

public class ShouldISearchForItAndString
{
    public bool shouldISearchForIt;
    public object value;
}
