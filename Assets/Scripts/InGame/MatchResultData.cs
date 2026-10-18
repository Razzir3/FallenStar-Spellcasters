public static class MatchResultData
{
    public static bool HasResult { get; private set; }
    public static bool IsWinner { get; private set; }
    public static bool IsDraw { get; private set; }
    public static MatchEndReason Reason { get; private set; }

    public static void Set(bool isWinner, bool isDraw, MatchEndReason reason)
    {
        HasResult = true;
        IsWinner = isWinner;
        IsDraw = isDraw;
        Reason = reason;
    }

    public static void Clear()
    {
        HasResult = false;
        IsWinner = false;
        IsDraw = false;
        Reason = default;
    }
}
