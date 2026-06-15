# Документация проекта RORTYPE

## Обзор

`RORTYPE` сейчас представляет собой Unity-проект на стадии раннего gameplay-прототипа. Репозиторий уже содержит собственное `top-down` движение, combat sandbox, MVP-врагов, порталы, магазины, контейнеры, pickups, миникарту, часть маршрутных интерактивов и несколько gameplay-сцен. Импортированный пакет сторонних контроллеров движения/камеры остается в проекте как внешний ассет, но не является основой текущего игрока.

Основное целевое видение игры и уровней описано в документе `Forest Rises 112.1.6 Дизайн Уровней.docx`, который задает структуру мира, список интерактивов, навигацию, прогрессию и параметры трех основных локаций. Этот документ описывает целевой дизайн, а не гарантирует наличие всех систем в Unity. Поэтому любые дальнейшие задачи нужно рассматривать в двух плоскостях:

- что уже реально существует в Unity-проекте
- что должно быть реализовано по дизайн-документу

## Источники правды

### Текущая реализация

- Основные существующие сцены: `Assets/Game/Scene/Level_1.unity`, `Assets/Game/Scene/Level_2.unity`, `Assets/Game/Scene/Hub_1.unity`, `Assets/Game/Scene/PlayerMovementTest.unity`
- Важно: `ProjectSettings/EditorBuildSettings.asset` также ссылается на `Hub_2.unity` и `Level_3.unity`, но эти `.unity` файлы сейчас не найдены в `Assets/Game/Scene`
- Настройки проекта: `ProjectSettings/`
- Пакеты Unity: `Packages/manifest.json`

### Целевой дизайн

- Исходный документ: `C:\Users\senne\Downloads\Forest Rises 112.1.6 Дизайн Уровней.docx`

### Постоянный контекст проекта

- Память проекта: `docs/project-memory.md`
- Инструкции для агентов: `AGENTS.md`

## Техническая база

- Движок: Unity `6000.3.10f1`
- Из используемых пакетов явно подключены:
  - `com.unity.probuilder`
  - `com.unity.multiplayer.center`
  - стандартные модули Unity

## Структура репозитория

### `Assets/Game`

Проектная зона игры в текущем состоянии уже включает основные прототипные gameplay-системы:

- `Scene/Level_1.unity` — основная gameplay-сцена/грейбокс для первого вертикального среза
- `Scene/Level_2.unity` — вторая gameplay-сцена
- `Scene/Hub_1.unity` — существующая хаб-сцена
- `Scene/PlayerMovementTest.unity` — отдельная тестовая сцена для проверки движения игрока
- `Scripts/Player/` — собственные runtime-скрипты `top-down` движения и камеры
- `Scripts/Combat/`, `Scripts/AI/`, `Scripts/Interaction/`, `Scripts/UI/`, `Scripts/Environment/` — текущие gameplay-системы прототипа
- `Prefabs/Player/TopDownPlayer.prefab` — базовый prefab игрока под новое движение
- `Prefabs/Enemies/` — MVP-враги для ручной расстановки и spawn zones
- `Prefabs/PointOfInterest/` — порталы, магазины, контейнеры, двери, totem-door, elevator и destructible объекты
- `Prefabs/UI/InteractionUi.prefab` и `Prefabs/UI/Minimap.prefab` — authored UI-prefabs для сцен
- `Other/Mat_red.mat` и `Other/Mat_yelow.mat` — базовые материалы

### `Assets/Plagin/Player Movement and Camera`

Импортированный внешний пакет, содержащий:

- контроллеры движения игрока для разных режимов
- скрипты камеры
- анимационные контроллеры
- префабы
- демо-сцены
- документацию ассета

По данным `.meta` это пакет `Player Movements, Camera Control and More` версии `5.0`.

## Текущее состояние сцен

`Level_1.unity` больше не следует описывать как сцену без gameplay-логики: в проекте уже существуют и подключаются собственный игрок, combat/AI-системы, интерактивы, миникарта и authored interaction UI. При этом `Level_1` все еще остается прототипной сценой, а не финальной production-локацией из дизайн-документа.

Существующие `.unity` файлы в `Assets/Game/Scene`:

- `PlayerMovementTest.unity` — тест движения и быстрых проверок игрока
- `Level_1.unity` — основная сцена для первого vertical slice
- `Hub_1.unity` — текущий хабовый шаг
- `Level_2.unity` — вторая gameplay-сцена

Найденная нестыковка:

- `ProjectSettings/EditorBuildSettings.asset` содержит ссылки на `Assets/Game/Scene/Hub_2.unity` и `Assets/Game/Scene/Level_3.unity`, но таких `.unity` файлов сейчас нет в папке сцен. Нужно либо восстановить/создать эти сцены, либо убрать ссылки из build settings и временно сузить portal travel loop.

## Игровая структура по дизайн-документу

Дизайн-документ задает следующую игру:

- 3 основные боевые локации
- 2 под-локации/варианта хаба
- автосохранение
- перенос прогресса персонажа между локациями
- усиление монстров при переходе между локациями
- линейно-хабовую структуру прохождения

### Хабы

Предусмотрены два варианта:

- обычный хаб — закрытый круговой закуток без мобов, с торговцем и наковальней
- усиленный хаб — к обычному хабу добавляется торговец черного рынка

Попадание в хаб может происходить случайно раз в несколько уровней, иначе игрок переходит сразу на следующий уровень.

## Системы и механики по дизайну

### Интерактивность

На уровнях должны существовать:

- порталы
- торговец
- кузнец
- капсулы
- сундуки
- капсулы модификаций

Терминал-портал должен выполнять две функции:

- запускать задачу/квест уровня
- переводить игрока на следующий уровень

### Навигация

Дизайн требует наличия нескольких слоев навигационной поддержки:

- мини-карта
- миниатюра уровня
- маркеры/подсказки
- квестовые точки
- компас

Эти системы должны вести игрока к:

- боссам
- капсулам
- торговцу/кузнецу
- порталу перехода

Быстрое перемещение не предусмотрено.

### Текущая реализация миникарты на 2026-06-15

В репозиторий добавлен код миникарты как отдельной scene-bound UI-системы:

- `Assets/Game/Scripts/UI/MinimapController.cs`
- `Assets/Game/Scripts/UI/MinimapTrackable.cs`
- `Assets/Game/Scripts/UI/MinimapMarkerGraphic.cs`
- `Assets/Game/Editor/MinimapBuilder.cs`

Принята такая рабочая схема:

- миникарта живёт как prefab в сцене, в правом верхнем углу
- каждая сцена должна использовать свою картинку карты
- размер карты в мировых метрах задаётся через сериализованные поля
- маркеры объектов задаются в prefab самих world-объектов через `MinimapTrackable`
- новые UI-маркеры не должны создаваться через runtime instantiate; вместо этого используется заранее подготовленный пул слотов
- рисование маркеров делается через `MinimapMarkerGraphic`, то есть без внешних scene-specific icon sprite для trackable-объектов

Для первого боевого уровня стартово принимались такие данные:

- картинка карты: `Assets/Game/Minimap_var_2.png`
- размер prefab миникарты: `500 x 500` UI-единиц
- прозрачность изображения карты: `0.75`
- стартовый размер мира в контроллере prefab: `500 x 500` метров, но scene instance в `Level_1` дополнительно получает рассчитанные bounds по текущим trackable-объектам
- маркер игрока рисуется отдельным player marker slot
- world marker slots сейчас ограничены `MinimapIconGroup.PointOfInterest`
- двери остаются валидными POI-маркерами и отображаются красными прямоугольниками, которые могут учитывать yaw и world bounds
- destructible environment objects (`DestructibleCover`, `DestructibleLootContainer`, `ExplosiveBarrel`) не должны появляться на миникарте даже при случайно добавленном `MinimapTrackable`
- старые правила про видимые маркеры врагов, сундуков и капсул считаются устаревшими

Важно: `Assets/Game/Prefabs/UI/Minimap.prefab` существует как scene-bound UI prefab. `MinimapBuilder` больше не должен быть обычным способом настройки новых сцен и больше не конфигурирует enemy/chest/capsule markers; дальнейшая работа должна выполняться вручную через scene-local instance, корректные bounds/картинку карты и явно настроенные `MinimapTrackable` только там, где они нужны.

Обновленное правило: дальнейшая настройка сцен больше не должна опираться на новые editor-builder scripts, runtime-builder scripts или генерацию в Play Mode. Сцены настраиваются руками прямо в Unity-сцене, prefabs создаются и обновляются вручную, а serialized references связываются явно. Для миникарты это означает, что каждый уровень должен иметь собственный настроенный scene instance: корректную картинку/границы карты, player marker setup и POI-trackables для порталов, магазинов, кузницы, дверей и других навигационно важных объектов. Простое копирование `MinimapCanvas` из другой сцены не считается завершенной настройкой.

### Действия игрока

Базовый набор:

- бег
- прыжок
- рывок / перекат / ускорение
- ближний бой
- дальний бой
- применение способностей
- комбинирование боя и перемещения

Дополнительные действия:

- подбор предметов
- открытие капсул
- торговля
- крафт
- сбор руды с противников
- покупка рецептов
- разбор предметов на ресурс

### Экономика

Основные ресурсы:

- здоровье
- руда
- опыт

Источники наград:

- убийства противников
- выполнение задач уровня
- содержимое капсул и сундуков

## Основной геймплейный цикл

Из дизайн-документа следует такой цикл:

1. Исследование уровня и скрытых закутков
2. Поиск ключевых интерактивов и точек интереса
3. Активация задачи уровня через терминал-портал
4. Бой с противниками и боссами
5. Получение лута, опыта и ресурсов
6. Переход в хаб или на следующий уровень

## Локации

## 1. Лесной водоворот

### Роль

Закрытая боевая локация с рандомной основной задачей и выраженным акцентом на вертикальную мобильность.

### Пространственная идея

- уровень имеет округлую форму
- основа уровня — спиралевидная гора/холм
- основной маршрут ведет от берега по широкой спирали вверх
- на вершине пространство переворачивается внутрь и превращается во внутренний котлован

### Дизайн маршрутов

- обязательны скрытые пути
- обязательны альтернативные пути
- при падении с основной тропы игрок не должен безнадежно застревать
- обходной путь должен быть длиннее или сложнее, но возвращать игрока на основной маршрут и вознаграждать лутом

### Визуальное развитие

- низ: зеленая природная зона, бамбук, дерево, тропы из покраса, досок и камней
- середина: выцветшая болотная растительность, меньше травы и цветов, появляются техно-элементы
- пик: отсутствие растительности, металл, трубы, панели, оголенная порода

### Ключевой левел-дизайн акцент

- прыжковые секции
- пропасти
- переходы по ландшафтным элементам
- нижние тропы для возврата после падения

## 2. Захваченный порт

### Роль

Закрытая боевая локация с акцентом на плоские островные секции и джамп-пазлы.

### Пространственная идея

- форма близка к прямоугольной
- уровень состоит из отдельных островов
- игрок перемещается между островами прыжками

### Поведение при ошибке

- падение в воду не убивает игрока
- игрок переносится на ближайшую точку у ближайшего острова

### Визуальное развитие

- разрушенный техногенный архипелаг
- мало растительности
- доминируют серые, болотные, коричневые, темные оттенки
- техно-пропсы должны объяснять разрушение исходного массива острова

### Ключевой левел-дизайн акцент

- уровень почти плоский
- вокруг основного маршрута нужны малозаметные островки со скрытым лутом

## 3. Лесной простор

### Роль

Закрытая боевая локация большей площади, чем первые две, со структурой условных двух этажей.

### Пространственная идея

- форма смешивает прямоугольник и круг
- первый этаж — длинная природная зона
- второй этаж — верхняя техно-зона

### Связи между этажами

- переходы на второй этаж расположены по бокам
- один мост цельный
- второй мост разрушен и работает как большой джамп-пазл

### Визуальное развитие

- нижний слой — насыщенный лес с мягкими холмами, деревьями и горным массивом
- верхний слой — техно-постройки, блеклая растительность, панели и темно-серая почва вместо обычных троп

### Ключевой левел-дизайн акцент

- площадь и читаемость уровня важнее, чем сложные альтернативные маршруты

## Противники

Документ перечисляет общий пул противников для локаций:

- Пехотинец
- Пехотинец-стрелок
- Пехотинец-ученый
- Взрыв-бот
- Бот-отравитель
- Турель
- Ходячая турель
- Орбитальная турель
- Мех-молотильщик
- Мех-обезьяна
- Мех-дракон
- `G0Le-M`
- `ShAR-1k`
- `GR1-F0n`
- `DR1a-MECH`
- `d-EE/r`
- `sPaRKLE`

## Текущие пробелы реализации

На текущем состоянии репозитория уже найдены и частично реализованы собственная gameplay-логика, враги, спавнеры, хабовая сцена, магазины, контейнеры, порталы, миникарта и часть маршрутных интерактивов. Оставшиеся крупные пробелы:

- цельная задача/квест уровня через терминал-портал
- завершенный encounter loop с состояниями `активация -> бой -> награда -> переход`
- автосохранение и полноценный persistent run state
- опыт/уровень и долгосрочная прогрессия
- финальная сеть сцен: `Hub_2` и `Level_3` сейчас упомянуты в build settings, но их `.unity` файлы не найдены
- production-layout трех целевых локаций из дизайн-документа
- полный пул противников из дизайн-документа
- VFX/SFX и системный QA/playtest

## Собственная система движения игрока

В проекте реализован первый проектный инкремент `top-down` управления без использования импортированного ассет-пака:

- `TopDownInputAdapter` — чтение ввода `Horizontal/Vertical`, `WASD`, sprint, прыжка, dash, стрельбы, ближнего боя и позиции курсора
- `TopDownGroundProbe` — проверка поверхности под игроком и базовое определение уклона
- `TopDownPlayerMotor` — planar movement на `Rigidbody`, sprint, прыжок, dash, ускорение/торможение и устойчивость на простом рельефе
- `TopDownFacingController` — разворот персонажа к курсору мыши на горизонтальной плоскости, спавн простого сферического снаряда для `Left Mouse Button`, runtime-кулаки из двух красных сфер для `Right Mouse Button` с чередованием ударов при удержании и общий `bounce`-эффект персонажа на атаке
- `TopDownProjectileSphere` — runtime-снаряд без гравитации с красным material tint, усиленным `stretch/squash` эффектом на старте и возвратом к базовому масштабу
- `TopDownCameraRig` — следящая наклонная `top-down` камера
- `PlayerRespawnController` — возврат на последнюю безопасную точку при падении ниже порога

Этот инкремент подключен в сцене `PlayerMovementTest.unity` и оформлен отдельным prefab `TopDownPlayer.prefab`.

Важное ограничение текущей реализации:

- `dash` и прыжок уже добавлены в текущий тестовый контроллер как часть `TopDownPlayerMotor`, а не как отдельные ability-компоненты
- На 2026-06-15 `dash` и прыжок в `TopDownPlayerMotor` являются distance-controlled действиями, а не остаточной физической инерцией: принятые значения `jumpDistance = 8m`, `jumpDuration = 0.5s`, `jumpArcHeight = 4m`, `dashDistance = 6m`, `dashDuration = 0.18s`. Рывок во время прыжка добавляет свою дистанцию, а после завершения действий planar/external carry-over velocity сбрасывается. На подъёмах прыжок и рывок используют горизонтальное XZ-направление действия, а высоту берут из stable-ground sampling с allowance на перепад рампы за один action-step.
- Поле `jumpDistance` выделено желтым label в Inspector через `YellowInspectorLabelAttribute`, а `jumpArcHeight` выделено красным label через `RedInspectorLabelAttribute`, чтобы настройки на `TopDownPlayer.prefab` было проще найти.
- Высота прыжка считается от нижней точки капсулы/ног относительно grounded body position в момент старта прыжка, а не по макушке визуальной модели.
- Squash/stretch игрока для прыжка, dash и приземления рассчитывает `TopDownPlayerMotor`; `TopDownFacingController` умножает этот множитель на существующий bounce от атаки. Пока активен dash, визуальный squash dash имеет приоритет над squash прыжка.
- Приземление отделено от старта прыжка: обычное приземление после прыжка дает слабый squash, а падение с высоты `>= 1m` запускает более сильный landing squash (`fallLandingHeightSquash = 0.33`).
- `Rigidbody.useGravity` для этого контроллера должен быть выключен, потому что вертикальное движение и snap к земле управляются кодом
- боевая система уже вышла за рамки раннего теста: есть player damage, enemy damage, enemy prefabs, spawn zones, hit feedback, pickups и runtime cleanup; следующий уровень работы — собрать эти элементы в завершенный encounter loop
- анимационные системы с новым мотором пока не интегрированы

## Практический вывод для следующих задач

Если задача касается разработки, сначала нужно определить, к какому из двух слоев она относится:

- документирование и уточнение целевого дизайна
- реализация конкретного куска прототипа в Unity

Если задача про изменение сцены или систем, нужно явно фиксировать:

- что берется из дизайн-документа
- что уже реально существует в проекте
- какие решения принимаются как новое рабочее состояние проекта

## Связанные roadmap-файлы

Для движения игрока принят отдельный рабочий план:

- `docs/player-movement-roadmap.md`

Для общей реализации проекта принят отдельный roadmap:

- `docs/implementation-roadmap.md`

Эти roadmap-файлы задают:

- общий порядок реализации проекта
- детальный порядок реализации движения игрока
- обязательный объем ближайшего `v1`
- то, что сознательно откладывается на более поздний этап

Текущее принятое направление для движения:

- только собственный `top-down` контроллер
- без использования импортированного пакета движения даже как референса

## Текущее состояние combat sandbox на 2026-06-08

В репозитории больше нет полного расхождения между документацией и реализацией по линии «игрок умеет атаковать, но врагов ещё нет». На текущий момент в Unity-проекте уже реализован первый рабочий срез боевого sandbox:

- у игрока стрельба и ближний бой наносят реальный урон `IDamageable`-целям
- добавлен отдельный combat-слой для данных попадания и поиска получателя урона
- логика врагов вынесена в самостоятельные AI-скрипты и не строится на скриптах игрока

### Реально существующие враги в проекте

В проекте собраны три prefab-ассета для ручной расстановки:

- `Assets/Game/Prefabs/Enemies/EnemyShooter.prefab`
- `Assets/Game/Prefabs/Enemies/EnemyMelee.prefab`
- `Assets/Game/Prefabs/Enemies/EnemyExploder.prefab`

Их текущее поведение в реализации:

- `EnemyShooter` — жёлтая капсула, держит дистанцию, ведёт прицельную стрельбу и дополнительно выпускает круговой залп с интервалом `3-6s`
- `EnemyMelee` — оранжевая капсула, догоняет игрока и атакует вблизи 2 раза в секунду
- `EnemyExploder` — фиолетовая капсула, при сближении или при потере всех HP запускает 3 красных предупреждающих мигания и затем подрывается; визуальный шар взрыва расширен до `3 м`, а в радиусе `2 м` наносится `30` урона игроку

### Текущие ограничения и следующий слой

- игрок уже получает HP/shield-урон от вражеских атак и видимый hit feedback
- shooter projectile knockback сейчас отключен, чтобы снаряды наносили урон без физического отталкивания игрока
- поведение врагов всё еще предназначено для MVP/sandbox-проверки читаемости и ручной расстановки prefab-ов
- spawn zones уже существуют, но полноценный encounter-state и loop комнаты `активация -> бой -> награда -> переход` еще не закрыты

### Визуальный фидбэк текущей реализации

- при атаке враги делают `bounce` через squash/stretch
- при получении урона враги 3 раза мигают белым
- над врагами показываются runtime-HP bar и текст текущего/максимального здоровья; полоска HP увеличена вдвое и окрашена в красный
- при попадании по врагам появляются вылетающие цифры нанесённого урона
- melee-враг использует отдельные runtime-кулаки для удара, чтобы ближняя атака читалась визуально

### Editor workflow

Для воспроизводимого создания ассетов добавлен `Assets/Game/Editor/EnemyPrefabBuilder.cs`.

Он:

- создаёт материалы `Mat_enemy_shooter`, `Mat_enemy_melee`, `Mat_enemy_exploder`
- собирает три enemy-prefab в `Assets/Game/Prefabs/Enemies/`
- подходит и для меню Unity `RORTYPE/Build Enemy Prefabs`, и для batchmode-сборки

## Обновление combat sandbox на 2026-06-08: NavMesh и spawn zone

Текущая реализация enemy sandbox больше не ограничена только ручной расстановкой трёх prefab-врагов без навигации. В проекте принят следующий рабочий срез:

- `EnemyCapsuleController` использует `NavMeshAgent` и двигается только по navmesh
- враги патрулируют между navmesh-точками вокруг позиции спавна или стартовой позиции
- радиус первичного обнаружения игрока равен `25` метрам
- дальность стрельбы игрока и стрелков-врагов приведена к `20` метрам через ограничение effective lifetime снаряда по формуле `distance / speed`
- runtime HP bar врагов изменён: высота x2, длина x0.5, красный fill расширен на `0.01`, чтобы не z-fight'иться с чёрным фоном

Для спавна врагов добавлена отдельная система:

- `Assets/Game/Scripts/AI/EnemySpawnZone.cs`
- `Assets/Game/Scripts/AI/EnemySpawnPoint.cs`
- `Assets/Game/Editor/SpawnZonePrefabBuilder.cs`

Рабочее поведение `EnemySpawnZone`:

- зона активируется при первом входе игрока в trigger-объём
- при активации зона сразу дропает по `3` врага в каждой дочерней `EnemySpawnPoint`
- у зоны есть лимит активных врагов, по умолчанию `25`
- у зоны есть общий budget спавна, по умолчанию `50` врагов за encounter
- враги спавнятся по дочерним `EnemySpawnPoint`
- интервал спавна случайный в диапазоне `3.5 - 6` секунд
- тип врага выбирается по весам: melee `50%`, shooter `30%`, exploder `20%`

Важно: в репозитории добавлен код и editor-builder для prefab зоны спавна, но фактическая batchmode-сборка prefab требует, чтобы проект не был открыт вторым экземпляром Unity одновременно.
## Таблица баланса combat sandbox на 2026-06-08

Эта таблица фиксирует актуальный рабочий баланс поверх более ранних описаний выше. Если текст документа и таблица расходятся, для combat sandbox считать актуальной именно таблицу ниже.

### Игрок

| Сущность | Параметр | Значение | Примечание |
| --- | --- | --- | --- |
| Player | Дальность стрельбы | `20 м` | Ограничивается effective lifetime снаряда |
| Player | Поведение пули в trigger-объёмах | `Игнорирует служебные trigger` | Пуля больше не уничтожается об `EnemySpawnZone` |
| Player | Ближний бой | `Урон только в фазе удара` | Нет постоянного contact-hit при простом сближении |

### Враги

| Враг | HP | Скорость движения | Дистанция боя | Темп атаки | Особенности |
| --- | --- | --- | --- | --- | --- |
| `EnemyShooter` | `5` | `10.4` | `20 м` | `1 выстрел / 0.85 сек` | Прицельная стрельба + круговой залп каждые `3 сек` |
| `EnemyMelee` | `10` | `10.2` | `1.65 м` | `2 удара / сек` | Патруль по navmesh, затем ускоренное сближение |
| `EnemyExploder` | `3` | `13.6` | `1.9 м` триггер подрыва | `3 warning flash` | Подрывается и при смерти по HP; visual AoE `3 м`, урон `3` в радиусе `2 м`, игрок исключён |

### Spawn Zone

| Сущность | Параметр | Значение | Примечание |
| --- | --- | --- | --- |
| `EnemySpawnZone` | Лимит активных врагов по умолчанию | `25` | Возвращён с `40` для более контролируемого encounter |
| `EnemySpawnZone` | Общий budget спавна | `50` | После исчерпания новые враги больше не появляются |
| `EnemySpawnZone` | Стартовый дроп при входе | `3` врага на каждую точку | Тип врага выбирается по текущим весам |
| `EnemySpawnZone` | Интервал спавна по умолчанию | `3.5 - 6 сек` | Было `8 - 12 сек` |
| `EnemySpawnZone` | Blocked radius | `0.8` | Чуть плотнее разрешён спавн рядом |
| `EnemySpawnZone` | Вес типов врагов | `50 / 30 / 20` | melee / shooter / exploder |
| `EnemySpawnZone` prefab | Trigger size | `18 x 4 x 18` | Увеличен базовый объём зоны |

### Spawn Point

| Сущность | Параметр | Значение | Примечание |
| --- | --- | --- | --- |
| `EnemySpawnPoint` | Базовый радиус спавна | `0.75` | Используется для navmesh-сэмплинга вокруг точки |
| `EnemySpawnPoint` | Burst-спавн | `1 - 4 врага за тик` | Зависит от масштаба точки по X/Z |
| `EnemySpawnPoint` | Большая точка | `Scale 2 -> burst 2` | Чем крупнее точка, тем интенсивнее спавн |
| `EnemySpawnZone` prefab | Дефолтные точки | `4 угловые точки, scale 2` | Дают более плотный стартовый encounter |
## Обновление combat sandbox на 2026-06-08: revision 2

Эта ревизия дополняет предыдущую таблицу баланса и уточняет актуальное runtime-поведение combat sandbox.

### Новое поведение стрелка

- `EnemyShooter` сохраняет обычный прицельный выстрел по игроку.
- Дополнительно `EnemyShooter` теперь раз в `3` секунды выпускает круговой залп.
- Enemy projectiles не должны останавливаться об других врагов или другие enemy projectiles и не должны тратиться на friendly collision; для них рабочие цели сейчас — игрок и препятствия.

### Актуальные дальности и обнаружение

| Сущность | Параметр | Значение |
| --- | --- | --- |
| Player | Дальность стрельбы | `20 м` |
| `EnemyShooter` | Дальность стрельбы | `20 м` |
| Враги | Радиус обнаружения | `25 м` |
| Враги | Lose target radius | `30 м` |

### Актуальный баланс врагов

| Враг | HP | Скорость движения | Темп обычной стрельбы | Доп. паттерн |
| --- | --- | --- | --- | --- |
| `EnemyShooter` | `5` | `10.4` | `1 выстрел / 0.85 сек` | круговой залп каждые `3-6 сек`, `5-7` лучей, со случайным углом |
| `EnemyMelee` | `10` | `10.2` | n/a | ускорен для более уверенного догоняния |
| `EnemyExploder` | `3` | `13.6` | n/a | rush на игрока, затем одинаковый warning-взрыв и по proximity, и при смерти по HP; враги от его урона получают тот же hit-flash |

### Игрок и камера

- Для старых scene-объектов игрока без назначенного `visualRoot` runtime создаёт дочерний `RuntimeVisual` из root mesh/material и отключает root renderer. Дополнительно `TopDownPlayerMotor` теперь сглаживает позицию visualRoot даже если это прямой child игрока, чтобы visible model не дёргалась вместе с physics-root.
- `EnemyCapsuleController` использует такой же LateUpdate-smoothing для `visualRoot`, поэтому NavMesh-движение врагов больше не должно давать покадровое подёргивание модели.
- Для scene-local игрока в `Assets/Game/Scene/Level_1.unity` дополнительно зафиксирован `groundSnapOffset = 0.02`, чтобы grounded-снап оставался стабильным и не провоцировал лишнее дрожание на поверхности.
- `TopDownCameraRig` теперь дополнительно смещает framing в сторону курсора через cursor-lookahead, но сам lookahead проходит через отдельный `lag` (`cursorLookAheadLag = 0.28` по умолчанию), чтобы офсет не рывком улетал к курсору.

## Current portal travel implementation on 2026-06-10

Portal travel is now implemented as a real runtime scene-travel slice and no longer exists only at the design-document level.

- `Assets/Game/Prefabs/Portal.prefab` is configured with `ScenePortal`, but portal routing is no longer hardcoded in the script. Each portal instance is now configured manually through a serialized `destinations` list, so the level setup decides where the portal can lead.
- Portal interaction uses `E`, and the hint is handled by runtime UI instead of hand-authored scene canvases. Active portal discovery prefers trigger contact and falls back to the portal's serialized interaction radius if trigger registration is missed.
- `Assets/Game/Prefabs/Portal.prefab` has an explicit root `SphereCollider` trigger with `7m` radius. If a custom or older portal root has no trigger collider, `ScenePortal` auto-creates a runtime `SphereCollider` trigger using `interactionRadius`, so those instances still get an active interaction volume.
- While the player is touching the portal trigger, `ScenePortal` recolors the child mesh renderer(s) named `Sphere`, so the portal provides an in-world proximity signal in addition to the UI prompt.
- Multi-destination portals open a runtime button choice panel (`PortalUiRuntime`) with mouse-click support and `1..9` keyboard shortcuts. Single-destination portals load the target scene immediately.
- Spawn-point-based arrival has been removed. The repository now goes back to manual scene-local player placement: each gameplay scene contains its own player prefab instance, and scene load no longer applies a post-load teleport to a separate arrival marker.
- The old spawn runtime (`PlayerSpawnPoint`, `PlayerSceneSpawnController`, `PortalTravelRuntime`) and the previous portal builder script are no longer part of the accepted implementation.

## Current spawn zone cleanup implementation on 2026-06-10

- `EnemySpawnZone` starts a cleanup countdown when the last player leaves the zone trigger.
- The default cleanup delay is `30` seconds through `cleanupDelayAfterPlayerExit`.
- If the player re-enters the zone before the countdown expires, cleanup is cancelled and the existing spawned enemies remain alive.
- When the countdown expires, the zone destroys only enemies it spawned and clears its active enemy list.
- Cleanup does not reset `encounterActivated` or `totalSpawnedEnemies`, so the zone keeps its existing encounter state and consumed spawn budget.

## Current door button implementation on 2026-06-10

- Door/button gameplay is now implemented with `Assets/Game/Scripts/Interaction/SlidingDoor.cs` and `Assets/Game/Scripts/Interaction/DoorPressureButton.cs`.
- `SlidingDoor` moves its configured root from the closed local position toward an inspector-defined open offset. The first Level 1 door opens from top to bottom by moving downward.
- `DoorPressureButton` is a pressure plate trigger: the player activates it by standing on it, using the same player-side `ScenePortalInteractionController` contact identity used by portal interaction.
- The button indicator renderer and point light are red while idle and green when pressed. The first Level 1 setup is latched, so the button and door remain active after the player steps on it.
- `Assets/Game/Scene/Level_1.unity` contains a manually placed `DoorButtonPuzzle` near the scene-local `TopDownPlayer` start position for quick testing.

## Current interaction/resource implementation on 2026-06-10

- `Assets/Game/Scripts/Player/PlayerResourceController.cs` stores player ammo and stamina. The current accepted defaults are starting ammo `100`, max ammo `999`, stamina `100`, sprint drain `10/sec`, stamina regen `35/sec` after `0.45 sec`.
- `TopDownFacingController` now spends `1` ammo before spawning a projectile. If ammo is `0`, ranged shooting does not fire; melee attacks still work.
- `TopDownPlayerMotor` now has `2` dash charges and recovers one charge every `5 sec`. Sprinting consumes stamina through `PlayerResourceController`.
- `Assets/Game/Scripts/UI/PlayerStatusUiRuntime.cs` creates a runtime bottom-right HUD showing ammo, two dash rectangles, and stamina.
- `Assets/Game/Scripts/Interaction/WorldInteractable.cs` adds portal-style proximity interaction for non-portal objects, using the existing `ScenePortalInteractionController` and `PortalUiRuntime` prompt.
- `Store` and `HAMMER` prefabs have root `SphereCollider` trigger interaction and show `Buy: press E` / `Bought` style prompt behavior configured with Russian prefab text.
- `Chest` and `Capsule` prefabs have root `SphereCollider` trigger interaction. Opening either container now spawns physical resource pickups, then disables its `MinimapTrackable` and trigger so it cannot be taken again.

## Current interaction fallback implementation on 2026-06-16

- `ScenePortalInteractionController` resolves `E` interactions through trigger contact first, then falls back to distance or authored trigger-bounds checks for portals, shops, chests/capsules, totem pickups, and totem pedestals.
- Portal, shop, and resource-container fallback range uses the same serialized `interactionRadius` that sizes their authored/root trigger collider. Shops still require an authored trigger collider and do not create one at runtime.
- Totem pickup and totem pedestal fallback checks use their authored trigger collider bounds plus a small player-overlap tolerance.
- Scene-local players in `Level_1`, `Level_2`, `Hub_1`, and `PlayerMovementTest` have `ScenePortalInteractionController`; `PlayerMovementTest` also keeps `InteractionUi.prefab` for prompt/UI verification.

## Current economy, health, pickups, and destructibles on 2026-06-10

This section supersedes the older ammo-only chest/capsule description above.

- `PlayerResourceController` is now the player source of truth for ammo, money, health, stamina, and the one-time damage upgrade. Defaults are health `500`, starting ammo `100`, money `1000` for shop testing, stamina `100`.
- Ammo, money, health, and damage-upgrade ownership persist through static runtime state, so portal scene changes that instantiate a new scene-local player do not reset the player's core resources.
- Player HP damage values are: melee enemy `10`, shooter projectile `20`, enemy exploder explosion `30`.
- `PlayerStatusUiRuntime` shows ammo/dashes/stamina/health in the bottom-right HUD and yellow outlined money text under the top-right minimap area. Ammo, money, and health changes give a short UI pulse.
- Stamina and health bars explicitly scale their fill rects on X every frame, so bar depletion is visible even when the runtime-created UI images have no authored sprite.
- Superseded shop detail: `WorldInteractable` previously supported resource pickups and shop menus; current `WorldInteractable` is resource/container-only. Chests and capsules now drop physical `ResourcePickupCollectible` objects instead of adding resources directly: `10` money spheres/cubes worth `10` gold each, `5` ammo spheres/cubes worth `10` ammo each, plus at most one `150 HP` health pickup with `60%` chance. Pickup interaction shows floating reward text, disables the minimap marker and interaction trigger, then removes the world object after feedback.
- `Store` opens a merchant menu after pressing `E`: buy ammo (`10` gold for `10` ammo), buy health (`20` gold for `20` HP), or buy the configured merchant utility items.
- `HAMMER` opens a blacksmith menu after pressing `E`: buy ammo (`10` gold for `10` ammo) or buy one-time upgrades such as shield unlock, extra dash, and damage x2. One-time unavailable items disappear from the open menu after purchase.
- Enemy death drops `1-3` resource pickups. Yellow random sphere/cube pickups give `2` gold. Shooter enemies can also drop red random sphere/cube pickups that give `1` ammo. All enemies can now also drop a red cross-shaped health pickup that restores `20 HP`. Enemy money, ammo, and health pickups magnetize to the player inside a horizontal `2m` radius and use trigger-only colliders so they are collectible without physically blocking or slowing the player.
- `Assets/Game/Scripts/Environment/DestructibleCover.cs` and `Assets/Game/Prefabs/Environment/DestructibleCover.prefab` define a six-block destructible cover target with `15 HP`, `4m` width, and `2m` height. The cover's blocking visual is authored directly in the prefab as six child cube meshes/colliders. Destructible cover is intentionally not shown on the minimap.
- `Assets/Game/Scripts/Environment/ExplosiveBarrel.cs` and `Assets/Game/Prefabs/Environment/ExplosiveBarrel.prefab` define a light-red explosive cylinder. The barrel's blocking visual/collider is authored directly in the prefab as a child `BarrelCylinder`; it explodes after three player hits, flashes red three times before detonation, deals `3` damage to enemies within `5m`, and immediately destroys `DestructibleCover` objects caught in the blast.
- `TopDownPlayerMotor` grounded movement now performs a Rigidbody sweep before `MovePosition`, preventing direct grounded movement from pushing the player through wall colliders.

## Current economy/health follow-up on 2026-06-10

- Resource and shop scripts now normalize their runtime defaults in `Awake`, so older scene/prefab instances with missing serialized fields still get valid money, health, stamina, and shop values.
- `TopDownPlayer.prefab` and scene-local player instances explicitly serialize `maxMoney = 999999`, `maxHealth = 500`, `startingHealth = 500`, `maxStamina = 100`, `sprintDrainPerSecond = 10`, and `wallSkinWidth = 0.05` where applicable.
- `PlayerStatusUiRuntime` uses a `1920 x 1080` reference resolution and sorting order `1000`; the money label is yellow with black outline and anchored below the top-right minimap.
- `Chest.prefab`, `Capsule.prefab`, `Store.prefab`, and `HAMMER.prefab` explicitly serialize the accepted economy/shop behavior. Chest and capsule resource rewards are now governed by `WorldInteractable` container drop defaults: `10 x 10` gold pickups, `5 x 10` ammo pickups, and one optional `150 HP` pickup at `60%` chance. Merchant and blacksmith purchase values remain inspector-driven; one-time upgrades that previously cost `1000G` currently cost `100G` for shop testing.
- `Level_1.unity` contains manually placed test instances near the player start for `DestructibleCover` and two `ExplosiveBarrel` objects.
- The wall collision fix is now stronger than the earlier Rigidbody sweep: grounded movement performs a capsule cast and a penetration correction pass before `MovePosition`.

## Current runtime cleanup and FPS stability implementation on 2026-06-12

- `Assets/Game/Scripts/Combat/RuntimeRendererUtility.cs` colors runtime renderers through `MaterialPropertyBlock`, avoiding hidden per-object material instances from `renderer.material`.
- `Assets/Game/Scripts/Combat/CombatRuntimeBudget.cs` caps temporary combat objects: player projectiles, enemy projectiles, resource pickups, floating texts, and explosion effects. When a category exceeds its cap, the oldest live object is destroyed.
- Player/enemy projectiles, resource pickups, floating combat text, and explosion scale effects register themselves with the runtime budget as soon as they are initialized.
- `EnemyCapsuleController` maintains an active-enemy registry and reuses a shared collider buffer when setting up enemy projectile collision ignores. This replaces a per-shot global `FindObjectsByType<EnemyCapsuleController>()` scan.
- `PlayerResourceController.ActivePlayer` exposes the current scene-local player resources to lightweight runtime systems. Pickup magnetization and enemy target resolution use it instead of searching the scene every frame.

## Current player ramp locomotion implementation on 2026-06-12

- `TopDownPlayerMotor` no longer moves grounded players across ramps by using only the old ground point under the current position.
- During grounded movement it samples stable ground at the intended next body position and snaps the body height to that surface before `Rigidbody.MovePosition`.
- Grounded collision casts still protect against walls, but hits with normals inside `TopDownGroundProbe.maxSlopeAngle` are treated as walkable surfaces rather than blockers.
- `groundedSlopeSnapDistance = 0.65` is serialized on `TopDownPlayer.prefab` and on scene-local players in `Level_1`, `Level_2`, `Hub_1`, and `PlayerMovementTest`, so descending ramps remains grounded instead of flickering into falling.

## Current pickup prefab implementation on 2026-06-12

- Resource pickup drops are prefab-backed through `Assets/Game/Resources/ResourcePickups/`.
- Standard pickup prefabs are `GoldPickup`, `AmmoCubePickup`, `AmmoSpherePickup`, and `HealthPickup`.
- The per-item nominal is configured on each prefab's `ResourcePickupCollectible.amount`: gold `10`, ammo cube/sphere `10`, health `150`.
- Enemy drops and chest/capsule drops resolve these prefabs automatically from `Resources`; explicit serialized prefab fields on enemies/containers can override the standard prefabs.
- Older missing-prefab setups keep a runtime primitive fallback, but the accepted workflow is to tune item values on the pickup prefabs.

## Current destructible loot prop implementation on 2026-06-14

- `Assets/Game/Scripts/Environment/DestructibleLootContainer.cs` defines non-explosive destructible props. It implements `IDamageable`, so player hits, enemy projectiles, enemy melee hits, enemy exploder blasts, and `ExplosiveBarrel` blasts can destroy these props through the shared combat path.
- Player dash impact is now supported by `TopDownPlayerMotor`: while dashing, the movement blocker found by the grounded capsule cast receives one `CombatTeam.Player` hit per dash. This avoids depending only on `OnCollisionEnter`, which may not fire when collision-aware movement stops before penetration.
- `Assets/Game/Prefabs/Environment/DestructibleBarrel.prefab` and `Assets/Game/Prefabs/Environment/DestructibleCrate.prefab` are authored prefabs with child mesh/collider pieces, a root kinematic Rigidbody, and the shared yellow material. On destruction, child pieces detach and are cleaned up after a short debris lifetime. Props with a positive debris impulse turn detached pieces into dynamic colliding rigidbodies; zero-impulse props still detach into falling Rigidbody parts, but their colliders are trigger-only and they receive only small horizontal scatter to avoid upward physics launches from overlapping authored pieces.
- `DestructibleCrate.prefab` is a compact `3 x 3 x 3` matrix of 27 small cube blocks with total bounds `2 x 2 x 2`. `DestructibleBarrel.prefab` has debris impulse disabled so it does not launch upward when destroyed.
- Only `DestructibleCrate.prefab` and `DestructibleBarrel.prefab` may drop loot. Their `dropsLoot` flag is enabled and they call `ResourcePickupCollectible.SpawnEnemyStyleDrops`, the same 1-3 pickup path used by enemy death. `DistructableTree.prefab` has `dropsLoot = false`, and `DestructibleCover` has no loot component.
- `DestructibleCover` and `DistructableTree` use the same yellow material family as crate/barrel and flash white on hits like enemies. Loot from cover, tree, or other non-crate/non-barrel interactive objects should be treated as a bug.

## Current shop UI implementation on 2026-06-13

- `PortalUiRuntime` is portal-only again. Portal destination choices are still handled by portal-specific runtime UI, but merchant/blacksmith logic no longer lives in the portal choice component.
- `WorldInteractable` is now resource/container-only. Old `mode = 0` shop data on existing Store/HAMMER serialized objects is treated as legacy and does not register as an active interaction.
- `ShopInteractable` is the accepted shop component for merchants and blacksmiths. `Store.prefab` and `HAMMER.prefab` now carry separate `ShopInteractable` components with distinct `shopKind`, prompts, and serialized `shopItems`.
- Shop UI is authored through `ShopUiPanel` and `ShopItemCard` components in `Assets/Game/Prefabs/UI/InteractionUi.prefab`. `ShopInteractable` no longer creates a fallback runtime canvas; if a scene is missing the authored interaction UI, the shop logs a warning and does not open.
- `ShopInteractable` does not create trigger colliders at runtime; shops still need authored trigger colliders on the prefab or scene object.
- Shop cards are designed as a grid of item buttons with text icons, `G` gold prices, and hover hints. Purchases are executed by the card/button click handler only, so one mouse click produces one purchase. While a shop panel is open, player combat mouse input is ignored so clicking a shop item cannot fire the player's weapon.
- The runtime ammo HUD label uses a black outline for readability over bright level backgrounds, matching the money counter's outline treatment.
- Temporary shop test pricing: Store shield unlock, HAMMER extra dash, HAMMER damage x2, and HAMMER shield unlock cost `100G` instead of their older `1000G` price.

## Current door and totem door implementation on 2026-06-14

- `Assets/Game/Scripts/Interaction/SlidingDoor.cs` drives authored door geometry from a closed local position toward `openLocalOffset`. It now has separate open and close speeds, so pressure doors can open gradually and close faster when the player leaves early.
- With `lockOpenWhenFullyOpen` enabled, a door becomes permanently open only after the moving root fully reaches the open target. Later close requests are ignored by that locked door.
- `Assets/Game/Scripts/Interaction/DoorPressureButton.cs` remains the ordinary pressure-platform component. `Assets/Game/Prefabs/PointOfInterest/DoorButtonPuzzle (1).prefab` is configured as non-latched pressure behavior: the linked door opens while the player stands on the platform, closes faster after early exit, and stays open after fully opening.
- Totem-door gameplay is implemented by `TotemPickup`, `TotemCarrier`, `TotemPedestal`, and `TotemDoorController`. The same `ScenePortalInteractionController`/`PortalUiRuntime` prompt flow handles `E` pickup and installation prompts.
- `Assets/Game/Prefabs/PointOfInterest/Totem.prefab` is a separate pickup prefab rendered as a floating purple diamond. Pressing `E` near it adds it to the player's runtime `TotemCarrier`; multiple carried totems orbit the player at reduced visual scale, and installed totems keep bobbing/rotating on their platforms.
- `Assets/Game/Prefabs/PointOfInterest/ShardDoor.prefab` is the totem-door prefab. It contains three totem platforms by default; the controller discovers child `TotemPedestal` components at runtime, so copying or deleting platforms changes how many placed totems are required.

## Current player skills implementation on 2026-06-14

- `Assets/Game/Scripts/Player/PlayerSkillController.cs` is the player skill source of truth. It is attached to `TopDownPlayer.prefab` and to the manually authored scene-local players in `Level_1`, `Level_2`, `Hub_1`, and `PlayerMovementTest`.
- Skill 1 is radial burst: `Alpha1`, `5s` cooldown, `7` red radial projectiles that use player-team damage and the player's damage multiplier.
- Skill 2 is sticky bomb: `Alpha2`, `5s` cooldown, a larger purple player-style projectile (`0.3` radius) that sticks to enemies or non-trigger surfaces, waits `1.2s`, then explodes with exploder-style values: `30` damage, `2m` damage radius, `3m` visual radius, and `4.8` impulse.
- `Assets/Game/Scripts/Player/StickyBombProjectile.cs` handles the sticky projectile lifetime, stick-to-target parenting, explosion damage, and transient purple explosion visual.
- `PlayerStatusUiRuntime` shows two square skill slots above the dash charge row. Each slot shows its key label and, while cooling down, a numeric countdown on the square.

## Current minimap marker visibility on 2026-06-15

This supersedes older minimap notes that listed enemies, chests, capsules, or destructible objects as visible world markers.

- `MinimapController` still draws the player marker through its dedicated player marker slot.
- General minimap marker slots render only `MinimapIconGroup.PointOfInterest`.
- Doors are accepted point-of-interest markers and remain visible as red, yaw-rotating rectangular markers that can scale from world bounds.
- Destructible environment objects must not appear on the minimap. The runtime explicitly blocks `DestructibleCover`, `DestructibleLootContainer`, and `ExplosiveBarrel` markers even if a `MinimapTrackable` is accidentally added to one of those objects.
- `MinimapBuilder` no longer configures enemy, chest, or capsule markers and uses only player/point-of-interest trackables for minimap bounds.

## Current player hit feedback and fall threshold on 2026-06-15

- `PlayerResourceController.ReceiveHit` deducts shield/health, spawns the existing floating shield/HP damage text, and now also flashes the player's visual renderers white after any accepted non-player hit.
- Player hit flashing excludes runtime melee fist renderers so attack visuals keep their configured red color after the flash.
- `PlayerRespawnController.fallDistance` is now `5m`; the value is serialized on `TopDownPlayer.prefab` and on scene-local players in `Level_1`, `Level_2`, `Hub_1`, and `PlayerMovementTest`.

## Current player occlusion ghost implementation on 2026-06-15

- `Assets/Game/Scripts/Player/PlayerOcclusionGhost.cs` checks whether a non-player collider blocks the line from `Camera.main` to the player's smoothed render probe point.
- While blocked, the player gets a blue transparent fresnel ghost overlay that renders through obstacles. The effect uses `Assets/Game/Shaders/PlayerGhostFresnel.shader` and `Assets/Game/Resources/Materials/PlayerGhostFresnel.mat`.
- The ghost overlay is created as duplicate runtime renderers on the player's visual meshes, not by replacing the normal player material. This keeps the normal red player visual, hit flash, and combat feedback intact.
- `PlayerResourceController` auto-adds `PlayerOcclusionGhost` so scene-local players in existing gameplay scenes receive the behavior without manual scene edits.

## Current elevator platform implementation on 2026-06-15

- `Assets/Game/Scripts/Interaction/ElevatorPlatform.cs` implements a pressure-triggered moving lift for manually authored scene placement.
- `Assets/Game/Prefabs/PointOfInterest/ElevatorPlatform.prefab` contains a yellow solid deck, a red/green pressure button trigger on the deck, a point light indicator, and a kinematic Rigidbody root.
- Default behavior matches the accepted prototype requirement: stepping onto the platform button raises the elevator by inspector-configured `liftHeightMeters` (`5m` by default); leaving the platform trigger starts a `3s` return delay; after the delay the elevator moves back down.
- While the player remains inside the pressure trigger, the elevator applies its movement delta to the player's Rigidbody root, keeping the current `TopDownPlayerMotor` riding with the moving platform.
