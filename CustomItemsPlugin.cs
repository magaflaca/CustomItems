using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;

namespace CustomItems;

[ApiVersion(2, 1)]
public sealed class CustomItemsPlugin : TerrariaPlugin
{
    private const string CustomItemPermission = "customitem";
    private const string GiveCustomItemPermission = "customitem.give";

    private readonly List<Command> _registeredCommands = new();
    private bool _commandsRegistered;

    public override string Name => "CustomItems";
    public override string Author => "Interverse; updated for TShock v6.x by isawicca";
    public override string Description => "Allows you to spawn custom items";
    public override Version Version => new(1, 5, 0);

    public CustomItemsPlugin(Main game) : base(game)
    {
    }

    public override void Initialize()
    {
        ServerApi.Hooks.GameInitialize.Register(this, OnGameInitialize);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServerApi.Hooks.GameInitialize.Deregister(this, OnGameInitialize);
            DeregisterCommands();
        }

        base.Dispose(disposing);
    }

    private void OnGameInitialize(EventArgs args)
    {
        RegisterCommands();
    }

    private void RegisterCommands()
    {
        if (_commandsRegistered)
        {
            return;
        }

        var customItem = new Command(CustomItemPermission, CustomItemCommand, "customitem", "citem")
        {
            HelpText = HelpText.ForCustomItem
        };

        var giveCustomItem = new Command(GiveCustomItemPermission, GiveCustomItemCommand, "givecustomitem", "gcitem")
        {
            HelpText = HelpText.ForGiveCustomItem
        };

        Commands.ChatCommands.Add(customItem);
        Commands.ChatCommands.Add(giveCustomItem);

        _registeredCommands.Add(customItem);
        _registeredCommands.Add(giveCustomItem);
        _commandsRegistered = true;
    }

    private void DeregisterCommands()
    {
        foreach (var command in _registeredCommands)
        {
            Commands.ChatCommands.Remove(command);
        }

        _registeredCommands.Clear();
        _commandsRegistered = false;
    }

    private void CustomItemCommand(CommandArgs args)
    {
        if (!EnsurePlayerCommand(args))
        {
            return;
        }

        if (args.Parameters.Count == 0 || IsHelpRequest(args.Parameters[0]))
        {
            args.Player.SendErrorMessage(HelpText.InvalidCustomItemSyntax);
            return;
        }

        if (!TryResolveItem(args.Player, args.Parameters[0], out var item))
        {
            return;
        }

        if (!TryParseCustomization(args.Player, args.Parameters.Skip(1).ToList(), out var customization))
        {
            return;
        }

        var created = CreateAndBroadcastCustomItem(item, args.Player, customization);
        args.Player.SendSuccessMessage("You were successfully given {0} custom {1}!", created.stack, created.HoverName);
    }

    private void GiveCustomItemCommand(CommandArgs args)
    {
        if (args.Parameters.Count == 0 || IsHelpRequest(args.Parameters[0]))
        {
            args.Player.SendErrorMessage(HelpText.InvalidGiveCustomItemSyntax);
            return;
        }

        if (args.Parameters.Count == 1)
        {
            args.Player.SendErrorMessage("Failed to provide arguments to item.");
            return;
        }

        var players = TSPlayer.FindByNameOrID(args.Parameters[0]);
        if (players.Count != 1)
        {
            args.Player.SendErrorMessage("Failed to find player of: {0}", args.Parameters[0]);
            return;
        }

        var target = players[0];
        if (target == null || !target.Active)
        {
            args.Player.SendErrorMessage("Failed to find player of: {0}", args.Parameters[0]);
            return;
        }

        if (!TryResolveItem(args.Player, args.Parameters[1], out var item))
        {
            return;
        }

        if (!TryParseCustomization(args.Player, args.Parameters.Skip(2).ToList(), out var customization))
        {
            return;
        }

        var created = CreateAndBroadcastCustomItem(item, target, customization);
        target.SendSuccessMessage("{0} gave you {1} custom {2}!", args.Player.Name, created.stack, created.HoverName);
        args.Player.SendSuccessMessage("Successfully gave {0} {1} custom {2}!", target.Name, created.stack, created.HoverName);
    }

    private static bool EnsurePlayerCommand(CommandArgs args)
    {
        if (args.Player == null || args.Player.Index < 0)
        {
            args.Player?.SendErrorMessage("This command must be used by an in-game player. Use /givecustomitem from console instead.");
            return false;
        }

        return true;
    }

    private static bool IsHelpRequest(string value)
    {
        return value.Equals("help", StringComparison.OrdinalIgnoreCase) || value.Equals("?", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveItem(TSPlayer player, string rawItem, out Item item)
    {
        var matches = TShock.Utils.GetItemByIdOrName(rawItem);
        if (matches.Count == 0)
        {
            player.SendErrorMessage("No item found by the name of {0}.", rawItem);
            item = new Item();
            return false;
        }

        item = matches[0];
        return true;
    }

    private static bool TryParseCustomization(TSPlayer player, IReadOnlyList<string> parameters, out ItemCustomization customization)
    {
        customization = new ItemCustomization();

        if (parameters.Count == 0)
        {
            return true;
        }

        if (parameters.Count % 2 != 0)
        {
            player.SendErrorMessage("Invalid parameter list. Parameters must be key/value pairs. Example: d 500 kb 8 ss 12");
            return false;
        }

        for (var i = 0; i < parameters.Count; i += 2)
        {
            var key = parameters[i].Trim().ToLowerInvariant();
            var value = parameters[i + 1].Trim();

            try
            {
                switch (key)
                {
                    case "hexcolor":
                    case "hc":
                    case "color":
                    case "c":
                        customization.Color = ParseColor(value);
                        break;

                    case "damage":
                    case "dmg":
                    case "d":
                        customization.Damage = ParseInt(value, key, min: 0, max: ushort.MaxValue);
                        break;

                    case "knockback":
                    case "kb":
                        customization.KnockBack = ParseFloat(value, key, min: 0f);
                        break;

                    case "useanimation":
                    case "ua":
                        customization.UseAnimation = ParseInt(value, key, min: 0, max: ushort.MaxValue);
                        break;

                    case "usetime":
                    case "ut":
                        customization.UseTime = ParseInt(value, key, min: 0, max: ushort.MaxValue);
                        break;

                    case "shoot":
                    case "s":
                        customization.Shoot = ParseInt(value, key, min: short.MinValue, max: short.MaxValue);
                        break;

                    case "shootspeed":
                    case "speed":
                    case "ss":
                        customization.ShootSpeed = ParseFloat(value, key, min: 0f);
                        break;

                    case "width":
                    case "w":
                        customization.Width = ParseInt(value, key, min: 1, max: short.MaxValue);
                        break;

                    case "height":
                    case "h":
                        customization.Height = ParseInt(value, key, min: 1, max: short.MaxValue);
                        break;

                    case "scale":
                    case "sc":
                        customization.Scale = ParseFloat(value, key, min: 0.01f);
                        break;

                    case "ammo":
                    case "a":
                        customization.Ammo = ParseInt(value, key, min: 0, max: short.MaxValue);
                        break;

                    case "useammo":
                    case "uam":
                    case "ua_ammo":
                        customization.UseAmmo = ParseInt(value, key, min: 0, max: short.MaxValue);
                        break;

                    case "notammo":
                    case "na":
                        customization.NotAmmo = ParseBool(value, key);
                        break;

                    case "stack":
                    case "st":
                        customization.Stack = ParseInt(value, key, min: 1, max: short.MaxValue);
                        break;

                    default:
                        player.SendErrorMessage("Unknown CustomItems parameter: {0}", key);
                        return false;
                }
            }
            catch (FormatException ex)
            {
                player.SendErrorMessage(ex.Message);
                return false;
            }
            catch (OverflowException ex)
            {
                player.SendErrorMessage(ex.Message);
                return false;
            }
        }

        return true;
    }

    private static Item CreateAndBroadcastCustomItem(Item baseItem, TSPlayer target, ItemCustomization customization)
    {
        var working = TShock.Utils.GetItemById(baseItem.type);
        customization.ApplyTo(working);

        object? source = Projectile.GetNoneSource();

        var index = NewItemCompat(
            source,
            (int)target.X,
            (int)target.Y,
            Math.Max(1, working.width),
            Math.Max(1, working.height),
            working.type,
            Math.Max(1, working.stack));

        var spawnedWorldItem = GetSpawnedWorldItem(index);
        if (spawnedWorldItem != null)
        {
            TrySetMemberValue(spawnedWorldItem, "playerIndexTheItemIsReservedFor", target.Index);
            customization.ApplyToReflectedObject(spawnedWorldItem);

            var containedItem = TryGetContainedItem(spawnedWorldItem);
            if (containedItem != null)
            {
                customization.ApplyTo(containedItem);
                TrySetMemberValue(containedItem, "playerIndexTheItemIsReservedFor", target.Index);
            }

            TryInvokeParameterless(spawnedWorldItem, "UpdateEntityFields");
        }

        TSPlayer.All.SendData((PacketTypes)90, string.Empty, index, 0f, 0f, 0f, 0);
        TSPlayer.All.SendData((PacketTypes)22, string.Empty, index, 0f, 0f, 0f, 0);
        TSPlayer.All.SendData((PacketTypes)88, string.Empty, index, 255f, 63f, 0f, 0);

        return working;
    }

    private static int NewItemCompat(object? source, int x, int y, int width, int height, int type, int stack)
    {
        var methods = typeof(Item)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(Item.NewItem) && method.ReturnType == typeof(int))
            .OrderByDescending(method => method.GetParameters().Length)
            .ToArray();

        var errors = new List<string>();

        foreach (var method in methods)
        {
            if (!TryBuildNewItemArguments(method, source, x, y, width, height, type, stack, out var args))
            {
                continue;
            }

            try
            {
                return (int)method.Invoke(null, args)!;
            }
            catch (TargetInvocationException ex)
            {
                errors.Add($"{method}: {ex.InnerException?.GetType().Name ?? ex.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"{method}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        var detail = errors.Count == 0 ? "No compatible overload was found." : string.Join(" | ", errors);
        throw new MissingMethodException("Could not invoke a compatible Terraria.Item.NewItem overload. " + detail);
    }

    private static bool TryBuildNewItemArguments(
        MethodInfo method,
        object? source,
        int x,
        int y,
        int width,
        int height,
        int type,
        int stack,
        out object?[] args)
    {
        var parameters = method.GetParameters();
        args = new object?[parameters.Length];
        var index = 0;

        if (parameters.Length == 0)
        {
            return false;
        }

        if (CanAccept(parameters[index].ParameterType, source))
        {
            args[index++] = source;
        }

        if (TryBuildRectangleIntNewItemArguments(parameters, args, index, x, y, width, height, type, stack))
        {
            return true;
        }

        if (TryBuildVector2NewItemArguments(parameters, args, index, x, y, width, height, type, stack))
        {
            return true;
        }

        if (TryBuildRectangleObjectNewItemArguments(parameters, args, index, x, y, width, height, type, stack))
        {
            return true;
        }

        args = Array.Empty<object?>();
        return false;
    }

    private static bool TryBuildRectangleIntNewItemArguments(
        ParameterInfo[] parameters,
        object?[] args,
        int index,
        int x,
        int y,
        int width,
        int height,
        int type,
        int stack)
    {
        if (parameters.Length - index < 6)
        {
            return false;
        }

        if (parameters[index].ParameterType != typeof(int) ||
            parameters[index + 1].ParameterType != typeof(int) ||
            parameters[index + 2].ParameterType != typeof(int) ||
            parameters[index + 3].ParameterType != typeof(int) ||
            parameters[index + 4].ParameterType != typeof(int) ||
            parameters[index + 5].ParameterType != typeof(int))
        {
            return false;
        }

        args[index++] = x;
        args[index++] = y;
        args[index++] = width;
        args[index++] = height;
        args[index++] = type;
        args[index++] = stack;

        return FillNewItemOptionalArguments(parameters, args, index);
    }

    private static bool TryBuildVector2NewItemArguments(
        ParameterInfo[] parameters,
        object?[] args,
        int index,
        int x,
        int y,
        int width,
        int height,
        int type,
        int stack)
    {
        if (parameters.Length - index < 5 || parameters[index].ParameterType != typeof(Vector2))
        {
            return false;
        }

        if (parameters[index + 1].ParameterType != typeof(int) ||
            parameters[index + 2].ParameterType != typeof(int) ||
            parameters[index + 3].ParameterType != typeof(int) ||
            parameters[index + 4].ParameterType != typeof(int))
        {
            return false;
        }

        args[index++] = new Vector2(x, y);
        args[index++] = width;
        args[index++] = height;
        args[index++] = type;
        args[index++] = stack;

        return FillNewItemOptionalArguments(parameters, args, index);
    }

    private static bool TryBuildRectangleObjectNewItemArguments(
        ParameterInfo[] parameters,
        object?[] args,
        int index,
        int x,
        int y,
        int width,
        int height,
        int type,
        int stack)
    {
        if (parameters.Length - index < 3 || parameters[index].ParameterType != typeof(Rectangle))
        {
            return false;
        }

        if (parameters[index + 1].ParameterType != typeof(int) ||
            parameters[index + 2].ParameterType != typeof(int))
        {
            return false;
        }

        args[index++] = new Rectangle(x, y, width, height);
        args[index++] = type;
        args[index++] = stack;

        return FillNewItemOptionalArguments(parameters, args, index);
    }

    private static bool FillNewItemOptionalArguments(ParameterInfo[] parameters, object?[] args, int index)
    {
        while (index < parameters.Length)
        {
            var parameter = parameters[index];
            var parameterType = parameter.ParameterType;
            var name = parameter.Name ?? string.Empty;

            if (parameterType == typeof(bool))
            {
                args[index] = name.Contains("grab", StringComparison.OrdinalIgnoreCase)
                    ? true
                    : false;
            }
            else if (parameterType == typeof(int))
            {
                args[index] = 0;
            }
            else if (parameter.HasDefaultValue)
            {
                args[index] = parameter.DefaultValue;
            }
            else
            {
                return false;
            }

            index++;
        }

        return true;
    }

    private static bool CanAccept(Type destinationType, object? value)
    {
        if (value == null)
        {
            return !destinationType.IsValueType || Nullable.GetUnderlyingType(destinationType) != null;
        }

        var targetType = Nullable.GetUnderlyingType(destinationType) ?? destinationType;
        return targetType.IsAssignableFrom(value.GetType());
    }

    private static object? GetSpawnedWorldItem(int index)
    {
        var itemField = typeof(Main).GetField("item", BindingFlags.Public | BindingFlags.Static);
        if (itemField?.GetValue(null) is not Array items || index < 0 || index >= items.Length)
        {
            return null;
        }

        return items.GetValue(index);
    }

    private static Item? TryGetContainedItem(object target)
    {
        if (target is Item item)
        {
            return item;
        }

        var targetType = target.GetType();
        foreach (var name in new[] { "Item", "item", "_item", "InnerItem", "ContainedItem" })
        {
            if (TryGetMemberValue(target, name) is Item namedItem)
            {
                return namedItem;
            }
        }

        for (var type = targetType; type != null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(Item).IsAssignableFrom(field.FieldType) && field.GetValue(target) is Item fieldItem)
                {
                    return fieldItem;
                }
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(Item).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        if (property.GetValue(target) is Item propertyItem)
                        {
                            return propertyItem;
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        return null;
    }

    private static object? TryGetMemberValue(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(target);
            }

            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(target);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    internal static bool TrySetMemberValue(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, ConvertMemberValue(value, field.FieldType));
                return true;
            }

            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(target, ConvertMemberValue(value, property.PropertyType));
                return true;
            }
        }

        return false;
    }

    private static object ConvertMemberValue(object value, Type destinationType)
    {
        var nullableType = Nullable.GetUnderlyingType(destinationType);
        if (nullableType != null)
        {
            destinationType = nullableType;
        }

        if (destinationType.IsInstanceOfType(value))
        {
            return value;
        }

        if (destinationType.IsEnum)
        {
            return Enum.ToObject(destinationType, value);
        }

        return Convert.ChangeType(value, destinationType, CultureInfo.InvariantCulture);
    }

    private static void TryInvokeParameterless(object target, string methodName)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);
            if (method == null)
            {
                continue;
            }

            try
            {
                method.Invoke(target, null);
            }
            catch
            {
            }

            return;
        }
    }

    private static int ParseInt(string value, string key, int min, int max)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid integer value for {key}: {value}");
        }

        if (parsed < min || parsed > max)
        {
            throw new OverflowException($"Value for {key} must be between {min} and {max}.");
        }

        return parsed;
    }

    private static float ParseFloat(string value, string key, float min)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid numeric value for {key}: {value}");
        }

        if (float.IsNaN(parsed) || float.IsInfinity(parsed) || parsed < min)
        {
            throw new OverflowException($"Value for {key} must be a finite number >= {min.ToString(CultureInfo.InvariantCulture)}.");
        }

        return parsed;
    }

    private static bool ParseBool(string value, string key)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "yes" or "y" or "on" => true,
            "0" or "no" or "n" or "off" => false,
            _ => throw new FormatException($"Invalid boolean value for {key}: {value}. Use true/false, 1/0, yes/no, or on/off.")
        };
    }

    private static Color ParseColor(string value)
    {
        var normalized = value.Trim().TrimStart('#');

        if (normalized.Contains(','))
        {
            var parts = normalized.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is not (3 or 4))
            {
                throw new FormatException("Invalid color value. Use RRGGBB, #RRGGBB, R,G,B, or R,G,B,A.");
            }

            var r = ParseByte(parts[0], "red");
            var g = ParseByte(parts[1], "green");
            var b = ParseByte(parts[2], "blue");
            var a = parts.Length == 4 ? ParseByte(parts[3], "alpha") : byte.MaxValue;
            return new Color(r, g, b, a);
        }

        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        if (normalized.Length == 6)
        {
            var r = byte.Parse(normalized[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var g = byte.Parse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var b = byte.Parse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color(r, g, b);
        }

        if (normalized.Length == 8)
        {
            var r = byte.Parse(normalized[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var g = byte.Parse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var b = byte.Parse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var a = byte.Parse(normalized.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color(r, g, b, a);
        }

        throw new FormatException("Invalid color value. Use RRGGBB, #RRGGBB, RRGGBBAA, R,G,B, or R,G,B,A.");
    }

    private static byte ParseByte(string value, string name)
    {
        if (!byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"Invalid {name} color component: {value}");
        }

        return parsed;
    }
}

internal sealed class ItemCustomization
{
    public Color? Color { get; set; }
    public int? Damage { get; set; }
    public float? KnockBack { get; set; }
    public int? UseAnimation { get; set; }
    public int? UseTime { get; set; }
    public int? Shoot { get; set; }
    public float? ShootSpeed { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public float? Scale { get; set; }
    public int? Ammo { get; set; }
    public int? UseAmmo { get; set; }
    public bool? NotAmmo { get; set; }
    public int? Stack { get; set; }

    public void ApplyTo(Item item)
    {
        if (Color.HasValue)
        {
            item.color = Color.Value;
        }

        if (Damage.HasValue)
        {
            item.damage = Damage.Value;
        }

        if (KnockBack.HasValue)
        {
            item.knockBack = KnockBack.Value;
        }

        if (UseAnimation.HasValue)
        {
            item.useAnimation = UseAnimation.Value;
        }

        if (UseTime.HasValue)
        {
            item.useTime = UseTime.Value;
        }

        if (Shoot.HasValue)
        {
            item.shoot = Shoot.Value;
        }

        if (ShootSpeed.HasValue)
        {
            item.shootSpeed = ShootSpeed.Value;
        }

        if (Width.HasValue)
        {
            item.width = Width.Value;
        }

        if (Height.HasValue)
        {
            item.height = Height.Value;
        }

        if (Scale.HasValue)
        {
            item.scale = Scale.Value;
        }

        if (Ammo.HasValue)
        {
            item.ammo = Ammo.Value;
        }

        if (UseAmmo.HasValue)
        {
            item.useAmmo = UseAmmo.Value;
        }

        if (NotAmmo.HasValue)
        {
            item.notAmmo = NotAmmo.Value;
        }

        if (Stack.HasValue)
        {
            item.stack = Stack.Value;
        }
    }

    public void ApplyToReflectedObject(object target)
    {
        if (Color.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "color", Color.Value);
        }

        if (Damage.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "damage", Damage.Value);
        }

        if (KnockBack.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "knockBack", KnockBack.Value);
        }

        if (UseAnimation.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "useAnimation", UseAnimation.Value);
        }

        if (UseTime.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "useTime", UseTime.Value);
        }

        if (Shoot.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "shoot", Shoot.Value);
        }

        if (ShootSpeed.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "shootSpeed", ShootSpeed.Value);
        }

        if (Width.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "width", Width.Value);
        }

        if (Height.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "height", Height.Value);
        }

        if (Scale.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "scale", Scale.Value);
        }

        if (Ammo.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "ammo", Ammo.Value);
        }

        if (UseAmmo.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "useAmmo", UseAmmo.Value);
        }

        if (NotAmmo.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "notAmmo", NotAmmo.Value);
        }

        if (Stack.HasValue)
        {
            CustomItemsPlugin.TrySetMemberValue(target, "stack", Stack.Value);
        }
    }
}

internal static class HelpText
{
    private const string ParameterLine = "Parameters: hexcolor (hc), damage (d), knockback (kb), useanimation (ua), usetime (ut), " +
                                         "shoot (s), shootspeed (ss), width (w), height (h), scale (sc), ammo (a), " +
                                         "useammo (uam), notammo (na), stack (st).";

    public const string ForCustomItem = "/customitem <id/itemname> <parameters> <#> ... \n" + ParameterLine;
    public const string ForGiveCustomItem = "/givecustomitem <name> <id/itemname> <parameters> <#> ... \n" + ParameterLine;

    public const string InvalidCustomItemSyntax = "Invalid Syntax. " + ForCustomItem;
    public const string InvalidGiveCustomItemSyntax = "Invalid Syntax. " + ForGiveCustomItem;
}
