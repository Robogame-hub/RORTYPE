# Баланс RORTYPE

Этот документ фиксирует текущий рабочий баланс по фактическим prefab/script значениям проекта. Если числа в старых секциях `docs/project-documentation.md` расходятся с этим файлом, приоритет у текущих prefab/script файлов и этой сводки.

## Источники

| Область | Основные файлы |
| --- | --- |
| Игрок | `Assets/Game/Prefabs/Player/TopDownPlayer.prefab`, `Assets/Game/Scripts/Player/PlayerResourceController.cs`, `Assets/Game/Scripts/Player/TopDownPlayerMotor.cs`, `Assets/Game/Scripts/Player/TopDownFacingController.cs` |
| Противники | `Assets/Game/Prefabs/Enemies/EnemyMelee.prefab`, `Assets/Game/Prefabs/Enemies/EnemyShooter.prefab`, `Assets/Game/Prefabs/Enemies/EnemyExploder.prefab`, `Assets/Game/Scripts/AI/EnemyCapsuleController.cs` |
| Коллектаблы | `Assets/Game/Resources/ResourcePickups/*.prefab`, `Assets/Game/Scripts/Interaction/ResourcePickupCollectible.cs`, `Assets/Game/Scripts/Interaction/WorldInteractable.cs` |
| Магазины и контейнеры | `Assets/Game/Prefabs/PointOfInterest/Chest.prefab`, `Capsule.prefab`, `Store.prefab`, `HAMMER.prefab`, `WorldInteractable.cs` |
| Спавн | `Assets/Game/Prefabs/Spawning/EnemySpawnZone.prefab`, `Assets/Game/Scripts/AI/EnemySpawnZone.cs` |

## Номиналы коллектаблов

| Объект | Номинал | Где используется | Примечание |
| --- | ---: | --- | --- |
| `GoldPickup` | `10` золота | контейнеры, enemy drops | prefab-backed pickup |
| `AmmoCubePickup` | `10` патронов | контейнеры, enemy drops | prefab-backed pickup |
| `AmmoSpherePickup` | `10` патронов | контейнеры, enemy drops | prefab-backed pickup |
| `HealthPickup` | `150` HP | контейнеры, enemy drops | prefab-backed pickup |
| Сундук / капсула: золото | `10 x 10 = 100` золота | physical drops при открытии | прямые `moneyReward` поля не являются основным runtime-путем для контейнеров |
| Сундук / капсула: патроны | `5 x 10 = 50` патронов | physical drops при открытии | прямые `ammoReward` поля не являются основным runtime-путем для контейнеров |
| Сундук / капсула: здоровье | `150` HP | шанс `60%`, максимум 1 pickup | если roll не прошел, HP pickup не появляется |
| Enemy death drops | `1-3` pickups | все враги | health roll идет первым |
| Enemy health drop | `150` HP | шанс `20%` | использует `HealthPickup` |
| Shooter ammo drop | `10` патронов | шанс `45%`, если HP не выпал | для shooter enemies |
| Enemy money drop | `10` золота | fallback, если HP/ammo не выпали | использует `GoldPickup` |

## Магазины и траты

| Объект | Покупка | Стоимость | Результат |
| --- | --- | ---: | --- |
| `Store` | Патроны | `1` золото | `+1` патрон, можно удерживать цифру покупки |
| `Store` | Лечение | `20` золота | `+20` HP, можно удерживать цифру покупки |
| `Store` | Полное лечение | `500` золота | HP до максимума |
| `Store` | Доп. щит | `1000` золота | открывает `100` shield, одноразово |
| `Store` | Восстановление щита | `100` золота | shield до максимума, пункт виден только если щит куплен |
| `HAMMER` | Патроны | `1` золото | `+1` патрон, можно удерживать цифру покупки |
| `HAMMER` | Доп. рывок | `1000` золота | `+1` max dash charge, одноразово; пункт пропадает после покупки |
| `HAMMER` | Damage upgrade | `1000` золота | `x2` урон игрока, одноразово; пункт пропадает после покупки |
| `HAMMER` | Щит | `1000` золота | открывает `100` shield, одноразово |
| `HAMMER` | Щит +100 | `500` золота | `+100` max shield |
| `HAMMER` | HP +100 | `500` золота | `+100` max HP и `+100` текущего HP |

Покупки у торговцев и кузнецов теперь задаются списком `shopItems` на `WorldInteractable` в prefab. Для repeatable-товаров удержание цифровой клавиши пункта повторяет покупку с ускорением до лимита `3` покупки в секунду.

## Баланс противников

| Враг | HP | Скорость | Атака / урон | Дистанции и поведение |
| --- | ---: | ---: | --- | --- |
| `EnemyMelee` | `10` | `10.2` | melee `10`, интервал `0.5s` | detection `25m`, lose `30m`, melee range `1.65m` |
| `EnemyShooter` | `5` | `10.4` | projectile `20`, интервал `0.85s` | attack range `20m`, projectile speed `18`, lifetime `1.8s`, max distance `20m` |
| `EnemyExploder` | `3` | `13.6` | explosion `30` | trigger `1.9m`, damage radius `2m`, effect radius `3m`, warning `3` flashes |

## Shooter radial burst

| Параметр | Значение |
| --- | ---: |
| Интервал burst | `3-6s` |
| Количество снарядов | `5-7` |
| Урон снаряда | `20` |
| Projectile speed | `18` |
| Projectile max distance | `20m` |

## Игрок

| Параметр | Значение |
| --- | ---: |
| HP старт / максимум | `500 / 500` |
| Shield старт / максимум | `0 / 0`, появляется после покупки |
| Патроны старт / максимум | `100 / 999` |
| Деньги старт / максимум | `0 / 999999` |
| Stamina максимум | `100` |
| Sprint drain | `10/sec` |
| Stamina regen | `35/sec` |
| Stamina regen delay | `0.45s` |
| Walk speed | `10` |
| Sprint speed | `15` |
| Dash charges | `2` |
| Dash charges после upgrade | `3` |
| Dash distance | `5m` |
| Dash duration | `0.16s` |
| Dash cooldown | `0.65s` |
| Dash charge recovery | `5s` за заряд |
| Grounded slope snap distance | `0.65` |

## Атаки игрока

| Атака | Урон | Темп | Дальность / скорость | Ресурс |
| --- | ---: | --- | --- | --- |
| Ranged projectile | `1` | `1` выстрел / `0.18s` | range `20m`, speed `28`, lifetime `1.4s` | `1` патрон |
| Melee | `2` | `1` удар / `0.16s` | reach `0.72m`, fist radius `0.2m` | бесплатно |
| Ranged после upgrade | `2` | без изменения | без изменения | `1` патрон |
| Melee после upgrade | `4` | без изменения | без изменения | бесплатно |

## Щит и апгрейды выживаемости

| Параметр | Значение |
| --- | ---: |
| Базовый shield до покупки | `0` |
| Shield unlock | `+100` max shield и полный shield |
| Shield upgrade | `+100` max shield и `+100` текущего shield |
| Shield restore | полный shield |
| Max HP upgrade | `+100` max HP и `+100` текущего HP |

## Спавн противников

| Параметр | Значение | Примечание |
| --- | ---: | --- |
| Max active enemies | `25` | на `EnemySpawnZone` |
| Max total spawn count | `50` | бюджет encounter |
| Spawn interval | `3.5-6s` | min/max |
| Spawn blocked radius | `0.8` | проверка занятости точки |
| Вес типов | `50 / 30 / 20` | melee / shooter / exploder |
| Cleanup after exit | `30s` | после выхода последнего игрока |

## Окружение

| Объект | HP / триггер | Эффект |
| --- | --- | --- |
| `DestructibleCover` | `15` HP | разрушаемое укрытие |
| `ExplosiveBarrel` | взрыв после `3` попаданий игрока | `3` урона врагам в `5m`, сразу уничтожает `DestructibleCover` в зоне |

## Замечания по актуальности

- Старые поля `ammoReward` / `moneyReward` на `Chest.prefab` и `Capsule.prefab` могут сохраняться в prefab-файлах для совместимости, но текущий контейнерный runtime использует physical drops через `WorldInteractable`.
- Старые записи в памяти проекта упоминали enemy drops `2` gold / `1` ammo / `20 HP`; текущая prefab-backed схема использует номиналы `10` gold / `10` ammo / `150 HP`.
- Для изменения номиналов pickup-ов предпочтительно редактировать `ResourcePickupCollectible.amount` на prefab-ах в `Assets/Game/Resources/ResourcePickups/`.
