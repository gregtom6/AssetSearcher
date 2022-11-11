using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AssetSearcher : EditorWindow
{
    string myString = "Hello World";
    bool groupEnabled;
    bool myBool = true;
    float myFloat = 1.23f;

    // Add menu item named "My Window" to the Window menu
    [MenuItem("MoonStudios/AssetSearcher")]
    public static void ShowWindow()
    {
        //Show existing window instance. If one doesn't exist, make one.
        EditorWindow.GetWindow(typeof(AssetSearcher));
    }

    void OnGUI()
    {
        //GUILayout.Label("Base Settings", EditorStyles.boldLabel);
        //myString = EditorGUILayout.TextField("Text Field", myString);

        //groupEnabled = EditorGUILayout.BeginToggleGroup("Optional Settings", groupEnabled);

        UnityEngine.Object prevObj = null;
        UnityEngine.Object newObject = EditorGUILayout.ObjectField("", prevObj, typeof(UnityEngine.Object), true);


        //newObject.GetType().IsAssignableFrom(newObject.GetType());
        /*
        if (GUILayout.Button("adada"))
        {

        }
        */

        //myBool = EditorGUILayout.Toggle("Toggle", myBool);
        //myFloat = EditorGUILayout.Slider("Slider", myFloat, -3, 3);
        //EditorGUILayout.EndToggleGroup();
    }
}
