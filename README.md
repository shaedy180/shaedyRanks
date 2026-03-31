# shaedy Ranks

A CounterStrikeSharp MMR/ranking plugin with 18 rank tiers, prestige levels, and detailed point tracking.

## Features

- 18-tier rank ladder from Silver I to Global Elite
- Points for kills, headshots, knife kills, air kills, smoke/wallbang headshots, nade kills, and more
- Point loss on death and round loss
- Long-distance kill multiplier
- Time-to-kill penalty (slower kills earn fewer points)
- Team-size multipliers for balanced small lobbies
- Flash assist points
- Prestige system with configurable threshold and reset points
- Rank displayed as clan tag
- Promotion/demotion announcements
- Detailed per-round damage report
- Global leaderboard and personal rank stats
- Async save with JSON file storage
- Full history log of all point changes

## Commands

| Command | Description |
|---------|-------------|
| `!rank` | Show your current rank, K/D, wins, and global position |
| `!top` | Show the Top 10 leaderboard |
| `!prestige` | Prestige your rank (resets points, increases prestige level) |

## Installation

Drop the plugin folder into your CounterStrikeSharp `plugins` directory.

## Configuration

The config (`shaedyRanksConfig.json`) is auto-generated on first run. It has 30+ settings covering point values, multipliers, and prestige options. Key categories:

- **Point values**: kills, headshots, assists, bomb events, round win/loss, death penalty, special kills
- **Multipliers**: global multiplier, long-distance bonus, team-size scaling, TTK penalty
- **Toggles**: headshot bonus, flash assists, round win/loss logic, bot kills
- **Prestige**: enable/disable, threshold, reset points

## Data Files

| File | Description |
|------|-------------|
| `ranks.json` | Player rank data (points, kills, deaths, wins, prestige) |
| `rank_history.json` | Log of all point changes with timestamps |
| `shaedyRanksConfig.json` | Plugin configuration |
