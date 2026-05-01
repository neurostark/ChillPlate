using ScottPlot;

namespace ChillPlatePicoGK;

public static class PlotGenerator
{
    public static void GenerateAll(string outDir, List<SimulationPoint> flowSweep, List<(double ChannelWidthMm, SimulationPoint Point)> widthSweep)
    {
        Directory.CreateDirectory(outDir);

        SavePlot(Path.Combine(outDir, "temperature_vs_flow.png"), "Temperature vs Flow Rate", "Flow Rate (L/min)", "Max Surface Temp (°C)", flowSweep.Select(x => x.FlowRateLpm).ToArray(), flowSweep.Select(x => x.MaxSurfaceTemperatureC).ToArray());
        SavePlot(Path.Combine(outDir, "pressure_drop_vs_flow.png"), "Pressure Drop vs Flow Rate", "Flow Rate (L/min)", "Pressure Drop (Pa)", flowSweep.Select(x => x.FlowRateLpm).ToArray(), flowSweep.Select(x => x.PressureDropPa).ToArray());
        SavePlot(Path.Combine(outDir, "thermal_resistance_vs_channel_width.png"), "Thermal Resistance vs Channel Width", "Channel Width (mm)", "R_th (K/W)", widthSweep.Select(x => x.ChannelWidthMm).ToArray(), widthSweep.Select(x => x.Point.ThermalResistanceKPerW).ToArray());
        SavePlot(Path.Combine(outDir, "h_vs_reynolds.png"), "Heat Transfer Coefficient vs Reynolds", "Re", "h (W/m²-K)", flowSweep.Select(x => x.Reynolds).ToArray(), flowSweep.Select(x => x.H).ToArray());
    }

    private static void SavePlot(string path, string title, string xLabel, string yLabel, double[] xs, double[] ys)
    {
        var plot = new Plot();
        plot.Add.Scatter(xs, ys);
        plot.Title(title);
        plot.XLabel(xLabel);
        plot.YLabel(yLabel);
        plot.SavePng(path, 1000, 600);
    }
}
