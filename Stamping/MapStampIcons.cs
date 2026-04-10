#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace MapStamp;

public static class MapStampIcons
{
    private static readonly Dictionary<string, Texture2D> Cache = [];
    private static readonly string[] SupportedImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    public static Texture2D ToolbarStamp => Load("toolbar_stamp");

    public static IReadOnlyList<string> GetAvailableStampImageFiles()
    {
        var directoryPath = GetStampImagesDirectoryPath();
        if (Directory.Exists(directoryPath) == false)
        {
            Log.Warn($"[MapStamp] stamp_img directory not found: {directoryPath}");
            return [];
        }

        return Directory
            .EnumerateFiles(directoryPath)
            .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Where(static fileName => string.IsNullOrWhiteSpace(fileName) == false)
            .Cast<string>()
            .OrderBy(static fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static Texture2D GetMenuIcon(string imageFileName)
    {
        return LoadStampImage(imageFileName);
    }

    public static Image GetStampSourceImage(string imageFileName)
    {
        var path = GetStampImagePath(imageFileName);
        Log.Warn($"[MapStamp] Loading stamp source image: {path}");
        return Image.LoadFromFile(path);
    }

    private static Texture2D Load(string resourceName)
    {
        if (Cache.TryGetValue(resourceName, out var cachedTexture) == true)
        {
            return cachedTexture;
        }

        var assembly = typeof(MapStampIcons).Assembly;
        var fullResourceName = $"MapStamp.Assets.{resourceName}.svg";

        using var stream = assembly.GetManifestResourceStream(fullResourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded icon resource not found: {fullResourceName}");
        }

        using var reader = new StreamReader(stream);
        var svg = reader.ReadToEnd();

        var image = new Image();
        var error = image.LoadSvgFromString(svg);
        if (error != Error.Ok)
        {
            throw new IOException($"Failed to load icon SVG: {fullResourceName}. Error={error}");
        }

        var texture = ImageTexture.CreateFromImage(image);
        Cache[resourceName] = texture;
        return texture;
    }

    private static Texture2D LoadStampImage(string fileName)
    {
        var cacheKey = $"stamp::{fileName}";
        if (Cache.TryGetValue(cacheKey, out var cachedTexture) == true)
        {
            return cachedTexture;
        }

        var path = GetStampImagePath(fileName);
        Log.Warn($"[MapStamp] Loading stamp image: {path}");
        var image = Image.LoadFromFile(path);
        var texture = ImageTexture.CreateFromImage(image);
        Cache[cacheKey] = texture;
        return texture;
    }

    private static string GetStampImagesDirectoryPath()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(MapStampIcons).Assembly.Location) ?? AppContext.BaseDirectory;
        return Path.Combine(assemblyDirectory, "stamp_img");
    }

    private static string GetStampImagePath(string fileName)
    {
        return Path.Combine(GetStampImagesDirectoryPath(), fileName);
    }
}
