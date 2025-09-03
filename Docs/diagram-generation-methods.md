# Alternative Diagram Generation Methods for Catan3 Companion

## Method 1: Mermaid CLI (Recommended)

Install Mermaid CLI and run the generation script:

```bash
# Install Mermaid CLI globally
npm install -g @mermaid-js/mermaid-cli

# Run the PowerShell generation script
./generate-diagrams.ps1
```

## Method 2: Online Mermaid Editor (No Installation Required)

1. Visit [mermaid.live](https://mermaid.live)
2. Copy each Mermaid diagram code from the .mmd files
3. Paste into the online editor
4. Click "Export" > "SVG"
5. Save to `Catan3.GameService/wwwroot/diagrams/`

### Quick Links for Online Generation

- **Picking Game Flow**: Copy from `mermaid-source/picking-game-flow.mmd`
- **Joining Game Flow**: Copy from `mermaid-source/joining-game-flow.mmd`  
- **Updating Game Flow**: Copy from `mermaid-source/updating-game-flow.mmd`

## Method 3: VSCode Extension (For VSCode Users)

1. Install "Mermaid Markdown Syntax Highlighting" extension
2. Open any .mmd file in VSCode
3. Right-click in the editor ? "Export as SVG"
4. Save to diagrams folder

## Method 4: GitHub Integration (Automatic Rendering)

GitHub automatically renders Mermaid diagrams in README.md files, so the original Mermaid text will display as graphics when viewed on GitHub.

## Method 5: Generate PNG Instead of SVG

If you prefer PNG images, modify the generate-diagrams.ps1 script:

```powershell
# Change .svg to .png in the output paths
$outputPath = Join-Path $outputDir $diagram.Output.Replace('.svg', '.png')
mmdc -i $sourcePath -o $outputPath -t neutral -b white --scale 2 -f png
```

## Method 6: Docker-based Generation (Cross-platform)

```bash
# Using Docker to run Mermaid CLI without Node.js installation
docker run --rm -v $(pwd):/data minlag/mermaid-cli -i /data/mermaid-source/picking-game-flow.mmd -o /data/diagrams/picking-game-flow.svg
```

## Web Serving Configuration

The diagrams will be automatically served by your ASP.NET Core application at:

- `http://localhost:8080/diagrams/picking-game-flow.svg`
- `http://localhost:8080/diagrams/joining-game-flow.svg`
- `http://localhost:8080/diagrams/updating-game-flow.svg`

This works because the diagrams are in the `wwwroot` folder which serves static files.
