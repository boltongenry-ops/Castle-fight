# Лаунчер Castle Fight

Нативное окно Windows: панорама замка, отдельная панель аккаунта, золотая кнопка запуска и собственная шкала обновления. Окно поддерживает системный масштаб DPI, уменьшает размер под рабочую область экрана, перетаскивается за фон и закрывается по Escape. В полях Enter выполняет вход; вне полей Enter запускает готовую игру. Tab переключает элементы по порядку.

`LauncherTheme.cs` содержит оформление и кнопки. `Launcher.cs` отвечает за окно, аккаунт и запуск; `UpdateCore.cs` — за проверку, докачку и установку файлов. Для нового оформления сервер и игровые ресурсы не меняются. Сохранённый вход защищён DPAPI, гостевой запуск не передаёт его токен.

Сборка в Developer Command Prompt с .NET Framework 4.8 Developer Pack:

```bat
msbuild CastleFight-Launcher.csproj /t:Rebuild /p:Configuration=Release
```

Фон и WarcraftRussian.ttf встраиваются из обычных файлов UnityProject; восстановление архивных анимаций для сборки одного лаунчера не требуется.

Проверка настоящего окна без сетевых запросов и без чтения сохранённого аккаунта:

```bat
CastleFight-Launcher.exe --preview C:\Temp\launcher.png ready
```

Вместо `ready` доступны `loading`, `error`, `account`, `compact` (80%) и `dpi150` (150%). Это статические проверочные состояния настоящего WinForms-окна.

`Tests/LauncherDesignChecks.cs` проверяет уже скомпилированный EXE через reflection, геометрию элементов, переключение аккаунта, маску пароля, порядок клавиатуры, локальную валидацию и реальный гостевой/обычный запуск безопасного тестового процесса. В тестах используется отдельная папка с фиктивным аккаунтом.

```bat
csc /nologo /target:exe /out:LauncherFixturePlayer.exe Tests\LauncherFixturePlayer.cs
csc /nologo /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll /out:LauncherDesignChecks.exe Tests\LauncherDesignChecks.cs
LauncherDesignChecks.exe CastleFight-Launcher.exe C:\Temp\CastleFight-Launcher-QA C:\Temp\launcher-report.json LauncherFixturePlayer.exe
```

Эта проверка создаёт фиктивную игру в указанной QA-папке; не указывайте настоящую папку установки. Тесты докачки и входа на изолированном серверном QA-аккаунте выполняются отдельно.
