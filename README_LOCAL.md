# Как поднять проект локально (памятка)

## Что уже сделано на этом ПК
1. Клонирован репозиторий (ветка `Map`).
2. Установлен Unity Hub + редактор **2022.3.50f1** (точная версия из `ProjectSettings/ProjectVersion.txt`).
3. Исправлены мелкие баги в скриптах:
   - `CityGraph` — добавлен `[DefaultExecutionOrder(-100)]`, чтобы граф генерировался ДО старта DemoController (иначе при запуске сцена пустая);
   - `DemoController` — повторная генерация графа больше не наслаивает старые узлы/рёбра (всё предыдущее удаляется), время сбрасывается через `TimeCalculator.ResetTime()`;
   - `UIManager.UpdateSourceList` — TMP-текст ищется через `GetComponentInChildren` (не падает, если текст не на корне префаба).
4. Добавлен `Assets/Editor/SceneBuilder.cs` — авто-сборщик сцены.

## Что делает SceneBuilder (Tools → «Собрать демо-сцену»)
- Импортирует **TMP Essentials** (именно их нет в git — «textMesh картинки», шрифтовые атласы Unity генерирует локально).
- Создаёт **динамический шрифт Arial с кириллицей** (`Assets/Fonts/ArialCyrillic Dynamic SDF.asset`) + запасной Segoe UI Symbol для символов `✓ ✗ ○ ⚠ α`. Динамический атлас сам подтягивает нужные символы по мере появления текста.
- Создаёт префабы: `NodePrefab` (сфера + коллайдер + подписи), `EdgePrefab` (LineRenderer), `PlayerPrefab`, `SourceListItemPrefab`.
- Собирает `Assets/Scenes/SampleScene.unity`: объект `Managers` (CityGraph, PathfindingAlgorithm, SpawnValidator, TimeCalculator, DemoController), Canvas с инфопанелью, статусом, предупреждениями, кнопками «Сгенерировать / Проверить спавн / Завершить» (кнопки уже подключены к методам DemoController), список источников справа, камера наведена на карту.

## Как запустить
1. Открыть Unity Hub → проект `Project-Management-Game` (редактор 2022.3.50f1).
2. Дождаться компиляции.
3. Открыть `Assets/Scenes/SampleScene.unity` (если ещё не открыта).
4. Нажать **Play**. При старте сгенерируется граф: узлы, рёбра, игрок на стартовой вершине.
5. Кнопки: «Сгенерировать» — новый граф; «Проверить спавн» — валидация и подсчёт α; клик по соседнему узлу — перемещение игрока; «Завершить» — итоговый результат в %.

## Если русский текст отображается квадратиками
- Проверь, что у всех TMP-текстов назначен шрифт `ArialCyrillic Dynamic SDF`
  (или что он выбран как Default Font Asset в Project Settings → TextMesh Pro).
- Запусти Tools → «Собрать демо-сцену» заново — она переназначит шрифт везде.
