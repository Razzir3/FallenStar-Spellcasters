public static class DebugModeGuard
{
    // Release player builds never run debug paths, whatever the Inspector says (L9).
    public static bool Allow(bool inspectorValue)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return inspectorValue;
#else
        return false;
#endif
    }
}
