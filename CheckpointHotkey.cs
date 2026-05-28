using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.Shared.Nodes;

namespace CheckpointHotkey;

public class CheckpointHotkey : BaseSettingsPlugin<CheckpointHotkeySettings>
{
    private const string LogPrefix = "[CheckpointHotkey]";
    private const int MaxEscapeAttempts = 10;
    private const int EscapeRetryDelayMs = 250;
    private const int MaxHoverAttempts = 11;
    private const int HoverPollDelayMs = 30;

    private Task _respawnTask;
    private CancellationTokenSource _areaCancellation = new();

    private bool IsRespawnRunning => _respawnTask is { IsCompleted: false };

    public override bool Initialise()
    {
        RegisterHotkey(Settings.RespawnHotkey);
        return true;
    }

    private static void RegisterHotkey(HotkeyNode hotkey)
    {
        Input.RegisterKey(hotkey);
        hotkey.OnValueChanged += () => { Input.RegisterKey(hotkey); };
    }

    public override void AreaChange(AreaInstance area)
    {
        _areaCancellation.Cancel();
        _areaCancellation = new CancellationTokenSource();
        _respawnTask = null;
    }

    public override void Render()
    {
        if (!Settings.Enable) return;
        if (!GameController.InGame && !GameController.IsLoading) return;
        if (IsRespawnRunning) return;

        if (Settings.RespawnHotkey.PressedOnce())
        {
            LogMessage($"{LogPrefix} Respawning at checkpoint...");
            _respawnTask = Task.Run(() => HandleRespawnAsync(_areaCancellation.Token));
        }
    }

    private async Task HandleRespawnAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await TryOpenEscapeMenuAsync(cancellationToken))
            {
                LogError($"{LogPrefix} Escape menu did not open after repeated attempts. Aborting.");
                return;
            }

            var respawnButton = ResolveRespawnButton();
            if (respawnButton == null)
            {
                LogError($"{LogPrefix} Could not resolve the Respawn at Checkpoint button. The escape menu layout may have changed.");
                return;
            }

            await HoverAndClickAsync(respawnButton, cancellationToken);

            var confirmButton = await WaitForElementAsync(ResolveConfirmButton, cancellationToken);
            if (confirmButton == null)
            {
                LogError($"{LogPrefix} Confirm dialog did not appear. The layout may have changed.");
                return;
            }

            await HoverAndClickAsync(confirmButton, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogError($"{LogPrefix} Unexpected error in respawn sequence: {ex.Message}");
        }
    }

    private async Task<bool> TryOpenEscapeMenuAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxEscapeAttempts; attempt++)
        {
            if (GameController.Game.EscapeState.IsActive)
                return true;

            cancellationToken.ThrowIfCancellationRequested();
            Input.KeyPressRelease(Keys.Escape);
            await Task.Delay(EscapeRetryDelayMs, cancellationToken);
        }

        return GameController.Game.EscapeState.IsActive;
    }

    private Element ResolveRespawnButton() =>
        GameController.Game.EscapeState.HoveredElement?.GetChildFromIndices(0, 0, 0, 6);

    private Element ResolveConfirmButton() =>
        GameController.Game.EscapeState.HoveredElement?.GetChildFromIndices(0, 4, 0, 0, 3, 0);

    private async Task HoverAndClickAsync(Element element, CancellationToken cancellationToken)
    {
        var windowTopLeft = GameController.Window.GetWindowRectangle().TopLeft;

        for (int attempt = 0; attempt < MaxHoverAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Input.SetCursorPos(windowTopLeft + element.GetClientRect().Center);

            if (element.HasShinyHighlight)
                break;

            await Task.Delay(HoverPollDelayMs, cancellationToken);
        }

        Input.Click(MouseButtons.Left);
    }

    private static async Task<Element> WaitForElementAsync(Func<Element> resolve, CancellationToken cancellationToken, int attempts = 20, int pollIntervalMs = 100)
    {
        for (int i = 0; i < attempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Element element = null;
            try { element = resolve(); } catch { }
            if (element != null) return element;
            await Task.Delay(pollIntervalMs, cancellationToken);
        }
        return null;
    }
}
