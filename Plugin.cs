using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;

namespace PlayerNameMaskMod;

[BepInPlugin("local.amongus.playernamemask", "Player Name Mask", "1.0.2")]
public sealed class Plugin : BasePlugin
{
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ManualLogSource Logger = null!;
    public override void Load()
    {
        Logger = Log;
        Enabled = Config.Bind("General", "Enabled", true, "Mask other players' displayed names. F1 toggles this setting.");
        AddComponent<OfficialModStampBehaviour>();
        AddComponent<NameMaskBehaviour>();
        Log.LogInfo("Player Name Mask v1.0.2 loaded. F1 toggles masking; your own name stays visible. Host header included.");
    }
}

public sealed class NameMaskBehaviour : MonoBehaviour
{
    public NameMaskBehaviour(IntPtr pointer) : base(pointer) { }
    private sealed class Label
    {
        internal TMP_Text Text = null!;
        internal Func<int?> Owner = null!;
        internal bool ClientIdentity;
        internal Func<string?>? NamePart;
        internal MaskState State = new();
    }
    private readonly Dictionary<int, Label> labels = new();
    private readonly HashSet<int> seen = new();
    private readonly HashSet<string> ownNames = new(StringComparer.Ordinal);
    private float nextDiscovery;
    private bool wasEnabled, warned;

    public void Update()
    {
        if (Application.isFocused && Input.GetKeyDown(KeyCode.F1))
        {
            Plugin.Enabled.Value = !Plugin.Enabled.Value;
            Plugin.Logger.LogInfo("Name masking: " + (Plugin.Enabled.Value ? "ON" : "OFF"));
            nextDiscovery = 0;
        }
    }

    public void LateUpdate()
    {
        try { Tick(); }
        catch (Exception ex)
        {
            if (!warned) { warned = true; Plugin.Logger.LogError("Name mask update failed: " + ex); }
        }
    }

    [HideFromIl2Cpp]
    private void Tick()
    {
        var local = PlayerControl.LocalPlayer;
        if (!Plugin.Enabled.Value || !local || local.Data == null)
        {
            if (wasEnabled) RestoreAll();
            wasEnabled = false;
            return;
        }
        wasEnabled = true;
        int localId = local.PlayerId, localClient = local.Data.ClientId;
        ownNames.Clear();
        AddOwnName(local.Data.PlayerName);
        AddOwnName(local.CurrentOutfit?.PlayerName);
        if (Time.unscaledTime >= nextDiscovery)
        {
            nextDiscovery = Time.unscaledTime + 0.15f;
            Discover();
        }
        foreach (var label in labels.Values)
        {
            if (!label.Text) continue;
            string current = label.Text.text ?? "";
            string? namePart = label.NamePart?.Invoke();
            label.State.Update(current, label.Text.richText, false, namePart);
            int? owner = label.Owner();
            string visibleName = NameMask.VisibleName(label.State.Original, label.Text.richText);
            bool other = NameMask.IsOther(owner, label.ClientIdentity ? localClient : localId, visibleName, ownNames);
            string desired = label.State.Update(label.State.Original, label.Text.richText, other, namePart);
            if (current != desired) label.Text.text = desired;
        }
    }

    [HideFromIl2Cpp]
    private void AddOwnName(string? value)
    {
        if (value != null) { ownNames.Add(value); ownNames.Add(NameMask.VisibleName(value, true)); }
    }

    [HideFromIl2Cpp]
    private void Discover()
    {
        seen.Clear();
        foreach (var panel in Resources.FindObjectsOfTypeAll<HostInfoPanel>())
            if (panel)
            {
                Track(panel.playerName, HostClientId, true, HostPlayerName);
                if (panel.player && panel.player.cosmetics)
                    Track(panel.player.cosmetics.nameText, HostClientId, true);
            }
        foreach (var player in Resources.FindObjectsOfTypeAll<PlayerControl>())
            if (player && player.cosmetics)
                Track(player.cosmetics.nameText, () => player ? player.PlayerId : null);
        foreach (var area in Resources.FindObjectsOfTypeAll<PlayerVoteArea>())
            if (area)
            {
                Track(area.NameText, () => area ? (byte)area.PlayerId : null);
                if (area.PlayerIcon && area.PlayerIcon.cosmetics)
                    Track(area.PlayerIcon.cosmetics.nameText, () => area ? (byte)area.PlayerId : null);
            }
        foreach (var bubble in Resources.FindObjectsOfTypeAll<ChatBubble>())
            if (bubble)
            {
                Track(bubble.NameText, () => bubble && bubble.playerInfo != null ? bubble.playerInfo.PlayerId : null);
                if (bubble.Player && bubble.Player.cosmetics)
                    Track(bubble.Player.cosmetics.nameText, () => bubble && bubble.playerInfo != null ? bubble.playerInfo.PlayerId : null);
            }
        foreach (var notification in Resources.FindObjectsOfTypeAll<ChatNotification>())
            if (notification)
            {
                Track(notification.playerNameText, () => null);
                if (notification.player && notification.player.cosmetics)
                    Track(notification.player.cosmetics.nameText, () => null);
            }
        foreach (var button in Resources.FindObjectsOfTypeAll<BanButton>())
            if (button) Track(button.NameText, () => button ? button.TargetClientId : null, true);
        foreach (var bar in Resources.FindObjectsOfTypeAll<FriendsListBar>())
            if (bar) Track(bar.SenderName, () => bar && bar.ClientData != null ? bar.ClientData.Id : null, true);
        foreach (var panel in Resources.FindObjectsOfTypeAll<ShapeshifterPanel>())
            if (panel)
            {
                Track(panel.NameText, () => null);
                if (panel.PlayerIcon && panel.PlayerIcon.cosmetics)
                    Track(panel.PlayerIcon.cosmetics.nameText, () => null);
            }
        foreach (var popup in Resources.FindObjectsOfTypeAll<HideAndSeekDeathPopupNameplate>())
            if (popup)
            {
                Track(popup.nameText, () => null);
                if (popup.playerIcon && popup.playerIcon.cosmetics)
                    Track(popup.playerIcon.cosmetics.nameText, () => null);
            }
        foreach (var button in Resources.FindObjectsOfTypeAll<AmongUs.QuickChat.QuickChatMenuPhrasesPageCrewmateButton>())
            if (button)
            {
                Track(button.text, () => button && button.phrase != null ? button.phrase.PlayerId : null);
                if (button.player && button.player.cosmetics)
                    Track(button.player.cosmetics.nameText, () => button && button.phrase != null ? button.phrase.PlayerId : null);
            }

        foreach (int id in labels.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            Restore(labels[id]);
            labels.Remove(id);
        }
    }

    [HideFromIl2Cpp]
    private void Track(TMP_Text? text, Func<int?> owner, bool clientIdentity = false, Func<string?>? namePart = null)
    {
        if (!text || !text!.gameObject.scene.IsValid()) return;
        int id = text.GetInstanceID();
        seen.Add(id);
        if (!labels.TryGetValue(id, out var label)) labels[id] = label = new Label { Text = text };
        label.Owner = owner;
        label.ClientIdentity = clientIdentity;
        label.NamePart = namePart;
    }

    [HideFromIl2Cpp]
    private static int? HostClientId()
    {
        var client = AmongUsClient.Instance;
        return client ? client.HostId : null;
    }

    [HideFromIl2Cpp]
    private static string? HostPlayerName()
    {
        var client = AmongUsClient.Instance;
        if (!client || client.allClients == null) return null;
        foreach (var host in client.allClients)
            if (host != null && host.Id == client.HostId)
                return host.Character && host.Character.Data != null ? host.Character.Data.PlayerName : host.PlayerName;
        return null;
    }

    [HideFromIl2Cpp]
    private static void Restore(Label label)
    {
        if (!label.Text) return;
        string current = label.Text.text ?? "";
        string restored = label.State.Restore(current);
        if (current != restored) label.Text.text = restored;
    }

    [HideFromIl2Cpp]
    private void RestoreAll()
    {
        foreach (var label in labels.Values) Restore(label);
        labels.Clear();
        nextDiscovery = 0;
    }

    public void OnDestroy() => RestoreAll();
}
