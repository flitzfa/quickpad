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

Se midió disparando el mismo manejador que el hook de teclado (el hook ignora teclas simuladas) sobre una ventana real del Explorador con un archivo seleccionado, 10 aperturas, la primera ~2 s después de iniciar. Tiempo desde el doble Ctrl hasta el primer frame:

| Apertura | Total (ms) | De eso, leer la selección del Explorador (ms) |
|---|---|---|
| 1 (primera) | 131 | 77 |
| 2 | 53 | 30 |
| 3 | 55 | 33 |
| 4 | 45 | 26 |
| 5 | 58 | 37 |
| 6 | 48 | 27 |
| 7 | 49 | 31 |
| 8 | 47 | 28 |
| 9 | 46 | 27 |
| 10 | 44 | 25 |

- Todas las aperturas llegan al objetivo de 150 ms: la primera en 131 ms y de la 2 a la 10 entre 44 y 58 ms. En otra corrida: 128 ms la primera y 45 a 67 ms el resto. La ruta del archivo se lee en el hilo de la interfaz mientras la posición del archivo en pantalla se busca en paralelo (solo dentro de la vista de archivos); si esa búsqueda tarda más de 250 ms, la ventana se abre junto al cursor.
- RAM (WorkingSet): en la bandeja, 8 MB recién iniciado y 2 a 11 MB después de ocultar la ventana. Con la ventana visible: 91 MB después de la primera apertura y 42 a 65 MB en las otras nueve. **El objetivo de < 60 MB con la ventana visible no se cumple siempre**: falla en la primera apertura (91 MB) y en dos de las otras nueve (61 y 65 MB). PrivateMemorySize64: 74-92 MB al inicio, 86-99 MB después.
- El working set se recorta un segundo después de ocultar la ventana (o de terminar el precalentamiento al iniciar): recortado al instante, el trabajo que queda en segundo plano volvía a cargar ~20 MB enseguida.
- El precalentamiento al iniciar bloquea el hilo de la interfaz ~18 ms; el resto (~160 ms) corre en segundo plano.
