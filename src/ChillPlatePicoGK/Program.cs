using System.Globalization;

namespace ChillPlatePicoGK;

public static class Program
{
    public static void Main()
    {
        var input = new DesignInputs();
        input.Validate();

        var artifactsDir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
        var plotDir = Path.Combine(artifactsDir, "plots");
        var stlPath = Path.Combine(artifactsDir, "chillplate.stl");

        var geometry = new GeometryBuilder(input);
        geometry.BuildAndExportStl(stlPath);

        var runner = new SimulationRunner();
        var nominal = PhysicsModels.Evaluate(input, input.FlowRate);
        var flowSweep = runner.RunFlowSweep(input, 2, 12, 11);
        var widthSweep = runner.RunChannelWidthSweep(input, new[] { 2.0, 2.5, 3.0, 3.5, 4.0, 4.5 });

        PlotGenerator.GenerateAll(plotDir, flowSweep, widthSweep);
        WriteCsv(Path.Combine(artifactsDir, "sweep_results.csv"), flowSweep);

        var (bestDesign, bestPoint) = runner.OptimizeThermalResistance(input, 50000);

        Console.WriteLine("=== Chill Plate Summary ===");
        Console.WriteLine($"Max temperature: {nominal.MaxSurfaceTemperatureC:F2} °C");
        Console.WriteLine($"Pressure drop: {nominal.PressureDropPa:F0} Pa");
        Console.WriteLine($"Reynolds number: {nominal.Reynolds:F0}");
        Console.WriteLine($"Thermal resistance: {nominal.ThermalResistanceKPerW:F4} K/W");
        Console.WriteLine("Recommendation: Increase channel width or flow rate to reduce temperature while checking pump limit.");
        Console.WriteLine($"Optimization (ΔP<50 kPa): best width = {bestDesign.ChannelWidth:F1} mm, R_th = {bestPoint.ThermalResistanceKPerW:F4} K/W");
        Console.WriteLine($"STL exported: {stlPath}");
        Console.WriteLine($"Plots exported: {plotDir}");
    }

    private static void WriteCsv(string path, IEnumerable<SimulationPoint> points)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var sw = new StreamWriter(path);
        sw.WriteLine("FlowRateLpm,Reynolds,Nusselt,H,PressureDropPa,ThermalResistanceKPerW,MaxSurfaceTemperatureC");
        foreach (var p in points)
        {
            sw.WriteLine(string.Join(',', p.FlowRateLpm.ToString("F3", CultureInfo.InvariantCulture), p.Reynolds.ToString("F3", CultureInfo.InvariantCulture), p.Nusselt.ToString("F3", CultureInfo.InvariantCulture), p.H.ToString("F3", CultureInfo.InvariantCulture), p.PressureDropPa.ToString("F3", CultureInfo.InvariantCulture), p.ThermalResistanceKPerW.ToString("F6", CultureInfo.InvariantCulture), p.MaxSurfaceTemperatureC.ToString("F3", CultureInfo.InvariantCulture)));
        }
    }
}
