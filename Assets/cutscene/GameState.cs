using UnityEngine;

public static class GameState
{
    // When true → gameplay is frozen (cutscene, intro, etc.)
    public static bool IsGameplayLocked = true;

    // When true, the ready check is complete and the opening cutscene may start.
    public static bool ArePlayersReady = false;

    public static void ResetPregame()
    {
        IsGameplayLocked = true;
        ArePlayersReady = false;
    }
}
