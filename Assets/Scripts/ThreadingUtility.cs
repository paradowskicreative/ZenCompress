using System.Threading;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
 
#if UNITY_EDITOR
[InitializeOnLoad]
#endif
public static class ThreadingUtility
{
    static readonly CancellationTokenSource quitSource;
 
    public static CancellationToken QuitToken { get; }
 
    public static SynchronizationContext UnityContext { get; private set; }
 
    static ThreadingUtility()
    {
        quitSource = new CancellationTokenSource();
        QuitToken  = quitSource.Token;
    }
 
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void MainThreadInitialize()
    {
        UnityContext          = SynchronizationContext.Current;
        Application.quitting += quitSource.Cancel;
    #if UNITY_EDITOR
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    #endif
    }
 
#if UNITY_EDITOR
    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
            quitSource.Cancel();
    }
#endif
}