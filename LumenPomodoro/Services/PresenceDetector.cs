using System;
using Serilog;

namespace LumenPomodoro.Services;

/// <summary>
/// 基于帧差分的在位检测。比较连续帧的灰度缩略图，
/// 统计像素变化比例，判断是否有人在摄像头前。
/// </summary>
public class PresenceDetector
{
    private byte[]? _previousFrame;

    private const byte PixelChangeThreshold = 30;

    private const double MovementThreshold = 0.15;

    private int _stillFrameCount;

    public int StillFrameThreshold { get; set; } = 5;

    public bool IsPresent { get; private set; } = true;

    public int StillFrameCount => _stillFrameCount;

    public bool ProcessFrame(byte[] pixelData, int width, int height, int stride)
    {
        var current = DownsampleToGrayscale(pixelData, width, height, stride, 16, 16);

        if (_previousFrame == null)
        {
            _previousFrame = current;
            _stillFrameCount = 0;
            IsPresent = true;
            return true;
        }

        int changedPixels = 0;
        for (int i = 0; i < current.Length; i++)
        {
            if (Math.Abs(current[i] - _previousFrame[i]) > PixelChangeThreshold)
                changedPixels++;
        }

        double changeRatio = (double)changedPixels / current.Length;
        _previousFrame = current;

        if (changeRatio >= MovementThreshold)
        {
            _stillFrameCount = 0;
            IsPresent = true;
            return true;
        }
        else
        {
            _stillFrameCount++;
            if (_stillFrameCount >= StillFrameThreshold)
            {
                IsPresent = false;
            }
            return IsPresent;
        }
    }

    public void Reset()
    {
        _previousFrame = null;
        _stillFrameCount = 0;
        IsPresent = true;
    }

    private static byte[] DownsampleToGrayscale(byte[] pixelData, int srcWidth, int srcHeight,
        int stride, int dstWidth, int dstHeight)
    {
        var result = new byte[dstWidth * dstHeight];
        float xScale = (float)srcWidth / dstWidth;
        float yScale = (float)srcHeight / dstHeight;

        for (int dy = 0; dy < dstHeight; dy++)
        {
            for (int dx = 0; dx < dstWidth; dx++)
            {
                int sx = (int)(dx * xScale);
                int sy = (int)(dy * yScale);
                int offset = sy * stride + sx * 4;

                if (offset + 2 < pixelData.Length)
                {
                    byte b = pixelData[offset];
                    byte g = pixelData[offset + 1];
                    byte r = pixelData[offset + 2];
                    result[dy * dstWidth + dx] = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                }
            }
        }

        return result;
    }
}
