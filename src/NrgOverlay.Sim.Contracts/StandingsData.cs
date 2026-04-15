namespace NrgOverlay.Sim.Contracts;

/// <summary>
/// Full-field leaderboard, published at ~10 Hz.
/// All on-track drivers sorted by overall race position.
/// </summary>
public sealed class StandingsData
{
    public IReadOnlyList<StandingsEntry> Entries { get; init; } = [];
}

