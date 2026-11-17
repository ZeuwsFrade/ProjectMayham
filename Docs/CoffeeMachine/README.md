# Кофемашина убежища

## Как получить и использовать

1. Завершить спасение Intern (`Intern_00_Rescue`). Новый квест «Кофе для выживших» доступен через его обычное меню заданий, без дополнительного требования лояльности.
2. Принести Intern 2 кружки, 2 воды и 3 пачки бумаги. Награда: изученный чертёж кофемашины, 1 порция зёрен и 10 очков лояльности. Чертёж сохраняется как открытый модуль, отдельный предмет не занимает инвентарь.
3. На любом свободном месте для модуля построить кофемашину за 2 кружки, 2 папки и 8 монет. Ресурсы берутся из рюкзака и склада. Повторная постройка запрещена существующими правилами модулей.
4. Открыть машину клавишей взаимодействия. Долить до 3 единиц воды и загрузить 1 порцию зёрен. Она даёт 3 использования; новую порцию можно загрузить после их исчерпания.
5. Каждый следующий рейд даёт 1 обычный расходник `Coffee`, тратя 1 воду и 1 использование зёрен. Без одного из ресурсов машина ждёт, сохраняя другой ресурс. Загрузка после простоя не начисляет кофе за пропущенные рейды.
6. Забрать кофе в рюкзак/склад. Если места недостаточно, остаток хранится в машине. Чертёж, постройка, вода, использования зёрен и готовый кофе сохраняются между сессиями.

Зёрна находятся в таблицах добычи `Loot_BreakRoom`, `Loot_Storage`, `Loot_Cabinet`, по одной порции. Для них есть новый предмет, иконка, мировой префаб и поддержка переноса/выбрасывания в зацикленном мире рейда. Это не напиток: порцию загружают в машину.

Система использует существующее понятие дня: `QuestLog.CountRaid` при начале рейда. При следующем обращении к машине рассчитывается число прошедших рейдов, ограниченное имеющимися водой и зёрнами. Обновление формата сохранения не требуется: добавлено поле `ModuleStateData.beanUses`, у старых сохранений оно равно нулю. Поведение бумажного пресса не меняется.

## Файлы и повторная настройка

- Логика: `Assets/Scripts/Shelter/CoffeeMachine.cs`.
- Окно: `Assets/Scripts/UI/CoffeeMachineWindow.cs`.
- Модуль: `Assets/Resources/Modules/CoffeeMachine.asset`.
- Квест: `Assets/Resources/Quests/Intern/Intern_CoffeeMachine.asset`.
- Зёрна: `Assets/Data/Items/CoffeeBeans.asset` и `Assets/Prefabs/Items/CoffeeBeans.prefab`.
- Спрайты: `Assets/Sprites/Shelter/CoffeeMachine.png`, `Assets/Sprites/Items/CoffeeBeans.png`.
- Меню `Mayham > Shelter > Set Up Coffee Machine` создаёт отсутствующие ассеты и добавляет зёрна в добычу без дублей. Существующие тексты, цены и параметры не перезаписываются.
- Новые проверки подключены к `Mayham > Quests > Run Self-Test` в `QuestSelfTest.Coffee.cs` и используют временное состояние вместо игрового сохранения.

Изолированные превью Unity: `window-preview.png`, `machine-preview.png`. Они созданы без Play Mode и без изменений сохранения или сцен проекта.

## Генерация спрайтов

По уточнённому выбору пользователя использован встроенный `image_gen`, а не GPT‑6.1 Terra. Оба изображения созданы с `transparent_background: true`, сохранены в проект и импортированы как Single Sprite, Point Filter, без mipmaps и сжатия. Фон имеет настоящий альфа-канал. Масштаб мировых префабов настроен отдельно; исходные PNG сохранены в разрешении генератора.

### Промпт кофемашины

Обновление: текущий спрайт заменён на строгий вид сверху в серо-синей палитре офисной мебели проекта. Встроенный `image_gen` использовал прежний спрайт как цель правки, а `Big-Office-Printer.png`, `Desk.png`, `Water-Dispenser.png` — как стилевые референсы. Прежний PNG и настройки импорта сохранены в `Docs/ArtBackups/CoffeeMachine/`. Размер нового префаба — 1.5 × 1.5 единицы, коллайдер — 1.2 × 1.2 по центру. Актуальный промпт:

Replace image 1 coffee machine sprite with a substantially simpler strict TOP-DOWN coffee station sprite matching reference images 2, 3, 4 from the actual game's office furniture tileset. Image 1 is the edit target, not a style reference. Images 2-4 define style: tiny low-resolution pixel sprites, flat discrete muted blue-grey and lavender metal, near-black outline, pale grey highlights, 16-pixel-per-unit game furniture. Camera straight overhead at 90 degrees, orthographic plan view; show the TOP of the coffee machine, NOT its front elevation, NO isometric view, NO tall front-facing cabinet. Square compact footprint. Top-down small dark blue-grey rectangular coffee station worktop; rear half contains rectangular coffee maker housing and a circular brown bean hopper lid seen directly from above, small water reservoir at one side. Front half has dark rectangular drip grid and one white mug seen as a CIRCLE of brown coffee with tiny handle. Include two tiny status pixels green and amber. Only about 32x32 distinct logical pixels across the whole sprite, large clean square pixel blocks, very limited palette about 12 colors, flat simple shading, no fine texture, no shiny 3D rendering, no rust detail, no gradients or glow. Silhouette axis aligned and centered, top and bottom edges horizontal. Truly transparent background, no floor, no shadow outside outline, no text. The coffee station fills 85% of a square image and is readable when reduced to 32x32. Redesign completely to satisfy overhead camera and existing tileset style; do not retain image 1's perspective or cabinet.

Первоначальный промпт (версия сбоку, сохранена только как история):

Create a single game sprite asset: a compact scavenged office espresso coffee machine on a small dark metal cabinet for a post-apocalyptic office shelter. Pixel art matching a 16 pixels-per-unit top-down RPG with slightly visible front surfaces: clear angular silhouette, dark navy blue housing, worn silver front, warm copper details, small green indicator, a white coffee mug under the spout, coffee bean hopper on top. One isolated object, centered, no ground plane, no scenery, no text, no labels, no border. Entire machine visible. Real transparent background. Tight coherent limited palette, crisp deliberate pixel edges, no antialias blur, readable at 32x48 native game pixels. Render at available resolution with large crisp pixel blocks; final image is a standalone sprite, not an asset sheet.

### Промпт зёрен

Single inventory game sprite of a sealed small kraft paper pouch of roasted coffee beans. Match a pixel-art top-down office survival RPG: slightly visible top and front, chunky crisp pixel edges, dark brown outline, tan paper bag, dark blue label with a simple coffee bean pictogram, three small cream dots to suggest three servings, a couple of brown beans visible by the folded opening. No lettering, no words, no numbers. One centered isolated item, fully visible, no other items, no floor, no shadow or glow outside silhouette. Truly transparent background. Restrained colors, readable as a small 16x20 pixel game inventory icon, not photorealistic, no antialiasing.
