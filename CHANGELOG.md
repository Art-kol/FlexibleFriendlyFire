# Changelog

## 1.1.1

- Fixed: a player hit himself with full damage when an FF rule covered his own role (`fire allow RR * *`...):
  the Micro-HID beam starts inside the shooter's own hitboxes and killed him. Self-damage is now never decided by
  FF rules, only by the vanilla self-damage rules of the weapon.
- SCP-106 attack proxy follows vertical movement too (jumps, falls over pits): SCP-106 can hit FF SCPs in the air.
- The proxy dummy shows no nickname / player info when SCP-106 / SCP-939 look at it.
- The proxy dummy is hidden from the player list (Tab), the Remote Admin player list and the RA dummy actions.

## 1.1.0

- `fire` needs no extra permission any more: whoever may switch the vanilla friendly fire (Remote Admin
  `ServerConfigs` permission) may use it. The `command_permission` config key is gone.
- Config `scp_vs_scp_with_server_friendly_fire`: while the vanilla friendly fire is on, SCPs may hurt each other.
- Config `scp_vs_scp_damage`: damage of every SCP attack against other SCPs (`0` default, `-1` one-hit kill,
  a fixed number or a percent of the target's max HP; a set value turns the attack's effects off).
- Config `scp079`: several SCP-079s - Any / All for generators and the recontainment button (SCPs and humans
  separately), and whether every SCP-079 or only the attackable ones are recontained.
- SCP-3114's disguise fools the SCPs it may hurt (config `scp3114_disguise_fools_scps`).
- `fire list` shows when SCP vs SCP is allowed by the server friendly fire.
- Fixed: an error in the Tesla damage check when no Tesla gate was near the player; recontainment button checks no
  longer fail when the map has no SCP-079 recontainer.
- Recontainment by a human that is blocked from some SCP-079s: the spared ones are no longer counted as recontained
  (CASSIE reports a failure if nobody was recontained).

## 1.0.0

First standalone release (the FriendlyFiresModule of TakChanges as its own LabAPI plugin).

- `fire allow/block/reset/list` command for player and role rules (Remote Admin and server console,
  `ffm.manage` permission).
- SCP vs SCP damage with SCP abilities: SCP-049 (attack, sense), SCP-049-2 (claws, bloodlust), SCP-096,
  SCP-106 (two-hit attack with the pocket dimension, stalk vision both ways), SCP-173 (blink kill, tantrum),
  SCP-939 (claws, lunge, amnestic cloud, perception radius), SCP-3114.
- SCP-079 against players and SCPs: door / room locks, blackouts, Tesla gates (5× XP for SCP kills),
  breach scanner, generators, recontainment glass (an SCP breaking it recontains every SCP-079).
- CASSIE for SCPs killed by SCPs and for SCPs lost in the pocket dimension.
- Recontainment glass: only the real button glass counts and an SCP has to look at it.
