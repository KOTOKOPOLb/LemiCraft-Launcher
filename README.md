<p align="center">
  <img src="assets/logo_rounded.svg" alt="LemiCraft" width="120"/>
</p>

<h3 align="center">Официальный лаунчер для Minecraft проекта LemiCraft</h3>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.6.0-blue" alt="version"/>
  <img src="https://img.shields.io/badge/.NET-8.0-purple" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/platform-Windows-lightgrey" alt="Windows"/>
</p>

---

## Возможности

- 🎮 **Автоматическая установка** модпаков и обновлений
- 🔐 **Авторизация** через Microsoft и Ely.by с поддержкой authlib-injector
- 📦 **Импорт модпаков** по коду или через ссылку `lemicraft://`
- 📰 **Новости** и обновления прямо в лаунчере
- ⚙️ **Гибкие настройки** Java, RAM, пути установки
- 📊 **Мониторинг** статуса сервера в реальном времени
- 🎨 **Современный UI** с темной темой и плавными анимациями
- 👤 **Смена скина** прямо из лаунчера
- 🛡️ **Анализатор крашей** - при аварийном закрытии Minecraft предлагает открыть лог

## Установка

**Через установщик (рекомендуется)**

Скачайте `LemiCraft_Installer.exe` со страницы [Releases](../../releases/latest), запустите и следуйте инструкциям

**Портативная версия**

Скачайте `LemiCraft_Launcher.exe` - запускается без установки, можно использовать с флешки

## Сборка из исходников

Требования: Visual Studio 22+, .NET 8 SDK, Inno Setup 6 (для сборки установщика)

```
git clone https://github.com/KOTOKOPOLb/LemiCraft-Launcher
cd lemicraft-launcher
```

**Запуск в режиме отладки**

Откройте `LemiCraft Launcher.sln` в Visual Studio, выберите конфигурацию Debug и нажмите F5.

**Сборка релиза**

```
# Портативная версия
dotnet publish "LemiCraft Launcher/LemiCraft Launcher.csproj" -p:PublishProfile=Portable

# Установщик (автоматически запускает Inno Setup)
dotnet publish "LemiCraft Launcher/LemiCraft Launcher.csproj" -p:PublishProfile=Installer
```

Или одной командой через `build.bat` - собирает обе версии и кладёт их в `publish/dist/`.

## Стек

| | |
|---|---|
| Runtime | .NET 8.0, WPF + WinForms |
| Minecraft | CmlLib.Core 4.x |
| Авторизация | Microsoft Identity Client, Ely.by API, authlib-injector |
| Новости | Markdig (Markdown) |
| Установщик | Inno Setup 6 |

## Ссылки

[Сайт](https://lemicraft.ru) · [Discord](https://discord.gg/ybC6QM8WTM) · [Wiki](https://wiki.lemicraft.ru) · [Правила](https://lemicraft.ru/rules)
