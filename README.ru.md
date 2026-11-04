# FriendlyFireManager

[![Build](https://github.com/YOUR_GITHUB/FriendlyFireManager/actions/workflows/build.yml/badge.svg)](https://github.com/YOUR_GITHUB/FriendlyFireManager/actions/workflows/build.yml)
![LabAPI](https://img.shields.io/badge/LabAPI-1.1.7-blue)
![License](https://img.shields.io/badge/license-MIT-green)

[English](README.md) | **Русский**

Плагин на [LabAPI](https://github.com/northwood-studios/LabAPI) для SCP: Secret Laboratory: одной командой
Remote Admin администрация **разрешает или запрещает урон между любыми игроками и ролями**. В том числе
**SCP против SCP**: если SCP разрешено бить другого SCP, на него работают не только атаки, но и способности,
так же как на людей.

Без правил игра полностью ванильная. Правила задаются на раунд, так что плагин удобен для ивентов, дуэлей,
раундов «SCP против SCP» и похожего.

## Возможности

### Правила
- `fire allow` / `fire block` / `fire reset` / `fire list`.
- Правила игрок → игрок, игрок → роль, роль → игрок и роль → роль, в одну сторону или взаимные (по умолчанию взаимные).
- Правила для игроков важнее правил для ролей, запрет важнее разрешения. Нет правила = ваниль.
- Правила игрока удаляются, когда он умирает или выходит. Все правила сбрасываются при рестарте раунда.
- Работает для любого урона, у которого есть атакующий: оружие, взрывчатка, ближний бой (Jailbird и т.п.), атаки SCP.
- Запрет убирает и влияние SCP на этого игрока: его взгляд не замораживает SCP-173, не злит SCP-096,
  SCP-049 не может его «учуять», лужа SCP-173 и амнестическое облако SCP-939 на него не действуют.

### SCP против SCP
Когда SCP **A** разрешено бить SCP **B**:

| SCP | Что работает против B |
|---|---|
| SCP-049 | Атака, остановка сердца, «чутьё» (sense) |
| SCP-049-2 | Когти, кровожадность (bloodlust) |
| SCP-096 | Впадает в ярость от взгляда B, делает рывок и атакует B |
| SCP-106 | Атака (как по людям: первый удар — коррозия, второй отправляет B в карманное измерение), сталк / зрение охотника с ванильным радиусом, в обе стороны |
| SCP-173 | Замирает под взглядом B, убивает B телепортом при моргании, лужа замедляет B (щелчок шеи без моргания не поддерживается) |
| SCP-939 | Когти, прыжок (lunge), амнестическое облако (действует ещё и на SCP, которых владелец может бить, но не на самого владельца), восприятие: видит B только в своём радиусе, сколько бы людей ни было живо |
| SCP-3114 | Атаки |

Побочные эффекты видимости тоже учтены: SCP-939 вне радиуса и закопанный SCP-106 не злят SCP-096 и не
замораживают SCP-173, а SCP-079 не видит закопанного SCP-106, которого может бить.

### SCP-079
Игроки, которых SCP-079 **не может** бить (запрет):
- открывают двери и комнаты, заблокированные этим SCP-079;
- получают ночное зрение в комнатах, где он выключил свет;
- не получают урон от тесл, которые он активировал;
- не замечаются его сканером прорыва (breach scanner).

SCP, которых SCP-079 **может** бить (разрешение), для него как люди:
- его блокировки дверей и комнат их держат;
- при его блэкауте они в темноте, без «SCP-зрения»;
- убийство такого SCP теслой, которую он активировал, даёт в 5 раз больше опыта, чем за человека.

Игроки, которым запрещено бить SCP-079, не могут открыть и включить генераторы и не могут разбить стекло
кнопки реконтейнмента (ни выстрелами, ни гранатами). SCP, которым разрешено бить хотя бы одного SCP-079:
- могут дёрнуть рычаг генератора (дверцу генератора всё равно сначала надо открыть картой);
- могут разбить стекло кнопки реконтейнмента своими атаками; тогда реконтейнятся **все** SCP-079.

### CASSIE
- SCP, убитый другим SCP, объявляется как *«SCP-X TERMINATED BY SCP-Y»* (SCP-049-2 читается правильно,
  смерть SCP-049-2, как и в ванили, не объявляется).
- SCP, погибший в карманном измерении: *«SCP-X LOST IN POCKET DIMENSION»*.

## Установка

1. Скачайте `FriendlyFireManager.dll` и `0Harmony.dll` из [Releases](https://github.com/YOUR_GITHUB/FriendlyFireManager/releases).
2. `FriendlyFireManager.dll` положите в `LabAPI/plugins/global/` (или `LabAPI/plugins/<порт>/`).
3. `0Harmony.dll` положите в `LabAPI/dependencies/global/` (если Harmony 2.x уже стоит от другого плагина — не нужно).
4. Выдайте право своим группам (см. ниже) и перезапустите сервер.

Папка `LabAPI` находится в
- Windows: `%AppData%\SCP Secret Laboratory\LabAPI\`
- Linux: `~/.config/SCP Secret Laboratory/LabAPI/`

> [!WARNING]
> Не запускайте плагин вместе с другой копией тех же патчей (например, со старым плагином, в котором есть
> FriendlyFiresModule): оба будут патчить одни и те же методы игры.

## Права

Для команды `fire` нужно право LabAPI `ffm.manage` (меняется в конфиге). Добавьте его в
`LabAPI/configs/permissions.yml`; имена групп — это группы Remote Admin из `config_remoteadmin.txt`:

```yaml
owner:
  inherited_groups: []
  permissions:
  - ffm.manage
```

Из консоли сервера команда доступна всегда.

## Команда

```
fire <allow/block/reset/list> <PP/PR/RP/RR> <атакующие> <цели> [0/1]
```

- Две буквы — тип атакующего и тип цели: `P` — игроки (ID игроков), `R` — роли (ID ролей).
- Несколько ID пишутся через точку: `3.4.7`. `*` или `all` — все роли.
- Последний аргумент: `1` (по умолчанию) — правило работает в обе стороны, `0` — только в одну.
- `fire reset` без аргументов удаляет все правила, `fire list` (или `fire ls`) показывает их.

Примеры:

| Команда | Что делает |
|---|---|
| `fire allow RR 0 3` | SCP-173 и SCP-106 могут бить друг друга |
| `fire allow RR * *` | Все могут бить всех, включая SCP |
| `fire allow PP 3.4 5 0` | Игроки 3 и 4 могут бить игрока 5; игрок 5 — по ванильным правилам |
| `fire block RR 0.3 12` | SCP-173 и SCP-106 не могут бить капитанов МОГ, и наоборот |
| `fire block RP 7 2.9` | SCP-079 не может бить игроков 2 и 9 (на них не действуют теслы, блокировки и темнота), а они не могут бить 079 |
| `fire reset RP 0 1.2 0` | Удаляет правила SCP-173 для игроков 1 и 2 |

ID ролей:

| ID | Роль | ID | Роль | ID | Роль |
|---|---|---|---|---|---|
| 0 | SCP-173 | 8 | Chaos Conscript | 16 | SCP-939 |
| 1 | Class-D | 9 | SCP-096 | 18 | Chaos Rifleman |
| 3 | SCP-106 | 10 | SCP-049-2 | 19 | Chaos Marauder |
| 4 | NTF Specialist | 11 | NTF Sergeant | 20 | Chaos Repressor |
| 5 | SCP-049 | 12 | NTF Captain | 23 | SCP-3114 |
| 6 | Scientist | 13 | NTF Private | | |
| 7 | SCP-079 | 14 | Tutorial | | |
| | | 15 | Facility Guard | | |

## Конфиг

`LabAPI/configs/<порт>/FriendlyFireManager/config.yml`:

```yaml
# LabAPI permission needed to use the 'fire' command from the Remote Admin panel (LabAPI/configs/permissions.yml). Empty = everyone with RA access. The server console can always use it.
command_permission: ffm.manage
```

Пустое значение — команда доступна всем, у кого есть RA. Выключить плагин можно в
`LabAPI/configs/<порт>/FriendlyFireManager/properties.yml` (`is_enabled: false`).

## Как это устроено (коротко)

- Правила хранятся в `FireLogics`; `FireLogics.CanDamage(attacker, target)` возвращает `true` / `false` / `null`
  (= правила нет, ваниль). Всё остальное спрашивает его.
- Почти вся игровая логика меняется Harmony-патчами (`Patches/`) и событиями LabAPI (`Handlers/`).
- Атаку SCP-106 и прыжок SCP-939 решает *клиент* игры, а он никогда не считает SCP целью. Для них плагин
  использует одного невидимого и беззвучного dummy-игрока (**«SCP FF Proxy»**, Tutorial за пределами карты).
  Каждый клиент видит его в своей точке, а сервер потом выбирает настоящую цель-SCP (`FfProxy/`).
  Dummy существует только пока нужен и виден в списке игроков RA. Не кикайте его.

## Сборка

Нужны .NET SDK 8 или новее (любая ОС) и папка `SCPSL_Data/Managed` выделенного сервера SCP:SL
(Steam app 996560). DLL игры в репозитории **нет**.

1. Укажите сборке, где лежат DLL игры:
   - переменная окружения `SL_REFERENCES` с путём к папке `Managed`, или
   - скопируйте `References.props.example` в `References.props.user` и впишите путь туда (файл в .gitignore).
2. Соберите:
   ```
   dotnet build -c Release
   ```
   Плагин: `FriendlyFireManager/bin/Release/net48/FriendlyFireManager.dll`. На Windows локальная сборка ещё и
   копируется в `%AppData%\SCP Secret Laboratory\LabAPI\plugins\global\`, если такая папка есть.

`Assembly-CSharp.dll` публицизируется прямо при сборке пакетом
[BepInEx.AssemblyPublicizer.MSBuild](https://github.com/BepInEx/BepInEx.AssemblyPublicizer), вручную
публицизированная DLL не нужна.

### GitHub Actions
- `.github/workflows/build.yml` собирает каждый push и pull request; DLL прикладываются к запуску как artifact.
- `.github/workflows/release.yml` собирает при публикации GitHub-релиза и прикладывает к нему
  `FriendlyFireManager.dll` и `0Harmony.dll`. Перед релизом поднимите `<Version>` в
  `FriendlyFireManager/FriendlyFireManager.csproj`.

Оба скачивают DLL игры из Steam через
[Axwabo/scpsl-references-downloader](https://github.com/Axwabo/scpsl-references-downloader).

## Лицензия

[MIT](LICENSE). SCP: Secret Laboratory и её сборки принадлежат Northwood Studios и не входят в этот проект.
