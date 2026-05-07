# IconImporter

Bring SVG icon packs into Unity, manage them visually, and render them as PNG sprites ready for UI. IconImporter ships with built-in providers for Tabler, Feather, Heroicons, Iconoir, css.gg, and Simple Icons, and supports your own local SVG folders or custom GitHub-hosted repos.

---
### Install Package from Git URL: **https://github.com/DaveRune/IconImporter.git**
---

## Overview

IconImporter has three pieces. Providers know where SVGs come from. Built-in providers point at a GitHub repo and download on demand. Local providers point at any folder on your disk.

Icon Packs pick a subset of icons from one or more providers and embed them as PNG subassets at a chosen size, color, and stroke width. Drag the resulting sprite or texture onto a UI Image, or reference it in code.

The Icon Manager window is where you browse a provider's full catalogue, search by name or alias, and toggle which icons are included in a pack.

Conversion uses ImageMagick under the hood, so it's a one-time install on your machine.

## Usage

### 1. Run First-Time Setup

Open `Tools > IconImporter > Setup`. The wizard auto-detects ImageMagick (or links you to the download), then offers to add the built-in providers to your project.

### 2. Download a Provider's Icons

Select a provider asset (e.g. `Tabler Icons`) and click `Download and Setup`. The repository is downloaded and SVGs are extracted to an `IconProviders` folder at your project root, alongside `Assets`.

### 3. Create an Icon Pack

Right-click in the Project window and choose `Create > IconImporter > Icon Pack`. Drag one or more providers onto the pack's `Providers` list, then set size, color, and stroke width.

### 4. Pick Icons and Update

Click `Manage Icons` on the pack. Search, filter by variant, and click icons to mark them for inclusion. Click `Update` to convert to PNGs and embed them as subassets on the pack.

## Example

Setting up a pack of outline icons for your UI:

1. Open `Tools > IconImporter > Setup` and add the Tabler provider.
2. Select `Tabler Icons` in the Project window and click `Download and Setup`.
3. Right-click in `Assets/` and choose `Create > IconImporter > Icon Pack`. Name it `UI Icons`.
4. Set Icon Color to your UI accent, Stroke Width to `1.5`, and Icon Size to `32`.
5. Drag `Tabler Icons` into the pack's `Providers` list.
6. Click `Manage Icons`, enable the `Outline` variant, search for the icons you need, click each one, then click `Update`.
7. Drag the resulting sprite subassets onto your UI Image components.

To swap providers later, add another (say Feather) to the pack's `Providers` list and pick its icons via Manage Icons. The conversion settings apply across every provider in the pack.
