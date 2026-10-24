# Game launch + proxy lifecycle patch

The launcher source ZIP contains the Select Game handler and proxy start/stop message handlers, but the reviewed `Form1.cs` has no `launchGame` message case. This patch adds `GameProcessManager.cs` so the game process owns the proxy lifetime instead of relying on the web UI to send a stop message.

## Integrate in Form1

1. Add a field:
   ```csharp
   private GameProcessManager? _gameProcessManager;
   ```

2. After `_proxy` and Windows proxy setup methods are available, initialize it so the delegates use the existing proxy methods:
   ```csharp
   _gameProcessManager = new GameProcessManager(
       startProxy: StartProxyAsync,
       stopProxy: StopProxyAsync);
   ```

3. Add this case to the existing WebView2 message switch:
   ```csharp
   case "launchGame":
   {
       var state = LoadState();
       if (string.IsNullOrWhiteSpace(state.GamePath))
       {
           await SendToPageAsync(new { type = "gameLaunchResult", ok = false, message = "กรุณาเลือกไฟล์เกม .exe ก่อน" });
           break;
       }

       _gameProcessManager ??= new GameProcessManager(StartProxyAsync, StopProxyAsync);
       try
       {
           await _gameProcessManager.LaunchAsync(state.GamePath);
           await SendToPageAsync(new { type = "gameLaunchResult", ok = true, message = "เริ่มเกมแล้ว" });
       }
       catch (Exception ex)
       {
           await SendToPageAsync(new { type = "gameLaunchResult", ok = false, message = ex.Message });
       }
       break;
   }
   ```

4. The UI must send `{ type: "launchGame" }` when Launch Game is clicked. Do not also start the proxy from the UI; the process manager owns this lifecycle.

5. In `Form1_FormClosing`, dispose the process manager asynchronously or synchronously wait for its `DisposeAsync()` before the form exits. Ensure the existing `StopProxyAsync` restores the original Windows proxy settings in a `finally` path.

## Important limitation

This is a source patch, not a verified full launcher build. The GitHub repository currently does not contain the main WinForms project; the source ZIP needs to be imported before CI can compile the complete app. Test on Windows with the actual game executable and verify proxy restoration after normal exit, startup failure, and forced game termination.
