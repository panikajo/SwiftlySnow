using System.Text.Json;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlySnow;

[PluginMetadata(Id = "SwiftlySnow", Version = "0.1.0", Name = "Swiftly Snow", Author = "Notion AI", Description = "Personal snow effect with a menu")]
public sealed class SwiftlySnow(ISwiftlyCore core) : BasePlugin(core)
{
    // Resource must exist on BOTH the server and clients; see README.
    private const string Effect = "particles/kolka/snow_more_snow.vpcf";
    private readonly Dictionary<int, CHandle<CParticleSystem>> _effects = new();
    private Dictionary<string, bool> _preferences = new();
    private IMenu? _menu;
    private string SettingsFile => Path.Combine(Core.PluginPath, "snow-preferences.json");

    public override void Load(bool hotReload)
    {
        LoadPreferences();
        Core.Event.OnPrecacheResource += Precache;
        Core.Event.OnClientPutInServer += ClientPutInServer;
        Core.Event.OnClientDisconnected += ClientDisconnected;
        Core.Event.OnClientSteamAuthorize += ClientAuthorized;
        Core.Event.OnEntityDeleted += EntityDeleted;
        Core.Event.OnMapUnload += MapUnload;
        _menu = Core.Menu.CreateMenu("swiftlysnow.settings")
            .WithTitle("Снег")
            .Add(new ButtonComponent("Включить снег", ctx => { SetSnow(ctx.Player, true); return ValueTask.CompletedTask; }) { CloseOnSelect = true })
            .Add(new ButtonComponent("Выключить снег", ctx => { SetSnow(ctx.Player, false); return ValueTask.CompletedTask; }) { CloseOnSelect = true })
            .Build();
        if (hotReload)
            Core.Scheduler.NextTick(() => { foreach (var p in Core.PlayerManager.GetAllPlayers()) if (p.IsValid && p.IsAlive && !p.IsFakeClient && Enabled(p)) CreateSnow(p); });
    }

    public override void Unload()
    {
        foreach (var slot in _effects.Keys.ToArray()) RemoveSnow(slot);
        Core.Event.OnPrecacheResource -= Precache;
        Core.Event.OnClientPutInServer -= ClientPutInServer;
        Core.Event.OnClientDisconnected -= ClientDisconnected;
        Core.Event.OnClientSteamAuthorize -= ClientAuthorized;
        Core.Event.OnEntityDeleted -= EntityDeleted;
        Core.Event.OnMapUnload -= MapUnload;
        Core.Menu.CloseAll();
    }

    private void Precache(IOnPrecacheResourceEvent e) => e.AddItem(Effect);
    private bool Enabled(IPlayer p) => p.IsAuthorized && p.SteamID != 0 && _preferences.TryGetValue(p.SteamID.ToString(), out var value) ? value : true;

    [Command("snow", helpText: "Открыть меню снега")]
    public void SnowCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null) { context.Reply("Команда доступна только игрокам."); return; }
        _menu?.Open(context.Sender);
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnSpawn(EventPlayerSpawn e)
    {
        var p = e.UserIdPlayer;
        if (p is null || p.IsFakeClient) return HookResult.Continue;
        int slot = p.PlayerID; ulong session = p.SessionId;
        Core.Scheduler.NextTick(() => {
            var player = Core.PlayerManager.GetPlayer(slot);
            if (player is null || player.SessionId != session || !player.IsValid) return;
            RemoveSnow(slot);
            if (Enabled(player) && player.IsAlive) CreateSnow(player);
        });
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnDeath(EventPlayerDeath e)
    {
        if (e.UserIdPlayer is { } player) RemoveSnow(player.PlayerID);
        return HookResult.Continue;
    }

    private void ClientAuthorized(IOnClientSteamAuthorizeEvent e)
    {
        var p = Core.PlayerManager.GetPlayer(e.PlayerId);
        if (p is null || p.IsFakeClient || !p.IsValid) return;
        if (!Enabled(p)) RemoveSnow(p.PlayerID);
        else if (p.IsAlive && !_effects.ContainsKey(p.PlayerID)) CreateSnow(p);
    }
    private void EntityDeleted(IOnEntityDeletedEvent e)
    {
        var index = e.Entity.Index;
        foreach (var entry in _effects.ToArray())
        {
            if (entry.Value.EntityIndex != index) continue;
            _effects.Remove(entry.Key);
            ClearBlocks(entry.Key, (int)index);
        }
    }
    private void ClearBlocks(int owner, int index)
    {
        foreach (var viewer in Core.PlayerManager.GetAllPlayers())
            if (viewer.PlayerID != owner) viewer.ShouldBlockTransmitEntity(index, false);
    }

    private void ClientPutInServer(IOnClientPutInServerEvent e)
    {
        var p = Core.PlayerManager.GetPlayer(e.PlayerId);
        if (p is null) return;
        foreach (var (owner, h) in _effects)
            if (owner != p.PlayerID && h.IsValid && h.Value is { IsValid: true } ent)
                p.ShouldBlockTransmitEntity((int)ent.Index, true);
    }
    private void ClientDisconnected(IOnClientDisconnectedEvent e) => RemoveSnow(e.PlayerId);
    private void MapUnload(IOnMapUnloadEvent e) { foreach (var slot in _effects.Keys.ToArray()) RemoveSnow(slot); }

    private void SetSnow(IPlayer p, bool enabled)
    {
        if (p.IsAuthorized && p.SteamID != 0) { _preferences[p.SteamID.ToString()] = enabled; SavePreferences(); }
        else { p.SendChat("[Снег] Steam ещё не авторизован. Попробуйте позже."); return; }
        RemoveSnow(p.PlayerID);
        if (enabled && p.IsAlive) CreateSnow(p);
        p.SendChat(enabled ? "[Снег] Включён." : "[Снег] Выключен.");
    }

    private void CreateSnow(IPlayer owner)
    {
        if (!owner.IsValid || owner.PlayerPawn is not { IsValid: true } pawn || pawn.AbsOrigin is not { } origin) return;
        RemoveSnow(owner.PlayerID);
        CParticleSystem? ent = null;
        try
        {
            ent = Core.EntitySystem.CreateEntity<CParticleSystem>();
            ent.StartActive = true;
            ent.EffectName = Effect;
            ent.Teleport(new Vector(origin.X, origin.Y, origin.Z + 200), QAngle.Zero, Vector.Zero);
            ent.DispatchSpawn();
            ent.AcceptInput("SetParent", "!activator", pawn, ent);
            _effects[owner.PlayerID] = Core.EntitySystem.GetRefEHandle(ent);
            foreach (var viewer in Core.PlayerManager.GetAllPlayers())
                if (viewer.PlayerID != owner.PlayerID) viewer.ShouldBlockTransmitEntity((int)ent.Index, true);
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "Failed to create snow for slot {Slot}", owner.PlayerID);
            if (_effects.ContainsKey(owner.PlayerID)) RemoveSnow(owner.PlayerID);
            else if (ent is { IsValid: true }) ent.Despawn();
        }
    }
    private void RemoveSnow(int slot)
    {
        if (!_effects.Remove(slot, out var handle)) return;
        ClearBlocks(slot, (int)handle.EntityIndex);
        if (handle.IsValid && handle.Value is { IsValid: true } ent) ent.Despawn();
    }
    private void LoadPreferences()
    {
        try { if (File.Exists(SettingsFile)) _preferences = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(SettingsFile)) ?? new(); }
        catch (Exception ex) { Core.Logger.LogError(ex, "Cannot read snow preferences; using defaults."); }
    }
    private void SavePreferences()
    {
        try { File.WriteAllText(SettingsFile, JsonSerializer.Serialize(_preferences, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Core.Logger.LogError(ex, "Cannot save snow preferences."); }
    }
}
