using UnityEngine;

public static class PlayerNotice
{
    public static event System.Action Changed;
    public static string Key { get; private set; }
    public static float PostedAt { get; private set; } = -1f;

    public static void Post(string key)
    {
        Key = key;
        PostedAt = Time.realtimeSinceStartup;
        Changed?.Invoke();
    }

    public static bool IsActive(float duration) =>
        !string.IsNullOrEmpty(Key) && PostedAt >= 0f &&
        Time.realtimeSinceStartup - PostedAt < duration;

    public static void Clear()
    {
        Key = null;
        PostedAt = -1f;
        Changed?.Invoke();
    }
}
