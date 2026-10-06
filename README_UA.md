# PsychoOverlay

Повноекранний WebGL-ефект для вибраного монітора. Програма захоплює екран, обробляє кадр і показує його на весь дисплей. У проєкті є п’ять звичайних режимів і режим 6 з оригінальним Nilk-шейдером NoxusBoss.

## Запуск

Запусти `run_overlay.bat` або `PsychoOverlay.exe`. Натисни кнопку захоплення й вибери **весь монітор** у вікні Windows. `Ctrl+Alt+R` зупиняє захоплення та відкриває вибір джерела повторно.

## Режими й клавіші

| Клавіші | Дія |
|---|---|
| `Ctrl+Alt+1`–`6` | Flow, Texture, Glitch, Full Trip, Chaos, Nilk cycle |
| `Ctrl+Alt+7` | NILK EXACT: поточний upstream Nilk shader, повний 60-хвилинний таймлайн |
| `Ctrl+Alt+8` | NILK EXACT REF: той самий upstream shader, старт з 13:30 для звірки з відео |
| `Ctrl+Alt+PageUp/PageDown` | У режимі 8: 13:30, 14:00, 22:30, 28:00, 28:30, 29:00, 30:00, 30:30, 31:00 |
| `Ctrl+Alt+Left/Right` | Перемкнути режим |
| `Ctrl+Alt+Up/Down` | Змінити силу звичайного ефекту |
| `Ctrl+Alt+Space` або `Ctrl+Alt+P` | Пауза / продовження |
| `Ctrl+Alt+R` | Зупинити захоплення / вибрати монітор |
| `Ctrl+Alt+Esc` або `Ctrl+Alt+Q` | Вийти |

Під час захоплення панель і HUD ховаються. Системний курсор приховується, а WebGL малює один курсор у викривлених координатах. Захоплення всього монітора оновлюється під час Alt+Tab.

## Nilk

`Ctrl+Alt+6` лишається старим таймлайновим режимом для A/B-порівняння.

`Ctrl+Alt+7` — **NILK 2024**, буквальний порт Nilk shader із Wrath of the Gods 1.1.20 (квітень 2024): вертикальні двочастотні хвилі, 12-tap центральний blur, palette remap через `sin(luminance * PI - globalTime * 0.75)`, vignette й overlay. У цій версії ще немає broad XY wobble та datamosh.

`Ctrl+Alt+8` — **NILK 2025**, буквальний порт shader із Wrath of the Gods 1.2+ (лютий 2025): broad XY cos-warp, дрібні хвилі, `datamoshIntensity = 0.51`, Perlin noise, 12-tap blur, `sin(luminance * 2PI - globalTime * 1.5)`, vignette та оригінальні 8-кольорові палітри.

Критичне виправлення режиму 8: `previousScreenTexture` тепер є попереднім **вже відфільтрованим результатом** через GPU feedback ping-pong, а не просто попереднім сирим кадром захоплення. Саме це створює накопичувальні рідкі/рипляві сліди, які видно на референсах.

Обидва режими 7/8 стартують на повній ручній інтенсивності, щоб не чекати 18 хвилин оригінального 60-хвилинного debuff. `Ctrl+Alt+Up/Down` змінює силу, `Ctrl+Alt+PageUp/PageDown` вручну перемикає оригінальні Nilk palettes.

`run_overlay.bat` збирає й запускає актуальний source build.
