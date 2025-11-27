using System.Runtime.InteropServices;

public static class AnalyticsBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void IncrementSceneCounter(string id, string name, int index);
    [DllImport("__Internal")] private static extern void LogSceneVisit(string id, string name, int index);
    [DllImport("__Internal")] private static extern void GetTopVisitedScenes(string unityObjName, string callbackName);

#else
    private static void IncrementSceneCounter(string id, string name, int index) { }
    private static void LogSceneVisit(string id, string name, int index) { }
    private static void GetTopVisitedScenes(string unityObjName, string callbackName) { }

#endif

    public static void Increment(string id, string title, int index) => IncrementSceneCounter(id, title, index);
    public static void Log(string id, string title, int index) => LogSceneVisit(id, title, index);

    public static void RequestTopVisited(string unityReceiver, string callback)
    {
        GetTopVisitedScenes(unityReceiver, callback);
    }
}
