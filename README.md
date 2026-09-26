# ULTRAROGUE

A roguelike mode mod for ULTRAKILL. Adds custom characters, items, curses, and procedurally generated rooms and floors.

## Requirements

- **ULTRAKILL** (Steam version)
- **BepInEx 5** (x64) installed in your ULTRAKILL folder
- **.NET SDK** (.NET 8.0 or newer)

## How to Build

### 1. Set your ULTRAKILL path
Open `ULTRAROGUE/ULTRAROGUE.csproj` in a text editor.

Find line 12 and change `<UltrakillDir>` to point to your actual ULTRAKILL folder:

- **Linux (Steam default):**
  ```xml
  <UltrakillDir>/home/YOUR_USER/.steam/steam/steamapps/common/ULTRAKILL/</UltrakillDir>
  ```
- **Windows (Steam default):**
  ```xml
  <UltrakillDir>C:/Program Files (x86)/Steam/steamapps/common/ULTRAKILL/</UltrakillDir>
  ```

> Always use forward slashes (`/`), even on Windows.

### 2. Build the project
Open a terminal in the project folder and run:

```bash
cd ULTRAROGUE
dotnet build
```

### 3. Output file
The compiled DLL will be located at:

```
ULTRAROGUE/bin/Debug/netstandard2.1/Ultrarogue.dll
```

### 4. Install the mod
Copy `Ultrarogue.dll` into your BepInEx plugins folder:
