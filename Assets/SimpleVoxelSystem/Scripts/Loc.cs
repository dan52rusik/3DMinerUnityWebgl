using System;
using System.Collections.Generic;
using UnityEngine;
using YG;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// Централизованная система локализации, интегрированная с Yandex Games SDK.
    ///
    /// Использование:
    ///   1. Добавить строки в словари ниже (или расширить через AddString).
    ///   2. В коде: Loc.T("key") — возвращает строку на текущем языке.
    ///   3. Подписаться на Loc.OnLanguageChanged чтобы обновить UI.
    ///   4. Сменить язык: Loc.SetLanguage("ru") / Loc.SetLanguage("en").
    ///
    /// Язык определяется автоматически из YG2.lang при старте.
    /// Игрок может переключить вручную — выбор сохраняется в PlayerPrefs.
    /// </summary>
    public static class Loc
    {
        // ── Поддерживаемые языки ─────────────────────────────────────────────
        public const string LangRu = "ru";
        public const string LangEn = "en";
        public const string LangTr = "tr";

        private const string LangPrefKey = "svs_ui_language";

        // ── Текущий язык ─────────────────────────────────────────────────────
        private static string _currentLang = LangRu;
        public static string CurrentLanguage => _currentLang;

        /// <summary>Срабатывает при смене языка. Подпишитесь чтобы обновить UI.</summary>
        public static event Action OnLanguageChanged;

        // ── Словарь переводов ─────────────────────────────────────────────────
        // Формат: strings[key][lang] = "перевод"
        private static readonly Dictionary<string, Dictionary<string, string>> _strings =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        // ── Статический конструктор — заполняем все строки ───────────────────
        static Loc()
        {
            // ── HUD / Общее ──────────────────────────────────────────────────
            Add("money",            ru: "Монеты",        en: "Coins",        tr: "Altın");
            Add("level",            ru: "Уровень",       en: "Level",        tr: "Seviye");
            Add("xp",               ru: "Опыт",          en: "XP",           tr: "XP");
            Add("backpack",         ru: "Рюкзак",        en: "Backpack",     tr: "Sırt Çantası");
            Add("backpack_full",    ru: "Рюкзак заполнен!", en: "Backpack full!", tr: "Sırt çantası dolu!");
            Add("sell",             ru: "Продать",       en: "Sell",         tr: "Sat");
            Add("buy",              ru: "Купить",        en: "Buy",          tr: "Satın Al");
            Add("cancel",           ru: "Отмена",        en: "Cancel",       tr: "İptal");
            Add("confirm",          ru: "Подтвердить",   en: "Confirm",      tr: "Onayla");
            Add("close",            ru: "Закрыть",       en: "Close",        tr: "Kapat");
            Add("not_enough_money", ru: "Недостаточно монет", en: "Not enough coins", tr: "Yeterli altın yok");

            // ── Магазин шахт ─────────────────────────────────────────────────
            Add("mine_shop",        ru: "Магазин шахт",  en: "Mine Shop",    tr: "Maden Dükkanı");
            Add("place_mine",       ru: "Разместить шахту на острове", en: "Place mine on island", tr: "Adaya maden koy");
            Add("sell_mine",        ru: "Продать шахту", en: "Sell mine",    tr: "Madeni sat");
            Add("mine_limit",       ru: "Лимит шахт достигнут! Продайте одну.", en: "Mine limit reached! Sell one first.", tr: "Maden limiti doldu! Bir tane sat.");
            Add("mine_bronze",      ru: "Бронзовая шахта", en: "Bronze Mine", tr: "Bronz Maden");
            Add("mine_silver",      ru: "Серебряная шахта", en: "Silver Mine", tr: "Gümüş Maden");
            Add("mine_gold",        ru: "Золотая шахта", en: "Gold Mine",    tr: "Altın Maden");

            // ── Кирки ────────────────────────────────────────────────────────
            Add("pickaxe_shop",     ru: "Магазин кирок", en: "Pickaxe Shop", tr: "Kazma Dükkanı");
            Add("pickaxe_stone",    ru: "Каменная кирка", en: "Stone Pickaxe", tr: "Taş Kazma");
            Add("pickaxe_iron",     ru: "Железная кирка", en: "Iron Pickaxe", tr: "Demir Kazma");
            Add("pickaxe_gold",     ru: "Золотая кирка", en: "Gold Pickaxe", tr: "Altın Kazma");
            Add("pickaxe_diamond",  ru: "Алмазная кирка", en: "Diamond Pickaxe", tr: "Elmas Kazma");
            Add("req_level",        ru: "Требуется ур. {0}", en: "Requires Lv. {0}", tr: "Gerekli Sv. {0}");
            Add("power",            ru: "Мощность: {0}",    en: "Power: {0}",        tr: "Güç: {0}");

            // ── Апгрейды ─────────────────────────────────────────────────────
            Add("upgrade",          ru: "Улучшить",      en: "Upgrade",      tr: "Geliştir");
            Add("upgrade_strength", ru: "Сила удара",    en: "Strike Power", tr: "Darbe Gücü");
            Add("upgrade_backpack", ru: "Объём рюкзака", en: "Backpack Size", tr: "Çanta Kapasitesi");
            Add("upgrade_cost",     ru: "Цена: {0}",     en: "Cost: {0}",    tr: "Fiyat: {0}");

            // ── Миньоны ──────────────────────────────────────────────────────
            Add("minion_shop",      ru: "Миньоны",       en: "Minions",      tr: "Minyonlar");
            Add("hire_minion",      ru: "Нанять миньона", en: "Hire Minion", tr: "Minyon kirala");
            Add("minion_strength",  ru: "Сила миньона",  en: "Minion Strength", tr: "Minyon Gücü");
            Add("minion_capacity",  ru: "Вместимость",   en: "Capacity",     tr: "Kapasite");

            // ── Остров / Мир ─────────────────────────────────────────────────
            Add("create_island",    ru: "Создать остров", en: "Create Island", tr: "Ada Oluştur");
            Add("to_lobby",         ru: "В лобби",        en: "To Lobby",     tr: "Lobiye Git");
            Add("to_island",        ru: "На остров",      en: "To Island",    tr: "Adaya Git");
            Add("island",           ru: "Остров",         en: "Island",       tr: "Ada");
            Add("lobby",            ru: "Лобби",          en: "Lobby",        tr: "Lobi");

            Add("btn_mine",      ru: "КОПАТЬ",    en: "MINE",     tr: "KAZI");
            Add("btn_jump",      ru: "ПРЫЖОК",    en: "JUMP",     tr: "ZIPLA");
            Add("btn_run",       ru: "СПРИНТ",    en: "RUN",      tr: "KOŞ");
            Add("btn_act",       ru: "АКТ",       en: "ACT",      tr: "EYLEM");
            Add("btn_minions",   ru: "МИНЬОНЫ",   en: "MINIONS",  tr: "MINYONLAR");
            Add("btn_place",     ru: "ПОСТАВИТЬ", en: "PLACE",    tr: "YERLEŞTIR");
            Add("btn_del",       ru: "УДАЛИТЬ",   en: "DEL",      tr: "SİL");
            Add("btn_to_island", ru: "НА ОСТРОВ", en: "TO ISLAND",tr: "ADAYA GİT");
            Add("btn_to_lobby",  ru: "В ЛОББИ",   en: "TO LOBBY", tr: "LOBİYE GİT");
            Add("btn_cancel",    ru: "ОТМЕНА",    en: "CANCEL",   tr: "İPTAL");
            Add("btn_close",     ru: "Закрыть",   en: "Close",    tr: "Kapat");
            Add("btn_sell_mine", ru: "Продать шахту", en: "Sell Mine", tr: "Madeni Sat");
            Add("btn_set_spawn", ru: "Точка старта",  en: "Set Spawn", tr: "Başlangıç Noktası");

            // ── Магазин шахт — дополнительные строки ────────────────────────
            Add("mine_shop_title",    ru: "МАГАЗИН ШАХТ",  en: "MINE SHOP",    tr: "MADEN DÜKKANI");
            Add("shop_balance",       ru: "Баланс",        en: "Balance",      tr: "Bakiye");
            Add("mine_depth_format",  ru: "🕳 Глубина: {0}-{1} слоёв", en: "🕳 Depth: {0}-{1} layers", tr: "🕳 Derinlik: {0}-{1} katman");

            // ── Настройки / Язык ─────────────────────────────────────────────
            Add("settings",         ru: "Настройки",     en: "Settings",     tr: "Ayarlar");
            Add("language",         ru: "Язык",          en: "Language",     tr: "Dil");
            Add("lang_ru",          ru: "Русский",       en: "Russian",      tr: "Rusça");
            Add("lang_en",          ru: "Английский",    en: "English",      tr: "İngilizce");
            Add("lang_tr",          ru: "Турецкий",      en: "Turkish",      tr: "Türkçe");

            // ── Туториал ─────────────────────────────────────────────────────
            Add("tut_controls_title",  ru: "УПРАВЛЕНИЕ",   en: "CONTROLS",     tr: "KONTROLLER");
            Add("tut_controls_pc",
                ru: "Движение:  W A S D\nПрыжок:  Пробел\nКопать:  Левая Кнопка Мыши\n\nНажми любую клавишу или кликни чтобы начать.",
                en: "Movement:  W A S D\nJump:   Space\nDig:   Left Mouse Button\n\nPress any key or click to start.",
                tr: "Hareket:  W A S D\nZıpla:   Boşluk\nKaz:   Sol Fare Tuşu\n\nBaşlamak için herhangi bir tuşa bas.");
            Add("tut_controls_mob",
                ru: "Это джойстик движения.\nДвигай его чтобы идти вперёд.",
                en: "This is the movement joystick.\nMove it to walk forward.",
                tr: "Bu hareket joystick'i.\nİlerlemek için hareket ettir.");
            Add("tut_buttons_title",   ru: "КНОПКИ",        en: "BUTTONS",      tr: "DÜĞMELER");
            Add("tut_buttons_body",
                ru: "КОПАТЬ — добыть блок\nПРЫЖОК — прыжок\nДЕЙСТВИЕ — взаимодействие\nБЕГ — ускорение\nМИНЬОНЫ — меню миньонов\n\nНажми на экран чтобы продолжить.",
                en: "MINE  — dig block\nJUMP  — jump\nACT   — interact\nRUN   — sprint\nMINIONS — minions menu\n\nTap the screen to continue.",
                tr: "KAZI — blok kaz\nZIPLA — zıpla\nEYLEM — etkileş\nKOŞ — sprint\nMİNYONLAR — minyon menüsü\n\nDevam etmek için ekrana dokun.");
            Add("tut_island_title",    ru: "ТВОЙ ОСТРОВ",   en: "YOUR ISLAND",  tr: "ADAN");
            Add("tut_island_body",
                ru: "Вот твой остров, дорогой шахтёр!\nИсследуй его. Когда будешь готов —\nвернись в лобби.",
                en: "Here is your island, dear miner!\nExplore it. When you're ready —\ngo back to the lobby.",
                tr: "İşte adan, sevgili madenci!\nKeşfet. Hazır olduğunda —\nlobiye geri dön.");
            Add("tut_buy_mine_title",  ru: "ПЕРВАЯ ШАХТА",  en: "FIRST MINE",   tr: "İLK MADEN");
            Add("tut_buy_mine_body",
                ru: "Для начала купи свою первую шахту.\nЛуч указывает на магазин шахт.",
                en: "To start, purchase your first mine.\nThe beam points to the mine shop.",
                tr: "Başlamak için ilk madenini satın al.\nIşık huzmesi maden dükkanını gösteriyor.");
            Add("tut_place_mine_title", ru: "ПОСТАВЬ ШАХТУ", en: "PLACE MINE",  tr: "MADENİ YERLEŞTİR");
            Add("tut_place_mine_body",
                ru: "Отлично! Вернись на остров\nи поставь купленную шахту.",
                en: "Great! Return to your island\nand place the purchased mine.",
                tr: "Harika! Adana geri dön\nve satın aldığın madeni yerleştir.");
            Add("tut_mining_title",    ru: "ДОБЫЧА",        en: "MINING",       tr: "KAZIM");
            Add("tut_mining_mob",
                ru: "Иди в шахту и тапни на блок который хочешь сломать.\nЗажми экран на блоке или нажми кнопку КОПАТЬ.",
                en: "Go to the mine and tap the block you want to break.\nHold the screen on the block or press the MINE button.",
                tr: "Madene git ve kırmak istediğin bloğa dokun.\nBloğu basılı tut veya KAZI butonuna bas.");
            Add("tut_mining_pc",
                ru: "Иди в свою шахту.\nНажимай ЛКМ на блоки чтобы копать.\nНаполни рюкзак до отказа!",
                en: "Go to your mine.\nClick LEFT MOUSE BUTTON on a block to dig.\nFill your backpack to the brim!",
                tr: "Madene git.\nKazmak için bloğa SOL FARE TUŞU ile tıkla.\nSırt çantanı ağzına kadar doldur!");
            Add("tut_backpack_title",  ru: "РЮКЗАК ПОЛНЫЙ!", en: "BACKPACK FULL!", tr: "ÇANTA DOLU!");
            Add("tut_backpack_body",
                ru: "Отлично! Пора разгрузиться.\nВернись в лобби — луч покажет точку продажи руды.",
                en: "Great job! Time to unload.\nReturn to the lobby — the beam will show the ore selling point.",
                tr: "Harika iş! Boşaltma zamanı.\nLobiye dön — ışık huzmesi cevher satış noktasını gösterir.");
            Add("tut_sell_title",      ru: "ПРОДАЙ РУДУ",   en: "SELL ORE",     tr: "CEVHER SAT");
            Add("tut_sell_body",
                ru: "Иди к точке продажи и сдай содержимое рюкзака.\nЛуч указывает путь.",
                en: "Go to the selling point and turn in your backpack contents.\nThe beam points the way.",
                tr: "Satış noktasına git ve çanta içeriğini teslim et.\nIşık huzmesi yolu gösteriyor.");
            Add("tut_upgrade_title",   ru: "ПРОКАЧАЙ СНАРЯЖЕНИЕ", en: "UPGRADE EQUIPMENT", tr: "EKİPMANI GELİŞTİR");
            Add("tut_upgrade_body",
                ru: "Хочешь копать быстрее и добраться до редких руд?\nИди в магазин кирок и прокачай снаряжение.",
                en: "Want to dig faster and reach rare ores?\nGo to the pickaxe shop and upgrade your gear.",
                tr: "Daha hızlı kazmak ve nadir cevherlere ulaşmak ister misin?\nKazma dükkanına git ve ekipmanını geliştir.");
            Add("tut_minion_title",    ru: "АВТОМАТИЗИРУЙ ШАХТУ", en: "AUTOMATE THE MINE", tr: "MADENİ OTOMATIZE ET");
            Add("tut_minion_body",
                ru: "Хочешь автоматизировать добычу —\nпридёшь сюда и наймёшь своего первого рабочего!\n\nНажми любую кнопку / тапни чтобы закрыть.",
                en: "If you want to automate your mining —\ncome here and hire your first minion worker!\n\nPress any key / tap to close.",
                tr: "Kazımını otomatize etmek istiyorsan —\nburaya gel ve ilk minyonunu kirala!\n\nKapatmak için herhangi bir tuşa bas / dokun.");
            Add("tut_tap_hint",
                ru: "Нажми на экран чтобы продолжить ›",
                en: "Tap the screen to continue ›",
                tr: "Devam etmek için ekrana dokun ›");
            Add("tut_create_island_title", ru: "СОЗДАТЬ ОСТРОВ",  en: "CREATE ISLAND", tr: "ADA OLUŞTUR");
            Add("tut_create_island_body",
                ru: "Нажми кнопку Создать Остров.\nДо этого все остальные действия заблокированы.",
                en: "Press the Create Island button.\nUntil then, all other actions are blocked.",
                tr: "Ada Oluştur butonuna bas.\nO zamana kadar diğer tüm eylemler bloklanır.");
        }

        // ── Инициализация ────────────────────────────────────────────────────

        /// <summary>
        /// Вызывать при старте игры (например из Awake/Start любого MonoBehaviour).
        /// Определяет язык из YG2.lang или PlayerPrefs.
        /// </summary>
        public static void Initialize()
        {
            string saved = PlayerPrefs.GetString(LangPrefKey, string.Empty);

            if (!string.IsNullOrWhiteSpace(saved) && IsSupported(saved))
            {
                // Игрок ранее выбрал язык вручную — уважаем его выбор
                _currentLang = saved.ToLowerInvariant();
            }
            else
            {
                // Берём из Яндекса
                string yandexLang = GetYandexLang();
                _currentLang = IsSupported(yandexLang) ? yandexLang : LangRu;
            }

            Debug.Log($"[Loc] Language initialized: {_currentLang}");
        }

        // ── API ──────────────────────────────────────────────────────────────

        /// <summary>Перевести ключ на текущий язык.</summary>
        public static string T(string key)
        {
            if (_strings.TryGetValue(key, out var langs))
            {
                if (langs.TryGetValue(_currentLang, out string val) && !string.IsNullOrEmpty(val))
                    return val;
                // fallback EN
                if (langs.TryGetValue(LangEn, out string enVal) && !string.IsNullOrEmpty(enVal))
                    return enVal;
                // fallback RU
                if (langs.TryGetValue(LangRu, out string ruVal))
                    return ruVal;
            }
            return $"[{key}]"; // отсутствующий ключ виден сразу
        }

        /// <summary>Перевод с форматированием: Loc.Tf("req_level", 5) → "Требуется ур. 5"</summary>
        public static string Tf(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch { return T(key); }
        }

        /// <summary>Сменить язык программно. Сохраняет выбор в PlayerPrefs.</summary>
        public static void SetLanguage(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return;
            lang = lang.ToLowerInvariant().Trim();
            if (!IsSupported(lang)) return;
            if (_currentLang == lang) return;

            _currentLang = lang;
            PlayerPrefs.SetString(LangPrefKey, lang);
            PlayerPrefs.Save();

            Debug.Log($"[Loc] Language changed to: {lang}");
            OnLanguageChanged?.Invoke();
        }

        /// <summary>Сбросить выбор языка — будет определён из YG2.lang заново.</summary>
        public static void ResetToAuto()
        {
            PlayerPrefs.DeleteKey(LangPrefKey);
            Initialize();
            OnLanguageChanged?.Invoke();
        }

        /// <summary>Добавить/перезаписать строку во время выполнения.</summary>
        public static void AddString(string key, string ru = null, string en = null, string tr = null)
        {
            Add(key, ru, en, tr);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void Add(string key, string ru = null, string en = null, string tr = null)
        {
            if (!_strings.ContainsKey(key))
                _strings[key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (ru != null) _strings[key][LangRu] = ru;
            if (en != null) _strings[key][LangEn] = en;
            if (tr != null) _strings[key][LangTr] = tr;
        }

        private static bool IsSupported(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return false;
            lang = lang.ToLowerInvariant().Trim();
            return lang == LangRu || lang == LangEn || lang == LangTr;
        }

        private static string GetYandexLang()
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                string lang = YG2.lang;
                if (!string.IsNullOrWhiteSpace(lang))
                    return lang.ToLowerInvariant().Trim();
#endif
            }
            catch { /* SDK может быть не инициализирован */ }
            return LangRu;
        }
    }
}
