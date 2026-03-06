using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using YG;
using YG.Utils;

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
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr SVS_GetYandexSdkLanguage();
#endif

        // ── Поддерживаемые языки ─────────────────────────────────────────────
        public const string LangRu = "ru";
        public const string LangEn = "en";
        public const string LangTr = "tr";

        private const string LangPrefKey = "svs_ui_language";
        private const string YgLangPrefKey = "langYG";

        // ── Текущий язык ─────────────────────────────────────────────────────
        private static string _currentLang = LangRu;
        public static string CurrentLanguage => _currentLang;
        public static bool HasManualOverride => PlayerPrefs.HasKey(LangPrefKey);

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
            Add("minion_shop_title",ru: "МАГАЗИН МИНЬОНОВ", en: "MINION SHOP", tr: "MINYON DUKKANI");
            Add("hire_minion",      ru: "Нанять миньона", en: "Hire Minion", tr: "Minyon kirala");
            Add("minion_strength",  ru: "Сила миньона",  en: "Minion Strength", tr: "Minyon Gücü");
            Add("minion_capacity",  ru: "Вместимость",   en: "Capacity",     tr: "Kapasite");
            Add("minion_standard_name", ru: "Стандартный миньон", en: "Standard Minion", tr: "Standart Minyon");
            Add("minion_standard_desc", ru: "Небольшой помощник, который копает за тебя.", en: "A small helper to mine for you.", tr: "Senin yerine kazacak kucuk bir yardimci.");

            // ── Остров / Мир ─────────────────────────────────────────────────
            Add("create_island",    ru: "Создать остров", en: "Create Island", tr: "Ada Oluştur");
            Add("to_lobby",         ru: "В лобби",        en: "To Lobby",     tr: "Lobiye Git");
            Add("to_island",        ru: "На остров",      en: "To Island",    tr: "Adaya Git");
            Add("island",           ru: "Остров",         en: "Island",       tr: "Ada");
            Add("lobby",            ru: "Лобби",          en: "Lobby",        tr: "Lobi");

            Add("btn_mine",      ru: "КОПАТЬ",    en: "MINE",     tr: "KAZI");
            Add("btn_jump",      ru: "ПРЫЖОК",    en: "JUMP",     tr: "ZIPLA");
            Add("btn_run",       ru: "СПРИНТ",    en: "RUN",      tr: "KOŞ");
            Add("btn_act",       ru: "ВЗАИМОД.",  en: "ACT",      tr: "EYLEM");
            Add("btn_sell",      ru: "ПРОДАТЬ",   en: "SELL",     tr: "SAT");
            Add("btn_mines",     ru: "ШАХТЫ",    en: "MINES",    tr: "MADENLER");
            Add("btn_upgrades",  ru: "УЛУЧШЕНИЯ", en: "UPGRADES", tr: "YÜKSELTMELER");
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

            // ── Ресурсы / Блоки ──────────────────────────────────────────────
            Add("block_dirt",       ru: "Земля",         en: "Dirt",         tr: "Toprak");
            Add("block_stone",      ru: "Камень",        en: "Stone",        tr: "Taş");
            Add("block_iron",       ru: "Железо",        en: "Iron",         tr: "Demir");
            Add("block_gold",       ru: "Золото",        en: "Gold",         tr: "Altın");

            // ── Характеристики и статусы ─────────────────────────────────────
            Add("pickaxe_shop_title", ru: "МАГАЗИН КИРОК", en: "PICKAXE SHOP", tr: "KAZMA DÜKKANI");
            Add("mining_level_label", ru: "Уровень копания", en: "Mining Level", tr: "Madencilik Seviyesi");
            Add("mining_level_format",ru: "{0}: {1} ({2} {3})", en: "{0}: {1} ({2} {3})", tr: "{0}: {1} ({2} {3})");
            Add("lv_short",         ru: "Ур.",           en: "Lv.",           tr: "Sv.");
            Add("xp_short",         ru: "ОП",           en: "XP",           tr: "TP");
            
            Add("stats_power",      ru: "Сила",          en: "Power",        tr: "Güç");
            Add("stats_req_lv",     ru: "Нужен Ур.",     en: "Req Lv",       tr: "Gerekli Sv.");
            Add("stats_price",      ru: "Цена",          en: "Price",        tr: "Fiyat");
            Add("btn_buy",          ru: "КУПИТЬ",        en: "BUY",          tr: "SATIN AL");
            Add("btn_owned",        ru: "КУПЛЕНО",       en: "OWNED",        tr: "SAHİP");
            Add("btn_equipped",     ru: "ЭКИПИРОВАНО",    en: "EQUIPPED",     tr: "KUŞANILDI");
            Add("balance_header",   ru: "Баланс: ${0}  |  [B]/[X] закрыть", en: "Balance: ${0}  |  [B]/[X] close", tr: "Bakiye: ${0}  |  [B]/[X] kapat");
            Add("balance_bar_format",ru: "${0}  |  {1} {2} ({3} {4})", en: "${0}  |  {1} {2} ({3} {4})", tr: "${0}  |  {1} {2} ({3} {4})");

            // ── Боковые панели улучшений ─────────────────────────────────────
            Add("upgrade_str_title",  ru: "СИЛА",          en: "STRENGTH",      tr: "GÜÇ");
            Add("upgrade_str_subtitle", ru: "Урон по блокам", en: "Block Damage", tr: "Blok Hasari");
            Add("upgrade_str_stats",  ru: "Текущий бонус:\n<color=#FFD700>+{0}</color>", en: "Current Bonus:\n<color=#FFD700>+{0}</color>", tr: "Mevcut Bonus:\n<color=#FFD700>+{0}</color>");
            Add("upgrade_bp_title",   ru: "РЮКЗАК",        en: "BACKPACK",      tr: "SIRT ÇANTASI");
            Add("upgrade_bp_subtitle", ru: "Лимит руды", en: "Ore Capacity", tr: "Cevher Kapasitesi");
            Add("upgrade_bp_stats",   ru: "Текущая ёмкость:\n<color=#00EAFF>{0}</color>", en: "Current Capacity:\n<color=#00EAFF>{0}</color>", tr: "Mevcut Kapasite:\n<color=#00EAFF>{0}</color>");
            Add("upgrade_btn_format", ru: "УЛУЧШИТЬ: ${0}", en: "UPGRADE: ${0}", tr: "YÜKSELT: ${0}");

            // ── Названия и описания шахт ─────────────────────────────────────
            Add("mine_bronze_name", ru: "Бронзовая шахта", en: "Bronze Mine",  tr: "Bronz Maden");
            Add("mine_bronze_desc", ru: "Небольшая, в основном земля и камень.", en: "Small, mostly dirt and stone.", tr: "Küçük, çoğunlukla toprak ve taş.");
            Add("mine_silver_name", ru: "Серебряная шахта", en: "Silver Mine",  tr: "Gümüş Maden");
            Add("mine_silver_desc", ru: "Средняя. Железо и немного золота на глубине.", en: "Medium. Iron and some gold at depth.", tr: "Orta. Derinlikte demir ve biraz altın.");
            Add("mine_gold_name",   ru: "Золотая шахта",   en: "Gold Mine",    tr: "Altın Maden");
            Add("mine_gold_desc",   ru: "Глубокая. Много железа и золота.", en: "Deep. Lots of iron and gold.", tr: "Derin. Çok fazla demir ve altın.");

            // ── Названия и описания кирок ────────────────────────────────────
            Add("pickaxe_stone_name",  ru: "Каменная кирка",   en: "Stone Pickaxe",  tr: "Taş Kazma");
            Add("pickaxe_stone_desc",  ru: "Быстрее стандартной.", en: "Faster than default.", tr: "Varsayılandan daha hızlı.");
            Add("pickaxe_iron_name",   ru: "Железная кирка",   en: "Iron Pickaxe",   tr: "Demir Kazma");
            Add("pickaxe_iron_desc",   ru: "Хорошее улучшение для добычи.", en: "Solid upgrade for mining.", tr: "Madencilik için sağlam bir yükseltme.");
            Add("pickaxe_gold_name",   ru: "Золотая кирка",    en: "Gold Pickaxe",   tr: "Altın Kazma");
            Add("pickaxe_gold_desc",   ru: "Очень быстрая, но дорогая.", en: "Very fast but expensive.", tr: "Çok hızlı ama pahalı.");
            Add("pickaxe_diamond_name",ru: "Алмазная кирка",   en: "Diamond Pickaxe",tr: "Elmas Kazma");
            Add("pickaxe_diamond_desc",ru: "Кирка высшего уровня.",     en: "Top tier pickaxe.", tr: "En üst seviye kazma.");
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

            // ── Подсказки зон магазина ────────────────────────────────────────
            Add("sell_point",           ru: "Точка продажи",    en: "Sell Point",       tr: "Satış Noktası");
            Add("zone_tap_open",        ru: "Тап <color=#FFD700><b>[{0}]</b></color> чтобы открыть {1}",  en: "Tap <color=#FFD700><b>[{0}]</b></color> to open {1}",    tr: "<color=#FFD700><b>[{0}]</b></color>'a dokun, {1} aç");
            Add("zone_press_open",      ru: "Нажми <color=#FFD700><b>[{0}]</b></color> чтобы открыть {1}",en: "Press <color=#FFD700><b>[{0}]</b></color> to open {1}",  tr: "<color=#FFD700><b>[{0}]</b></color> tuşuna bas, {1} aç");
            Add("zone_tap_sell",        ru: "Тап <color=#FFD700><b>[{0}]</b></color> чтобы продать ресурсы",    en: "Tap <color=#FFD700><b>[{0}]</b></color> to sell resources",    tr: "<color=#FFD700><b>[{0}]</b></color>'a dokun, kaynakları sat");
            Add("zone_press_sell",      ru: "Нажми <color=#FFD700><b>[{0}]</b></color> чтобы продать ресурсы",  en: "Press <color=#FFD700><b>[{0}]</b></color> to sell resources",  tr: "<color=#FFD700><b>[{0}]</b></color> tuşuna bas, kaynakları sat");

            // ── Статусы размещения ──────────────────────────────────────────
            Add("status_mine_bought",   ru: "<color=yellow>Шахта куплена.</color> Иди на Остров чтобы поставить её.", en: "<color=yellow>Mine purchased.</color> Go to Island to place it.", tr: "<color=yellow>Maden satin alindi.</color> Yerlestirmek icin Adaya git.");
            Add("status_placement",     ru: "<color=yellow>Режим размещения.</color> Выбери место и нажми ПОСТАВИТЬ.", en: "<color=yellow>Placement mode.</color> Choose position and press PLACE.", tr: "<color=yellow>Yerlestirme modu.</color> Bir konum sec ve YERLESTIR'e bas.");
            Add("status_not_enough_money_detail", ru: "Не хватает денег. Нужно {0}, у тебя {1}.", en: "Not enough money. Need {0}, have {1}.", tr: "Yeterli para yok. Gereken: {0}, sende olan: {1}.");
            Add("status_place_mine_hint", ru: "ЛКМ чтобы поставить {0}. Esc чтобы отменить.", en: "Left click to place {0}. Esc to cancel.", tr: "{0} yerlestirmek icin sol tikla. Iptal icin Esc.");
            Add("status_mine_placed", ru: "Шахта {0} установлена. Глубина: {1}.", en: "Mine {0} placed. Depth: {1}.", tr: "{0} madeni yerlestirildi. Derinlik: {1}.");
            Add("status_mine_sold", ru: "Шахта продана за {0}.", en: "Mine sold for {0}.", tr: "Maden {0} karsiliginda satildi.");
            Add("status_placement_cancelled", ru: "Размещение отменено. Деньги возвращены.", en: "Placement canceled. Money returned.", tr: "Yerlestirme iptal edildi. Para iade edildi.");
            Add("status_spawn_saved", ru: "Точка спавна сохранена на острове.", en: "Spawn point saved on island.", tr: "Dogma noktasi adada kaydedildi.");
            Add("status_spawn_save_failed", ru: "Нельзя сохранить спавн здесь. Встань на твердую землю острова.", en: "Can't save spawn here. Stand on solid island ground.", tr: "Burada dogma noktasi kaydedilemez. Adadaki saglam zeminde dur.");
        }

        // ── Инициализация ────────────────────────────────────────────────────

        /// <summary>
        /// Определяет язык из PlayerPrefs или YG2.lang и уведомляет все подписчики.
        /// Вызывается только из LocalizationManager — единственного владельца инициализации.
        /// </summary>
        public static void Initialize()
        {
            string saved = PlayerPrefs.GetString(LangPrefKey, string.Empty);

            if (!string.IsNullOrWhiteSpace(saved) && IsSupported(saved))
            {
                _currentLang = saved.ToLowerInvariant();
            }
            else
            {
                string yandexLang = GetYandexLang();
                _currentLang = IsSupported(yandexLang) ? yandexLang : LangRu;
            }

            Debug.Log($"[Loc] Language initialized: {_currentLang}");

            // Всегда уведомляем подписчиков — независимо от того, совпадает язык или нет.
            // Это гарантирует что UI построенный ДО Initialize() обновит свои тексты.
            OnLanguageChanged?.Invoke();
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
            PlayerPrefs.SetString(LangPrefKey, lang);
            PlayerPrefs.Save();

#if Localization_yg
            YG2.SwitchLanguage(lang);
#endif

            if (_currentLang == lang) return;

            _currentLang = lang;

            Debug.Log($"[Loc] Language changed to: {lang}");
            OnLanguageChanged?.Invoke();
        }

        /// <summary>Сбросить выбор языка — будет определён из YG2.lang заново.</summary>
        public static void ResetToAuto()
        {
            PlayerPrefs.DeleteKey(LangPrefKey);
#if Localization_yg
            LocalStorage.DeleteKey(YgLangPrefKey);
            YG2.GetLanguage();
#endif
            Initialize();
        }

        /// <summary>
        /// Re-reads platform language only when the player has not chosen a language manually.
        /// Useful for SDK mock tools that switch language after the game has already started.
        /// </summary>
        public static void RefreshFromPlatformLanguageIfAuto()
        {
            if (HasManualOverride)
                return;

            string platformLang = GetYandexLang();
            if (!IsSupported(platformLang))
                return;

            platformLang = platformLang.ToLowerInvariant().Trim();
            if (_currentLang == platformLang)
                return;

            _currentLang = platformLang;
            Debug.Log($"[Loc] Platform language changed to: {platformLang}");
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
#if Localization_yg
                if (!string.IsNullOrWhiteSpace(YG2.lang))
                    return NormalizeLanguageCode(YG2.lang);
#endif

                string webglLang = TryGetYG2LangFromWebGLBridge();
                if (!string.IsNullOrWhiteSpace(webglLang))
                    return NormalizeLanguageCode(webglLang);

                string lang = TryGetYG2LangByReflection();
                if (!string.IsNullOrWhiteSpace(lang))
                    return NormalizeLanguageCode(lang);
            }
            catch { /* SDK может быть не инициализирован */ }

            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                    return LangRu;
                case SystemLanguage.Turkish:
                    return LangTr;
                default:
                    return LangEn;
            }
        }

        private static string TryGetYG2LangByReflection()
        {
            Type yg2Type = typeof(YG2);

            string direct = ReadStringMember(null, yg2Type, "lang")
                ?? ReadStringMember(null, yg2Type, "language");
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;

            object envir = ReadObjectMember(null, yg2Type, "envir");
            string envirLang = ReadStringMember(envir, envir?.GetType(), "language")
                ?? ReadStringMember(envir, envir?.GetType(), "lang");
            if (!string.IsNullOrWhiteSpace(envirLang))
                return envirLang;

#if UNITY_EDITOR
            object info = ReadObjectMember(null, yg2Type, "infoYG");
            object simulation = ReadObjectMember(info, info?.GetType(), "Simulation");
            string simulationLang = ReadStringMember(simulation, simulation?.GetType(), "language");
            if (!string.IsNullOrWhiteSpace(simulationLang))
                return simulationLang;
#endif

            return null;
        }

        private static string TryGetYG2LangFromWebGLBridge()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                IntPtr langPtr = SVS_GetYandexSdkLanguage();
                if (langPtr == IntPtr.Zero)
                    return null;

                string lang = Marshal.PtrToStringAnsi(langPtr);
                if (string.IsNullOrWhiteSpace(lang))
                    return null;

                int dashIndex = lang.IndexOf('-');
                if (dashIndex > 0)
                    lang = lang.Substring(0, dashIndex);

                return lang;
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }

        private static object ReadObjectMember(object instance, Type type, string memberName)
        {
            if (type == null)
                return null;

            const System.Reflection.BindingFlags Flags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Instance;

            var prop = type.GetProperty(memberName, Flags);
            if (prop != null)
                return prop.GetValue(instance, null);

            var field = type.GetField(memberName, Flags);
            if (field != null)
                return field.GetValue(instance);

            return null;
        }

        private static string ReadStringMember(object instance, Type type, string memberName)
        {
            object value = ReadObjectMember(instance, type, memberName);
            return value as string;
        }

        private static string NormalizeLanguageCode(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
                return null;

            lang = lang.ToLowerInvariant().Trim();

            int dashIndex = lang.IndexOf('-');
            if (dashIndex > 0)
                lang = lang.Substring(0, dashIndex);

            if (lang == "us" || lang == "as" || lang == "ai")
                return LangEn;

            return lang;
        }
    }
}
