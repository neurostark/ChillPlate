# ChillPlate PicoGK Draft Workflow (C#)

This repository now provides a **C# draft workflow** for creating a code-defined chill-plate concept, simulating thermo-fluid performance, and exporting visualization + CAD exchange artifacts.

## Goal
Create a practical loop for:
1. defining assumptions,
2. generating a parametric physical model,
3. running first-principles simulation,
4. optimizing design candidates,
5. visualizing geometry + temperature trend.

## Run

```bash
dotnet run --project src/ChillPlatePicoGK/ChillPlatePicoGK.csproj
```

> Uses `.NET 9` (`net9.0`) because PicoGP/PicoGK environments typically target modern .NET runtimes.

## Outputs

Running the app writes:

- `artifacts/chillplate_topview.svg` (geometry preview)
- `artifacts/temperature_profile.csv` (axial temperature profile)
- `artifacts/chillplate_model.scad` (direct CAD exchange model; open in OpenSCAD and export STL)

## Physics implemented

Inside `PicoGKWorkflow.Simulate()`:

- Conduction (Fourier, resistance form)
- Convection (`q = hA(T_wall - T_fluid)`)
- Energy balance (`m_dot*Cp*ΔT = Q`)
- Reynolds/Prandtl/Nusselt correlations
- Darcy-Weisbach pressure drop + minor losses

## Manufacturability constraints

- Minimum channel width/height
- Minimum wall thickness
- Minimum tool diameter (CNC mode screening)
- Envelope fit check

## Next step for physical build

`chillplate_model.scad` is generated on every run so you can visually inspect or export manufacturing geometry each time. For STEP, import the SCAD into FreeCAD and export STEP.
