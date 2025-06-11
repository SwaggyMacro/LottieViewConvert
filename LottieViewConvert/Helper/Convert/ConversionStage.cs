namespace LottieViewConvert.Helper.Convert
{
    /// <summary>
    /// Conversion stages for Lottie animations.
    /// </summary>
    public enum ConversionStage
    {

        Initializing,
        PngExport,
        Converting,
        Cleanup,
        Completed
    }
}