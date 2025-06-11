namespace LottieViewConvert.Helper.Convert;

/// <summary>
/// Options for converting Lottie animations.
/// </summary>
public class ConversionOptions
{
    public int Fps { get; set; } = 60;
    public int Width { get; set; } = 512;
    public int Height { get; set; } = 512;
    public int Quality { get; set; } = 100;
    public double PlaySpeed { get; set; } = 1.0;
}