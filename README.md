# JellyLiveNow

Plugin para Jellyfin Server **10.10.7 / .NET 8** que detecta si existe una reproducción Live TV activa y publica un canal nativo llamado **Viendo en TV**.

## Funcionamiento

- Detecta únicamente elementos `LiveTvChannel` / `LiveTvProgram` de sesiones activas.
- No confunde películas, episodios o audio con Live TV.
- Publica como máximo una entrada activa.
- Cuando no hay Live TV activa, el proveedor no devuelve contenido.
- No expone usuario, IP, dispositivo ni número de espectadores.
- Implementa `IChannel` + `IRequiresMediaInfoCallback` y registra el proveedor como `IChannel` en el contenedor DI de Jellyfin.
- Para reproducir, resuelve el `LiveTvChannel` real mediante `IMediaSourceManager.GetPlaybackMediaSources`, de forma que Jellyfin pueda aportar sus fuentes dinámicas y `OpenToken` de Live TV.
- Si existe imagen primaria del canal, la API auxiliar devuelve la ruta HTTP de Jellyfin; nunca expone una ruta física del servidor.

## Compatibilidad objetivo

Servidor: Jellyfin 10.10.7. Runtime: .NET 8. El canal usa APIs nativas del servidor y está pensado para clientes que muestran los Channels de Jellyfin, incluido Android TV. La presentación exacta depende del cliente oficial y de cómo ese cliente exponga Channels; el plugin no modifica ni inyecta código en Android TV, Android móvil o Jellyfin Web.

## Instalación por repositorio

En **Panel de Control → Plugins → Repositorios**, añade únicamente el repositorio unificado ODOS3D:

```text
https://raw.githubusercontent.com/odoslf/Repositorio-plugin-Jelly-fin-odos3d.lab/main/manifest.json
```

Después instala **JellyLiveNow** desde el catálogo y reinicia Jellyfin.

## API auxiliar

`GET /JellyLiveNow/Status` (alias `/ActiveChannel`) devuelve el estado activo para un usuario autenticado. `POST /JellyLiveNow/Dismiss` permite marcar el aviso como descartado para ese usuario durante la emisión actual. Esta API queda disponible para una interfaz que quiera consumirla, pero **la versión actual no inyecta automáticamente un banner en los clientes oficiales**.

## Configuración

Panel de Control → Plugins → JellyLiveNow. Permite activar/desactivar el plugin y cambiar el nombre del canal. La opción histórica de Web banner se conserva por compatibilidad de configuración/API, pero no implica inyección automática en Jellyfin Web.

## Desarrollo y verificación

GitHub Actions restaura dependencias, compila con warnings como error, ejecuta tests, audita dependencias, empaqueta únicamente `JellyLiveNow.dll` y arranca el paquete resultante en la imagen oficial `jellyfin/jellyfin:10.10.7`. La versión estable actual es **1.0.3.0**.

## Instalación manual

Descarga el ZIP de la release correspondiente, extráelo en una carpeta `JellyLiveNow` dentro del directorio de plugins de Jellyfin y reinicia el servidor. No mezcles DLL de versiones anteriores en la misma carpeta.
