using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AssetSearcher : EditorWindow
{
    UnityEngine.Object newObject = null;


    [MenuItem("MoonStudios/AssetSearcher")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(AssetSearcher));
    }

    void OnGUI()
    {
        newObject = EditorGUILayout.ObjectField("", newObject, typeof(UnityEngine.Object), true);


        
        if (GUILayout.Button("search"))
        {
            Search();
        }
    }

    void Search()
    {

    }
}
