# Naming Convention Tool

**Enforce naming conventions across your Unity project with customizable rules per asset type and auto-renaming support.**

## Features

- Define per-type naming rules (e.g., Scripts in PascalCase, Textures in snake_case)
- Add optional custom prefixes
- Scan your project and detect non-conforming asset names
- One-click auto-renaming to fix issues
- Persistent configuration saved across sessions
- Clean and responsive Editor UI with colored action buttons
- Compatible with Unity 2021.3+

## Installation

### Using Unity Package Manager

1. Open `manifest.json` in your Unity project's `Packages/` folder.
2. Add the following line inside `dependencies`:

```json
"com.juliennoe.namingconventionmanager": "https://github.com/juliennoe/ConventionNamingManager.git"
```

## License

This project is licensed under the MIT License.
