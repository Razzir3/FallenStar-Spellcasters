public static class UiResolutionProfile
{
    public const int MediumMinWidth = 1366;

    public static bool IsLow(int screenWidth) => screenWidth < MediumMinWidth;
}
