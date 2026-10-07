package com.emeraldwall.puzzle2048.modes;

/** The ways to play. Only Classic keeps a resumable board and the power-ups. */
public enum GameMode
{
    CLASSIC,
    DAILY,
    TIME_ATTACK,
    SPRINT;

    public boolean isClassic()
    {
        return this == CLASSIC;
    }

    /** Suffix for per-mode preference keys; empty for Classic so old saves keep working. */
    public String keySuffix()
    {
        return isClassic() ? "" : "_" + name();
    }
}
