namespace ChillPlatePicoGK;

public sealed class DesignInputs
{
    public double PlateLength { get; set; } = 160; // mm
    public double PlateWidth { get; set; } = 100; // mm
    public double PlateThickness { get; set; } = 12; // mm
    public double ChannelWidth { get; set; } = 3; // mm
    public double ChannelHeight { get; set; } = 2.5; // mm
    public double ChannelSpacing { get; set; } = 2; // mm wall between channels
    public int NumberOfChannels { get; set; } = 12;
    public double InletDiameter { get; set; } = 10; // mm
    public double FlowRate { get; set; } = 6; // L/min
    public string CoolantType { get; set; } = "water";
    public double HeatLoad { get; set; } = 1200; // W
    public string Material { get; set; } = "Aluminum";
    public double AmbientTemperature { get; set; } = 25; // C

    public void Validate()
    {
        if (PlateLength <= 0 || PlateWidth <= 0 || PlateThickness <= 0) throw new ArgumentException("Plate dimensions must be positive.");
        if (ChannelWidth <= 0 || ChannelHeight <= 0 || ChannelSpacing <= 0) throw new ArgumentException("Channel geometry must be positive.");
        if (NumberOfChannels < 1) throw new ArgumentException("NumberOfChannels must be >= 1.");
        if (InletDiameter <= 0 || FlowRate <= 0 || HeatLoad <= 0) throw new ArgumentException("InletDiameter, FlowRate, and HeatLoad must be positive.");
        var requiredWidth = NumberOfChannels * ChannelWidth + (NumberOfChannels + 1) * ChannelSpacing;
        if (requiredWidth > PlateWidth) throw new ArgumentException("Channels do not fit in plate width. Reduce channel count/width or spacing, or increase plate width.");
        if (ChannelHeight >= PlateThickness) throw new ArgumentException("ChannelHeight must be less than PlateThickness.");
    }
}
