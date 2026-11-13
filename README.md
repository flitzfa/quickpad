# QuickPad

Editor de texto para Windows 11 que vive en la bandeja. Con doble Ctrl sobre un archivo seleccionado en el Explorador se abre una vista previa de vidrio al lado del archivo; un clic y ya lo editás.

## Requisitos

- Windows 11
- .NET 10 Desktop Runtime

## Instalar

```powershell
dotnet publish src/QuickPad -c Release -r win-x64 --self-contained false -o publish
```

Genera `publish\QuickPad.exe`. Ejecutalo una vez: queda en la bandeja.

## Uso

- Seleccioná un archivo en el Explorador y apretá **Ctrl dos veces** (menos de 350 ms entre toques): se abre la vista previa (primeras 40 líneas).
- **Clic** en la vista previa para editar. **Ctrl+S** guarda, **Ctrl+F** busca, **Esc** cierra.
- Guardar conserva la codificación y los saltos de línea originales.
- Menú de la bandeja: **Iniciar con Windows** (activa/desactiva el arranque automático) y **Salir**.

### Asociarlo con "Abrir con…"

Clic derecho en un archivo > **Abrir con** > **Elegir otra aplicación** > **Buscar otra aplicación en este equipo** y elegí `QuickPad.exe`.

## Log

`%LOCALAPPDATA%\QuickPad\log.txt` (incluye los tiempos de apertura).

## Mediciones

Build Release publicado (`--self-contained false`, ReadyToRun), esta máquina. Objetivos: ventana visible < 150 ms, ~20 MB en la bandeja, < 60 MB con la ventana visible.

Se midió disparando el mismo manejador que el hook de teclado (el hook ignora teclas simuladas) sobre una ventana real del Explorador, 10 aperturas. Tiempo desde el doble Ctrl hasta el primer frame:

| Apertura | Total (ms) | De eso, leer la selección del Explorador (ms) |
|---|---|---|
| 1 (primera) | 261 | 186 |
| 2 | 159 | 121 |
| 3 | 153 | 123 |
| 4 | 142 | 119 |
| 5 | 139 | 110 |
| 6 | 135 | 112 |
| 7 | 133 | 108 |
| 8 | 147 | 120 |
| 9 | 132 | 109 |
| 10 | 154 | 128 |

- Aperturas 2 a 10: 132 a 159 ms. **Las aperturas 2, 3 y 10 (159, 153 y 154 ms) no llegan al objetivo de 150 ms**; el resto sí. La primera apertura (261 ms) tampoco. Casi todo el tiempo es la consulta al Explorador (~110-130 ms).
- RAM (WorkingSet): en la bandeja tras ocultar la ventana, 5 a 18 MB (14 MB recién iniciado); con la ventana visible, 46 a 70 MB. **Con la ventana visible no se cumple siempre el objetivo de < 60 MB** (picos de hasta 70 MB justo después de abrir; luego se estabiliza en 46-61 MB). PrivateMemorySize64: ~73 MB al inicio, ~95-105 MB después de varias aperturas.
- Recortar el working set al ocultar la ventana no empeora la apertura (sin recorte: 238 ms la primera y 128-148 ms de la 2 a la 10) y baja la RAM de ~130-173 MB a ~10 MB, así que se mantiene.
