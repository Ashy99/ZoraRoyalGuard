using System;
using Dalamud.Game.ClientState;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace AutoPetToContent;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Auto Pet->Content Emote";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private DateTime _lastTrigger = DateTime.MinValue;
    private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(2);

    public Plugin()
    {
        PluginInterface.Create<Plugin>();
        Chat.ChatMessage += OnChatMessage;
        Log.Information("Auto Pet->Content Emote loaded.");
    }

    public void Dispose()
    {
        Chat.ChatMessage -= OnChatMessage;
        Log.Information("Auto Pet->Content Emote unloaded.");
    }

    private void OnChatMessage(XivChatType type, int senderId, ref SeString message, ref string sender, ref string? unused)
    {
        if (type != XivChatType.Emote) return;
        if (ClientState?.LocalPlayer == null) return;

        // ignore your own emotes
        if (!string.IsNullOrEmpty(sender) && sender.Equals(ClientState.LocalPlayer?.Name.TextValue, StringComparison.OrdinalIgnoreCase))
            return;

        var text = message.TextValue; // e.g., "Alice pets you."

        var isPetOnYou_EN = text.Contains("pets you", StringComparison.OrdinalIgnoreCase)
                            || text.Contains("petting you", StringComparison.OrdinalIgnoreCase);

        var isDirectedAtYou_ByPayload = IsDirectedAtLocalPlayer(message, "pet");

        if ((isPetOnYou_EN || isDirectedAtYou_ByPayload) && DateTime.UtcNow - _lastTrigger > _cooldown)
        {
            _lastTrigger = DateTime.UtcNow;
            try
            {
                Chat.SendMessage("/content");
                Log.Debug("Auto /content triggered by incoming /pet emote line: {0}", text);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send /content command.");
            }
        }
    }

    private bool IsDirectedAtLocalPlayer(SeString msg, string keyword)
    {
        try
        {
            var local = ClientState.LocalPlayer;
            if (local == null) return false;

            var hasKeyword = msg.TextValue.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                             || msg.TextValue.Contains("pets", StringComparison.OrdinalIgnoreCase)
                             || msg.TextValue.Contains("petting", StringComparison.OrdinalIgnoreCase);

            foreach (var p in msg.Payloads)
            {
                if (p is PlayerPayload pp)
                {
                    if (pp.PlayerName.Equals(local.Name.TextValue, StringComparison.OrdinalIgnoreCase)
                        && (pp.World.RowId == local.HomeWorld.Id || pp.World.RowId == local.CurrentWorld.Id))
                    {
                        return hasKeyword;
                    }
                }
            }
        }
        catch { }
        return false;
    }
}
