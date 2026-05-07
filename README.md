# Pixelab v2.1

A WPF desktop application for viewing pixel art images and generating bead patterns, designed to assist in live reproduction using beads, drawings, and other artistic techniques.

## Features

### Image Viewer
- Open individual files or entire folders via the file browser
- Pan and zoom with mouse or keyboard shortcuts
- Pixel grid overlay for precise bead counting
- Fit to window and actual size (1:1) view modes

### Pattern Generation
- Matches each pixel's color to the closest available bead color
- Configurable color matching algorithm: **RGB** (fast) or **Lab** (perceptually accurate)
- Color compression levels to reduce palette variety by favoring already-used colors
- Toggle generated image overlay on top of the original
- Click any bead color in the palette to highlight its pixels in the viewport

### Color Manager
- Enable or disable entire color groups or individual colors
- Add custom colors via an RGB/HSV/Hex color picker
- Import a color group from a JSON file
- Favorite colors for quick access

### Settings
- **Canvas** — accent color, background color, pixel grid color/opacity, highlight color/opacity
- **Language** — switch UI language (English, Italian, Spanish, French)
- **Color Manager** — pattern generation settings and color database management

## Requirements

- Windows 10 / 11
- .NET 8.0

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + O` | Open file |
| `Ctrl + 0` | Reset view |
| `Ctrl + +` | Zoom in |
| `Ctrl + -` | Zoom out |
| `Ctrl + G` | Toggle pixel grid |
| `Ctrl + Scroll` | Zoom in / out |

## Project Structure

```
Pixelab_v2.1/
├── App.xaml / App.xaml.cs
├── Pixelab.csproj
├── Pixelab_v2.1.sln
├── Views/                  # All windows (XAML + code-behind)
├── Services/               # PatternGenerator, LocalizationManager
├── Assets/                 # app.ico
├── Themes/                 # Colors.xaml, Styles.xaml
└── Resources/
    ├── Colors/             # colors.json (bead color database)
    └── Languages/          # en, it, es, fr JSON files
```

## Build & Run

```bash
cd Pixelab_v2.1
dotnet build
dotnet run
```

## License

MIT License

Copyright (c) 2026 Pixelab Studio

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
