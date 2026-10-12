# Guía técnica — Windows 10 ISO Downloader

Este documento explica **cómo funciona internamente la herramienta**, su
arquitectura, el modelo de configuración y el razonamiento detrás de las
principales decisiones de implementación. Está dirigido a desarrolladores que
necesiten mantener, ampliar o auditar el proyecto.

> Idioma: [English](TECHNICAL_GUIDE.md) | [Español](TECHNICAL_GUIDE.es.md)
> Documentación principal: [readme.es.md](../readme.es.md)

## 1. Propósito y contexto

Microsoft publica las ISO de Windows 10 y Windows 11 únicamente a través de una
página web interactiva. El enlace de descarga es una URL firmada y de corta
duración que la propia página genera tras seleccionar una edición y un idioma.
No existe una API pública oficial para esto.

Esta herramienta automatiza esa interacción con **Playwright para .NET** y luego
descarga la URL generada con `HttpClient`. El resultado es un único comando
repetible que un pipeline de compilación (por ejemplo
[`tiny11builder`](https://github.com/ntdevlabs/tiny11builder)) puede invocar para
obtener la ISO.

El repositorio incluye un `config.json` que apunta a la edición **Windows 10
(multi-edition ISO)** (`2618`, en `/software-download/windows10ISO`). Como el
flujo es puro dato, el mismo binario puede descargar Windows 11 (edición `3813`)
u otra edición editando la configuración.

## 2. Pila tecnológica

| Capa | Tecnología |
| --- | --- |
| Runtime | .NET 10 (`net10.0`) |
| Lenguaje | C# con instrucciones de nivel superior y tipos de referencia anulables |
| Automatización del navegador | `Microsoft.Playwright` (Firefox headless) |
| Análisis de JSON | `System.Text.Json` |
| Descarga HTTP | `System.Net.Http.HttpClient` |
| Plataformas objetivo | Windows, Linux y macOS |

## 3. Estructura del proyecto

```
WindowsIsoDownloader-2.0/
├── WindowsIsoDownloader.csproj   # Proyecto estilo SDK, net10.0, referencia a Playwright
├── WindowsIsoDownloader.sln      # Solución de Visual Studio
├── WindowsIsoDownloader.cs       # Punto de entrada + todo el flujo (nivel superior)
├── Config.cs                     # POCOs que reflejan config.json
├── config.json                   # El script de acciones (editable sin recompilar)
├── Extension/
│   ├── HttpClientExtensions.cs   # DownloadAsync: descarga en streaming + progreso
│   └── StreamExtensions.cs       # CopyToAsync: copia por búferes con progreso
├── readme.md / readme.es.md      # Documentación de usuario (EN / ES)
└── docs/
    ├── TECHNICAL_GUIDE.md        # Este archivo (EN)
    └── TECHNICAL_GUIDE.es.md     # Versión en español
```

## 4. Modelo de configuración

`config.json` se deserializa en las clases de `Config.cs`:

```
Config
 ├── DownloadFolder : string
 ├── DownloadFilename : string
 └── Actions : List<BrowserAction>
        ├── Order : int
        ├── Kind : string            // Goto | SelectOption | Click | QuerySelector
        ├── WaitBeforeAction : int?  // milisegundos
        └── Parameters : BrowserActionParameters
               ├── Url : string?
               ├── Selector : string?
               └── Values : string?
```

`JsonSerializerOptions` habilita `PropertyNameCaseInsensitive`,
`ReadCommentHandling = Skip` y `AllowTrailingCommas`, de modo que el archivo
tolera mayúsculas/minúsculas, comentarios y comas finales. Esto facilita su
edición manual.

Las acciones se ordenan por `Order` y las interpreta un pequeño intérprete
(`RunActionsAsync`) cuyo conjunto de acciones es intencionadamente mínimo:

| `Kind` | Efecto en la página |
| --- | --- |
| `Goto` | `page.GotoAsync(url)` |
| `SelectOption` | `page.SelectOptionAsync(selector, values)` |
| `Click` | `page.ClickAsync(selector)` |
| `QuerySelector` | `page.QuerySelectorAsync(selector)`, guardando el elemento |

Mantener el flujo en datos (en lugar de codificado) es la decisión de diseño
central: cuando Microsoft renumera una opción, es un cambio de una línea en
`config.json`.

## 5. Flujo de ejecución

```
Inicio
  │
  ├─ 1. Fijar la codificación de salida en UTF-8 (glifos de la barra)   [best effort]
  ├─ 2. Leer y deserializar config.json  ── ¿inválido? ──► salir -1
  ├─ 3. Validar config (carpeta, nombre de archivo, acciones no vacías)
  ├─ 4. Crear DownloadFolder; calcular la ruta absoluta de salida
  ├─ 5. Instalar dependencias del navegador Playwright (firefox) ── ¿falla? ──► salir -1
  ├─ 6. Playwright.CreateAsync() + Firefox.LaunchAsync()
  │
  ├─ 7. Bucle de reintentos (hasta 3 intentos, contexto nuevo cada vez):
  │        a. Nuevo contexto (Locale = en-US) + nueva página
  │        b. RunActionsAsync(page, actions)   → quizá un handle de QuerySelector
  │        c. ResolveIsoUrlAsync(page, handle):
  │              - sondea todos los enlaces `a[href*=".iso"]` (10 intentos / 1 s)
  │              - prefiere la imagen de 64 bits si hay varias
  │              - si no, usa el href del handle y luego el primer enlace
  │        d. Si se encontró enlace → dejar de reintentar
  │           si no, esperar 5 s y reintentar
  │
  ├─ 8. Si no hay enlace → salir -1
  ├─ 9. HttpClient.DownloadAsync(isoUrl, FileStream, progress)  → escribe la ISO
  ├─ 10. Mostrar éxito y salir 0
```

### 5.1 Por qué existe el bucle de reintentos

El conector de descarga de Microsoft está protegido por un mecanismo anti-bot
interno que responde:

```json
{"Errors":[{"Key":"ErrorSettings.SentinelReject",
           "Value":"Sentinel marked this request as rejected.","Type":8}]}
```

cuando desconfía de una sesión de navegador (habitual con acceso automatizado o
repetido). Un contexto de navegador completamente nuevo suele obtener una sesión
aceptada, así que la herramienta reintenta hasta tres veces. El fallo no lo
provoca el código, es una decisión del servidor, y la herramienta degrada con
elegancia mostrando un mensaje claro.

### 5.2 Resolución del enlace

El punto históricamente frágil era el selector CSS del botón de descarga
(`#card-info-content .button`). Microsoft cambió la clase en su marcado, así que
ahora el código:

1. Sondea la página buscando todas las anclas cuyo `href` contenga `.iso`,
   durante unos segundos, porque la sección de descarga se renderiza de forma
   asíncrona.
2. Cuando la página ofrece más de una imagen, **prefiere la de 64 bits**:
   Windows 10 publica una imagen de 32 bits (`..._x32v1.iso`) y otra de 64 bits
   (`..._x64v1.iso`), así que el código elige el enlace cuya URL contiene `x64`.
3. Si ningún enlace `.iso` contiene `x64`, recurre al elemento de la acción
   `QuerySelector` configurada y, por último, al primer enlace `.iso`.

La página devuelve los enlaces como una cadena JSON que el código deserializa
con `System.Text.Json` (el propio deserializador `EvaluateAsync<T>` de Playwright
no convierte de forma fiable un `List<string>`). La URL final se valida
(`Contains(".iso")`) antes de descargarla.

## 6. Descarga con progreso

`HttpClientExtensions.DownloadAsync` solicita la URL con
`HttpCompletionOption.ResponseHeadersRead` para que la respuesta se transmita
en streaming en vez de almacenarse en búfer, y delega en
`StreamExtensions.CopyToAsync`. Ese método copia el flujo en bloques de 80 KiB y
notifica el número total de bytes leídos mediante un `IProgress<long>`.
`DownloadAsync` convierte los bytes en una proporción `0..1` y la notifica como
`IProgress<float>`, que el programa dibuja como una barra de 100 columnas.

Las llamadas de `Progress<T>` se despachan en el contexto de sincronización
capturado; como el valor es un `float` simple y las escrituras a consola son
baratas, esto es suficiente. El renderizador de progreso protege
`Console.SetCursorPosition` con `!Console.IsOutputRedirected` y un
`try/catch (IOException)`, de modo que una salida redirigida (logs de CI,
archivos) no bloquee la herramienta.

## 7. Notas de multiplataforma

El proyecto original apuntaba a .NET 6 y asumía Windows (`"DownloadFolder":
"C:\\"`). Los siguientes cambios lo hacen funcionar también en Linux/macOS:

- **Framework de destino** elevado a `net10.0`.
- **Playwright** actualizado a una versión reciente con compilaciones de
  navegador al día para Linux.
- **Carpeta por defecto en `config.json`** cambiada a `"."` (relativa al
  directorio de trabajo) en vez de `C:\`.
- **Tratamiento de rutas** con `Path.Combine` + `Path.GetFullPath`, de modo que
  funcionan tanto rutas con `/` como con `\`.
- **Salida de consola** fijada a UTF-8 para que se vean los glifos `█`/`░`, y
  protegida la recolocación del cursor cuando la salida está redirigida.
- **Mensajes de error** que mencionan el paso
  `playwright install-deps firefox` de Linux.

El binario de Firefox y sus dependencias del sistema son el único requisito
específico del sistema operativo. En .NET, Playwright incluye automáticamente
los drivers propios de Windows, Linux y macOS a través del paquete NuGet.

## 8. Compilar, ejecutar y publicar

```bash
# Compilación de desarrollo
dotnet build -c Release

# Ejecución dependiente del framework
dotnet bin/Release/net10.0/WindowsIsoDownloader.dll

# Publicación autocontenida en una carpeta (ejemplo: Linux x64)
dotnet publish -c Release -r linux-x64 --self-contained true

# Publicación autocontenida en un único archivo
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true
```

`config.json` se copia a la carpeta de salida (`CopyToOutputDirectory=Always`).
En tiempo de ejecución la herramienta busca primero `config.json` junto al
ejecutable y, si no lo encuentra, en el directorio de trabajo actual.

### 8.1 Soporte de 32 bits (x86) y ARM

El proyecto es `AnyCPU` (`PlatformTarget=AnyCPU`, `Prefer32Bit=false`) y el
código C# es independiente del tamaño de puntero: los tamaños y desplazamientos
usan `long` (`ContentLength`, total de bytes leídos) y el archivo se escribe
mediante un `FileStream`, por lo que una ISO mayor de 2 GiB no se trunca en un
proceso de 32 bits.

```bash
# Windows x86 de 32 bits
dotnet publish -c Release -r win-x86 --self-contained true -p:PlaywrightPlatform=win

# Linux ARM de 32 bits
dotnet publish -c Release -r linux-arm --self-contained true
```

`Microsoft.Playwright.targets` selecciona la carpeta del driver a partir del
`RuntimeIdentifier` activo. `win-x86` y `linux-arm` no los reconoce ese target, y
un RID no reconocido hace que copie el driver de **todas** las plataformas
(~575 MB). Pasar `-p:PlaywrightPlatform=win` fija la copia en `win32_x64`, el
único driver que existe para Windows, y mantiene pequeño el paquete de 32 bits.
Por eso el `.csproj` deliberadamente **no** declara `RuntimeIdentifiers`
(declararlo provocaría el mismo fallback en una compilación normal).

Lo que puede ejecutarse realmente lo limita el motor del navegador, no el código
gestionado:

| Destino | ¿Compila? | ¿Ejecuta? |
| --- | --- | --- |
| `win-x64` | Sí | Sí |
| `win-x86` | Sí (añade `-p:PlaywrightPlatform=win`) | Solo en Windows de 64 bits: el proceso de 32 bits arranca el `node.exe` de 64 bits incluido en Playwright. Un Windows exclusivamente de 32 bits no puede ejecutarlo. |
| `linux-x64` | Sí | Sí |
| `linux-arm64` | Sí | Sí |
| `linux-arm` | Sí | No: Playwright incluye el driver `linux-x64` para Linux no arm64, así que el navegador no puede arrancar. |
| `linux-x86` | No | No: .NET 10 no tiene apphost Linux x86 de 32 bits (`NETSDK1084`). |

`Driver.GetPath`, dentro de `Microsoft.Playwright`, fija `win32_x64` en Windows y
elige `linux-arm64` frente a `linux-x64` en Linux, que es la causa raíz de las
limitaciones de ejecución anteriores.

## 9. Códigos de salida

| Código | Significado |
| --- | --- |
| `0` | ISO descargada correctamente. |
| `-1` | Error de configuración, fallo de Playwright/navegador, rechazo anti-bot o error de E/S de archivos. |

## 10. Lista de mantenimiento

Si la herramienta deja de encontrar el enlace:

1. Abre la [página de descarga](https://www.microsoft.com/en-us/software-download/windows10ISO)
   e inspecciona `#product-edition`; actualiza el `Values` de `Order: 2`.
2. Confirma la edición y luego inspecciona `#product-languages`; actualiza el
   `Values` de `Order: 4` con el valor JSON completo de tu idioma.
3. Verifica el selector final (por defecto `#card-info-content a`) contra el
   ancla de descarga generada.
4. Si la página se reestructuró, ajusta la secuencia de acciones/selectores.
5. Actualiza periódicamente el paquete NuGet de Playwright para que las
   compilaciones del navegador estén al día y pasen las comprobaciones de
   compatibilidad del sitio.

## 11. Referencia de idiomas

El `Values` de la acción `SelectOption` con `Order: 4` es la cadena JSON
`{"id":"<skuId>","language":"<nombre>"}` tomada de los `<option>` de
`#product-languages`. Microsoft genera esas opciones a partir de la lista de
SKU del conector de descarga
(`getskuinformationbyproductedition?profile=606624d44113&productEditionId=2618`),
cuyas entradas tienen el formato `{"Id":"16067","Language":"English",...}`. Por
tanto el mapeo entre `Id` y `Language` es 1:1, y por eso un idioma se selecciona
pegando `{"id":"16067","language":"English"}`.

Los 38 idiomas disponibles para la edición de Windows 10 `2618` son:

| Idioma | `id` | `Values` |
| --- | --- | --- |
| Arabic | `16058` | `{"id":"16058","language":"Arabic"}` |
| Brazilian Portuguese | `16059` | `{"id":"16059","language":"Brazilian Portuguese"}` |
| Bulgarian | `16060` | `{"id":"16060","language":"Bulgarian"}` |
| Chinese (Simplified) | `16061` | `{"id":"16061","language":"Chinese (Simplified)"}` |
| Chinese (Traditional) | `16062` | `{"id":"16062","language":"Chinese (Traditional)"}` |
| Croatian | `16063` | `{"id":"16063","language":"Croatian"}` |
| Czech | `16064` | `{"id":"16064","language":"Czech"}` |
| Danish | `16065` | `{"id":"16065","language":"Danish"}` |
| Dutch | `16066` | `{"id":"16066","language":"Dutch"}` |
| English | `16067` | `{"id":"16067","language":"English"}` |
| English International | `16068` | `{"id":"16068","language":"English International"}` |
| Estonian | `16069` | `{"id":"16069","language":"Estonian"}` |
| Finnish | `16070` | `{"id":"16070","language":"Finnish"}` |
| French | `16071` | `{"id":"16071","language":"French"}` |
| French Canadian | `16072` | `{"id":"16072","language":"French Canadian"}` |
| German | `16073` | `{"id":"16073","language":"German"}` |
| Greek | `16074` | `{"id":"16074","language":"Greek"}` |
| Hebrew | `16075` | `{"id":"16075","language":"Hebrew"}` |
| Hungarian | `16076` | `{"id":"16076","language":"Hungarian"}` |
| Italian | `16077` | `{"id":"16077","language":"Italian"}` |
| Japanese | `16078` | `{"id":"16078","language":"Japanese"}` |
| Korean | `16079` | `{"id":"16079","language":"Korean"}` |
| Latvian | `16080` | `{"id":"16080","language":"Latvian"}` |
| Lithuanian | `16081` | `{"id":"16081","language":"Lithuanian"}` |
| Norwegian | `16082` | `{"id":"16082","language":"Norwegian"}` |
| Polish | `16083` | `{"id":"16083","language":"Polish"}` |
| Portuguese | `16084` | `{"id":"16084","language":"Portuguese"}` |
| Romanian | `16085` | `{"id":"16085","language":"Romanian"}` |
| Russian | `16086` | `{"id":"16086","language":"Russian"}` |
| Serbian Latin | `16087` | `{"id":"16087","language":"Serbian Latin"}` |
| Slovak | `16088` | `{"id":"16088","language":"Slovak"}` |
| Slovenian | `16089` | `{"id":"16089","language":"Slovenian"}` |
| Spanish | `16090` | `{"id":"16090","language":"Spanish"}` |
| Spanish (Mexico) | `16091` | `{"id":"16091","language":"Spanish (Mexico)"}` |
| Swedish | `16092` | `{"id":"16092","language":"Swedish"}` |
| Thai | `16093` | `{"id":"16093","language":"Thai"}` |
| Turkish | `16094` | `{"id":"16094","language":"Turkish"}` |
| Ukrainian | `16095` | `{"id":"16095","language":"Ukrainian"}` |

> Seleccionar un idioma no es más que una acción `SelectOption`; el valor debe
> coincidir exactamente con el `<option value>`, y por eso se usa la cadena JSON
> completa (no solo el id numérico). La lista está ligada a la edición, así que
> si `2618` cambia (o cambias a Windows 11, edición `3813`) hay que volver a leer
> las opciones de la página. La etiqueta visible y el campo `language` pueden
> diferir: la opción de Windows 10 mostrada como *English (United States)* tiene
> el valor `{"id":"16067","language":"English"}`.

## 12. Ciclo de soporte de Windows 10 y rotación de certificados de Windows Update

Windows 10 alcanzó el fin de soporte el **14 de octubre de 2025**. Desde
entonces solo el programa Extended Security Updates (ESU) entrega parches de
seguridad:

| Etapa | Termina | A quién aplica |
| --- | --- | --- |
| Soporte ordinario | 14 de octubre de 2025 | Todos |
| ESU – Año 1 | 13 de octubre de 2026 | Comercial / educativo |
| ESU – Año 2 | 12 de octubre de 2027 | Consumidores (fin del ESU de consumo); Año 2 para comercial |
| ESU – Año 3 | 10 de octubre de 2028 | Solo comercial / educativo |

El propio Windows Update también se ve afectado por la **rotación de
certificados de 2027**. Los certificados del servicio caducan y sus reemplazos
deben instalarse antes del **17 de mayo de 2027** o del **19 de junio de 2027**
(según el producto). Los sistemas que no puedan recibirlos —**Windows 7,
Windows 8 / 8.1, Windows 10 sin ESU** y las versiones de Windows Server fuera de
soporte— no podrán conectar con Windows Update. Es una rotación de certificados
del lado del servidor, independiente de esta herramienta; la única solución es
un sistema operativo soportado o una instalación de Windows 10 con ESU que siga
instalando la actualización mensual.

Referencias:

- Fin de soporte y ESU:
  <https://www.microsoft.com/en-us/windows/extended-security-updates>
- Anuncio de la rotación de certificados:
  <https://techcommunity.microsoft.com/blog/windows-itpro-blog/prepare-for-windows-update-certificate-rotation-in-2027/4562463>

## 13. Notas de seguridad y legales

- La herramienta descarga únicamente las ISO públicas y oficiales de Microsoft;
  no elude ninguna licencia.
- Las URL de descarga firmadas caducan; se usan de inmediato y nunca se
  almacenan.
- Nunca incluyas un token de acceso personal (PAT) de GitHub en archivos de
  código fuente; usa un credential helper o una variable de entorno.
