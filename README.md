# CustomItems

CustomItems is a TShock plugin for spawning Terraria items with edited item stats.

Original plugin by **Interverse**. Updated for **TShock v6.x / .NET 9** by **isawicca**.

## Target

- TShock v6.x
- Terraria 1.4.5.x
- .NET 9

## Build

Place this project inside your TShock server folder, next to `TerrariaServer.dll` and `OTAPI.dll`.

Expected layout:

```text
TShockServer/
  TerrariaServer.dll
  OTAPI.dll
  ServerPlugins/
    TShockAPI.dll
  CustomItems/
    CustomItems.csproj
```

Build with:

```bash
dotnet build -c Release
```

If the project is somewhere else, pass the server path:

```bash
dotnet build -c Release -p:TShockServerPath="C:/path/to/TShockServer"
```

Copy the built `CustomItems.dll` to `ServerPlugins/` and restart the server.

## Commands

| Command | Alias | Permission |
| --- | --- | --- |
| `/customitem` | `/citem` | `customitem` |
| `/givecustomitem` | `/gcitem` | `customitem.give` |

## Usage

```text
/customitem <id or item name> <parameter> <value> ...
/givecustomitem <player> <id or item name> <parameter> <value> ...
```

Examples:

```text
/citem "Terra Blade" d 500 kb 8 s 266 ss 16 hc FF0000
/gcitem Alice Zenith s 857 ss 2
/citem 757 damage 999 scale 2.5 shoot 132 shootspeed 14
```

## Parameters

| Parameter | Alias | Value |
| --- | --- | --- |
| `hexcolor` | `hc` | `RRGGBB`, `#RRGGBB`, `R,G,B`, or `R,G,B,A` |
| `damage` | `d` | integer |
| `knockback` | `kb` | number |
| `useanimation` | `ua` | integer |
| `usetime` | `ut` | integer |
| `shoot` | `s` | projectile id |
| `shootspeed` | `ss` | number |
| `width` | `w` | integer |
| `height` | `h` | integer |
| `scale` | `sc` | number |
| `ammo` | `a` | ammo id |
| `useammo` | `uam` | ammo id |
| `notammo` | `na` | boolean |
| `stack` | `st` | integer |
