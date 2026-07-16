using LumenPomodoro.Services;

namespace LumenPomodoro.Tests.Services;

public class PresenceDetectorTests
{
    // Frame: 16x16 pixels, stride=64 bytes/row => 1024 bytes minimum
    private const int FrameSize = 1024;
    private const int FrameWidth = 16;
    private const int FrameHeight = 16;
    private const int FrameStride = 64;
    private const byte BaseValue = 128;

    private static byte[] CreateUniformFrame(byte value) =>
        Enumerable.Repeat(value, FrameSize).ToArray();

    [Fact]
    public void ProcessFrame_FirstFrame_ReturnsPresent()
    {
        var detector = new PresenceDetector();
        var frame = CreateUniformFrame(BaseValue);

        var result = detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);

        Assert.True(result);
        Assert.True(detector.IsPresent);
    }

    [Fact]
    public void ProcessFrame_SameFrameRepeated_BecomesNotPresentAfterThreshold()
    {
        var detector = new PresenceDetector { StillFrameThreshold = 3 };
        var frame = CreateUniformFrame(BaseValue);

        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        var result = detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);

        Assert.False(result);
        Assert.False(detector.IsPresent);
        Assert.True(detector.StillFrameCount >= detector.StillFrameThreshold);
    }

    [Fact]
    public void ProcessFrame_DifferentFrame_ResetsStillCount()
    {
        var detector = new PresenceDetector { StillFrameThreshold = 5 };
        var frame1 = CreateUniformFrame(BaseValue);
        var frame2 = CreateUniformFrame(200);

        detector.ProcessFrame(frame1, FrameWidth, FrameHeight, FrameStride);
        detector.ProcessFrame(frame1, FrameWidth, FrameHeight, FrameStride);
        var result = detector.ProcessFrame(frame2, FrameWidth, FrameHeight, FrameStride);

        Assert.True(result);
        Assert.True(detector.IsPresent);
        Assert.Equal(0, detector.StillFrameCount);
    }

    [Fact]
    public void ProcessFrame_SignificantChange_DetectedAsPresent()
    {
        var detector = new PresenceDetector { StillFrameThreshold = 3 };
        var frame1 = CreateUniformFrame(50);
        // Change first 3 rows (48 pixels = 18.75% > 15% threshold)
        var frame2 = new byte[FrameSize];
        for (int i = 0; i < FrameSize; i++)
        {
            frame2[i] = i < FrameStride * 3 ? (byte)200 : (byte)50;
        }

        detector.ProcessFrame(frame1, FrameWidth, FrameHeight, FrameStride);
        var result = detector.ProcessFrame(frame2, FrameWidth, FrameHeight, FrameStride);

        Assert.True(result);
        Assert.True(detector.IsPresent);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var detector = new PresenceDetector { StillFrameThreshold = 2 };
        var frame = CreateUniformFrame(BaseValue);

        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        Assert.False(detector.IsPresent);

        detector.Reset();

        Assert.True(detector.IsPresent);
        Assert.Equal(0, detector.StillFrameCount);
    }

    [Fact]
    public void ProcessFrame_MovementRecoversFromAbsence()
    {
        var detector = new PresenceDetector { StillFrameThreshold = 2 };
        var stillFrame = CreateUniformFrame(BaseValue);
        // Change first 3 rows (48 pixels = 18.75% > 15% threshold) to trigger movement
        var movingFrame = new byte[FrameSize];
        for (int i = 0; i < FrameSize; i++)
        {
            movingFrame[i] = i < FrameStride * 3 ? (byte)200 : (byte)BaseValue;
        }

        detector.ProcessFrame(stillFrame, FrameWidth, FrameHeight, FrameStride);
        detector.ProcessFrame(stillFrame, FrameWidth, FrameHeight, FrameStride);
        detector.ProcessFrame(stillFrame, FrameWidth, FrameHeight, FrameStride);
        Assert.False(detector.IsPresent);

        var result = detector.ProcessFrame(movingFrame, FrameWidth, FrameHeight, FrameStride);
        Assert.True(result);
        Assert.True(detector.IsPresent);
    }

    [Fact]
    public void StillFrameCount_IncrementsCorrectly()
    {
        var detector = new PresenceDetector { StillFrameThreshold = 10 };
        var frame = CreateUniformFrame(BaseValue);

        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        for (int i = 0; i < 5; i++)
            detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);

        Assert.Equal(5, detector.StillFrameCount);
        Assert.True(detector.IsPresent);
    }

    [Fact]
    public void ProcessFrame_NullPixelData_DoesNotThrow()
    {
        var detector = new PresenceDetector();

        var result = detector.ProcessFrame(null!, FrameWidth, FrameHeight, FrameStride);

        Assert.True(result);
        Assert.True(detector.IsPresent);
    }

    [Fact]
    public void ProcessFrame_EmptyPixelData_DoesNotThrow()
    {
        var detector = new PresenceDetector();

        var result = detector.ProcessFrame(Array.Empty<byte>(), FrameWidth, FrameHeight, FrameStride);

        Assert.True(result);
        Assert.True(detector.IsPresent);
    }

    [Fact]
    public void ProcessFrame_BufferTooShort_DoesNotThrow()
    {
        var detector = new PresenceDetector();
        var shortBuffer = new byte[500]; // Minimum required would be 16 * 64 = 1024

        var result = detector.ProcessFrame(shortBuffer, FrameWidth, FrameHeight, FrameStride);

        Assert.True(result);
        Assert.True(detector.IsPresent);
    }

    [Fact]
    public void ProcessFrame_StrideTooSmall_DoesNotThrow()
    {
        var detector = new PresenceDetector();
        var frame = CreateUniformFrame(100); // 1024 bytes, but stride=32 < width*4=64

        var result = detector.ProcessFrame(frame, FrameWidth, FrameHeight, stride: 32);

        Assert.True(result);
        Assert.True(detector.IsPresent);
    }

    [Fact]
    public void ProcessFrame_BufferExactlyAtBoundary_ProcessesNormally()
    {
        var detector = new PresenceDetector { StillFrameThreshold = 3 };
        // Frame: 16*64 = 1024 is the exact minimum
        var frame = CreateUniformFrame(BaseValue);

        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);
        var result = detector.ProcessFrame(frame, FrameWidth, FrameHeight, FrameStride);

        Assert.False(result);
        Assert.False(detector.IsPresent);
    }
}
