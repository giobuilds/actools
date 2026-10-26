namespace AcManager.Core.Bridge;

/// <summary>
/// Reads pages until the game leaves. Drive opens the socket once each visit, so a finished read keeps the helper up.
/// </summary>
public static class BridgeLoop
{
    public const int GameStartGraceMs = 15000;

    public static void Run(SharedPagePump pump, Func<bool> gameIsRunning, Func<long> nowMs, Action<int> wait)
    {
        var started = nowMs();
        var sawGame = false;
        while (true) {
            var playing = false;
            try {
                playing = gameIsRunning();
            } catch (SystemException) {
                playing = false;
            }

            if (playing) {
                sawGame = true;
            } else if (sawGame || nowMs() - started >= GameStartGraceMs) {
                return;
            }

            wait(pump.Tick(nowMs()));
        }
    }
}
