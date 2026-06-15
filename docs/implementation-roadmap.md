# Roadmap реализации проекта RORTYPE

Обновлено: 2026-06-15

## Назначение

Этот файл фиксирует рабочий план реализации `RORTYPE` относительно фактического состояния Unity-проекта. Старые версии плана от 2026-06-08 считать историческими: проект уже прошел этапы базового движения, combat sandbox, первых врагов, части интерактивов, миникарты, магазинов и переходов между сценами.

Приоритет источников:

1. текущие `Assets/Game/**`, `ProjectSettings/**` и prefab/script значения;
2. `docs/project-memory.md` как журнал устойчивых решений;
3. `docs/project-documentation.md` как человекочитаемое описание;
4. внешний `Forest Rises 112.1.6 Дизайн Уровней.docx` как целевой дизайн, а не доказательство реализации.

## Текущий статус

### Готово или принято как рабочая база

- Собственный `top-down` игрок без зависимости от `Assets/Plagin/Player Movement and Camera`.
- Player prefab и scene-local игроки в `Level_1`, `Level_2`, `Hub_1`, `PlayerMovementTest`.
- Базовое движение: ходьба, sprint, dash, прыжок, slope handling, fall respawn с порогом `7m`.
- Combat sandbox: player ranged/melee, damage team model, hit feedback, floating damage/resource text.
- Враги MVP: `EnemyMelee`, `EnemyShooter`, `EnemyExploder`, enemy projectiles, radial shooter burst, runtime cleanup budget.
- Enemy spawn zones с бюджетом активных/общих врагов и cleanup после выхода игрока.
- Ресурсы игрока: HP, shield, ammo, money, stamina, persistent runtime state между сценами.
- Prefab-backed pickups: gold, ammo cube/sphere, health.
- Контейнеры и лут: `Chest`, `Capsule`, destructible crate/barrel с physical drops.
- Shops: `ShopInteractable`, `ShopUiPanel`, `ShopItemCard`, отдельная логика `Store` и `HAMMER`.
- Portal travel: `ScenePortal`, `ScenePortalInteractionController`, destination choice UI через `PortalUiRuntime`.
- Scene-authored interaction UI через `Assets/Game/Prefabs/UI/InteractionUi.prefab`.
- Minimap как scene-bound prefab: игрок рисуется отдельно, world slots показывают только `PointOfInterest`; враги, сундуки, капсулы и destructibles не должны быть обычными world markers.
- Разрушаемые объекты: cover, explosive barrel, destructible barrel/crate/tree, shared neutral damage path.
- Двери и маршрутные интерактивы: pressure door, totem pickup/pedestal/door, elevator platform.
- Player active skills: radial burst на `Alpha1`, sticky bomb на `Alpha2`.
- Player occlusion ghost overlay для видимости за препятствиями.

### Частично готово

- Рабочие gameplay-сцены есть для `Level_1`, `Level_2`, `Hub_1`, `PlayerMovementTest`.
- `EditorBuildSettings.asset` ссылается также на `Hub_2` и `Level_3`, но соответствующие `.unity` файлы в `Assets/Game/Scene` сейчас не найдены.
- Переходы между сценами и портальная UI-логика реализованы, но полный run-loop с `Hub_2`/`Level_3` требует согласования недостающих сцен.
- Экономика и магазины работают как тестовый баланс; цены `100G` на часть одноразовых апгрейдов остаются временными.
- Миникарта имеет актуальное runtime-правило видимости, но документацию и scene instances нужно держать синхронно для каждой gameplay-сцены.

### Не закрыто

- Цельная задача/квест уровня через терминал-портал.
- Завершенный encounter loop вида `активация -> бой -> награда -> переход`.
- Автосохранение run state.
- Прогрессия опыта/уровня и долгосрочная мета-прогрессия.
- Финальные `Hub_2` и `Level_3`, если они остаются частью принятой сети переходов.
- Production-level layout трех целевых локаций из дизайн-документа.
- Полный пул противников из дизайн-документа.
- VFX/SFX, финальная читаемость UI и системный playtest.

## Актуальная стратегия

Сейчас план больше не должен начинаться с создания движения, первых врагов или первой миникарты. Ближайшая работа должна связывать уже созданные системы в один проверяемый вертикальный срез.

Порядок:

1. Сначала выровнять сцены и build settings.
2. Затем собрать один законченный encounter loop в `Level_1`.
3. После этого стабилизировать награды, портальный переход и хабовый шаг.
4. Только потом расширять до `Level_2`, `Hub_2`, `Level_3` и полноценной прогрессии.

## Этап A. Инвентаризация и выравнивание сцен

Цель:

- убрать расхождения между build settings, документацией и фактическими `.unity` файлами.

Что сделать:

- решить, нужны ли сейчас `Hub_2.unity` и `Level_3.unity`;
- если нужны, создать/восстановить эти сцены и добавить минимальный scene-authored набор: player, camera, interaction UI, minimap, portal(s);
- если не нужны, убрать ссылки на них из `ProjectSettings/EditorBuildSettings.asset` и временно сузить сеть переходов до существующих сцен;
- проверить, что `Level_1`, `Level_2`, `Hub_1`, `PlayerMovementTest` имеют scene-local `InteractionUi` и корректные minimap references;
- зафиксировать результат в `docs/project-documentation.md`.

Критерий выхода:

- build settings не ссылается на отсутствующие сцены, а документация явно описывает текущую сеть переходов.

## Этап B. Encounter loop в Level_1

Цель:

- превратить набор боевых систем в один законченный игровой цикл.

Что сделать:

- выбрать или добавить терминал/портал, который запускает encounter;
- связать `EnemySpawnZone` с состояниями encounter: idle, active, completed, failed/reset;
- определить MVP-условие завершения: очистка волн или убийство элитной цели;
- после завершения открывать награду и следующий переход;
- не создавать runtime geometry/builders для сцены: объекты расставлять и связывать вручную.

Критерий выхода:

- игрок в `Level_1` может активировать событие, пережить бой, получить награду и перейти дальше.

## Этап C. Награды и экономика после encounter

Цель:

- сделать награду читаемой и полезной для следующего шага run-loop.

Что сделать:

- согласовать, что именно выдается за completion: pickups, сундук, shop currency, upgrade token или комбинация;
- сверить значения с `docs/balance.md`;
- убрать временные цены или явно оставить их как test balance;
- проверить, что reward UI/floating text не конфликтует с shop UI и combat input block.

Критерий выхода:

- после encounter игрок понимает, что получил, и может потратить/использовать ресурс в хабе.

## Этап D. Хабовый шаг

Цель:

- сделать минимальный переход `Level -> Hub -> Next Level` рабочим, а не только технически возможным.

Что сделать:

- в `Hub_1` проверить `Store`, `HAMMER`, portal destinations, interaction radius и choice UI;
- определить, временно ли `Hub_1` заменяет оба типа хаба;
- если `Hub_2` нужен уже сейчас, создать/восстановить сцену и минимально настроить ее;
- убедиться, что player resources, shield/max HP/dash upgrades сохраняются при смене сцен.

Критерий выхода:

- игрок после `Level_1` попадает в хаб, покупает/улучшает что-то и переходит в следующий уровень без ручных действий в редакторе.

## Этап E. Сохранение и run state

Цель:

- заменить текущую runtime persistence между сценами полноценным сохранением.

Что сделать:

- определить минимальный набор сохраняемых данных: ресурсы, HP/max HP, shield, dash upgrade, damage upgrade, текущая сцена/run step;
- реализовать автосохранение на переходе сцены и после крупных покупок;
- добавить сброс run для тестирования;
- проверить, что сохранение не ломает scene-local player setup.

Критерий выхода:

- run можно продолжить после перезапуска, а тестовый сброс возвращает проект в контролируемое состояние.

## Этап F. Vertical slice первой локации

Цель:

- довести `Level_1` до состояния демонстрационного уровня.

Что сделать:

- выбрать, остается ли `Level_1` прототипом `Лесного водоворота`;
- добавить основной маршрут, альтернативные ответвления, безопасные возвраты после падения, 1-2 скрытые награды;
- расставить enemies, destructibles, doors/totems/elevators там, где они помогают маршруту;
- настроить minimap только для игрока и POI;
- исключить случайные маркеры destructibles/enemies/chests/capsules из миникарты.

Критерий выхода:

- `Level_1` демонстрирует движение, бой, лут, маршрутные интерактивы, миникарту и переход.

## Этап G. Масштабирование

Цель:

- перенести проверенный цикл на остальные локации без копирования layout.

Что сделать:

- `Level_2`: развивать как отдельную островную/плоскую структуру, если она соответствует `Захваченному порту`;
- `Level_3`: создать или восстановить только после закрытия `Level_1`/`Level_2` loop;
- хабы: развести обычный и усиленный хаб только после подтверждения, что хабовый шаг нужен в текущей версии run-loop;
- добавлять новые enemy archetypes после стабилизации существующих трех.

Критерий выхода:

- проект имеет несколько различимых рабочих сцен, связанных одним run-loop.

## Не делать раньше времени

- Не начинать production всех трех локаций до завершения encounter loop в `Level_1`.
- Не добавлять новые builder/runtime-generator workflows для настройки сцен.
- Не возвращать магазины в `WorldInteractable`; shops живут в `ShopInteractable`.
- Не показывать enemies/chests/capsules/destructibles на миникарте как обычные world markers, пока действует правило `PointOfInterest`-only.
- Не разворачивать полный список врагов из дизайн-документа до стабильного баланса MVP-врагов.

## Ближайший практический порядок работ

1. Исправить расхождение `EditorBuildSettings.asset` с отсутствующими `Hub_2.unity` и `Level_3.unity`: создать/восстановить сцены или убрать ссылки.
2. Проверить scene-local `InteractionUi` и minimap setup в существующих gameplay-сценах.
3. Собрать в `Level_1` один encounter start/completion flow поверх `EnemySpawnZone`.
4. Привязать completion reward и следующий портал.
5. Прогнать ручной путь `Level_1 -> Hub_1 -> Level_2` и зафиксировать найденные проблемы.
