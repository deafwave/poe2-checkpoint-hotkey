using System.Windows.Forms;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace CheckpointHotkey;

public class CheckpointHotkeySettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);
    public HotkeyNode RespawnHotkey { get; set; } = new(Keys.F4);
}
