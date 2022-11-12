using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TestScriptableObject", menuName = "TestScriptableObject")]
public class TestScriptableObject : ScriptableObject
{
    public AudioClip clip;
    public TestScript1 script1;
    public AnimationClip animclip;
    public RuntimeAnimatorController controller;
    public Sprite sprite;
    public Material material;
}
