using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum SuspiciousActionType
{
    RpcSenderMismatch,
    InvalidCardSelection,
    ManaOverBudget,
    ForeignCardPackage
}

public class SuspiciousActivityLog
{
    public readonly struct Entry
    {
        public SuspiciousActionType Type { get; }
        public GamePhase Phase { get; }
        public float Timestamp { get; }
        public string Detail { get; }

        public Entry(SuspiciousActionType type, GamePhase phase, float timestamp, string detail)
        {
            Type = type;
            Phase = phase;
            Timestamp = timestamp;
            Detail = detail;
        }
    }

    private const int k_RecentEntryLimit = 20;
    private readonly Dictionary<SuspiciousActionType, int> m_counts = new();
    private readonly Entry[] m_recentEntries = new Entry[k_RecentEntryLimit];
    private int m_nextEntryIndex;
    private int m_entryCount;

    public int Total { get; private set; }

    public void Record(SuspiciousActionType type, GamePhase phase, string detail)
    {
        m_counts[type] = GetCount(type) + 1;
        Total++;

        m_recentEntries[m_nextEntryIndex] = new Entry(type, phase, Time.time, detail);
        m_nextEntryIndex = (m_nextEntryIndex + 1) % k_RecentEntryLimit;
        if (m_entryCount < k_RecentEntryLimit)
        {
            m_entryCount++;
        }
    }

    public int GetCount(SuspiciousActionType type)
    {
        return m_counts.TryGetValue(type, out int count) ? count : 0;
    }

    public IReadOnlyList<Entry> GetRecentEntries()
    {
        Entry[] entries = new Entry[m_entryCount];
        int startIndex = (m_nextEntryIndex - m_entryCount + k_RecentEntryLimit) % k_RecentEntryLimit;
        for (int i = 0; i < m_entryCount; i++)
        {
            entries[i] = m_recentEntries[(startIndex + i) % k_RecentEntryLimit];
        }

        return Array.AsReadOnly(entries);
    }

    public string ToSummaryString()
    {
        StringBuilder summary = new StringBuilder($"Total: {Total}");
        foreach (KeyValuePair<SuspiciousActionType, int> count in m_counts)
        {
            summary.Append($"; {count.Key}: {count.Value}");
        }

        return summary.ToString();
    }
}
