using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class ShopCheckLauncher
{
    static ShopCheckLauncher() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Shop SideScroller/Verify Movement in Play Mode")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool("ShopChecksRequested",true);
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ShopChecksRequested",false))
        {
            SessionState.SetBool("ShopChecksRequested",false);
            new GameObject("Temporary movement verification").AddComponent<ShopPlayModeChecks>();
        }
    }
}


