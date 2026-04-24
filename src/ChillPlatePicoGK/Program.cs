using System.Globalization;
using System.Text;

namespace ChillPlatePicoGK;

public enum Topology
{
    Serpentine,
    Parallel
}

public record OperatingPoint(
    double HeatLoadW = 1800,
    double InletTempC = 28,
    double FlowLpm = 8,
    double MaxComponentTempC = 85,
    double MaxPressureDropPa = 60000);

public record Coolant(
    string Name = "30% Ethylene-Glycol / Water",
    double Density = 1035,
    double DynamicViscosity = 0.0022,
    double Cp = 3700,
    double ThermalConductivity = 0.43);

public record PlateMaterial(string Name = "Al 6061-T6", double ThermalConductivity = 167);

public record Manufacturing(
    double EnvelopeXmm = 220,
    double EnvelopeYmm = 160,
    double PlateThicknessMm = 10,
    double BaseThicknessMm = 8,
    double MinWallMm = 1.2,
    double MinChannelWidthMm = 1.8,
    double MinChannelHeightMm = 1.8,
    double MinToolDiameterMm = 1.5,
    bool CncMode = true);

public record DesignParams(
    double ChannelWidthMm,
    double ChannelHeightMm,
    double WallMm,
    int ChannelCount,
    int Passes,
    Topology Topology,
    double HeaderLengthMm = 12);

public record GeometryModel(
    List<(double Xmm, double Ymm)> Centerline,
    double FlowPathLengthM,
    double HydraulicDiameterM,
    double WettedPerimeterM,
    double FlowAreaM2,
    int ActiveChannels,
    double WettedAreaM2);

public record SimulationResult(
    DesignParams Design,
    double MaxWallTempC,
    double OutletTempC,
    double PressureDropPa,
    double PumpPowerW,
    double Reynolds,
    double Nusselt,
    double H,
    double MaterialVolumeCm3,
    bool Feasible,
    double Objective,
    List<double> Xmm,
    List<double> WallTempC,
    List<double> FluidTempC);

public class PicoGKWorkflow
{
    private readonly OperatingPoint _op;
    private readonly Coolant _coolant;
    private readonly PlateMaterial _material;
    private readonly Manufacturing _mfg;

    public PicoGKWorkflow(OperatingPoint op, Coolant coolant, PlateMaterial material, Manufacturing mfg)
    {
        _op = op;
        _coolant = coolant;
        _material = material;
        _mfg = mfg;
    }

    public bool IsManufacturable(DesignParams p)
    {
        var minWidth = Math.Max(_mfg.MinChannelWidthMm, _mfg.MinToolDiameterMm);
        if (p.ChannelWidthMm < minWidth) return false;
        if (p.ChannelHeightMm < _mfg.MinChannelHeightMm) return false;
        if (p.WallMm < _mfg.MinWallMm) return false;
        if (p.ChannelCount < 1 || p.Passes < 1) return false;

        var pitch = p.ChannelWidthMm + p.WallMm;
        var usedY = p.ChannelCount * pitch;
        if (usedY > _mfg.EnvelopeYmm - 2 * p.WallMm) return false;
        return true;
    }

    public GeometryModel GenerateGeometry(DesignParams p)
    {
        if (!IsManufacturable(p))
            throw new InvalidOperationException("Design violates manufacturing constraints.");

        var centerline = new List<(double Xmm, double Ymm)>();
        var xMin = p.WallMm + p.ChannelWidthMm / 2.0;
        var xMax = _mfg.EnvelopeXmm - p.WallMm - p.ChannelWidthMm / 2.0;
        var yBase = p.WallMm + p.ChannelWidthMm / 2.0;
        var pitch = p.ChannelWidthMm + p.WallMm;

        if (p.Topology == Topology.Serpentine)
        {
            var passes = Math.Min(p.Passes, p.ChannelCount);
            for (var i = 0; i < passes; i++)
            {
                var y = yBase + i * pitch;
                if (i % 2 == 0)
                {
                    centerline.Add((xMin, y));
                    centerline.Add((xMax, y));
                }
                else
                {
                    centerline.Add((xMax, y));
                    centerline.Add((xMin, y));
                }
            }
        }
        else
        {
            var y = yBase;
            centerline.Add((xMin, y));
            centerline.Add((xMax, y));
        }

        double lineLengthMm = 0;
        for (var i = 1; i < centerline.Count; i++)
        {
            var dx = centerline[i].Xmm - centerline[i - 1].Xmm;
            var dy = centerline[i].Ymm - centerline[i - 1].Ymm;
            lineLengthMm += Math.Sqrt(dx * dx + dy * dy);
        }

        if (p.Topology == Topology.Parallel)
        {
            lineLengthMm = _mfg.EnvelopeXmm - 2 * p.WallMm;
        }

        var w = p.ChannelWidthMm / 1000.0;
        var h = p.ChannelHeightMm / 1000.0;
        var a = w * h;
        var wp = 2 * (w + h);
        var dh = 4 * a / wp;
        var activeChannels = p.Topology == Topology.Parallel ? p.ChannelCount : 1;
        var lengthM = lineLengthMm / 1000.0;

        return new GeometryModel(
            centerline,
            lengthM,
            dh,
            wp,
            a,
            activeChannels,
            wp * lengthM * activeChannels);
    }

    public SimulationResult Simulate(DesignParams p, int cells = 120)
    {
        var g = GenerateGeometry(p);

        // Continuity: m_dot = rho * V_dot
        var mDotTotal = _coolant.Density * (_op.FlowLpm / 1000.0 / 60.0);
        var mDotChannel = mDotTotal / g.ActiveChannels;

        // Re = rho*V*Dh/mu
        var velocity = mDotChannel / (_coolant.Density * g.FlowAreaM2);
        var re = _coolant.Density * velocity * g.HydraulicDiameterM / _coolant.DynamicViscosity;
        var pr = _coolant.Cp * _coolant.DynamicViscosity / _coolant.ThermalConductivity;

        double nu;
        double f;
        if (re < 2300)
        {
            nu = 3.66;
            f = 64 / Math.Max(re, 1);
        }
        else
        {
            f = 0.3164 / Math.Pow(re, 0.25);
            var denom = 1 + 12.7 * Math.Sqrt(f / 8) * (Math.Pow(pr, 2.0 / 3.0) - 1);
            nu = ((f / 8) * (re - 1000) * pr) / denom;
        }

        // Convection: h = Nu*k/Dh, q = h*A*(Tw-Tf)
        var htc = nu * _coolant.ThermalConductivity / g.HydraulicDiameterM;

        // Darcy-Weisbach + minor losses
        var dpMajor = f * (g.FlowPathLengthM / g.HydraulicDiameterM) * (_coolant.Density * velocity * velocity / 2);
        var kMinor = p.Topology == Topology.Serpentine ? 1.2 * Math.Max(p.Passes - 1, 0) : 2.4;
        var dpMinor = kMinor * (_coolant.Density * velocity * velocity / 2);
        var dp = dpMajor + dpMinor;

        // FDM/FVM-like axial march + conduction through base (Fourier)
        var dx = g.FlowPathLengthM / cells;
        var areaCell = g.WettedPerimeterM * dx;
        var qCell = _op.HeatLoadW / cells / g.ActiveChannels;
        var tCond = (_mfg.BaseThicknessMm / 1000.0) * 0.5;
        var rCond = tCond / (_material.ThermalConductivity * areaCell);
        var rConv = 1.0 / (htc * areaCell);

        var x = new List<double>(cells);
        var tw = new List<double>(cells);
        var tf = new List<double>(cells);

        var fluidC = _op.InletTempC;
        for (var i = 0; i < cells; i++)
        {
            var dTf = qCell / (mDotChannel * _coolant.Cp);
            var nextFluid = fluidC + dTf;
            var wallHot = fluidC + qCell * (rCond + rConv);

            x.Add((i + 1) * dx * 1000.0);
            tw.Add(wallHot);
            tf.Add(nextFluid);
            fluidC = nextFluid;
        }

        var tOut = _op.InletTempC + _op.HeatLoadW / (mDotTotal * _coolant.Cp);
        var maxTw = tw.Max();
        var pumpPower = dp * (_op.FlowLpm / 1000.0 / 60.0);

        var blockVolumeM3 = (_mfg.EnvelopeXmm / 1000.0) * (_mfg.EnvelopeYmm / 1000.0) * (_mfg.PlateThicknessMm / 1000.0);
        var channelVolumeM3 = g.FlowAreaM2 * g.FlowPathLengthM * g.ActiveChannels;
        var materialCm3 = Math.Max(blockVolumeM3 - channelVolumeM3, 0) * 1e6;

        var feasible = maxTw <= _op.MaxComponentTempC && dp <= _op.MaxPressureDropPa;
        const double w1 = 0.65, w2 = 0.25, w3 = 0.10;
        var objective = w1 * (maxTw / _op.MaxComponentTempC)
                        + w2 * (dp / _op.MaxPressureDropPa)
                        + w3 * (materialCm3 / 1000.0);

        return new SimulationResult(p, maxTw, tOut, dp, pumpPower, re, nu, htc, materialCm3, feasible, objective, x, tw, tf);
    }

    public SimulationResult Optimize()
    {
        var widths = new[] { 2.0, 2.5, 3.0, 3.5 };
        var heights = new[] { 2.0, 2.5, 3.0 };
        var walls = new[] { 1.2, 1.5, 2.0 };
        var channels = new[] { 6, 8, 10, 12 };
        var passes = new[] { 4, 6, 8, 10 };

        SimulationResult? best = null;
        foreach (var topo in Enum.GetValues<Topology>())
        foreach (var w in widths)
        foreach (var h in heights)
        foreach (var wall in walls)
        foreach (var n in channels)
        foreach (var pass in passes)
        {
            var p = new DesignParams(w, h, wall, n, pass, topo);
            if (!IsManufacturable(p)) continue;
            var r = Simulate(p);
            if (best is null)
            {
                best = r;
                continue;
            }

            var pick = r.Feasible && !best.Feasible
                || r.Feasible == best.Feasible && r.Objective < best.Objective;
            if (pick) best = r;
        }

        return best ?? throw new InvalidOperationException("No valid designs in search range.");
    }

    public void WriteTopViewSvg(DesignParams p, string path)
    {
        var geom = GenerateGeometry(p);
        var width = _mfg.EnvelopeXmm;
        var height = _mfg.EnvelopeYmm;
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}' width='1000'>");
        sb.AppendLine($"<rect x='0' y='0' width='{width}' height='{height}' fill='#1f2937' stroke='#9ca3af' stroke-width='0.8'/>");

        if (p.Topology == Topology.Parallel)
        {
            var pitch = p.ChannelWidthMm + p.WallMm;
            for (var i = 0; i < p.ChannelCount; i++)
            {
                var y = p.WallMm + i * pitch;
                sb.AppendLine($"<rect x='{p.WallMm}' y='{y}' width='{_mfg.EnvelopeXmm - 2 * p.WallMm}' height='{p.ChannelWidthMm}' fill='#38bdf8'/>");
            }
        }
        else
        {
            for (var i = 1; i < geom.Centerline.Count; i++)
            {
                var p0 = geom.Centerline[i - 1];
                var p1 = geom.Centerline[i];
                sb.AppendLine($"<line x1='{p0.Xmm}' y1='{p0.Ymm}' x2='{p1.Xmm}' y2='{p1.Ymm}' stroke='#38bdf8' stroke-width='{p.ChannelWidthMm}' stroke-linecap='round'/>");
            }
        }

        sb.AppendLine("<text x='8' y='14' fill='white' font-size='8'>PicoGK Draft Chill Plate Top View</text>");
        sb.AppendLine("</svg>");
        File.WriteAllText(path, sb.ToString());
    }

    public void WriteTemperatureCsv(SimulationResult result, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("x_mm,wall_temp_c,fluid_temp_c");
        for (var i = 0; i < result.Xmm.Count; i++)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{result.Xmm[i]:F3},{result.WallTempC[i]:F4},{result.FluidTempC[i]:F4}"));
        }
        File.WriteAllText(path, sb.ToString());
    }

    public void WriteCadOpenScad(DesignParams p, string path)
    {
        var g = GenerateGeometry(p);
        var channelDepth = Math.Min(p.ChannelHeightMm, _mfg.PlateThicknessMm - 0.5);
        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated CAD exchange script.");
        sb.AppendLine("// Open in OpenSCAD, then render/export STL/STEP (via FreeCAD bridge).");
        sb.AppendLine("$fn=48;");
        sb.AppendLine("difference() {");
        sb.AppendLine(
            $"  cube([{_mfg.EnvelopeXmm.ToString(CultureInfo.InvariantCulture)}, {_mfg.EnvelopeYmm.ToString(CultureInfo.InvariantCulture)}, {_mfg.PlateThicknessMm.ToString(CultureInfo.InvariantCulture)}], center=false);");

        if (p.Topology == Topology.Parallel)
        {
            var pitch = p.ChannelWidthMm + p.WallMm;
            for (var i = 0; i < p.ChannelCount; i++)
            {
                var y = p.WallMm + i * pitch;
                sb.AppendLine(
                    $"  translate([{p.WallMm.ToString(CultureInfo.InvariantCulture)}, {y.ToString(CultureInfo.InvariantCulture)}, {_mfg.PlateThicknessMm - channelDepth:F4}]) cube([{(_mfg.EnvelopeXmm - 2 * p.WallMm).ToString(CultureInfo.InvariantCulture)}, {p.ChannelWidthMm.ToString(CultureInfo.InvariantCulture)}, {channelDepth.ToString(CultureInfo.InvariantCulture)}], center=false);");
            }
        }
        else
        {
            for (var i = 1; i < g.Centerline.Count; i++)
            {
                var p0 = g.Centerline[i - 1];
                var p1 = g.Centerline[i];
                sb.AppendLine("  hull() {");
                sb.AppendLine(
                    $"    translate([{p0.Xmm.ToString(CultureInfo.InvariantCulture)}, {p0.Ymm.ToString(CultureInfo.InvariantCulture)}, {_mfg.PlateThicknessMm - channelDepth:F4}]) cylinder(h={channelDepth.ToString(CultureInfo.InvariantCulture)}, d={p.ChannelWidthMm.ToString(CultureInfo.InvariantCulture)});");
                sb.AppendLine(
                    $"    translate([{p1.Xmm.ToString(CultureInfo.InvariantCulture)}, {p1.Ymm.ToString(CultureInfo.InvariantCulture)}, {_mfg.PlateThicknessMm - channelDepth:F4}]) cylinder(h={channelDepth.ToString(CultureInfo.InvariantCulture)}, d={p.ChannelWidthMm.ToString(CultureInfo.InvariantCulture)});");
                sb.AppendLine("  }");
            }
        }

        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString());
    }
}

public static class Program
{
    public static void Main()
    {
        var workflow = new PicoGKWorkflow(
            new OperatingPoint(),
            new Coolant(),
            new PlateMaterial(),
            new Manufacturing());

        var baseline = new DesignParams(
            ChannelWidthMm: 2.5,
            ChannelHeightMm: 2.5,
            WallMm: 1.5,
            ChannelCount: 8,
            Passes: 8,
            Topology: Topology.Parallel);

        var baselineResult = workflow.Simulate(baseline);
        var optimized = workflow.Optimize();

        Directory.CreateDirectory("artifacts");
        workflow.WriteTopViewSvg(optimized.Design, "artifacts/chillplate_topview.svg");
        workflow.WriteTemperatureCsv(optimized, "artifacts/temperature_profile.csv");
        workflow.WriteCadOpenScad(optimized.Design, "artifacts/chillplate_model.scad");

        Console.WriteLine("=== PicoGK Workflow: Draft Model + Visualization ===");
        Console.WriteLine($"Baseline: T_max={baselineResult.MaxWallTempC:F2} C, dP={baselineResult.PressureDropPa / 1000:F2} kPa");
        Console.WriteLine($"Optimized: {optimized.Design}");
        Console.WriteLine($"T_max={optimized.MaxWallTempC:F2} C, T_out={optimized.OutletTempC:F2} C, dP={optimized.PressureDropPa / 1000:F2} kPa, Pump={optimized.PumpPowerW:F2} W");
        Console.WriteLine("Visualization outputs:");
        Console.WriteLine(" - artifacts/chillplate_topview.svg");
        Console.WriteLine(" - artifacts/temperature_profile.csv");
        Console.WriteLine("CAD exchange output:");
        Console.WriteLine(" - artifacts/chillplate_model.scad");
    }
}
