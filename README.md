# LemiCraft Launcher

<p align="center">
  <img src="assets/logo_rounded.svg" width="96" height="96" alt="LemiCraft"/>
</p>

<p align="center">
  Официальный лаунчер для Minecraft сервера <a href="https://lemicraft.ru">LemiCraft</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.6.0-blue" alt="version"/>
  <img src="https://img.shields.io/badge/.NET-8.0-purple" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/platform-Windows-lightgrey" alt="Windows"/>
</p>

---

## Возможности

- **Авторизация** - Microsoft и Ely.by с поддержкой authlib-injector
- **Автоустановка модпаков** - скачивание, обновление и управление версиями
- **Импорт модов** - по коду или из каталога
- **Новости** - лента с поддержкой Markdown прямо в лаунчере
- **Скины** - просмотр, загрузка и управление через библиотеку скинов
- **Мониторинг сервера** - статус онлайн в реальном времени
- **Автообновление** - лаунчер и модпак обновляются автоматически
- **Анализатор крашей** - при аварийном закрытии Minecraft предлагает просмотреть лог
- **Гибкие настройки** - RAM, JVM-аргументы, путь Java, путь установки игры

## Установка

**Через установщик (рекомендуется)**

Скачайте `LemiCraft_Installer.exe` со страницы [Releases](../../releases/latest), запустите и следуйте инструкциям

**Портативная версия**

Скачайте `LemiCraft_Launcher_*_portable.exe` - запускается без установки, можно использовать портативно

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
