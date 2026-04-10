#nullable enable
using Godot;
using System;
using System.Collections.Generic;

namespace MapStamp;

public static class MapStampImageStrokeGenerator
{
    public const float BaseStampScale = 1.5f;

    private const float AlphaThreshold = 0.05f;
    private const float BackgroundColorThreshold = 0.16f;
    private const int TargetRasterSize = 48;
    private const float PixelScale = 2.8f;
    private const float DotHalfLength = 0.42f;

    public static Vector2[][] Generate(Image sourceImage, float scaleMultiplier)
    {
        var foregroundMask = BuildForegroundMask(sourceImage);
        var foregroundBounds = FindForegroundBounds(foregroundMask);
        if (foregroundBounds.Size.X <= 0 || foregroundBounds.Size.Y <= 0)
        {
            return [];
        }

        var targetSize = GetTargetSize(foregroundBounds.Size, scaleMultiplier);
        var raster = sourceImage.GetRegion(foregroundBounds);
        raster.Resize(targetSize.X, targetSize.Y, Image.Interpolation.Cubic);

        var resizedMask = ResampleMask(foregroundMask, foregroundBounds, targetSize.X, targetSize.Y);
        return BuildDotStrokes(raster, resizedMask, BaseStampScale);
    }

    private static Vector2[][] BuildDotStrokes(Image raster, bool[,] foregroundMask, float effectiveScale)
    {
        var width = foregroundMask.GetLength(0);
        var height = foregroundMask.GetLength(1);
        var edgeMask = BuildEdgeMask(foregroundMask);
        var histogram = new int[256];
        var foregroundCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (foregroundMask[x, y] == false)
                {
                    continue;
                }

                if (edgeMask[x, y] == true)
                {
                    continue;
                }

                var luminance = GetLuminance(raster.GetPixel(x, y));
                var bucket = Math.Clamp((int)MathF.Round(luminance * 255.0f), 0, 255);
                histogram[bucket]++;
                foregroundCount++;
            }
        }

        if (foregroundCount == 0 && HasAnyTrue(edgeMask) == false)
        {
            return [];
        }

        var threshold = foregroundCount > 0 ? ComputeOtsuThreshold(histogram, foregroundCount) : 0.5f;
        var strokes = new List<Vector2[]>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (foregroundMask[x, y] == false)
                {
                    continue;
                }

                if (edgeMask[x, y] == true)
                {
                    strokes.Add(CreateDotStroke(x, y, width, height, effectiveScale));
                    continue;
                }

                var luminance = GetLuminance(raster.GetPixel(x, y));
                if (luminance > threshold)
                {
                    continue;
                }

                strokes.Add(CreateDotStroke(x, y, width, height, effectiveScale));
            }
        }

        return strokes.ToArray();
    }

    private static bool[,] BuildEdgeMask(bool[,] foregroundMask)
    {
        var width = foregroundMask.GetLength(0);
        var height = foregroundMask.GetLength(1);
        var edgeMask = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (foregroundMask[x, y] == false)
                {
                    continue;
                }

                if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    edgeMask[x, y] = true;
                    continue;
                }

                edgeMask[x, y] =
                    foregroundMask[x - 1, y] == false ||
                    foregroundMask[x + 1, y] == false ||
                    foregroundMask[x, y - 1] == false ||
                    foregroundMask[x, y + 1] == false ||
                    foregroundMask[x - 1, y - 1] == false ||
                    foregroundMask[x + 1, y - 1] == false ||
                    foregroundMask[x - 1, y + 1] == false ||
                    foregroundMask[x + 1, y + 1] == false;
            }
        }

        return edgeMask;
    }

    private static bool[,] BuildForegroundMask(Image image)
    {
        var width = image.GetWidth();
        var height = image.GetHeight();
        var backgroundMask = new bool[width, height];
        var backgroundColor = EstimateBackgroundColor(image);
        var queue = new Queue<Vector2I>();

        for (int x = 0; x < width; x++)
        {
            TryEnqueueBackgroundPixel(image, backgroundColor, backgroundMask, queue, x, 0);
            TryEnqueueBackgroundPixel(image, backgroundColor, backgroundMask, queue, x, height - 1);
        }

        for (int y = 1; y < height - 1; y++)
        {
            TryEnqueueBackgroundPixel(image, backgroundColor, backgroundMask, queue, 0, y);
            TryEnqueueBackgroundPixel(image, backgroundColor, backgroundMask, queue, width - 1, y);
        }

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();

            TryEnqueueBackgroundPixel(image, backgroundColor, backgroundMask, queue, point.X - 1, point.Y);
            TryEnqueueBackgroundPixel(image, backgroundColor, backgroundMask, queue, point.X + 1, point.Y);
            TryEnqueueBackgroundPixel(image, backgroundColor, backgroundMask, queue, point.X, point.Y - 1);
            TryEnqueueBackgroundPixel(image, backgroundColor, backgroundMask, queue, point.X, point.Y + 1);
        }

        var foregroundMask = new bool[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.A < AlphaThreshold)
                {
                    continue;
                }

                foregroundMask[x, y] = backgroundMask[x, y] == false;
            }
        }

        return foregroundMask;
    }

    private static Rect2I FindForegroundBounds(bool[,] foregroundMask)
    {
        int minX = foregroundMask.GetLength(0);
        int minY = foregroundMask.GetLength(1);
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < foregroundMask.GetLength(1); y++)
        {
            for (int x = 0; x < foregroundMask.GetLength(0); x++)
            {
                if (foregroundMask[x, y] == false)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return new Rect2I(0, 0, 0, 0);
        }

        return new Rect2I(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
    }

    private static Vector2I GetTargetSize(Vector2I sourceSize, float scaleMultiplier)
    {
        var maxDimension = Math.Max(sourceSize.X, sourceSize.Y);
        if (maxDimension <= 0)
        {
            return new Vector2I(1, 1);
        }

        var scale = (TargetRasterSize * scaleMultiplier) / (float)maxDimension;
        var targetWidth = Math.Max(1, (int)MathF.Round(sourceSize.X * scale));
        var targetHeight = Math.Max(1, (int)MathF.Round(sourceSize.Y * scale));
        return new Vector2I(targetWidth, targetHeight);
    }

    private static bool[,] ResampleMask(bool[,] sourceMask, Rect2I bounds, int targetWidth, int targetHeight)
    {
        var resizedMask = new bool[targetWidth, targetHeight];

        for (int y = 0; y < targetHeight; y++)
        {
            var normalizedY = targetHeight == 1 ? 0.0f : y / (float)(targetHeight - 1);
            var sourceY = bounds.Position.Y + Math.Clamp((int)MathF.Round(normalizedY * (bounds.Size.Y - 1)), 0, bounds.Size.Y - 1);

            for (int x = 0; x < targetWidth; x++)
            {
                var normalizedX = targetWidth == 1 ? 0.0f : x / (float)(targetWidth - 1);
                var sourceX = bounds.Position.X + Math.Clamp((int)MathF.Round(normalizedX * (bounds.Size.X - 1)), 0, bounds.Size.X - 1);
                resizedMask[x, y] = sourceMask[sourceX, sourceY];
            }
        }

        return resizedMask;
    }

    private static Color EstimateBackgroundColor(Image image)
    {
        var sampleSize = Math.Clamp(Math.Min(image.GetWidth(), image.GetHeight()) / 10, 1, 4);
        var samples = new List<Color>();

        AddCornerSamples(image, samples, 0, 0, sampleSize);
        AddCornerSamples(image, samples, image.GetWidth() - sampleSize, 0, sampleSize);
        AddCornerSamples(image, samples, 0, image.GetHeight() - sampleSize, sampleSize);
        AddCornerSamples(image, samples, image.GetWidth() - sampleSize, image.GetHeight() - sampleSize, sampleSize);

        var red = 0.0f;
        var green = 0.0f;
        var blue = 0.0f;
        var alpha = 0.0f;

        foreach (var sample in samples)
        {
            red += sample.R;
            green += sample.G;
            blue += sample.B;
            alpha += sample.A;
        }

        var count = Math.Max(1, samples.Count);
        return new Color(red / count, green / count, blue / count, alpha / count);
    }

    private static void AddCornerSamples(Image image, List<Color> samples, int startX, int startY, int size)
    {
        var clampedStartX = Math.Max(0, startX);
        var clampedStartY = Math.Max(0, startY);
        var endX = Math.Min(image.GetWidth(), clampedStartX + size);
        var endY = Math.Min(image.GetHeight(), clampedStartY + size);

        for (int y = clampedStartY; y < endY; y++)
        {
            for (int x = clampedStartX; x < endX; x++)
            {
                samples.Add(image.GetPixel(x, y));
            }
        }
    }

    private static void TryEnqueueBackgroundPixel(Image image, Color backgroundColor, bool[,] backgroundMask, Queue<Vector2I> queue, int x, int y)
    {
        if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
        {
            return;
        }

        if (backgroundMask[x, y] == true)
        {
            return;
        }

        if (IsBackgroundLike(image.GetPixel(x, y), backgroundColor) == false)
        {
            return;
        }

        backgroundMask[x, y] = true;
        queue.Enqueue(new Vector2I(x, y));
    }

    private static bool IsBackgroundLike(Color pixel, Color backgroundColor)
    {
        if (pixel.A < AlphaThreshold)
        {
            return true;
        }

        return ColorDifference(pixel, backgroundColor) <= BackgroundColorThreshold;
    }

    private static float ColorDifference(Color a, Color b)
    {
        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;
        return MathF.Sqrt((dr * dr) + (dg * dg) + (db * db));
    }

    private static float GetLuminance(Color color)
    {
        return (color.R * 0.299f) + (color.G * 0.587f) + (color.B * 0.114f);
    }

    private static float ComputeOtsuThreshold(int[] histogram, int totalCount)
    {
        var total = 0.0f;
        for (int i = 0; i < histogram.Length; i++)
        {
            total += i * histogram[i];
        }

        var sumBackground = 0.0f;
        var weightBackground = 0;
        var maxVariance = -1.0f;
        var threshold = 128;

        for (int i = 0; i < histogram.Length; i++)
        {
            weightBackground += histogram[i];
            if (weightBackground == 0)
            {
                continue;
            }

            var weightForeground = totalCount - weightBackground;
            if (weightForeground == 0)
            {
                break;
            }

            sumBackground += i * histogram[i];
            var meanBackground = sumBackground / weightBackground;
            var meanForeground = (total - sumBackground) / weightForeground;
            var meanDelta = meanBackground - meanForeground;
            var betweenClassVariance = weightBackground * weightForeground * meanDelta * meanDelta;

            if (betweenClassVariance > maxVariance)
            {
                maxVariance = betweenClassVariance;
                threshold = i;
            }
        }

        return threshold / 255.0f;
    }

    private static bool HasAnyTrue(bool[,] mask)
    {
        for (int y = 0; y < mask.GetLength(1); y++)
        {
            for (int x = 0; x < mask.GetLength(0); x++)
            {
                if (mask[x, y] == true)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Vector2[] CreateDotStroke(int x, int y, int width, int height, float effectiveScale)
    {
        var center = ToOffset(x, y, width, height, effectiveScale);
        return
        [
            center + new Vector2(-(DotHalfLength * effectiveScale), 0.0f),
            center + new Vector2(DotHalfLength * effectiveScale, 0.0f),
        ];
    }

    private static Vector2 ToOffset(int x, int y, int width, int height, float effectiveScale)
    {
        var centeredX = x - ((width - 1) / 2.0f);
        var centeredY = y - ((height - 1) / 2.0f);
        return new Vector2(
            centeredX * PixelScale * effectiveScale,
            centeredY * PixelScale * effectiveScale);
    }
}
