# QuickPad

Editor de texto para Windows 11 que vive en la bandeja del sistema. Poné el mouse sobre un archivo en el Explorador, apretá **Ctrl dos veces** y se abre una vista previa al lado del archivo; un clic y ya lo estás editando. Pensado para tocar rápido un `.env`, una API key o un `.json` sin esperar a que abra un editor pesado.

## Descargar

1. Bajá **`QuickPad.exe`** de la [última versión](https://github.com/flitzfa/quickpad/releases/latest).
2. Ejecutalo. No hace falta instalar nada (ni .NET): queda corriendo en la bandeja, junto al reloj.
3. Para que arranque solo, clic derecho en el ícono de la bandeja > **Iniciar con Windows**.

> Windows puede mostrar "Windows protegió su PC" porque el ejecutable no está firmado. Elegí **Más información > Ejecutar de todas formas**.

Requisitos: Windows 11 (64 bits).

## Uso

- **Ctrl dos veces** (menos de 350 ms entre toques) con el mouse sobre un archivo del Explorador: se abre la vista previa con las primeras 40 líneas. Si el mouse no está sobre un archivo, se usa el archivo seleccionado; sobre una carpeta no pasa nada.
- Con la vista previa abierta, doble Ctrl sobre otro archivo cambia a ese archivo.
- **Clic** en la vista previa para editar. **Ctrl+S** guarda, **Ctrl+F** busca, **Esc** cierra. Clic en otra ventana también cierra.
- Si hay cambios sin guardar, siempre pregunta antes de cerrar o cambiar de archivo, también al apagar Windows.
- Guardar conserva la codificación y los saltos de línea originales del archivo, y escribe de forma segura (un corte de luz no deja el archivo a medias).
- Sigue el tema claro u oscuro de Windows.

### Abrir archivos directo con QuickPad

Clic derecho en un archivo > **Abrir con** > **Elegir otra aplicación** > **Buscar otra aplicación en este equipo** y elegí `QuickPad.exe`.

### Desinstalar

Desmarcá **Iniciar con Windows** en el menú de la bandeja, elegí **Salir** y borrá `QuickPad.exe`. El registro de errores está en `%LOCALAPPDATA%\QuickPad` (podés borrar esa carpeta también).

## Compilar desde el código

Requiere el [SDK de .NET 10](https://dotnet.microsoft.com/download).

```powershell
dotnet test
dotnet publish src/QuickPad -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

Genera `dist\QuickPad.exe`, el mismo que se publica en las versiones.

## Rendimiento

Medido en la máquina de desarrollo: la ventana aparece entre 45 y 70 ms después del doble Ctrl (la primera vez tras iniciar, ~130 ms). En la bandeja usa entre 2 y 12 MB de RAM; con la ventana abierta, entre 40 y 90 MB.

## Licencia

[MIT](LICENSE)
