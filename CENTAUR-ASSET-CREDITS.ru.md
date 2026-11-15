# Кентавры: источники и авторы

Новая раса «Ханства Пустошей» использует классические модели Warcraft III.
Оригинальные архивы и readme сохранены в `work/centaur028`; SHA-256 и перечни
файлов записаны в `work/centaur028/sources.json` и `models.json`.

- **Maximal** — рабочий, метатель топоров, ведьма с бубном и её анимации,
  отдельная модель метательного топора:
  https://www.hiveworkshop.com/threads/centaur-pack.371239/
- **Ujimasa Hojo** — оптимизированные кентавры, лучник, Khan, Paladin и
  Death Knight; три типа шатров:
  https://www.hiveworkshop.com/threads/centaur-and-derivatives.293906/
  https://www.hiveworkshop.com/threads/tent-centaur-0-1-and-derivatives.287991/
  https://www.hiveworkshop.com/threads/tent-centaur-2-and-derivatives.234439/
- **Mephestrial, Ujimasa Hojo** — кузница в варианте Centaur:
  https://www.hiveworkshop.com/threads/forge-gnoll.244274/
- **Filip95** — Tauren Watchtower, деревянная дозорная вышка:
  https://www.hiveworkshop.com/threads/tauren-watchtower.333171/
- **Villagerino** — иконки Centaur Model Pack Icons, приложенные к набору
  Maximal; заказчик/инициатор публикации наборов — Galendor an'Kreil.
- **Blizzard Entertainment** — исходные Warcraft III модели/текстуры,
  встроенные текстуры и стандартные иконки, использованные авторами наборов.

У Maximal указана публикация для общего использования; readme требует указать
автора. Ujimasa разрешает редактировать свои модели. Публичная доступность
наборов не означает передачу авторских прав или универсальную open-source
лицензию. Оригинальные уведомления сохранены; авторы указаны также в описании
расы в меню игры. Адаптация MDX → GLB и материалы Unity выполнены в проекте.

Иллюстрация VS создана встроенным **image_gen**. Файл:
`outputs/Art/Versus/centaur.png`; сохранённый промпт:
`outputs/Art/Versus/centaur-prompt.txt`. В игре используется копия в
`Assets/StreamingAssets/Frontend/Versus/centaur.png`.

Круглая эмблема выбора расы также создана встроенным **image_gen**, с
существующими эмблемами орков, северян и людей как референсами оформления.
Файл `outputs/Art/RaceIcons/centaur.png`, точный промпт
`outputs/Art/RaceIcons/centaur-prompt.txt`; игровая копия
`Assets/Resources/RaceIcon-centaur.png`. Сохранён исходный GUID этой иконки.

В версии 0.29 отдельный защитный тотем использует оригинальную классическую
модель Blizzard `Buildings/Orc/TaurenTotem/TaurenTotem.mdx`, прочитанную из
установленных MPQ. Исходный файл игры не изменялся; путь и SHA-256 записаны в
`work/centaur028/native-totem029-source.json`.

19 иконок строительства и 14 отдельных портретов бойцов созданы рендером
реальных моделей редактором `BuildCentaurIcons`. Это игровые изображения
из тех же ассетов, а не дополнительная генерация. Значки ролей и способностей
взяты из классических CommandButtons; соответствия сохранены в
`work/centaur028/ability-icons029.json`.
