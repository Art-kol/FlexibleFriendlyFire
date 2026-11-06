# FriendlyFireManager

[![Build](https://github.com/Art-kol/FlexibleFriendlyFire/actions/workflows/build.yml/badge.svg)](https://github.com/Art-kol/FlexibleFriendlyFire/actions/workflows/build.yml)
![LabAPI](https://img.shields.io/badge/LabAPI-1.1.7-blue)
![License](https://img.shields.io/badge/license-MIT-green)

A [LabAPI](https://github.com/northwood-studios/LabAPI) plugin for SCP: Secret Laboratory that lets server staff
**allow or block damage between any players and roles** with one Remote Admin command. That includes
**SCP vs SCP**: once an SCP is allowed to hurt another SCP, its attacks *and* its abilities work on that SCP
the way they work on humans.

Without rules the game is completely vanilla. Rules are set per round, which makes the plugin a tool for events,
duels, "SCP civil war" rounds and similar.

## Features

### Rules
- `fire allow` / `fire block` / `fire reset` / `fire list`.
- Player → player, player → role, role → player and role → role rules, one-way or mutual (mutual by default).
- Player rules win over role rules, blocks win over allows. No rule = vanilla.
- Player rules are dropped when that player dies or leaves. All rules are cleared when the round restarts.
- Optional: while the vanilla friendly fire is on, SCPs may hurt each other too (config
  `scp_vs_scp_with_server_friendly_fire`). Rules still win over it.
- Works for all damage that has an attacker: firearms, explosives, melee (Jailbird and similar), SCP attacks.
- A block also removes an SCP's influence on that player: SCP-173 isn't frozen by their gaze, SCP-096 isn't
  enraged by them, SCP-049 can't sense them, SCP-173 tantrum and SCP-939 amnestic cloud don't affect them.

### SCP vs SCP
When SCP **A** is allowed to hit SCP **B**:

| SCP | What works against B |
|---|---|
| SCP-049 | Attack, cardiac arrest, *Good Sense of the Doctor* (sense) |
| SCP-049-2 | Claws, bloodlust |
| SCP-096 | Gets enraged by B looking at it, charges and attacks B |
| SCP-106 | Attack (like on humans: the first hit corrodes, the second one sends B to the pocket dimension), stalk / hunter vision with the vanilla radius, in both directions |
| SCP-173 | Is frozen by B's gaze, kills B by blink teleport, tantrum slows B (a neck snap without a blink is not supported) |
| SCP-939 | Claws, lunge, amnestic cloud (also affects SCPs the owner may hit, never the owner), perception: sees B only within its own radius, no matter how many humans are alive |
| SCP-3114 | Slap; its disguise fools B (B sees it as the human whose skin it wears) |

Visibility side effects are handled too: an SCP-939 that is out of range or a buried SCP-106 doesn't trigger
SCP-096 and doesn't freeze SCP-173, and an SCP-079 doesn't see a buried SCP-106 it may hit.

The damage of every SCP attack against SCPs can be set in the config (`scp_vs_scp_damage`): default behaviour,
a one-hit kill, a fixed amount or a percent of the target's maximum HP. With a set value the attack only deals that
damage, without its effects (pocket dimension, cardiac arrest, corrosion, concussion).

SCP-3114's disguise against SCPs: while it is active, an SCP that SCP-3114 may hurt gets SCP-3114 sent as the stolen
human class (model, held item, unit name; it's not in the SCP list). The nickname is not changed: that SCP sees the
real nickname of the SCP-3114 player.

### SCP-079
Players SCP-079 may **not** hit (block rule):
- open doors and rooms locked by that SCP-079;
- get night vision in rooms it darkened;
- aren't hurt by Tesla gates it activated;
- are ignored by its breach scanner.

SCPs SCP-079 **may** hit (allow rule) are treated like humans:
- its door locks and room lockdowns hold against them;
- its blackouts leave them in the dark, without the SCP vision boost;
- killing one of them with a Tesla gate it activated gives 5× the human-kill XP.

Players blocked from hurting SCP-079 can't unlock or activate generators and can't break the recontainment
button glass (neither by shooting nor with throwables). SCPs allowed to hurt SCP-079:
- can pull the lever of a generator (the generator door still has to be opened with a keycard first);
- can break the recontainment button glass with their attacks; then **every** SCP-079 is recontained.

With several SCP-079s, when a player or an SCP may hurt only some of them, the config (`scp079`) decides, separately
for SCPs and humans: is one SCP-079 enough to use generators / the button or must it be all of them, and are all
SCP-079s recontained or only the ones the player who broke the glass may hurt. Defaults: one is enough; an SCP
recontains all of them, a human only the ones he isn't blocked from.

### CASSIE
- An SCP killed by another SCP is announced as *"SCP-X TERMINATED BY SCP-Y"* (SCP-049-2 is read out correctly,
  victims that are SCP-049-2 are not announced, as in vanilla).
- An SCP that dies in the pocket dimension: *"SCP-X LOST IN POCKET DIMENSION"*.

## Installation

1. Download `FriendlyFireManager.dll` and `0Harmony.dll` from [Releases](https://github.com/Art-kol/FlexibleFriendlyFire/releases).
2. Put `FriendlyFireManager.dll` into `LabAPI/plugins/global/` (or `LabAPI/plugins/<port>/`).
3. Put `0Harmony.dll` into `LabAPI/dependencies/global/` (skip it if another plugin already installed Harmony 2.x).
4. Restart the server.

The `LabAPI` folder is in
- Windows: `%AppData%\SCP Secret Laboratory\LabAPI\`
- Linux: `~/.config/SCP Secret Laboratory/LabAPI/`

> [!WARNING]
> Don't run this plugin together with another copy of the same patches (for example an older plugin that
> contains the FriendlyFiresModule): both would patch the same game methods.

## Permissions

Nothing extra: whoever may switch the vanilla friendly fire may use `fire`. That's the `ServerConfigs` Remote Admin
permission (Server configs tab, `setconfig friendly_fire`), set per group in `config_remoteadmin.txt`. The server
console can always use the command.

## Command

```
fire <allow/block/reset/list> <PP/PR/RP/RR> <attackers> <targets> [0/1]
```

- The two letters are the attacker type and the target type: `P` = players (player IDs), `R` = roles (role IDs).
- Several IDs are separated with a dot: `3.4.7`. `*` or `all` = all roles.
- The last argument: `1` (default) = the rule works both ways, `0` = one-way only.
- `fire reset` without arguments clears every rule, `fire list` (or `fire ls`) shows them.

Examples:

| Command | Effect |
|---|---|
| `fire allow RR 0 3` | SCP-173 and SCP-106 may hurt each other |
| `fire allow RR * *` | Everybody may hurt everybody, SCPs included |
| `fire allow PP 3.4 5 0` | Players 3 and 4 may hurt player 5; player 5 follows the vanilla rules |
| `fire block RR 0.3 12` | SCP-173 and SCP-106 can't hurt NTF Captains and vice versa |
| `fire block RP 7 2.9` | SCP-079 can't hurt players 2 and 9 (no Tesla, no locks, no darkness for them), and they can't hurt 079 |
| `fire reset RP 0 1.2 0` | Removes SCP-173's rules for players 1 and 2 |

Role IDs:

| ID | Role | ID | Role | ID | Role |
|---|---|---|---|---|---|
| 0 | SCP-173 | 8 | Chaos Conscript | 16 | SCP-939 |
| 1 | Class-D | 9 | SCP-096 | 18 | Chaos Rifleman |
| 3 | SCP-106 | 10 | SCP-049-2 | 19 | Chaos Marauder |
| 4 | NTF Specialist | 11 | NTF Sergeant | 20 | Chaos Repressor |
| 5 | SCP-049 | 12 | NTF Captain | 23 | SCP-3114 |
| 6 | Scientist | 13 | NTF Private | | |
| 7 | SCP-079 | 14 | Tutorial | | |
| | | 15 | Facility Guard | | |

## Configuration

`LabAPI/configs/<port>/FriendlyFireManager/config.yml`:

```yaml
# true = while the vanilla friendly fire is on, SCPs may hurt each other too (rules still win)
scp_vs_scp_with_server_friendly_fire: false
# true = SCP-3114's disguise also fools the SCPs it may hurt
scp3114_disguise_fools_scps: true
# Damage of SCP attacks against OTHER SCPs (see the table below)
scp_vs_scp_damage:
  scp049_attack: 0
  scp0492_claws: 0
  scp079_tesla: 0
  scp096_slap: 0
  scp096_charge: 0
  scp096_gate_kill: 0
  scp106_attack: 0
  scp173_snap: 0
  scp939_claws: 0
  scp939_lunge: 0
  scp939_lunge_splash: 0
  scp3114_slap: 0
# Several SCP-079s, when a player / an SCP may hurt only some of them
scp079:
  # Any = may hurt at least one SCP-079, All = every SCP-079
  scp_generators: Any
  scp_recontainment_button: Any
  # All = every SCP-079 is recontained, Attackable = only the ones the glass breaker may hurt
  scp_recontainment_targets: All
  human_generators: Any
  human_recontainment_button: Any
  human_recontainment_targets: Attackable
```

The real file has a comment above every key.

`scp_vs_scp_damage` values (only for SCPs that may hurt each other; humans are never affected):

| Value | Effect |
|---|---|
| `0` | Default behaviour of the attack, with its effects |
| `-1` | Kills in one hit |
| `150` | Deals 150 damage (hume shield first, then HP) |
| `25%` | Deals 25% of the target's maximum HP |

Any value other than `0` turns the attack's effects off: SCP-106 doesn't corrode or send to the pocket dimension,
SCP-049 doesn't stop the heart, SCP-096 doesn't concuss. Abilities that aren't attacks (sense, bloodlust, stalk,
amnestic cloud, tantrum...) work as usual. SCP-096 only hits its targets, as with humans.

The plugin can be turned off in `LabAPI/configs/<port>/FriendlyFireManager/properties.yml` (`is_enabled: false`).

## How it works (short)

- Rules are stored in `FireLogics`; `FireLogics.CanDamage(attacker, target)` returns `true` / `false` / `null`
  (= no rule, vanilla). Everything else asks it.
- Most of the game logic is changed with Harmony patches (`Patches/`) and LabAPI events (`Handlers/`).
- SCP-106's attack and SCP-939's lunge are decided by the game *client*, which never treats an SCP as a target.
  For them the plugin uses one invisible, silent dummy player (**"SCP FF Proxy"**, a Tutorial parked outside the
  map). Each client sees it at its own position, the server then picks the real SCP target (`FfProxy/`).
  The dummy only exists while it is needed; it's visible in the RA player list. Don't kick it.

## Building

Requirements: .NET SDK 8 or newer (any OS) and the `SCPSL_Data/Managed` folder of an SCP:SL dedicated server
(Steam app 996560). The game DLLs are **not** in the repository.

1. Tell the build where the game DLLs are, either
   - set the `SL_REFERENCES` environment variable to that `Managed` folder, or
   - copy `References.props.example` to `References.props.user` and put the path there (the file is git-ignored).
2. Build:
   ```
   dotnet build -c Release
   ```
   The plugin is `FriendlyFireManager/bin/Release/net48/FriendlyFireManager.dll`. On Windows a local build is also
   copied into `%AppData%\SCP Secret Laboratory\LabAPI\plugins\global\` if that folder exists.

`Assembly-CSharp.dll` is publicized during the build by
[BepInEx.AssemblyPublicizer.MSBuild](https://github.com/BepInEx/BepInEx.AssemblyPublicizer), so no manually
publicized DLL is needed.

### GitHub Actions
`.github/workflows/build.yml` builds the plugin on GitHub's servers:
- on every push to `main`/`master`, every pull request and on a manual run (Actions → Build → Run workflow)
  it checks that the code compiles and keeps the DLLs as an artifact of the run for 14 days;
- when a GitHub release is published, it also attaches `FriendlyFireManager.dll` and `0Harmony.dll` to it.
  Bump `<Version>` in `FriendlyFireManager/FriendlyFireManager.csproj` first.

The game DLLs are downloaded on every run with Valve's SteamCMD (dedicated server, app 996560, anonymous login).
They exist only on the temporary build machine. No secrets or settings are needed.

## License

[MIT](LICENSE). SCP: Secret Laboratory and its assemblies belong to Northwood Studios and are not part of this
project.
