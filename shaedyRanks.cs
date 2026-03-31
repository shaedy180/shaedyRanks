using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace shaedyRanks;

public class PlayerData
{
    public string Name { get; set; } = "Unknown";
    public int Points { get; set; } = 12500;
    public int Kills { get; set; } = 0;
    public int Deaths { get; set; } = 0;
    public int Wins { get; set; } = 0;
    public int Losses { get; set; } = 0;
    public int MVP { get; set; } = 0;
    public int Prestige { get; set; } = 0;
}

public class RankHistoryEntry
{
    public string timestamp { get; set; } = "";
    public string steamid { get; set; } = "";
    public string name { get; set; } = "";
    public int change { get; set; }
    public int new_total { get; set; }
    public string reason { get; set; } = "";
    public int prestige { get; set; } = 0;
}

public class shaedyRanksConfig
{
    [JsonPropertyName("multiplier_global")] public float GlobalPointMultiplier { get; set; } = 1.0f;
    [JsonPropertyName("multiplier_long_distance")] public float MultiplierLongDistance { get; set; } = 1.25f;
    [JsonPropertyName("threshold_long_distance")] public float LongDistanceThreshold { get; set; } = 2250.0f;
    [JsonPropertyName("count_bot_kills")] public bool CountBotKills { get; set; } = false;

    [JsonPropertyName("points_per_kill")] public int PointsPerKill { get; set; } = 23;
    [JsonPropertyName("points_headshot_bonus")] public int PointsHeadshotBonus { get; set; } = 8;
    [JsonPropertyName("points_assist_flash")] public int PointsAssistFlash { get; set; } = 3;
    [JsonPropertyName("points_bomb_defuse")] public int PointsBombDefuse { get; set; } = 0;
    [JsonPropertyName("points_bomb_plant")] public int PointsBombPlant { get; set; } = 0;
    [JsonPropertyName("points_mvp")] public int PointsMVP { get; set; } = 0;
    [JsonPropertyName("points_round_win")] public int PointsRoundWin { get; set; } = 15;
    [JsonPropertyName("points_round_loss")] public int PointsRoundLoss { get; set; } = -15;
    [JsonPropertyName("points_loss_on_death")] public int PointsLossOnDeath { get; set; } = -8;
    [JsonPropertyName("points_teamkill_bonus")] public int PointsTeamKillBonus { get; set; } = 15;

    [JsonPropertyName("enable_headshot_bonus")] public bool EnableHeadshotBonus { get; set; } = true;
    [JsonPropertyName("enable_flash_assist")] public bool EnableFlashAssist { get; set; } = true;
    [JsonPropertyName("enable_round_win_loss_logic")] public bool EnableRoundLogic { get; set; } = true;

    [JsonPropertyName("multiplier_ct_2_players")] public float MultiplierCT2Players { get; set; } = 1.25f;
    [JsonPropertyName("multiplier_t_3_players")] public float MultiplierT3Players { get; set; } = 1.5f;
    [JsonPropertyName("multiplier_t_5_players")] public float MultiplierT5Players { get; set; } = 1.25f;

    [JsonPropertyName("ttk_penalty_per_second")] public float TtkPenaltyPerSecond { get; set; } = 1.5f;

    [JsonPropertyName("points_air_kill")] public int PointsAirKill { get; set; } = 8;
    [JsonPropertyName("points_air_headshot")] public int PointsAirHeadshot { get; set; } = 15;
    [JsonPropertyName("points_smoke_headshot")] public int PointsSmokeHeadshot { get; set; } = 15;
    [JsonPropertyName("points_wall_headshot")] public int PointsWallHeadshot { get; set; } = 15;
    [JsonPropertyName("points_nade_molly_kill")] public int PointsNadeMollyKill { get; set; } = 15;
    [JsonPropertyName("points_knife_kill")] public int PointsKnifeKill { get; set; } = 38;

    [JsonPropertyName("prestige_enabled")] public bool PrestigeEnabled { get; set; } = true;
    [JsonPropertyName("prestige_threshold")] public int PrestigeThreshold { get; set; } = 50000;
    [JsonPropertyName("prestige_reset_points")] public int PrestigeResetPoints { get; set; } = 7000;
}

public class KillDetail { public string VictimName { get; set; } = ""; public int Damage { get; set; } public int Hits { get; set; } public bool IsHeadshot { get; set; } }

public class DamageTracker
{
    public int TotalDamage { get; set; }
    public int Hits { get; set; }
    public float FirstHitTime { get; set; }
    public string VictimName { get; set; } = "Unknown";
}

public class shaedyRanksPlugin : BasePlugin
{
    public override string ModuleName => "shaedy Ranks";
    public override string ModuleVersion => "5.0";
    public override string ModuleAuthor => "shaedy";

    public shaedyRanksConfig Config { get; set; } = new();

    private Dictionary<ulong, PlayerData> _playerData = new();
    private string _dbFilePath = "";
    private string _historyFilePath = "";
    private string _configFilePath = "";

    private static readonly object _historyLock = new object();
    private static readonly object _dbLock = new object();

    private Dictionary<ulong, int> _roundPointCache = new();
    private Dictionary<ulong, List<KillDetail>> _roundKillsCache = new();
    private Dictionary<ulong, Dictionary<ulong, DamageTracker>> _damageLog = new();
    private Dictionary<ulong, Dictionary<ulong, DamageTracker>> _damageReceived = new();

    // Hardcoded prefix
    private string _prefix = $" {ChatColors.White}[{ChatColors.Green}shaedy-Ranks{ChatColors.White}]";

    public override void Load(bool hotReload)
    {
        _dbFilePath = Path.Combine(ModuleDirectory, "ranks.json");
        _historyFilePath = Path.Combine(ModuleDirectory, "rank_history.json");
        _configFilePath = Path.Combine(ModuleDirectory, "shaedyRanksConfig.json");

        LoadData();
        LoadConfig();

        Console.WriteLine("[shaedyRanks] Plugin loaded.");
    }

    public override void Unload(bool hotReload) => SaveData(async: false);

    private void LoadConfig()
    {
        if (!File.Exists(_configFilePath))
        {
            Config = new shaedyRanksConfig();
            File.WriteAllText(_configFilePath, JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            try { Config = JsonSerializer.Deserialize<shaedyRanksConfig>(File.ReadAllText(_configFilePath)) ?? new(); } catch { }
        }
    }

    private readonly Dictionary<string, int> _rankLadder = new() {
        { "Silver I", 0 }, { "Silver II", 5000 }, { "Silver III", 7000 }, { "Silver IV", 9000 },
        { "Silver Elite", 11000 }, { "Silver Elite Master", 13000 },
        { "Gold Nova I", 15000 }, { "Gold Nova II", 17000 }, { "Gold Nova III", 19000 }, { "Gold Nova Master", 21000 },
        { "MG I", 23000 }, { "MG II", 25000 }, { "MGE", 27000 }, { "DMG", 29000 },
        { "Eagle", 31000 }, { "LEM", 33000 }, { "Supreme", 36000 }, { "Global", 40000 }
    };

    private (string rankName, int nextRankPoints) GetRankInfo(int points)
    {
        string currentRank = "Silver I"; int nextThreshold = 5000;
        foreach (var rank in _rankLadder.OrderBy(r => r.Value)) { if (points >= rank.Value) currentRank = rank.Key; else { nextThreshold = rank.Value; break; } }
        if (points >= 40000) nextThreshold = 99999;
        return (currentRank, nextThreshold);
    }

    private char GetRankColor(string rankName)
    {
        if (rankName.Contains("Silver")) return ChatColors.Blue;
        if (rankName.Contains("Gold")) return ChatColors.Gold;
        if (rankName.Contains("MG") || rankName.Contains("DMG") || rankName.Contains("Eagle")) return ChatColors.DarkBlue;
        if (rankName.Contains("LEM") || rankName.Contains("Supreme") || rankName.Contains("Global")) return ChatColors.Red;
        return ChatColors.White;
    }

    private void ModifyPoints(CCSPlayerController player, int points, string reason)
    {
        if (player.IsBot) return;

        if (Config.GlobalPointMultiplier != 1.0f) points = (int)Math.Round((float)points * Config.GlobalPointMultiplier);
        if (points == 0) return;

        int realPlayers = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && !p.IsHLTV);
        if (realPlayers <= 1) return;

        var data = GetPlayerData(player);
        string oldRank = GetRankInfo(data.Points).rankName;

        data.Points += points;

        int currentPoints = data.Points;
        string steamId = player.SteamID.ToString();
        string playerName = player.PlayerName;
        int prestigeLevel = data.Prestige;

        Task.Run(() => LogHistoryAsync(steamId, playerName, points, currentPoints, reason, prestigeLevel));

        if (!_roundPointCache.ContainsKey(player.SteamID)) _roundPointCache[player.SteamID] = 0;
        _roundPointCache[player.SteamID] += points;

        string newRank = GetRankInfo(data.Points).rankName;

        if (oldRank != newRank)
        {
            bool upRank = points > 0;
            char color = upRank ? ChatColors.Green : ChatColors.Red;
            string action = upRank ? "PROMOTED" : "DEMOTED";

            Server.PrintToChatAll($" {ChatColors.DarkRed}--------------------------------------------------");
            Server.PrintToChatAll($"{_prefix} {ChatColors.White}Player {ChatColors.Green}{player.PlayerName} {ChatColors.White}was {color}{action}!");
            string promoDemoRank = data.Prestige > 0 ? $"P{data.Prestige} - {newRank}" : newRank;
            Server.PrintToChatAll($" {ChatColors.White}New Rank: {GetRankColor(newRank)}{promoDemoRank}");
            Server.PrintToChatAll($" {ChatColors.DarkRed}--------------------------------------------------");

            Server.ExecuteCommand($"css_svlog \"[RANK] {player.PlayerName} moved from {oldRank} to {newRank}\"");
            UpdateClanTag(player, newRank);
        }
    }

    private void LogHistoryAsync(string steamId, string name, int change, int newTotal, string reason, int prestige = 0)
    {
        var entry = new RankHistoryEntry
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            steamid = steamId,
            name = name,
            change = change,
            new_total = newTotal,
            reason = reason,
            prestige = prestige
        };

        lock (_historyLock)
        {
            try
            {
                List<RankHistoryEntry> history = new();

                if (File.Exists(_historyFilePath))
                {
                    string json = File.ReadAllText(_historyFilePath);
                    if (!string.IsNullOrWhiteSpace(json)) history = JsonSerializer.Deserialize<List<RankHistoryEntry>>(json) ?? new();
                }

                history.Insert(0, entry);
                if (history.Count > 2000) history = history.GetRange(0, 2000);

                File.WriteAllText(_historyFilePath, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[shaedyRanks] History Write Error: {ex.Message}");
            }
        }
    }

    private void UpdateClanTag(CCSPlayerController player, string rankName = "")
    {
        if (player.IsBot || !player.IsValid) return;
        var data = GetPlayerData(player);
        if (string.IsNullOrEmpty(rankName)) { rankName = GetRankInfo(data.Points).rankName; }
        if (data.Prestige > 0)
            player.Clan = $"[P{data.Prestige} {rankName}]";
        else
            player.Clan = $"[{rankName}]";
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (@event.Userid != null && !@event.Userid.IsBot) UpdateClanTag(@event.Userid);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _roundPointCache.Clear();
        _roundKillsCache.Clear();
        _damageLog.Clear();
        _damageReceived.Clear();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var attacker = @event.Attacker; var victim = @event.Userid;
        if (attacker == null || victim == null || !attacker.IsValid || !victim.IsValid || attacker == victim) return HookResult.Continue;

        if (!_damageLog.ContainsKey(attacker.SteamID)) _damageLog[attacker.SteamID] = new Dictionary<ulong, DamageTracker>();
        if (!_damageLog[attacker.SteamID].ContainsKey(victim.SteamID)) _damageLog[attacker.SteamID][victim.SteamID] = new DamageTracker();

        var track = _damageLog[attacker.SteamID][victim.SteamID];
        if (track.Hits == 0) track.FirstHitTime = Server.CurrentTime;

        track.VictimName = victim.PlayerName;
        track.TotalDamage += @event.DmgHealth;
        track.Hits++;

        if (!_damageReceived.ContainsKey(victim.SteamID)) _damageReceived[victim.SteamID] = new Dictionary<ulong, DamageTracker>();
        if (!_damageReceived[victim.SteamID].ContainsKey(attacker.SteamID)) _damageReceived[victim.SteamID][attacker.SteamID] = new DamageTracker();
        var recvTrack = _damageReceived[victim.SteamID][attacker.SteamID];
        recvTrack.VictimName = attacker.PlayerName;
        recvTrack.TotalDamage += @event.DmgHealth;
        recvTrack.Hits++;

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var attacker = @event.Attacker; var victim = @event.Userid;
        if (attacker == null || victim == null || !attacker.IsValid || !victim.IsValid) return HookResult.Continue;
        if (attacker.IsBot) return HookResult.Continue;
        if (victim.IsBot && !Config.CountBotKills) return HookResult.Continue;

        if (attacker != victim && attacker.TeamNum != victim.TeamNum)
        {
            float points = (float)Config.PointsPerKill;
            int playerCount = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot && !p.IsHLTV);
            if (playerCount == 2 && attacker.TeamNum == (int)CsTeam.CounterTerrorist) points *= Config.MultiplierCT2Players;
            else if (playerCount == 3 && attacker.TeamNum == (int)CsTeam.Terrorist) points *= Config.MultiplierT3Players;
            else if (playerCount == 5 && attacker.TeamNum == (int)CsTeam.Terrorist) points *= Config.MultiplierT5Players;

            if (attacker.PlayerPawn.Value != null && victim.PlayerPawn.Value != null)
            {
                var aPos = attacker.PlayerPawn.Value.AbsOrigin; var vPos = victim.PlayerPawn.Value.AbsOrigin;
                if (aPos != null && vPos != null)
                {
                    float dist = (float)Math.Sqrt(Math.Pow(aPos.X - vPos.X, 2) + Math.Pow(aPos.Y - vPos.Y, 2) + Math.Pow(aPos.Z - vPos.Z, 2));
                    if (dist >= Config.LongDistanceThreshold) points *= Config.MultiplierLongDistance;
                }
            }

            bool isHS = @event.Headshot;

            bool isAir = false;
            if (attacker.PlayerPawn != null && attacker.PlayerPawn.Value != null)
                isAir = (attacker.PlayerPawn.Value.Flags & ((uint)1 << 0)) == 0;

            if (isHS && Config.EnableHeadshotBonus) ModifyPoints(attacker, Config.PointsHeadshotBonus, "Headshot Bonus");
            if (isAir) ModifyPoints(attacker, isHS ? Config.PointsAirHeadshot : Config.PointsAirKill, "Air Kill");
            if (@event.Thrusmoke && isHS) ModifyPoints(attacker, Config.PointsSmokeHeadshot, "Smoke HS");
            if (@event.Penetrated > 0 && isHS) ModifyPoints(attacker, Config.PointsWallHeadshot, "Wallbang HS");

            string w = @event.Weapon;
            if (w.Contains("grenade") || w.Contains("molotov")) ModifyPoints(attacker, Config.PointsNadeMollyKill, "Nade/Molly Kill");
            if (w.Contains("knife")) ModifyPoints(attacker, Config.PointsKnifeKill, "Knife Kill");

            if (_damageLog.ContainsKey(attacker.SteamID) && _damageLog[attacker.SteamID].ContainsKey(victim.SteamID))
            {
                var d = _damageLog[attacker.SteamID][victim.SteamID];
                if (d.FirstHitTime > 0 && (Server.CurrentTime - d.FirstHitTime) > 0.1f) points -= (Server.CurrentTime - d.FirstHitTime) * Config.TtkPenaltyPerSecond;
            }
            if (points < 1) points = 1;

            GetPlayerData(attacker).Kills++;
            ModifyPoints(attacker, (int)points, "Kill");

            if (!_roundKillsCache.ContainsKey(attacker.SteamID)) _roundKillsCache[attacker.SteamID] = new List<KillDetail>();
            _roundKillsCache[attacker.SteamID].Add(new KillDetail { VictimName = victim.PlayerName, IsHeadshot = isHS });
        }
        else if (attacker.TeamNum == victim.TeamNum && attacker != victim)
        {
            ModifyPoints(attacker, Config.PointsTeamKillBonus, "Teamkill-Bonus");
        }

        if (!victim.IsBot)
        {
            GetPlayerData(victim).Deaths++;
            ModifyPoints(victim, Config.PointsLossOnDeath, "Death");
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerBlind(EventPlayerBlind @event, GameEventInfo info)
    {
        if (@event.Attacker != null && !@event.Attacker.IsBot && Config.EnableFlashAssist && @event.Attacker.TeamNum != @event.Userid.TeamNum)
            ModifyPoints(@event.Attacker, Config.PointsAssistFlash, "Flash Assist");
        return HookResult.Continue;
    }

    [GameEventHandler] public HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info) { if (@event.Userid != null && !@event.Userid.IsBot) ModifyPoints(@event.Userid, Config.PointsBombDefuse, "Bomb Defused"); return HookResult.Continue; }
    [GameEventHandler] public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info) { if (@event.Userid != null && !@event.Userid.IsBot) ModifyPoints(@event.Userid, Config.PointsBombPlant, "Bomb Planted"); return HookResult.Continue; }
    [GameEventHandler]
    public HookResult OnRoundMVP(EventRoundMvp @event, GameEventInfo info)
    {
        if (@event.Userid != null && !@event.Userid.IsBot)
        {
            GetPlayerData(@event.Userid).MVP++;
            ModifyPoints(@event.Userid, Config.PointsMVP, "MVP");
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!Config.EnableRoundLogic) return HookResult.Continue;
        int winnerTeam = @event.Winner; if (winnerTeam < 2) return HookResult.Continue;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || player.IsBot || player.TeamNum <= 1) continue;
            var data = GetPlayerData(player);
            bool isWin = (player.TeamNum == winnerTeam);
            int winPoints = isWin ? Config.PointsRoundWin : Config.PointsRoundLoss;
            if (isWin) data.Wins++; else data.Losses++;

            ModifyPoints(player, winPoints, isWin ? "Round Win" : "Round Loss");

            int statsPoints = _roundPointCache.ContainsKey(player.SteamID) ? _roundPointCache[player.SteamID] : 0;
            var rankInfo = GetRankInfo(data.Points);
            string result = isWin ? $"{ChatColors.Green}VICTORY" : $"{ChatColors.Red}DEFEAT";

            char statsColor = statsPoints >= 0 ? ChatColors.Green : ChatColors.Red;

            // Build damage report
            List<string> dmgReport = new();

            HashSet<ulong> allOpponents = new();
            if (_damageLog.ContainsKey(player.SteamID))
                foreach (var key in _damageLog[player.SteamID].Keys) allOpponents.Add(key);
            if (_damageReceived.ContainsKey(player.SteamID))
                foreach (var key in _damageReceived[player.SteamID].Keys) allOpponents.Add(key);

            foreach (var opponentId in allOpponents)
            {
                int givenDmg = 0, givenHits = 0;
                int recvDmg = 0, recvHits = 0;
                string opponentName = "Unknown";

                if (_damageLog.ContainsKey(player.SteamID) && _damageLog[player.SteamID].ContainsKey(opponentId))
                {
                    var given = _damageLog[player.SteamID][opponentId];
                    givenDmg = given.TotalDamage;
                    givenHits = given.Hits;
                    opponentName = given.VictimName;
                }

                if (_damageReceived.ContainsKey(player.SteamID) && _damageReceived[player.SteamID].ContainsKey(opponentId))
                {
                    var recv = _damageReceived[player.SteamID][opponentId];
                    recvDmg = recv.TotalDamage;
                    recvHits = recv.Hits;
                    if (opponentName == "Unknown") opponentName = recv.VictimName;
                }

                if (givenDmg > 0 || recvDmg > 0)
                {
                    string givenStr = givenDmg > 0 ? $"{ChatColors.Green}{givenDmg}{ChatColors.Grey} in {ChatColors.Green}{givenHits}" : $"{ChatColors.Grey}0 in 0";
                    string recvStr = recvDmg > 0 ? $"{ChatColors.Red}{recvDmg}{ChatColors.Grey} in {ChatColors.Red}{recvHits}" : $"{ChatColors.Grey}0 in 0";
                    dmgReport.Add($"{ChatColors.White}{opponentName} {ChatColors.Grey}- given: {givenStr} {ChatColors.Grey}| from: {recvStr}");
                }
            }

            player.PrintToChat($" {ChatColors.DarkRed}--------------------------------------------------");
            player.PrintToChat($" {result} {ChatColors.White}({winPoints:+0;-#}) | Total Round: {statsColor}{statsPoints:+0;-#} MMR");
            string roundDisplayRank = data.Prestige > 0 ? $"P{data.Prestige} - {rankInfo.rankName}" : rankInfo.rankName;
            player.PrintToChat($" {ChatColors.Grey}> Rank: {GetRankColor(rankInfo.rankName)}{roundDisplayRank} {ChatColors.Gold}({data.Points})");

            if (dmgReport.Count > 0)
            {
                player.PrintToChat($" {ChatColors.Grey}> Damage:");
                foreach (var line in dmgReport)
                    player.PrintToChat($"   {line}");
            }

            if (Config.PrestigeEnabled && data.Points >= Config.PrestigeThreshold)
                player.PrintToChat($" {ChatColors.Gold}* {ChatColors.White}You can {ChatColors.Gold}PRESTIGE{ChatColors.White}! Type {ChatColors.Green}!prestige {ChatColors.White}in chat.");

            player.PrintToChat($" {ChatColors.DarkRed}--------------------------------------------------");
        }

        SaveData(async: true);

        return HookResult.Continue;
    }

    [ConsoleCommand("css_top", "Shows Top 10 Leaderboard")]
    public void OnCommandTop(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null) return;
        var sorted = _playerData.Values.OrderByDescending(p => p.Prestige * Config.PrestigeThreshold + p.Points).Take(10).ToList();
        player.PrintToChat($" {ChatColors.Gold}TOP 10 LEADERBOARD");
        int rank = 1;
        foreach (var p in sorted)
        {
            var rInfo = GetRankInfo(p.Points);
            string displayRank = p.Prestige > 0 ? $"P{p.Prestige} - {rInfo.rankName}" : rInfo.rankName;
            string prefix = (rank == 1) ? $"{ChatColors.Gold}#1" : $"{ChatColors.White}#{rank}";
            string kd = (p.Deaths > 0) ? $"{(float)p.Kills / p.Deaths:0.0}" : $"{p.Kills}";
            string prestigeTag = p.Prestige > 0 ? $"{ChatColors.Gold}* " : "";
            player.PrintToChat($" {prefix} {prestigeTag}{ChatColors.Green}{p.Name} {ChatColors.Grey}- {GetRankColor(rInfo.rankName)}{displayRank} {ChatColors.White}({p.Points}) | K/D: {kd}");
            rank++;
        }
    }

    [ConsoleCommand("css_rank", "Shows your current rank stats")]
    public void OnCommandRank(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || player.IsBot) return;
        var data = GetPlayerData(player);
        var rInfo = GetRankInfo(data.Points);
        int toNext = rInfo.nextRankPoints - data.Points;
        var sorted = _playerData.OrderByDescending(p => p.Value.Prestige * Config.PrestigeThreshold + p.Value.Points).Select(p => p.Key).ToList();
        int pos = sorted.IndexOf(player.SteamID) + 1;
        float kdRatio = (data.Deaths > 0) ? (float)data.Kills / data.Deaths : data.Kills;
        string displayRank = data.Prestige > 0 ? $"P{data.Prestige} - {rInfo.rankName}" : rInfo.rankName;

        player.PrintToChat($"{_prefix} {ChatColors.Grey}{player.PlayerName}");
        player.PrintToChat($" {ChatColors.Grey}> Rank:   {GetRankColor(rInfo.rankName)}{displayRank} {ChatColors.Gold}({data.Points} {ChatColors.Grey}MMR{ChatColors.Gold})");
        if (data.Prestige > 0)
            player.PrintToChat($" {ChatColors.Grey}> Prestige: {ChatColors.Gold}* {data.Prestige}");
        player.PrintToChat($" {ChatColors.Grey}> Global: {ChatColors.White}#{pos}");
        if (Config.PrestigeEnabled && data.Points >= Config.PrestigeThreshold)
            player.PrintToChat($" {ChatColors.Grey}> Next:   {ChatColors.Gold}PRESTIGE READY! {ChatColors.White}Type {ChatColors.Green}!prestige");
        else
            player.PrintToChat($" {ChatColors.Grey}> Next:   {ChatColors.White}{toNext} Points needed");
        player.PrintToChat($" {ChatColors.Grey}> K/D:    {ChatColors.Green}{data.Kills}/{data.Deaths} {ChatColors.White}({kdRatio:0.00})");
        player.PrintToChat($" {ChatColors.Grey}> Wins:   {ChatColors.Green}{data.Wins} {ChatColors.Grey}| MVP: {ChatColors.Green}{data.MVP}");
    }

    [ConsoleCommand("css_prestige", "Prestige your rank")]
    public void OnCommandPrestige(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || player.IsBot) return;
        if (!Config.PrestigeEnabled)
        {
            player.PrintToChat($"{_prefix} {ChatColors.Red}Prestige is currently disabled.");
            return;
        }
        var data = GetPlayerData(player);
        if (data.Points < Config.PrestigeThreshold)
        {
            int needed = Config.PrestigeThreshold - data.Points;
            player.PrintToChat($"{_prefix} {ChatColors.Red}You need {ChatColors.Gold}{needed} {ChatColors.Red}more MMR to prestige.");
            player.PrintToChat($" {ChatColors.Grey}> Current: {ChatColors.White}{data.Points} / {Config.PrestigeThreshold}");
            return;
        }

        int oldPoints = data.Points;
        data.Prestige++;
        data.Points = Config.PrestigeResetPoints;

        string newRank = GetRankInfo(data.Points).rankName;
        string displayRank = $"P{data.Prestige} - {newRank}";

        int change = data.Points - oldPoints;
        string steamId = player.SteamID.ToString();
        string playerName = player.PlayerName;
        int prestigeLevel = data.Prestige;
        Task.Run(() => LogHistoryAsync(steamId, playerName, change, data.Points, $"PRESTIGE {prestigeLevel}", prestigeLevel));

        Server.PrintToChatAll($" {ChatColors.DarkRed}--------------------------------------------------");
        Server.PrintToChatAll($"{_prefix} {ChatColors.Gold}* {player.PlayerName} {ChatColors.White}has reached {ChatColors.Gold}PRESTIGE {data.Prestige}{ChatColors.White}!");
        Server.PrintToChatAll($" {ChatColors.White}New Rank: {GetRankColor(newRank)}{displayRank}");
        Server.PrintToChatAll($" {ChatColors.DarkRed}--------------------------------------------------");

        UpdateClanTag(player, newRank);
        SaveData(async: true);

        Server.ExecuteCommand($"css_svlog \"[PRESTIGE] {player.PlayerName} reached Prestige {data.Prestige}\"");
    }

    private PlayerData GetPlayerData(CCSPlayerController player)
    {
        ulong steamId = player.SteamID;
        if (!_playerData.ContainsKey(steamId)) _playerData[steamId] = new PlayerData { Name = player.PlayerName };
        _playerData[steamId].Name = player.PlayerName; return _playerData[steamId];
    }

    private void LoadData()
    {
        if (File.Exists(_dbFilePath))
        {
            try
            {
                lock (_dbLock)
                {
                    _playerData = JsonSerializer.Deserialize<Dictionary<ulong, PlayerData>>(File.ReadAllText(_dbFilePath)) ?? new();
                }
            }
            catch { }
        }
    }

    private void SaveData(bool async = true)
    {
        if (async)
        {
            Dictionary<ulong, PlayerData> dataCopy;
            lock (_playerData)
            {
                dataCopy = new Dictionary<ulong, PlayerData>(_playerData);
            }

            Task.Run(() =>
            {
                lock (_dbLock)
                {
                    try
                    {
                        File.WriteAllText(_dbFilePath, JsonSerializer.Serialize(dataCopy, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[shaedyRanks] Save Error: {ex.Message}");
                    }
                }
            });
        }
        else
        {
            lock (_dbLock)
            {
                try { File.WriteAllText(_dbFilePath, JsonSerializer.Serialize(_playerData, new JsonSerializerOptions { WriteIndented = true })); } catch { }
            }
        }
    }
}

