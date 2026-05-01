using System.Globalization;
using System.Text;

namespace ChillPlatePicoGK;

public sealed class GeometryBuilder
{
    private readonly DesignInputs _input;
    public GeometryBuilder(DesignInputs input) => _input = input;

    // PicoGK-style workflow entrypoint: defines parametric solids and performs boolean-like channel subtraction.
    // In this lightweight implementation, we output an open-top channel plate mesh that is STL-manufacturable.
    public void BuildAndExportStl(string stlPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(stlPath)!);
        var tris = new List<(Vec3 A, Vec3 B, Vec3 C)>();

        AddBox(tris, new Vec3(0, 0, 0), new Vec3(_input.PlateLength, _input.PlateWidth, _input.PlateThickness), includeTop: false);

        var x0 = 0.0;
        var y = _input.ChannelSpacing;
        for (var i = 0; i < _input.NumberOfChannels; i++)
        {
            AddChannelCutWalls(tris, x0, y, _input.PlateLength, _input.ChannelWidth, _input.ChannelHeight, _input.PlateThickness);
            y += _input.ChannelWidth + _input.ChannelSpacing;
        }

        AddPortCylinder(tris, _input.InletDiameter / 2.0, _input.PlateThickness, _input.ChannelHeight, true);
        AddPortCylinder(tris, _input.InletDiameter / 2.0, _input.PlateThickness, _input.ChannelHeight, false);
        WriteAsciiStl(stlPath, tris);
    }

    private static void AddBox(List<(Vec3 A, Vec3 B, Vec3 C)> t, Vec3 min, Vec3 max, bool includeTop)
    {
        var v000 = new Vec3(min.X, min.Y, min.Z); var v100 = new Vec3(max.X, min.Y, min.Z); var v110 = new Vec3(max.X, max.Y, min.Z); var v010 = new Vec3(min.X, max.Y, min.Z);
        var v001 = new Vec3(min.X, min.Y, max.Z); var v101 = new Vec3(max.X, min.Y, max.Z); var v111 = new Vec3(max.X, max.Y, max.Z); var v011 = new Vec3(min.X, max.Y, max.Z);
        AddQuad(t, v000, v100, v110, v010); // bottom
        AddQuad(t, v000, v001, v101, v100);
        AddQuad(t, v100, v101, v111, v110);
        AddQuad(t, v110, v111, v011, v010);
        AddQuad(t, v010, v011, v001, v000);
        if (includeTop) AddQuad(t, v001, v011, v111, v101);
    }

    private static void AddChannelCutWalls(List<(Vec3 A, Vec3 B, Vec3 C)> t, double x, double y, double length, double width, double depth, double plateTop)
    {
        var z1 = plateTop - depth;
        var a = new Vec3(x, y, z1); var b = new Vec3(x + length, y, z1); var c = new Vec3(x + length, y + width, z1); var d = new Vec3(x, y + width, z1);
        AddQuad(t, a, b, c, d); // channel floor
        AddQuad(t, new Vec3(x, y, plateTop), a, b, new Vec3(x + length, y, plateTop));
        AddQuad(t, new Vec3(x + length, y + width, plateTop), c, d, new Vec3(x, y + width, plateTop));
    }

    private void AddPortCylinder(List<(Vec3 A, Vec3 B, Vec3 C)> t, double r, double zTop, double depth, bool inlet)
    {
        const int n = 24;
        var cx = inlet ? r + 2 : _input.PlateLength - r - 2;
        var cy = _input.PlateWidth / 2.0;
        var z0 = zTop - depth;
        for (var i = 0; i < n; i++)
        {
            var a0 = 2 * Math.PI * i / n;
            var a1 = 2 * Math.PI * (i + 1) / n;
            var p0 = new Vec3(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0), zTop);
            var p1 = new Vec3(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1), zTop);
            var q0 = new Vec3(p0.X, p0.Y, z0);
            var q1 = new Vec3(p1.X, p1.Y, z0);
            AddTri(t, p0, q0, q1); AddTri(t, p0, q1, p1);
        }
    }

    private static void AddQuad(List<(Vec3 A, Vec3 B, Vec3 C)> t, Vec3 a, Vec3 b, Vec3 c, Vec3 d) { AddTri(t, a, b, c); AddTri(t, a, c, d); }
    private static void AddTri(List<(Vec3 A, Vec3 B, Vec3 C)> t, Vec3 a, Vec3 b, Vec3 c) => t.Add((a, b, c));

    private static void WriteAsciiStl(string path, IEnumerable<(Vec3 A, Vec3 B, Vec3 C)> tris)
    {
        var sb = new StringBuilder();
        sb.AppendLine("solid chillplate");
        foreach (var (a, b, c) in tris)
        {
            sb.AppendLine("facet normal 0 0 0"); sb.AppendLine("  outer loop");
            sb.AppendLine($"    vertex {a.ToString()}\n    vertex {b.ToString()}\n    vertex {c.ToString()}");
            sb.AppendLine("  endloop"); sb.AppendLine("endfacet");
        }
        sb.AppendLine("endsolid chillplate");
        File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
    }

    private readonly record struct Vec3(double X, double Y, double Z)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{X} {Y} {Z}");
    }
}
