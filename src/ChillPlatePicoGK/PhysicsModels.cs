namespace ChillPlatePicoGK;

public readonly record struct CoolantProperties(double Density, double Viscosity, double ThermalConductivity, double Cp);

public readonly record struct SimulationPoint(double FlowRateLpm, double Reynolds, double Nusselt, double H, double PressureDropPa, double ThermalResistanceKPerW, double MaxSurfaceTemperatureC);

public static class PhysicsModels
{
    public static CoolantProperties GetCoolant(string coolantType)
    {
        return coolantType.ToLowerInvariant() switch
        {
            "water" => new CoolantProperties(997, 0.00089, 0.6, 4180),
            "glycol mix" => new CoolantProperties(1040, 0.0035, 0.38, 3500),
            _ => throw new ArgumentException("CoolantType must be 'water' or 'glycol mix'.")
        };
    }

    public static double GetMaterialConductivity(string material) => material.ToLowerInvariant() switch
    {
        "aluminum" => 167,
        "copper" => 385,
        _ => throw new ArgumentException("Material must be Aluminum or Copper.")
    };

    public static SimulationPoint Evaluate(DesignInputs input, double flowRateLpm)
    {
        var coolant = GetCoolant(input.CoolantType);
        var kPlate = GetMaterialConductivity(input.Material);

        var w = input.ChannelWidth / 1000.0;
        var h = input.ChannelHeight / 1000.0;
        var area = w * h;
        var perimeter = 2 * (w + h);
        var dh = 4 * area / perimeter;
        var L = input.PlateLength / 1000.0;
        var mdot = coolant.Density * (flowRateLpm / 1000.0 / 60.0);
        var mdotPerChannel = mdot / input.NumberOfChannels;
        var velocity = mdotPerChannel / (coolant.Density * area);

        // Reynolds number: Re = rho * V * Dh / mu
        var re = coolant.Density * velocity * dh / coolant.Viscosity;
        var pr = coolant.Cp * coolant.Viscosity / coolant.ThermalConductivity;

        // Dittus-Boelter for turbulent internal flow: Nu = 0.023 * Re^0.8 * Pr^0.4
        // For laminar fallback we use fully developed constant heat flux approximation Nu=4.36
        var nu = re >= 3000 ? 0.023 * Math.Pow(re, 0.8) * Math.Pow(pr, 0.4) : 4.36;

        // Convective coefficient: h = Nu * k / Dh
        var htc = nu * coolant.ThermalConductivity / dh;

        // Darcy friction factor
        var f = re < 2300 ? 64 / Math.Max(re, 1) : 0.3164 * Math.Pow(re, -0.25);

        // Darcy-Weisbach: dp = f * (L/Dh) * (rho*V^2/2)
        var dp = f * (L / dh) * (coolant.Density * velocity * velocity / 2);

        var convArea = input.NumberOfChannels * perimeter * L;
        var rConv = 1.0 / Math.Max(htc * convArea, 1e-12);
        var baseThickness = (input.PlateThickness - input.ChannelHeight) / 1000.0;
        var conductionArea = (input.PlateLength / 1000.0) * (input.PlateWidth / 1000.0);
        var rCond = baseThickness / Math.Max(kPlate * conductionArea, 1e-12);
        var rTh = rConv + rCond;

        var maxTemp = input.AmbientTemperature + input.HeatLoad * rTh;
        return new SimulationPoint(flowRateLpm, re, nu, htc, dp, rTh, maxTemp);
    }
}
