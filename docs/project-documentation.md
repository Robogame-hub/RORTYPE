# Документация проекта RORTYPE

## Обзор

`RORTYPE` сейчас представляет собой Unity-проект на стадии раннего прототипа уровня. Репозиторий содержит грейбокс уровня, отдельную тестовую сцену движения, собственные скрипты `top-down` управления, базовые материалы и импортированный пакет сторонних контроллеров движения/камеры. Основное целевое видение игры и уровней описано в документе `Forest Rises 112.1.6 Дизайн Уровней.docx`, который задает структуру мира, список интерактивов, навигацию, прогрессию и параметры трех основных локаций.

На момент документирования проект находится на этапе, когда дизайн уже определен значительно детальнее, чем текущая реализация в Unity. Поэтому любые дальнейшие задачи нужно рассматривать в двух плоскостях:

- что уже реально существует в Unity-проекте
- что должно быть реализовано по дизайн-документу

## Источники правды

### Текущая реализация

- Сцена: `Assets/Game/Scene/Level_1.unity`
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

Проектная зона игры в текущем состоянии уже включает первый собственный инкремент движения:

- `Scene/Level_1.unity` — основная сцена-грейбокс
- `Scene/PlayerMovementTest.unity` — отдельная тестовая сцена для проверки движения игрока
- `Scripts/Player/` — собственные runtime-скрипты `top-down` движения и камеры
- `Prefabs/TopDownPlayer.prefab` — базовый prefab игрока под новое движение
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

## Состояние текущей сцены

`Level_1.unity` в текущем виде остается грейбоксом и не содержит подтвержденной собственной gameplay-логики. На сцене присутствуют базовые объекты:

- `GameController`
- `Map`
- `Plane`
- несколько `Capsule`
- `Directional Light`
- объект `Other`

Найденные в сцене компоненты `m_Script` относятся к `Unity.ProBuilder`, то есть это редакторские/геометрические компоненты, а не пользовательские игровые скрипты. Из этого следует:

- текущая сцена пока описывает форму и расстановку примитивов
- логика боевых событий, интерактивов, квестов и прогрессии пока не реализована или не подключена

Дополнительно важно:

- `ProjectSettings/EditorBuildSettings.asset` теперь содержит `Assets/Game/Scene/PlayerMovementTest.unity` как текущую тестовую сцену входа
- следовательно, текущая сцена еще не оформлена как рабочая точка входа в сборку

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

### Текущая реализация миникарты на 2026-06-10

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

Для первого боевого уровня приняты стартовые данные:

- картинка карты: `Assets/Game/Minimap_var_2.png`
- размер prefab миникарты: `500 x 500` UI-единиц
- прозрачность изображения карты: `0.75`
- стартовый размер мира в контроллере prefab: `500 x 500` метров, но scene instance в `Level_1` дополнительно получает рассчитанные bounds по текущим trackable-объектам
- маркер игрока: зелёный кружок
- маркеры врагов: красные кресты
- маркеры сундуков и капсул: синие кресты
- маркеры точек интереса: `Store` синий квадрат, `Portal` синий круг, `HAMMER` синий треугольник

Важно: в проект возвращён `MinimapBuilder`, и через него уже создан `Assets/Game/Prefabs/UI/Minimap.prefab`, а также инстанс `MinimapCanvas` в `Assets/Game/Scene/Level_1.unity`. Для `Level_1` builder также навешивает недостающий `MinimapTrackable` на scene-local `TopDownPlayer` и другие именованные объекты уровня, чтобы маркер игрока не зависел от того, является ли объект prefab instance. Финальные цвета, формы и размеры карты в метрах всё равно остаются ручными inspector-настройками на `MinimapTrackable` и `MinimapController`.

Обновленное правило на 2026-06-10: дальнейшая настройка сцен больше не должна опираться на новые editor-builder scripts, runtime-builder scripts или генерацию в Play Mode. Сцены настраиваются руками прямо в Unity-сцене, prefabs создаются и обновляются вручную, а serialized references связываются явно. Для миникарты это означает, что каждый уровень должен иметь собственный настроенный scene instance: корректную картинку/границы карты и `MinimapTrackable` на scene-local игроке, сундуках, порталах, магазинах, кузнице, капсулах, врагах и других точках интереса. Простое копирование `MinimapCanvas` из `Level_1` на другой уровень не считается завершенной настройкой, если маркеры игрока и POI не подключены в этой сцене.

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

## Что отсутствует в текущей реализации

На текущем состоянии репозитория еще не найдены или не подключены:

- собственная игровая логика уровня
- враги и спавнеры
- хабы
- торговец, кузнец, капсулы и сундуки
- система задач уровня
- терминалы-порталы
- мини-карта, компас и маркеры
- система прогрессии между локациями
- автосохранение

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
- `Rigidbody.useGravity` для этого контроллера должен быть выключен, потому что вертикальное движение и snap к земле управляются кодом
- боевая система находится на раннем прототипе: персонаж уже может разворачиваться к мыши и стрелять runtime-сферами, но урон, враги и полноценная боевая интеграция еще не подключены
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

- `EnemyShooter` — жёлтая капсула, подходит на дистанцию, ведёт частую прицельную стрельбу и дополнительно выпускает круговой залп раз в 3 секунды
- `EnemyMelee` — оранжевая капсула, догоняет игрока и атакует вблизи 2 раза в секунду
- `EnemyExploder` — фиолетовая капсула, при сближении или при потере всех HP запускает 3 красных предупреждающих мигания и затем подрывается; визуальный шар взрыва расширен до `3 м`, а в радиусе `2 м` наносится `3` урона всем, кроме игрока

### Текущие ограничения этой версии

- игрок урон от противников не получает — это сознательное ограничение текущего этапа
- атаки врагов при этом уже могут отталкивать игрока лёгким runtime-knockback без изменения HP
- поведение врагов предназначено для sandbox-проверки читаемости, попаданий и ручной расстановки префабов
- wave-spawner, encounter-state и полноценный loop комнаты ещё не подключены

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
- Portal interaction uses `E`, and the hint is handled by runtime UI instead of hand-authored scene canvases. Active portal discovery now uses trigger contact, not a raw distance check.
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
- `Chest` and `Capsule` prefabs have root `SphereCollider` trigger interaction. Chest grants `20` ammo, capsule grants `50` ammo, then disables its `MinimapTrackable` and trigger so it cannot be taken again.

## Current economy, health, pickups, and destructibles on 2026-06-10

This section supersedes the older ammo-only chest/capsule description above.

- `PlayerResourceController` is now the player source of truth for ammo, money, health, stamina, and the one-time damage upgrade. Defaults are health `500`, starting ammo `100`, money `0`, stamina `100`.
- Ammo, money, health, and damage-upgrade ownership persist through static runtime state, so portal scene changes that instantiate a new scene-local player do not reset the player's core resources.
- Player HP damage values are: melee enemy `10`, shooter projectile `20`, enemy exploder explosion `30`.
- `PlayerStatusUiRuntime` shows ammo/dashes/stamina/health in the bottom-right HUD and yellow outlined money text under the top-right minimap area. Ammo, money, and health changes give a short UI pulse.
- Stamina and health bars explicitly scale their fill rects on X every frame, so bar depletion is visible even when the runtime-created UI images have no authored sprite.
- `WorldInteractable` supports resource pickups and shop menus. Chests resolve to `200` gold. Capsules resolve to `200` gold plus `100` ammo. Pickup interaction shows floating reward text, disables the minimap marker and interaction trigger, then removes the world object after feedback.
- `Store` opens a two-option merchant menu after pressing `E`: buy ammo (`1` gold for `1` ammo) or buy health (`20` gold for `20` HP).
- `HAMMER` opens a blacksmith menu after pressing `E`: buy ammo or buy a one-time `x2` damage upgrade for `1000` gold.
- Enemy death drops `1-3` resource pickups. Yellow random sphere/cube pickups give `2` gold. Shooter enemies can also drop red random sphere/cube pickups that give `1` ammo. All enemies can now also drop a red cross-shaped health pickup that restores `20 HP`. Enemy money, ammo, and health pickups magnetize to the player inside a horizontal `2m` radius.
- `Assets/Game/Scripts/Environment/DestructibleCover.cs` and `Assets/Game/Prefabs/Environment/DestructibleCover.prefab` define a six-block destructible cover target with `15 HP`, `4m` width, and `2m` height. The cover's blocking visual is authored directly in the prefab as six child cube meshes/colliders, and the prefab includes a minimap trackable so it can be authored as a separate map point.
- `Assets/Game/Scripts/Environment/ExplosiveBarrel.cs` and `Assets/Game/Prefabs/Environment/ExplosiveBarrel.prefab` define a light-red explosive cylinder. The barrel's blocking visual/collider is authored directly in the prefab as a child `BarrelCylinder`; it explodes after three player hits, flashes red three times before detonation, and deals `3` damage to enemies and neutral destructible objects within `5m`.
- `TopDownPlayerMotor` grounded movement now performs a Rigidbody sweep before `MovePosition`, preventing direct grounded movement from pushing the player through wall colliders.

## Current economy/health follow-up on 2026-06-10

- Resource and shop scripts now normalize their runtime defaults in `Awake`, so older scene/prefab instances with missing serialized fields still get valid money, health, stamina, and shop values.
- `TopDownPlayer.prefab` and scene-local player instances explicitly serialize `maxMoney = 999999`, `maxHealth = 500`, `startingHealth = 500`, `maxStamina = 100`, `sprintDrainPerSecond = 10`, and `wallSkinWidth = 0.05` where applicable.
- `PlayerStatusUiRuntime` uses a `1920 x 1080` reference resolution and sorting order `1000`; the money label is yellow with black outline and anchored below the top-right minimap.
- `Chest.prefab`, `Capsule.prefab`, `Store.prefab`, and `HAMMER.prefab` explicitly serialize the accepted economy values: chest `200` gold; capsule `200` gold + `100` ammo; merchant ammo/health purchases; blacksmith ammo and one-time `1000` gold damage upgrade.
- `Level_1.unity` contains manually placed test instances near the player start for `DestructibleCover` and two `ExplosiveBarrel` objects.
- The wall collision fix is now stronger than the earlier Rigidbody sweep: grounded movement performs a capsule cast and a penetration correction pass before `MovePosition`.
