namespace ChillPlatePicoGK;

public sealed class SimulationRunner
{
    public List<SimulationPoint> RunFlowSweep(DesignInputs input, double minLpm, double maxLpm, int count)
    {
        var points = new List<SimulationPoint>();
        var step = (maxLpm - minLpm) / (count - 1);
        for (var i = 0; i < count; i++)
            points.Add(PhysicsModels.Evaluate(input, minLpm + i * step));
        return points;
    }

    public List<(double ChannelWidthMm, SimulationPoint Point)> RunChannelWidthSweep(DesignInputs baseline, IEnumerable<double> widthsMm)
    {
        var result = new List<(double, SimulationPoint)>();
        foreach (var width in widthsMm)
        {
            var candidate = new DesignInputs
            {
                PlateLength = baseline.PlateLength, PlateWidth = baseline.PlateWidth, PlateThickness = baseline.PlateThickness,
                ChannelWidth = width, ChannelHeight = baseline.ChannelHeight, ChannelSpacing = baseline.ChannelSpacing,
                NumberOfChannels = baseline.NumberOfChannels, InletDiameter = baseline.InletDiameter, FlowRate = baseline.FlowRate,
                CoolantType = baseline.CoolantType, HeatLoad = baseline.HeatLoad, Material = baseline.Material, AmbientTemperature = baseline.AmbientTemperature
            };
            candidate.Validate();
            result.Add((width, PhysicsModels.Evaluate(candidate, candidate.FlowRate)));
        }
        return result;
    }

    public (DesignInputs BestDesign, SimulationPoint BestPoint) OptimizeThermalResistance(DesignInputs baseline, double maxPressureDropPa)
    {
        DesignInputs? best = null;
        SimulationPoint bestPoint = default;
        foreach (var w in Enumerable.Range(20, 21).Select(i => i / 10.0))
        {
            var candidate = new DesignInputs
            {
                PlateLength = baseline.PlateLength, PlateWidth = baseline.PlateWidth, PlateThickness = baseline.PlateThickness,
                ChannelWidth = w, ChannelHeight = baseline.ChannelHeight, ChannelSpacing = baseline.ChannelSpacing,
                NumberOfChannels = baseline.NumberOfChannels, InletDiameter = baseline.InletDiameter, FlowRate = baseline.FlowRate,
                CoolantType = baseline.CoolantType, HeatLoad = baseline.HeatLoad, Material = baseline.Material, AmbientTemperature = baseline.AmbientTemperature
            };
            try { candidate.Validate(); } catch { continue; }
            var point = PhysicsModels.Evaluate(candidate, candidate.FlowRate);
            if (point.PressureDropPa > maxPressureDropPa) continue;
            if (best is null || point.ThermalResistanceKPerW < bestPoint.ThermalResistanceKPerW) { best = candidate; bestPoint = point; }
        }
        return (best ?? baseline, best is null ? PhysicsModels.Evaluate(baseline, baseline.FlowRate) : bestPoint);
    }
}
