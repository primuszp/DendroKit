# 🌳 DendroKit

**Procedural tree generator for Windows** — a faithful C# port of the classic
[Arbaro 1.9.9](http://arbaro.sourceforge.net/) Java application, built on the
Weber & Penn algorithm published in *SIGGRAPH '95*.

![DendroKit screenshot placeholder](docs/screenshot.png)

---

## Table of Contents

- [Features](#features)
- [Algorithm](#algorithm)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Building](#building)
- [Running](#running)
- [Parameter Reference](#parameter-reference)
- [XML File Format](#xml-file-format)
- [Preset Trees](#preset-trees)
- [OBJ Export](#obj-export)
- [Running Tests](#running-tests)
- [Project Roadmap](#project-roadmap)
- [References](#references)
- [License](#license)

---

## Features

| Feature | Details |
|---------|---------|
| Real-time 3D preview | OpenGL fixed-function pipeline, warm ivory theme |
| Hilton 7-parameter camera | Orbit · Pan · Zoom-to-cursor · Perspective / Orthographic |
| Live parameter editing | Debounced 450 ms auto-regeneration with drag-hold mode |
| 80+ parameters | Fully grouped, level-filtered (trunk → branch → twig → detail) |
| Leaf shapes | 13 built-in presets + custom string IDs, slider selector |
| Pruning envelope | Weber & Penn shape-8 envelope with power-law curves |
| OBJ export | Wavefront `.obj` with per-level material groups |
| XML parameter files | Full Arbaro-compatible `.xml` format (load / save / save-as) |
| 16 preset species | Bundled in `arbaro_1_9_9/trees/` |
| xUnit regression suite | Random state contract, param validation, mesh vertex count |

---

## Algorithm

DendroKit implements the **Weber & Penn recursive botanical model** (1995).
A tree is described as a hierarchy of *stems*, each parameterised per level:

```
Tree
└─ Trunk (level 0)
   ├─ Segments  →  taper + curvature
   ├─ Sub-stems (level 1)  →  length × GetShapeRatio(height)
   │   ├─ Sub-sub-stems (level 2)
   │   │   └─ Leaf-stems (level 3)  →  leaves
   │   └─ …
   └─ Base-splits  →  forked trunks
```

Key generation steps:
1. **`TreeParams.Prepare(seed)`** — resolves all parameter fields from `ParamDb`, initialises deterministic `TreeRandom` sequences per level
2. **`StemImpl.Make()`** — recursive stem construction: length, radius, segments, splits, sub-stems, leaves
3. **`MeshGenerator.CreateStemMesh()`** — converts the stem tree into indexed triangle strips
4. **`ObjExporter.Write()`** — serialises mesh + leaf quads to Wavefront OBJ

> Paper: J. Weber & J. Penn, *"Creation and Rendering of Realistic Trees"*,
> SIGGRAPH 1995 — [PDF (Duke)](https://www2.cs.duke.edu/courses/cps124/spring08/assign/07_papers/p119-weber.pdf)

---

## Architecture

```
DendroKit/
├─ source/
│   ├─ DendroKit.Core/          # Pure .NET 8 class library — no UI dependency
│   │   ├─ Params/              # AbstractParam, TypedParams, TreeParams, LevelParams
│   │   ├─ Tree/                # TreeImpl, StemImpl, SegmentImpl, LeafImpl
│   │   ├─ Mesh/                # MeshGenerator, TreeMesh, MeshSection
│   │   ├─ Export/              # ObjExporter
│   │   └─ Transformation/      # Vector3d, Matrix3, Transformation
│   │
│   ├─ DendroKit.WpfApp/        # WPF desktop application (primary UI)
│   │   ├─ MainWindow.xaml/.cs  # Main window, toolbar, param panel, GL host
│   │   ├─ ParamViewModel.cs    # MVVM wrapper — slider, textbox, leaf-shape selector
│   │   ├─ TreeGLControl.cs     # WinForms UserControl hosting OpenTK GLControl
│   │   └─ Themes/
│   │       └─ DendroTheme.xaml # Full warm bark/parchment colour theme
│   │
│   ├─ DendroKit.View/          # WinForms desktop application (lightweight)
│   │   └─ TreeGLControl.cs     # Shared GL control (same camera model)
│   │
│   ├─ DendroKit.Tests/         # xUnit test project
│   │   └─ RegressionTests.cs
│   │
│   └─ DendroKit.sln
│
├─ arbaro_1_9_9/                # Original Arbaro reference (Java source + presets)
│   ├─ src/                     # Java source (reference only)
│   └─ trees/                   # 16 species XML files
│
└─ docs/
    └─ p119-weber.pdf           # Weber & Penn SIGGRAPH 1995 paper
```

### Dependency graph

```
DendroKit.Tests
    ├─▶ DendroKit.Core
    └─▶ DendroKit.WpfApp
            └─▶ DendroKit.Core
DendroKit.View
    └─▶ DendroKit.Core
```

---

## Prerequisites

| Requirement | Minimum |
|-------------|---------|
| OS | Windows 10 / 11 (x64) |
| .NET SDK | [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8) |
| GPU | OpenGL 2.1 compatible (any modern integrated or discrete GPU) |
| RAM | 512 MB |

> **Note:** Both the WPF and WinForms frontends are Windows-only.
> `DendroKit.Core` itself is platform-agnostic (`net8.0`).

---

## Building

```bash
# Clone
git clone https://github.com/<your-username>/DendroKit.git
cd DendroKit

# Restore & build everything
dotnet build source/DendroKit.sln -c Release
```

Or open `source/DendroKit.sln` in **Visual Studio 2022** (v17.8+) and press **F6**.

---

## Running

### WPF application (recommended)

```bash
dotnet run --project source/DendroKit.WpfApp -c Release
```

Or in Visual Studio: set **DendroKit.WpfApp** as startup project → **F5**.

### WinForms application

```bash
dotnet run --project source/DendroKit.View -c Release
```

---

## Parameter Reference

Parameters are organised into eight groups:

| Group | Parameters | Description |
|-------|-----------|-------------|
| **SHAPE** | `Shape`, `Levels`, `Scale`, `BaseSize`, `RatioPower`, `AttractionUp`, … | Overall tree profile and scale |
| **TRUNK** | `Ratio`, `Flare`, `Lobes`, `LobeDepth`, `0Scale`, `0ScaleV` | Trunk-specific geometry |
| **LENTAPER** | `nLength`, `nLengthV`, `nTaper` × 4 levels | Branch length and taper per level |
| **SPLITTING** | `nSegSplits`, `nSplitAngle`, `nSplitAngleV` × 4 levels | Forking behaviour |
| **CURVATURE** | `nCurveRes`, `nCurve`, `nCurveV`, `nCurveBack` × 4 levels | Stem curvature |
| **BRANCHING** | `nBranches`, `nDownAngle`, `nRotate`, `nBranchDist` × 4 levels | Sub-stem placement |
| **LEAVES** | `Leaves`, `LeafShape`, `LeafScale`, `LeafScaleX`, `LeafBend`, `LeafStemLen` | Leaf geometry |
| **PRUNING** | `PruneRatio`, `PruneWidth`, `PruneWidthPeak`, `PrunePowerLow/High` | Envelope pruning |
| **QUALITY** | `Smooth`, `LeafQuality` | Mesh detail level |

> **Level-specific groups** (LENTAPER, SPLITTING, CURVATURE, BRANCHING) have
> a level selector strip: **0 = törzs / trunk**, **1 = ág / branch**,
> **2 = hajtás / twig**, **3 = részlet / detail**.

### Selected parameters explained

| Parameter | Range | Effect |
|-----------|-------|--------|
| `Shape` | 0–8 | Crown profile: 0=Conical, 1=Spherical, 2=Hemispherical, 3=Cylindrical, 4=TaperedCylindrical, 5=Flame, 6=InverseConical, 7=TendFlame, 8=Envelope |
| `Levels` | 1–4+ | Recursion depth — each level adds a branch generation |
| `Scale` | > 0 | Tree height in metres |
| `BaseSize` | 0–1 | Fraction of trunk height without branches |
| `AttractionUp` | −∞..+∞ | Positive = branches curve upward; negative = droop |
| `nCurveBack` | any | Non-zero enables S-curve (separate upper/lower arc) |
| `PruneRatio` | 0–1 | 0 = no pruning; 1 = full envelope enforced |

---

## XML File Format

DendroKit uses the same XML format as Arbaro:

```xml
<?xml version='1.0' ?>
<arbaro>
  <species name='my_tree'>
    <param name='Shape'  value='1'/>
    <param name='Levels' value='3'/>
    <param name='Scale'  value='12'/>
    <!-- level 0 (trunk) -->
    <param name='0Length'     value='1'/>
    <param name='0CurveRes'   value='6'/>
    <!-- level 1 (branches) -->
    <param name='1Branches'   value='20'/>
    <param name='1Length'     value='0.6'/>
    <!-- ... -->
  </species>
</arbaro>
```

Level-specific parameter names follow the pattern `<level><ParameterName>`,
e.g. `0CurveRes`, `1DownAngle`, `2Branches`.

---

## Preset Trees

Sixteen species parameter files are included in `arbaro_1_9_9/trees/`:

| File | Species |
|------|---------|
| `black_tupelo.xml` | Black Tupelo |
| `ca_black_oak.xml` | California Black Oak |
| `eastern_cottonwood.xml` | Eastern Cottonwood |
| `european_larch.xml` | European Larch |
| `lombardy_poplar.xml` | Lombardy Poplar |
| `quaking_aspen.xml` | Quaking Aspen |
| `sassafras.xml` | Sassafras |
| `tamarack.xml` | Tamarack |
| `weeping_willow.xml` | Weeping Willow |
| `desert_bush.xml` | Desert Bush |
| `fanpalm.xml` | Fan Palm |
| `palm.xml` | Palm |
| `barley.xml` | Barley |
| `rush.xml` | Rush |
| `shave-grass.xml` | Shave Grass |
| `wheat.xml` | Wheat |

Open via **Megnyitás** (Ctrl+O) or from `source/DendroKit.View/Trees/`.

---

## OBJ Export

Click **OBJ export** in the toolbar. The exported `.obj` contains:

- One geometry group per stem level (`stem_level_0`, `stem_level_1`, …)
- One geometry group for leaves (`leaves`)
- Vertex normals for smooth shading
- A stub `.mtl` file with material placeholders

Import into Blender, Maya, 3ds Max, or any DCC tool.

---

## Running Tests

```bash
dotnet test source/DendroKit.Tests -c Release --logger "console;verbosity=normal"
```

Test coverage includes:

- `TreeRandom` get/set state contract (Arbaro compatibility)
- `TreeParams` out-of-range value rejection
- Mesh vertex count regression for known parameter sets
- Parameter copy constructor correctness

---

## Project Roadmap

- [ ] Normal-map baking for bark texture
- [ ] Blender add-on / Python bridge via named pipe
- [ ] Wind animation keyframe export
- [ ] Cross-platform headless mode (`DendroKit.Core` only, no GPU)
- [ ] More leaf shape primitives (needle, compound)
- [ ] GLTF 2.0 export

---

## References

| Resource | Link |
|----------|------|
| Weber & Penn paper (SIGGRAPH 1995) | [p119-weber.pdf (Duke)](https://www2.cs.duke.edu/courses/cps124/spring08/assign/07_papers/p119-weber.pdf) |
| Arbaro (original Java) | [arbaro.sourceforge.net](http://arbaro.sourceforge.net/) |
| OpenTK | [opentk.net](https://opentk.net/) |
| .NET 8 | [dot.net](https://dotnet.microsoft.com/download/dotnet/8) |
| Weber & Penn video walkthrough | [YouTube](https://www.youtube.com/watch?v=8XPogyaQ-gM) |

---

## License

This project is released under the **GNU General Public License v2.0**,
consistent with Arbaro's original GPL-2 licence.

```
DendroKit — C# port of Arbaro 1.9.9
Copyright (C) 2024  <your name>

This program is free software; you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation; either version 2 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
```

See [LICENSE](LICENSE) for the full text.

---

*Built with ❤️ and a lot of recursive geometry.*
