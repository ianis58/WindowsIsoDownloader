# Windows 10 ISO Downloader

Windows 10 ISO Downloader es una pequeña herramienta multiplataforma que
descarga de forma programática la última **ISO oficial de Windows 10** directa
de los servidores de Microsoft. Controla un navegador Firefox real (en modo
headless) mediante [Playwright](https://playwright.dev/dotnet/) para recorrer la
página de descarga, obtiene el enlace temporal de descarga y después transmite
la ISO a disco con una barra de progreso en vivo (tamaño, velocidad y tiempo
restante). Las descargas son **reanudables** —una ejecución interrumpida
continúa desde un archivo `.part`— y la herramienta ofrece una pequeña
**interfaz de línea de comandos** para automatizar.

Resulta útil en proyectos que construyen instaladores personalizados de
Windows 10 (por ejemplo
[`tiny11builder`](https://github.com/ntdevlabs/tiny11builder)), donde los pasos
manuales del navegador son la única parte que no se puede automatizar.

El flujo está basado en datos (ver [`config.json`](#configuración-configjson)),
así que la misma herramienta también puede descargar Windows 11 u otra edición
cambiando unos pocos valores; la configuración incluida apunta a la
**Windows 10 (multi-edition ISO)**.

> **Idioma:** [English](readme.md) | [Español](readme.es.md)
> **Guía técnica:** [English](docs/TECHNICAL_GUIDE.md) | [Español](docs/TECHNICAL_GUIDE.es.md)

> **Estado de soporte de Windows 10:** el soporte ordinario terminó el
> **14 de octubre de 2025**; el **ESU** de consumo llega hasta el
> **12 de octubre de 2027** y el ESU comercial/educativo hasta el
> **10 de octubre de 2028**. Windows Update también dejará de funcionar en
> **Windows 7 hasta Windows 10 sin ESU** cuando se roten los certificados del
> servicio de actualizaciones (mayo/junio de 2027). Ver
> [Fin de soporte de Windows 10 y actualizaciones de seguridad extendidas](#fin-de-soporte-de-windows-10-y-actualizaciones-de-seguridad-extendidas).

## Requisitos

- SDK de **.NET 10** (para compilar) o el runtime de .NET 10 (para ejecutar una
  compilación publicada).
- Acceso a Internet a `microsoft.com` y `software.download.prss.microsoft.com`.
- **Solo Linux:** las librerías del sistema que necesita la compilación de
  Firefox de Playwright. Instálalas con:
  ```bash
  ./bin/Release/net10.0/playwright install-deps firefox
  ```
  (requiere `sudo`; en Debian/Ubuntu el script usa `apt-get`).

No hace falta instalar ningún navegador manualmente: la herramienta descarga la
compilación de Firefox de Playwright correspondiente la primera vez que se
ejecuta.

## Compilación

```bash
dotnet build -c Release
```

El resultado de la compilación queda en `bin/Release/net10.0/`.

La solución incluye además un proyecto de pruebas xUnit (reanudación de
descargas, lectura de configuración, línea de comandos, selección de enlace,
validación de ISO, …):

```bash
dotnet test -c Release
```

Para generar un ejecutable autocontenido para una plataforma concreta:

```bash
# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained true

# Windows x64
dotnet publish -c Release -r win-x64 --self-contained true
```

### 32 bits (x86) y otras arquitecturas

El proyecto se compila como **AnyCPU** con `Prefer32Bit` desactivado, así que un
mismo ensamblado gestionado funciona en runtimes de 32 y 64 bits; en Windows de
64 bits el proceso por defecto es de 64 bits. Compilaciones adicionales:

```bash
# Windows de 32 bits (x86)
dotnet publish -c Release -r win-x86 --self-contained true -p:PlaywrightPlatform=win

# Linux ARM de 64 bits
dotnet publish -c Release -r linux-arm64 --self-contained true
```

El flag `-p:PlaywrightPlatform=win` indica a Playwright qué driver incluir: para
un identificador de runtime no reconocido como `win-x86`, de lo contrario
copiaría el driver del navegador de **todas** las plataformas (unos 575 MB).

> **Importante:** el trabajo pesado lo hace el motor del navegador. Playwright
> solo distribuye un driver de **64 bits** (`win32_x64`, `linux-x64`,
> `linux-arm64`, macOS). Por eso una compilación `win-x86` funciona en una
> instalación de Windows de 64 bits (el proceso de 32 bits arranca el driver de
> 64 bits incluido), pero no puede ejecutarse en un sistema Windows
> exclusivamente de 32 bits. Del mismo modo, .NET 10 no tiene runtime Linux x86
> de 32 bits (`linux-x86`) y Playwright no tiene driver para ARM de 32 bits, por
> lo que una compilación `linux-arm` compila pero el navegador no puede
> arrancar. Consulta la
> [guía técnica](docs/TECHNICAL_GUIDE.es.md#81-soporte-de-32-bits-x86-y-arm)
> para ver la tabla completa.

## Ejecución

La herramienta lee `config.json` desde la misma carpeta que el ejecutable (si
no lo encuentra, usa el directorio de trabajo actual).

**Linux / macOS**

```bash
cd bin/Release/net10.0
./WindowsIsoDownloader
```

**Windows**

```bat
cd bin\Release\net10.0
WindowsIsoDownloader.exe
```

La ISO se escribe en la carpeta configurada en `config.json` (por defecto el
directorio de trabajo actual, generando `./windows10.iso`).

Si la ISO ya existe, la herramienta termina de inmediato; usa `--force` para
volver a descargarla. Si la ejecución se interrumpe (corte de red, `Ctrl+C`),
los datos parciales se guardan en `windows10.iso.part` y la siguiente ejecución
la reanuda desde ahí.

### Opciones de línea de comandos

| Opción | Descripción |
| --- | --- |
| `-c`, `--config <ruta>` | Usa un archivo `config.json` concreto. |
| `-o`, `--output <ruta>` | Ruta completa de la ISO (anula `DownloadFolder`/`DownloadFilename`). |
| `-f`, `--force` | Sobrescribe una ISO existente. |
| `--dry-run` | Obtiene el enlace de descarga pero no descarga. |
| `--arch <x64\|x86\|arm64\|any>` | Arquitectura de imagen preferida (por defecto `x64`). |
| `--headed` | Muestra la ventana del navegador (útil para depurar). |
| `--headless` | Fuerza el modo headless (por defecto). |
| `--slow-mo <ms>` | Ralentiza las acciones del navegador el retardo indicado. |
| `--list-languages` | Muestra los idiomas disponibles para la edición configurada y sale. |
| `--experimental-sentinel` | Calentamiento best-effort del anti-bot de Microsoft. |
| `-v`, `--verbose` | Registro detallado. |
| `-h`, `--help` | Muestra la ayuda. |

```bash
# Ver los idiomas disponibles para la edición configurada
./WindowsIsoDownloader --list-languages

# Descargar la imagen de 32 bits en una ruta concreta
./WindowsIsoDownloader --arch x86 -o /home/usuario/isos/windows10.iso

# Comprobar solo que se puede obtener un enlace
./WindowsIsoDownloader --dry-run
```

## Configuración (`config.json`)

Todo el flujo está basado en datos, de modo que si Microsoft cambia su página
solo tienes que editar `config.json` en lugar del código fuente.

| Campo | Descripción |
| --- | --- |
| `DownloadFolder` | Carpeta donde se guarda la ISO. Se aceptan rutas de Windows (`C:\\isos`) y de Linux (`/home/usuario/isos`). Una ruta relativa (por defecto `.`) se resuelve respecto al directorio de trabajo. |
| `DownloadFilename` | Nombre del archivo ISO resultante. Debe ser un nombre simple (sin separadores de ruta). |
| `PreferredArchitecture` | Imagen preferida cuando la página ofrece varias: `X64` (por defecto), `X86`, `Arm64` o `Any`. |
| `Attempts` | Número de intentos de todo el flujo (por defecto `3`). |
| `RetryDelaySeconds` | Retardo base entre intentos, en segundos; crece de forma exponencial y se limita a 60 s (por defecto `5`). |
| `Headless` | Ejecuta el navegador sin ventana visible (por defecto `true`). |
| `ExperimentalSentinel` | Activa el calentamiento anti-bot experimental (por defecto `false`). |
| `Actions` | Lista ordenada de acciones del navegador para llegar al enlace de descarga. |

Cada acción tiene:

| Campo | Descripción |
| --- | --- |
| `Order` | Orden de ejecución (ascendente). |
| `Kind` | Uno de `Goto`, `SelectOption`, `Click`, `QuerySelector`, `WaitForSelector`. |
| `WaitBeforeAction` | Retardo opcional en **milisegundos** antes de ejecutar la acción. |
| `Optional` | Cuando es `true`, un fallo (por ejemplo, el elemento no existe) se registra y el flujo continúa (por defecto `false`). |
| `Timeout` | Tiempo máximo opcional de la acción, en milisegundos. |
| `Parameters` | Parámetros propios de la acción (`Url`, `Selector`, `Values`). |

Acciones soportadas:

- **`Goto`** – navega a `Parameters.Url`.
- **`SelectOption`** – selecciona `Parameters.Values` en el elemento que
  coincide con `Parameters.Selector`.
- **`Click`** – hace clic en el elemento que coincide con
  `Parameters.Selector`.
- **`QuerySelector`** – guarda el elemento que coincide con
  `Parameters.Selector`; la herramienta lee después su `href` para obtener el
  enlace de la ISO.
- **`WaitForSelector`** – espera a que el elemento que coincide con
  `Parameters.Selector` esté añadido al DOM.

`config.json` declara `"$schema": "./config.schema.json"`, de modo que los
editores con soporte de esquemas JSON ofrecen autocompletado y validación
mientras lo editas.

### Actualizar los valores de las opciones

Microsoft cambia periódicamente los identificadores internos de la edición y del
idioma. Puedes obtener los valores actuales directamente en la página:

1. Abre la [página de descarga de la ISO de Windows 10](https://www.microsoft.com/en-us/software-download/windows10ISO).
2. Abre las herramientas de desarrollador del navegador.
3. Id de edición: inspecciona el `<select>` `#product-edition`. El valor de la
   opción "Windows 10 (multi-edition ISO)" va en la acción `SelectOption` con
   `Order: 3`.
4. Idioma: elige la edición, pulsa **Confirm** y luego inspecciona el
   `<select>` `#product-languages`. Copia el `value` completo de tu idioma
   (tiene el formato `{"id":"16067","language":"English"}`) en la acción
   `SelectOption` con `Order: 5`.

La herramienta es resistente: después de ejecutar las acciones también busca
cualquier enlace `a[href*=".iso"]` en la página, por lo que pequeños cambios en
el marcado normalmente no la rompen. Cuando Microsoft ofrece más de una imagen
—Windows 10 publica una ISO de **32 bits** y otra de **64 bits**— la herramienta
elige automáticamente la que coincide con `PreferredArchitecture` (la de
**64 bits** por defecto; usa `--arch x86` para la de 32 bits).

### Idiomas disponibles

La edición **Windows 10 (multi-edition ISO)** (`2618`) ofrece los siguientes 38
idiomas. Para descargar uno de ellos, sustituye el `Values` de la acción
`SelectOption` con `Order: 5` en `config.json`:

| Idioma | `id` | `Values` (pegar esto) |
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

> Estos `id` pertenecen a la edición de Windows 10 `2618` (la acción
> `SelectOption` con `Order: 3`).
> Los idiomas que puedas echar en falta (por ejemplo indonesio, malayo, persa o
> vietnamita) no se ofrecen para esta edición. La opción más a prueba de futuro
> sigue siendo copiar el `value` completo desde la página, como se describió
> antes.

## Fin de soporte de Windows 10 y actualizaciones de seguridad extendidas

Windows 10 alcanzó el **fin de soporte el 14 de octubre de 2025**: ya no recibe
actualizaciones de seguridad gratuitas y solo el programa **Extended Security
Updates (ESU)** lo mantiene parcheado.

| Etapa | Termina | A quién aplica |
| --- | --- | --- |
| Soporte ordinario | **14 de octubre de 2025** | Todos |
| ESU – Año 1 | **13 de octubre de 2026** | Comercial / educativo |
| ESU – Año 2 | **12 de octubre de 2027** | Consumidores (fin del ESU de consumo); Año 2 para comercial |
| ESU – Año 3 | **10 de octubre de 2028** | Solo comercial / educativo |

- Los **consumidores** pueden inscribirse en el ESU —gratis en el Espacio
  Económico Europeo y con una cuenta de Microsoft / puntos de Microsoft Rewards
  en el resto— y quedan cubiertos hasta el **12 de octubre de 2027**.
- Los clientes **comerciales / educativos** pueden comprar hasta **tres años** de
  ESU: el Año 1 termina el 13 de octubre de 2026, el Año 2 el 12 de octubre de
  2027 y el Año 3 el **10 de octubre de 2028**.
- El ESU contiene **solo** actualizaciones de seguridad críticas e importantes;
  ninguna característica nueva, corrección no de seguridad o cambio de diseño.

> **Advertencia — rotación de certificados de Windows Update.** Los
> certificados que protegen la conexión con Windows Update caducan en **2027**
> (sus reemplazos deben instalarse **antes del 17 de mayo de 2027** o **antes
> del 19 de junio de 2027**, según el producto). Los equipos que nunca reciban
> los certificados de reemplazo —**Windows 7, Windows 8 / 8.1, cualquier
> instalación de Windows 10 sin ESU** y las versiones de Windows Server fuera de
> soporte— **perderán el acceso a Windows Update** y dejarán de recibir
> actualizaciones por completo. Es una rotación de los **certificados raíz** del
> servicio y no tiene relación con esta herramienta. Para seguir actualizado
> hace falta Windows 10 **con ESU** (instalando la actualización mensual, al
> menos la de julio de 2026, antes del 19 de junio de 2027) o un sistema
> operativo soportado (Windows 11 24H2 o posterior, Windows Server 2022 o
> posterior, o las versiones LTSC que Microsoft indica).
>
> Anuncio oficial:
> <https://techcommunity.microsoft.com/blog/windows-itpro-blog/prepare-for-windows-update-certificate-rotation-in-2027/4562463>

## Códigos de salida

| Código | Significado |
| --- | --- |
| `0` | Éxito (ISO descargada, o `--dry-run`/listado de idiomas finalizado). |
| `1` | Error inesperado (línea de comandos incorrecta). |
| `2` | Error de configuración o de estructura de la página (no se encontró el enlace). |
| `3` | Error de red durante la descarga. |
| `4` | Rechazo anti-bot (Sentinel). |
| `5` | Error de disco (permisos, E/S). |
| `6` | Error del navegador (Firefox no pudo arrancar). |
| `130` | Cancelado con `Ctrl+C`. |

## Solución de problemas

- **`Could not launch Firefox` en Linux** – instala las dependencias del sistema
  de Playwright: `playwright install-deps firefox` (ver *Requisitos*).
- **`Download button was not found` / anti-bot** – Microsoft protege el
  conector de descarga con un mecanismo llamado *Sentinel* que puede rechazar
  una sesión del navegador, sobre todo con uso intensivo o automatizado. La
  herramienta reintenta hasta `Attempts` veces (3 por defecto) con una sesión
  nueva y un retardo creciente; si sigue fallando, espera unos minutos e
  inténtalo de nuevo. Ejecutar con `--headed` y `--verbose` muestra lo que hace
  la página.
- **La descarga se interrumpió** – no se pierde nada: los datos parciales quedan
  en `*.part` y la siguiente ejecución los reanuda con una petición HTTP
  `Range`. Borra el archivo `.part` para empezar de cero.
- **Errores de permisos** – asegúrate de que `DownloadFolder` sea escribible. En
  Windows ejecuta la terminal como administrador; en Linux revisa los permisos
  de la carpeta.
- **La página cambió** – usa `--dry-run --headed` para ver la automatización,
  guarda una captura/HTML de diagnóstico (se escriben en `diagnostics/` cuando
  una ejecución falla) y ajusta `config.json` en consecuencia.

## Licencia

Consulta [LICENSE](LICENSE).




## Cierre de Windows Update — notas prácticas

> Notas personales recopiladas por el mantenedor el 12 de octubre de 2026.
> Complementan el resumen oficial de arriba con soluciones prácticas y sus
> contrapartidas; no son indicaciones oficiales de Microsoft.

ATENCION, AVISO (12 de octubre de 2026): microsoft cortara el acceso a partir
del 19 de junio del 2027 el acceso a windows update a todos los equipos que
tengan windows 10 sin ESU hacia abajo (el bloqueo anterior fue hasta windows
vista-server 2008 sin R2), esto logicamente afecta a las versiones de servidor
(server 2008 R2 hasta server 2019, a partir de server 2022, no afecta el cierre
de windows update), esto es debido a la caducidad de los certificados raiz
del servicio de actualizaciones que caduca a partir de esta fecha, si quieres
echar un vistazo al link oficial de la noticia de microsoft, aqui te lo paso (usa la traduccion del navegador, esta en ingles):

https://techcommunity.microsoft.com/blog/windows-itpro-blog/prepare-for-windows-update-certificate-rotation-in-2027/4562463

recuerda que los certificados raiz forman parte de la navegacion por internet, una de las caracteristicas del protocolo https
es que ahora todo lo que se transfiere al router esta cifrado (anteriormente no era asi), pero para que programas que se uti
lizan para analizar todo lo que se transfiere en una red domestica como wireshark no surta ningun efecto (no se vea ningun
dato sensible), los certificados raiz deben de funcionar y estar al dia, si no funciona, da error de certificado en el navegador
y los programas que conectan a internet dejaran de funcionar, esto lo tienes que tener en cuenta a la hora de utilizar un windows
antiguo instalado en el ordenador (por cierto, el cierre de windows update tambien afecta a las maquinas virtuales, o sea, a los PCs
arrancados dentro de un equipo fisico).

cuando llegue la fecha de caducidad del certificado raiz es posible que en los offices instalados en windows sin soporte den errores
de comprobacion de actualizaciones (hasta 10 y server 2019 que son cliente y servidor respectivamente se puede instalar el 2021-365
7 y 8, hasta office 2016), solucionandose esto desactivando las actualizaciones de office (cuando llegue la fecha de caducidad de
dichos certificados raiz).

en la ventana anterior (las actualizaciones automaticas se impusieron a partir de windows 2000) no dio ningun tipo de fallo a la
hora de arrancar el sistema operativo, unicamente a la hora de abrir la pagina de microsoft update o iniciar el windows update.
(pase la experiencia de la ventana anterior de cierre del update).

en el caso de que sea windows 10 con ESU, windows 11 24H2, servers LTSC de 2019 y 2016 y server 2022, microsoft ha puesto como
metodo oficial para solucionar el problema, la actualizacion, o sea, que tendras que actualizar el segundo martes de cada mes
hasta la fecha del 19 de junio de 2027 en el cual ya...piano, piano (entenderas lo que te digo).

en el caso de que te encuentres con este tipo de equipos, estos son los caminos que tienes:

-  comprar el ESU (windows 10) (en el caso de paises de la union europea, no tiene ningun coste) 
-  parchear el update para acceder al ESU, que el unico parche (y recomendable para windows 10) es el MAS (microsoft activation script)
-  instalar un parche no oficial para actualizar los certificados raiz, que es el legacy update
-  en el caso de versiones compatibles (no es obligatorio por el momento a partir de 11 24H2, windows 10 sin ESU es imprescindible
a medias) aceptar las actualizaciones a versiones mayores de windows via update o haciendo una actualizacion desde los medios de
instalacion de windows.
-  mantener la instalacion de windows y hacer un dual boot con linux (no aconsejable).
-  desinstalar windows e instalar linux junto con una maquina virtual de windows (ahi se permite todo, desde originales hasta modificados)

en el caso de que elijas la opcion sensata (la ultima), recuerda que debes de montar vmware o virtualbox en linux (instala linux en el ordenador
logicamente) y la maquina virtual con el windows a elegir (que ahi si puedes instalar windows antiguos) de manera oficial hasta junio de 2027, posteriormente
no va a funcionar y solo vas a poder instalar y actualizar este tipo de PCs por metodos no oficiales.

en el caso de que hayas perdido tu empleo o el curso al haberle dicho este inconveniente, mucha suerte en tu busqueda de empleo o en la busqueda de curso
(que espero que lo encuentres), con todas estas pistas que te digo ya veras como seguro al final encuentras el empleo o curso soñado.
