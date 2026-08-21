# JellyLiveNow

**JellyLiveNow** es un plugin independiente para Jellyfin Server (v10.10.7, .NET 8) que permite a los usuarios del servidor descubrir cuándo alguien está viendo un canal de televisión en directo (Live TV / HDHomeRun) y unirse a esa emisión en directo de manera transparente e instantánea.

---

## 🚀 Características principales

- **Viendo en TV**: Muestra de forma nativa un contenedor/canal con el canal Live TV que se está emitiendo actualmente en el servidor.
- **Unirse con un clic**: Al pulsar en la tarjeta del canal, el cliente de Jellyfin (Android TV, Web, Android Móvil, etc.) inicia su propia reproducción nativa del mismo canal real de Live TV en Jellyfin.
- **Regla de oro (Solo un canal)**: Se muestra como máximo **un único canal activo**. Si varios usuarios están viendo ese mismo canal, aparece una sola entrada en la interfaz.
- **Ocultamiento automático**: Si nadie está reproduciendo televisión en directo, "Viendo en TV" desaparece por completo sin dejar tarjetas vacías ni mensajes.
- **Privacidad deliberada**: No muestra nombres de usuario, número de espectadores, avatares ni quién inició la emisión.
- **Integración Nativa para Android TV**: Utiliza la arquitectura de canales nativos de Jellyfin (`IChannel`), permitiendo que el cliente oficial de Jellyfin para Android TV navegue y reproduzca con el mando a distancia sin hacks de frontend ni JS inyectado.
- **Soporte de Banner para clientes Web**: Incluye un aviso opcional en Jellyfin Web con opción de descarte temporal (la "X") para esa sesión/emisión.

---

## 🛠️ Requisitos del sistema

- **Jellyfin Server**: 10.10.7
- **Runtime**: .NET 8
- **Live TV**: HDHomeRun (o cualquier sintonizador de Live TV configurado en Jellyfin)
- **Clientes soportados**:
  - Jellyfin Android TV (Nativo)
  - Jellyfin Web
  - Jellyfin Android / iOS / Desktop

---

## 📦 Instalación

1. Descarga el archivo `JellyLiveNow_1.0.0.0.zip` de las [Releases](https://github.com/odoslf/Jellyfin-JellyLiveNow/releases).
2. Extrae el contenido en el directorio de plugins de tu servidor Jellyfin:
   - Linux: `/var/lib/jellyfin/plugins/JellyLiveNow/`
   - Docker: `/config/plugins/JellyLiveNow/`
   - Windows: `%ProgramData%\Jellyfin\Server\plugins\JellyLiveNow\`
3. Reinicia Jellyfin Server.

Alternativamente, añade el manifiesto `manifest.json` al catálogo de repositorios de Jellyfin.

---

## ⚙️ Configuración

Accede a **Panel de Control -> Plugins -> JellyLiveNow**:

- **Enable JellyLiveNow**: Activa o desactiva la funcionalidad general del plugin.
- **Enable Web banner**: Activa o desactiva el banner informativo superior en el cliente Jellyfin Web.
- **Nombre del canal**: Personaliza el nombre del contenedor (por defecto: `Viendo en TV`).

---

## 📱 Compatibilidad y funcionamiento por cliente

| Cliente | Navegación Nativa | Reproductor Nativo | Banner Superior |
| :--- | :---: | :---: | :---: |
| **Android TV Oficial** | ✅ Sí | ✅ Sí (Directo) | N/A (Usa contenedor nativo) |
| **Jellyfin Web** | ✅ Sí | ✅ Sí | ✅ Sí (con opción de descarte `✕`) |
| **Jellyfin Android Móvil** | ✅ Sí | ✅ Sí | ✅ Sí |

---

## ℹ️ Limitaciones reales y privacidad

1. **Un solo canal a la vez**: Por diseño, se promociona un único canal activo en el servidor.
2. **Sin streaming duplicado**: JellyLiveNow no retransmite ni duplica el stream del primer usuario; el segundo usuario abre una sesión de reproducción estándar sobre el canal Live TV de Jellyfin.
3. **Privacidad**: No se registra ni se expone el historial de reproducciones, UserId, IP ni datos del dispositivo.
