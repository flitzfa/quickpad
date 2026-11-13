# QuickPad — Diseño

> "QuickPad" es un nombre provisorio; se puede cambiar sin afectar el diseño.

**Fecha:** 2026-11-13
**Estado:** Pendiente de revisión

## 1. Objetivo

Editar archivos de texto chicos (API keys, `.env`, `.json`, configs) mucho más rápido que abriendo VS Code. El flujo ideal es: abrir, cambiar, guardar y cerrar en segundos. Además, que la herramienta se vea moderna y cuidada, con estilo glass tipo iOS.

**Criterios de éxito**
- La ventana aparece en **menos de 150 ms** desde el doble Ctrl.
- Consumo en reposo (en la bandeja) de **~20 MB de RAM**. Con la ventana visible, menos de 60 MB.
- Guardar nunca corrompe ni "ensucia" un archivo: se mantienen la codificación y los saltos de línea originales.

**Alcance:** solo Windows 11, uso personal.

## 2. Fuera de alcance (v1)

Pestañas, resaltado de sintaxis completo, plugins, configuración avanzada, archivos en el escritorio de Windows (solo ventanas del Explorador), soporte para macOS/Linux y archivos de más de ~5 MB.

## 3. Experiencia de uso

| Acción | Resultado |
|---|---|
| Doble Ctrl con un archivo seleccionado en el Explorador | Se abre la ventana de vidrio al lado del archivo, con animación de escala y fundido (rebote suave). Muestra las primeras ~40 líneas en **modo vista previa** (solo lectura). |
| Clic en el texto | Pasa a **modo edición**: la ventana crece un poco y aparece el cursor. |
| Ctrl+F | Barra de búsqueda arriba; Enter va a la siguiente coincidencia y Shift+Enter a la anterior. |
| Ctrl+S | Guarda y muestra un ✓ sutil. |
| Esc o clic afuera | Cierra con animación (se encoge hacia el archivo). Si hay cambios sin guardar, muestra un diálogo dentro de la ventana: **Guardar / Descartar / Seguir editando**. |
| Doble clic o "Abrir con…" sobre un archivo | Abre directo en modo edición, centrado en la pantalla. |

**Doble Ctrl:** dos pulsaciones de Ctrl **solo**, con menos de 350 ms entre ellas, sin otra tecla en el medio y únicamente cuando la ventana activa es el Explorador (`CabinetWClass`). Mantener Ctrl apretado no cuenta como toque. Ctrl+C, Ctrl+clic y los demás atajos siguen funcionando como siempre.

**Sin tapado de secretos:** la vista previa muestra los valores tal cual, incluidas claves y tokens, para poder ver qué hay y decidir si editar. (Decisión del usuario del 2026-11-13; reemplaza el tapado original.)

**Archivos aceptados:** cualquier archivo que sea texto (`.txt`, `.env`, `.json`, `.ini`, `.cfg`, `.yaml`, `.md`, `.log`, `.conf`, …). Si el archivo es binario, se muestra el aviso "Este archivo no es texto" y no se abre.

## 4. Apariencia

- **Tema:** sigue el de Windows. En modo claro, vidrio claro esmerilado (blanco translúcido, texto oscuro, acento azul). En modo oscuro, cristal oscuro (negro translúcido, texto claro, acento celeste). Si el tema de Windows cambia con la ventana abierta, se actualiza en vivo.
- **Vidrio:** desenfoque real del sistema (Acrylic de Windows 11, `DwmSetWindowAttribute` → `DWMWA_SYSTEMBACKDROP_TYPE`), esquinas redondeadas (`DWMWA_WINDOW_CORNER_PREFERENCE`) y un borde fino semitransparente.
- **Tipografía:** Segoe UI Variable para la interfaz y Cascadia Mono para el texto del archivo.
- **Claves:** en líneas `CLAVE=valor` o `"clave":`, el nombre de la clave se pinta con el color de acento. No hay resaltado de sintaxis completo.
- **Animaciones:** apertura con escala 0.9→1, fundido y un leve rebote (~250 ms). El cierre hace lo inverso, hacia la posición del archivo. Animaciones de WPF con easing (`BackEase`/`CubicEase`).

## 5. Arquitectura

**Stack:** C# + WPF sobre **.NET 10 (LTS)**. Un único proceso que queda residente en la bandeja del sistema.

```
src/QuickPad/
  App            — arranque, instancia única, icono en la bandeja
  Input/
    KeyboardHook       — hook WH_KEYBOARD_LL, filtra por ventana activa del Explorador
    DoubleTapDetector  — lógica pura de doble toque (testeable, sin Win32)
  Shell/
    ExplorerSelection  — archivo seleccionado y su rectángulo en pantalla
  Files/
    TextFile           — detección de texto/binario, codificación, saltos de línea, lectura y guardado seguro
  UI/
    GlassWindow        — ventana única reutilizada; vidrio, tema y animaciones
    PreviewFormatter   — vista previa: claves en color y corte de líneas (lógica pura)
tests/QuickPad.Tests   — xUnit
```

**Responsabilidad de cada pieza**

- **App:** asegura una sola instancia con un `Mutex` con nombre. Si el programa se lanza de nuevo con un archivo (doble clic o "Abrir con…"), le pasa la ruta a la instancia que ya está corriendo por un named pipe y se cierra. En la bandeja tiene un menú: "Iniciar con Windows" (activable, usa la clave `HKCU\…\Run`) y "Salir".
- **KeyboardHook + DoubleTapDetector:** el hook solo le pasa eventos al detector. El detector recibe `(tecla, bajada/subida, timestamp)` y avisa cuando hay un doble toque. El hook no bloquea ninguna tecla y nunca se traga eventos.
- **ExplorerSelection:** obtiene la ventana del Explorador en primer plano, la busca entre las de `Shell.Application.Windows()` (COM `IShellWindows`) y lee `Document.SelectedItems()`. En Windows 11, con pestañas, varias pestañas comparten la misma ventana principal: se toma la pestaña activa, que es el hijo visible `ShellTabWindowClass`. El rectángulo del archivo se consigue con UI Automation (el elemento seleccionado en la lista). Si eso falla, la ventana se abre al lado del cursor.
- **TextFile:**
  - **Binario:** se considera binario si hay bytes `NUL` en los primeros 8 KB (excepto si tiene BOM UTF-16).
  - **Codificación:** se detecta por BOM; si no tiene, se prueba UTF-8 estricto y, si falla, se usa la página de códigos ANSI del sistema.
  - **Saltos de línea:** se detecta el estilo dominante (CRLF o LF) y se respeta al guardar.
  - **Vista previa:** lee solo los primeros ~64 KB.
  - **Guardado seguro:** escribe un temporal en la misma carpeta y después hace `File.Replace` (o `File.Move` si el archivo es nuevo). Guarda la fecha de modificación y el tamaño al abrir para detectar cambios externos.
- **GlassWindow:** se crea una sola vez y después se muestra u oculta. Un único `TextBox` alterna entre `IsReadOnly` (vista previa) y edición. Lee el tema de `AppsUseLightTheme` y escucha `SystemEvents.UserPreferenceChanged`.
- **Memoria:** al ocultar la ventana se libera memoria (`SetProcessWorkingSetSize(-1,-1)`). Se mide que esto no afecte el objetivo de 150 ms; si lo afecta, se saca.

## 6. Errores y casos límite

| Caso | Comportamiento |
|---|---|
| Archivo de solo lectura o sin permisos | Se abre en vista previa; al intentar guardar, aparece un aviso dentro de la ventana con la causa. |
| Otro programa cambió el archivo | Al guardar: "El archivo cambió afuera. **Sobrescribir / Recargar**". |
| Archivo de más de 5 MB | La vista previa funciona; para editar, primero pide confirmación. |
| Binario | Aviso "Este archivo no es texto". |
| Doble Ctrl sin selección, con una carpeta o con varios archivos | Sin selección o con una carpeta, no hace nada. Con varios archivos, usa el primero. |
| Falla al leer la selección (COM/UIA) | Se registra en el log y no se abre nada. Nunca se cierra el programa con error. |
| Error inesperado | Se registra en `%LOCALAPPDATA%\QuickPad\log.txt`; la app sigue en la bandeja. |

## 7. Pruebas

**Automáticas (xUnit):**
- `DoubleTapDetector`: dos toques dentro del tiempo → sí; fuera del tiempo → no; otra tecla en el medio → no; Ctrl mantenido apretado → no; tres toques → un solo disparo.
- `TextFile`: detección de binario; UTF-8 con y sin BOM, UTF-16 y ANSI; un ida y vuelta (leer y guardar) que conserva los bytes de la codificación y los saltos de línea; el guardado seguro no deja temporales; detección de cambios externos.
- `PreviewFormatter`: claves en color, valores que parecen secretos mostrados tal cual, corte a 40 líneas y 200 caracteres.

**Manuales:** apariencia en tema claro y oscuro, animaciones, Explorador con varias pestañas, selección múltiple y medición de tiempo de apertura y de RAM.

## 8. Requisitos previos

- **.NET 10 SDK:** no está instalado en esta máquina; hay que instalarlo antes de empezar.
- Windows 11 (para el Acrylic del sistema y las esquinas redondeadas).

## 9. Riesgos

- **Pestañas del Explorador en Windows 11:** identificar la pestaña activa depende de detalles internos que pueden cambiar con actualizaciones de Windows. Mitigación: si falla, la ventana se abre junto al cursor.
- **Hook de teclado global:** si la app se cuelga, Windows desactiva el hook. El callback tiene que ser mínimo y no bloqueante.
- **Antivirus:** algunos marcan como sospechosos los hooks de teclado globales en ejecutables sin firmar. Se acepta este riesgo para uso personal.
