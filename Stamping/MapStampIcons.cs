#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using DrawingBitmap = System.Drawing.Bitmap;

namespace MapStamp;

public static class MapStampIcons
{
    private static readonly Dictionary<string, Texture2D> Cache = [];
    private static readonly HashSet<string> LoggedFormatMismatchFiles = [];
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
        var image = LoadImageWithFallback(imageFileName, path, "source");
        EnsureImageIsUsable(imageFileName, path, image, "source");
        return image;
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
        var image = LoadImageWithFallback(fileName, path, "menu");
        EnsureImageIsUsable(fileName, path, image, "menu");
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

    private static void EnsureImageIsUsable(string fileName, string path, Image image, string usage)
    {
        if (image.GetWidth() > 0 && image.GetHeight() > 0)
        {
            return;
        }

        throw new IOException($"Stamp {usage} image is empty or unreadable. file={fileName} path={path}");
    }

    private static Image LoadImageWithFallback(string fileName, string path, string usage)
    {
        string? primaryIssue = null;
        try
        {
            var image = Image.LoadFromFile(path);
            if (image.GetWidth() > 0 && image.GetHeight() > 0)
            {
                return image;
            }

            primaryIssue = "primary=empty";
        }
        catch (Exception ex)
        {
            primaryIssue = $"primary={ex.Message}";
        }

        try
        {
            return LoadImageFromBuffer(fileName, path, usage);
        }
        catch (Exception bufferEx)
        {
            try
            {
                return LoadImageViaSystemDrawing(fileName, path, usage);
            }
            catch (Exception drawingEx)
            {
                throw new IOException(
                    $"Failed to load stamp {usage} image. file={fileName} path={path} {primaryIssue ?? "primary=unknown"} buffer={bufferEx.Message} drawing={drawingEx.Message}",
                    drawingEx);
            }
        }
    }

    private static Image LoadImageFromBuffer(string fileName, string path, string usage)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to read stamp {usage} image bytes. file={fileName} path={path}", ex);
        }

        var image = new Image();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var detectedFormat = DetectImageFormat(bytes);
        if (string.Equals(detectedFormat, extension, StringComparison.OrdinalIgnoreCase) == false &&
            LoggedFormatMismatchFiles.Add(fileName) == true)
        {
            Log.Warn($"[MapStamp] Image extension/format mismatch detected: file={fileName} ext={extension} detected={detectedFormat}");
        }

        var effectiveFormat = detectedFormat ?? extension;
        var error = effectiveFormat switch
        {
            ".png" => image.LoadPngFromBuffer(bytes),
            ".jpg" => image.LoadJpgFromBuffer(bytes),
            ".jpeg" => image.LoadJpgFromBuffer(bytes),
            ".webp" => image.LoadWebpFromBuffer(bytes),
            _ => Error.Unavailable,
        };

        if (error != Error.Ok)
        {
            throw new IOException($"Failed to load stamp {usage} image from buffer. file={fileName} path={path} error={error}");
        }

        return image;
    }

    private static string? DetectImageFormat(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A)
        {
            return ".png";
        }

        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xD8 &&
            bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 &&
            bytes[1] == 0x49 &&
            bytes[2] == 0x46 &&
            bytes[3] == 0x46 &&
            bytes[8] == 0x57 &&
            bytes[9] == 0x45 &&
            bytes[10] == 0x42 &&
            bytes[11] == 0x50)
        {
            return ".webp";
        }

        return null;
    }

    private static Image LoadImageViaSystemDrawing(string fileName, string path, string usage)
    {
        using var bitmap = new DrawingBitmap(path);
        var width = bitmap.Width;
        var height = bitmap.Height;
        var rgba = new byte[width * height * 4];
        var index = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                rgba[index++] = pixel.R;
                rgba[index++] = pixel.G;
                rgba[index++] = pixel.B;
                rgba[index++] = pixel.A;
            }
        }

        var image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, rgba);
        return image;
    }
}
