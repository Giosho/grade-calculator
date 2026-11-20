# Калкулатор за оценки

Калкулатор за ученически успех: въвеждаш оценки и цел и той казва колко шестици или петици ти трябват.

Има две версии:

- **Windows програма** (C# WinForms): в папката на проекта. Сглобява се с `./build.ps1`.
- **Уеб версия, която се инсталира (PWA)**: в папка `web`. Работи в браузър, на телефон и без интернет след инсталиране.

Общо: табове Калкулатор, Предмети, Статистика, Настройки; светла и тъмна тема; внос на оценки от Школо.

## Уеб версия на Cloudflare Pages (pages.dev)

1. В Cloudflare: Workers & Pages -> Create -> Pages -> Connect to Git -> избери това repo.
2. Framework preset: None. Build command: (празно). Build output directory: `web`.
3. Save and Deploy. Адресът ще е `https://<име>.pages.dev`.

При всяко ново качване в `main` сайтът се обновява сам. При промяна на файловете смени `CACHE` в `web/sw.js` (напр. `gradecalc-v2`).

Тест на компютъра: служи папка `web` с произволен статичен сървър и отвори `http://localhost:порт`.

## Windows програма

Нужен е само Windows. В PowerShell: `./build.ps1`. Скриптът сваля Microsoft.Web.WebView2 от nuget.org и прави exe на Desktop.

Аргументи за проверка на външния вид: `--demo --theme=dark|light --accent=RRGGBB --tab=0..3`

Шрифтът на уеб версията е Caveat (SIL Open Font License), вижте `web/fonts/OFL.txt`.