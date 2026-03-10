using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public const string LangAr = "ar";
        public const string LangAz = "az";
        public const string LangBe = "be";
        public const string LangBg = "bg";
        public const string LangCa = "ca";
        public const string LangCs = "cs";
        public const string LangDe = "de";
        public const string LangEs = "es";
        public const string LangFa = "fa";
        public const string LangFr = "fr";
        public const string LangHe = "he";
        public const string LangHi = "hi";
        public const string LangHu = "hu";
        public const string LangHy = "hy";
        public const string LangId = "id";
        public const string LangIt = "it";
        public const string LangJa = "ja";
        public const string LangKa = "ka";
        public const string LangKk = "kk";
        public const string LangNl = "nl";
        public const string LangPl = "pl";
        public const string LangPt = "pt";
        public const string LangRo = "ro";
        public const string LangSk = "sk";
        public const string LangSr = "sr";
        public const string LangTh = "th";
        public const string LangTk = "tk";
        public const string LangUk = "uk";
        public const string LangUz = "uz";
        public const string LangVi = "vi";
        public const string LangZh = "zh";

        public readonly struct LanguageInfo
        {
            public LanguageInfo(string code, string nativeName, string fallbackLanguage)
            {
                Code = code;
                NativeName = nativeName;
                FallbackLanguage = fallbackLanguage;
            }

            public string Code { get; }
            public string NativeName { get; }
            public string FallbackLanguage { get; }
        }

        private static readonly ReadOnlyCollection<LanguageInfo> _supportedLanguages =
            Array.AsReadOnly(new[]
            {
                new LanguageInfo(LangRu, "Русский", LangRu),
                new LanguageInfo(LangEn, "English", LangEn),
                new LanguageInfo(LangTr, "Türkçe", LangTr),
                new LanguageInfo(LangAr, "العربية", LangEn),
                new LanguageInfo(LangAz, "Azərbaycanca", LangEn),
                new LanguageInfo(LangBe, "Беларуская", LangRu),
                new LanguageInfo(LangBg, "Български", LangEn),
                new LanguageInfo(LangCa, "Català", LangEn),
                new LanguageInfo(LangCs, "Čeština", LangEn),
                new LanguageInfo(LangDe, "Deutsch", LangEn),
                new LanguageInfo(LangEs, "Español", LangEn),
                new LanguageInfo(LangFa, "فارسی", LangEn),
                new LanguageInfo(LangFr, "Français", LangEn),
                new LanguageInfo(LangHe, "עברית", LangEn),
                new LanguageInfo(LangHi, "हिन्दी", LangEn),
                new LanguageInfo(LangHu, "Magyar", LangEn),
                new LanguageInfo(LangHy, "Հայերեն", LangEn),
                new LanguageInfo(LangId, "Bahasa Indonesia", LangEn),
                new LanguageInfo(LangIt, "Italiano", LangEn),
                new LanguageInfo(LangJa, "日本語", LangEn),
                new LanguageInfo(LangKa, "ქართული", LangEn),
                new LanguageInfo(LangKk, "Қазақша", LangRu),
                new LanguageInfo(LangNl, "Nederlands", LangEn),
                new LanguageInfo(LangPl, "Polski", LangEn),
                new LanguageInfo(LangPt, "Português", LangEn),
                new LanguageInfo(LangRo, "Română", LangEn),
                new LanguageInfo(LangSk, "Slovenčina", LangEn),
                new LanguageInfo(LangSr, "Српски", LangEn),
                new LanguageInfo(LangTh, "ไทย", LangEn),
                new LanguageInfo(LangTk, "Türkmençe", LangEn),
                new LanguageInfo(LangUk, "Українська", LangRu),
                new LanguageInfo(LangUz, "O'zbek", LangRu),
                new LanguageInfo(LangVi, "Tiếng Việt", LangEn),
                new LanguageInfo(LangZh, "中文", LangEn)
            });

        private static readonly Dictionary<string, LanguageInfo> _languageInfoByCode =
            new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyList<LanguageInfo> SupportedLanguages => _supportedLanguages;

        private const string LangPrefKey = "svs_ui_language";
        private const string YgLangPrefKey = "langYG";

        // ── Текущий язык ─────────────────────────────────────────────────────
        private static string _currentLang = LangRu;
        public static string CurrentLanguage => _currentLang;
        public static bool HasManualOverride => PlayerPrefs.HasKey(LangPrefKey);
        private static bool _buildKeysInjected;

        /// <summary>Срабатывает при смене языка. Подпишитесь чтобы обновить UI.</summary>
        public static event Action OnLanguageChanged;

        // ── Словарь переводов ─────────────────────────────────────────────────
        // Формат: strings[key][lang] = "перевод"
        private static readonly Dictionary<string, Dictionary<string, string>> _strings =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        // ── Статический конструктор — заполняем все строки ───────────────────
        static Loc()
        {
            foreach (LanguageInfo info in _supportedLanguages)
                _languageInfoByCode[info.Code] = info;

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
            Add("account",          ru: "Аккаунт",       en: "Account",      tr: "Hesap");
            Add("player_name_label",ru: "Игрок",         en: "Player",       tr: "Oyuncu");
            Add("best_money_label", ru: "Лучший баланс", en: "Best Balance", tr: "En Iyi Bakiye");
            Add("auth_sign_in",     ru: "Войти через Яндекс",         en: "Sign In with Yandex",      tr: "Yandex ile Giris Yap");
            Add("auth_connected",   ru: "Подключено",    en: "Connected",    tr: "Baglandi");
            Add("guest_player",     ru: "Гость",         en: "Guest",        tr: "Misafir");
            Add("auth_guest_hint",  ru: "Войдите через Яндекс, чтобы синхронизировать прогресс между устройствами.", en: "Sign in with Yandex to sync progress across devices.", tr: "Ilerlemeni cihazlar arasynda senkronize etmek icin Yandex ile giris yap.");
            Add("auth_connected_hint",  ru: "Прогресс можно сохранять и синхронизировать между устройствами.", en: "Progress can be saved and synced across devices.", tr: "Ilerleme kaydedilebilir ve cihazlar arasynda senkronize edilebilir.");
            Add("ads_bonus_title",  ru: "Бонус за рекламу", en: "Ad Bonus", tr: "Reklam Bonusu");
            Add("ads_bonus_desc",   ru: "Посмотри rewarded-рекламу и получи монеты.", en: "Watch a rewarded ad and get bonus coins.", tr: "Odullu reklam izle ve bonus altin kazan.");
            Add("ads_bonus_btn",    ru: "Смотреть рекламу: +{0} монет", en: "Watch ad: +{0} coins", tr: "Reklam izle: +{0} altin");

            // Multi-language core UI
            Set("settings", "ar", "الإعدادات"); Set("settings", "az", "Parametrlər"); Set("settings", "be", "Налады"); Set("settings", "bg", "Настройки");
            Set("settings", "ca", "Configuració"); Set("settings", "cs", "Nastavení"); Set("settings", "de", "Einstellungen"); Set("settings", "es", "Ajustes");
            Set("settings", "fa", "تنظیمات"); Set("settings", "fr", "Paramètres"); Set("settings", "he", "הגדרות"); Set("settings", "hi", "सेटिंग्स");
            Set("settings", "hu", "Beállítások"); Set("settings", "hy", "Կարգավորումներ"); Set("settings", "id", "Pengaturan"); Set("settings", "it", "Impostazioni");
            Set("settings", "ja", "設定"); Set("settings", "ka", "პარამეტრები"); Set("settings", "kk", "Баптаулар"); Set("settings", "nl", "Instellingen");
            Set("settings", "pl", "Ustawienia"); Set("settings", "pt", "Configurações"); Set("settings", "ro", "Setări"); Set("settings", "sk", "Nastavenia");
            Set("settings", "sr", "Подешавања"); Set("settings", "th", "การตั้งค่า"); Set("settings", "tk", "Sazlamalar"); Set("settings", "uk", "Налаштування");
            Set("settings", "uz", "Sozlamalar"); Set("settings", "vi", "Cài đặt"); Set("settings", "zh", "设置");

            Set("language", "ar", "اللغة"); Set("language", "az", "Dil"); Set("language", "be", "Мова"); Set("language", "bg", "Език");
            Set("language", "ca", "Idioma"); Set("language", "cs", "Jazyk"); Set("language", "de", "Sprache"); Set("language", "es", "Idioma");
            Set("language", "fa", "زبان"); Set("language", "fr", "Langue"); Set("language", "he", "שפה"); Set("language", "hi", "भाषा");
            Set("language", "hu", "Nyelv"); Set("language", "hy", "Լեզու"); Set("language", "id", "Bahasa"); Set("language", "it", "Lingua");
            Set("language", "ja", "言語"); Set("language", "ka", "ენა"); Set("language", "kk", "Тіл"); Set("language", "nl", "Taal");
            Set("language", "pl", "Język"); Set("language", "pt", "Idioma"); Set("language", "ro", "Limbă"); Set("language", "sk", "Jazyk");
            Set("language", "sr", "Језик"); Set("language", "th", "ภาษา"); Set("language", "tk", "Dil"); Set("language", "uk", "Мова");
            Set("language", "uz", "Til"); Set("language", "vi", "Ngôn ngữ"); Set("language", "zh", "语言");
            
            Set("close", "ar", "إغلاق"); Set("close", "az", "Bağlamaq"); Set("close", "be", "Зачыніць"); Set("close", "bg", "Затвори");
            Set("close", "ca", "Tancar"); Set("close", "cs", "Zavřít"); Set("close", "de", "Schließen"); Set("close", "es", "Cerrar");
            Set("close", "fa", "بستن"); Set("close", "fr", "Fermer"); Set("close", "he", "סגור"); Set("close", "hi", "बंद करें");
            Set("close", "hu", "Bezárás"); Set("close", "hy", "Փակել"); Set("close", "id", "Tutup"); Set("close", "it", "Chiudi");
            Set("close", "ja", "閉じる"); Set("close", "ka", "დახურვა"); Set("close", "kk", "Жабу"); Set("close", "nl", "Sluiten");
            Set("close", "pl", "Zamknij"); Set("close", "pt", "Fechar"); Set("close", "ro", "Închide"); Set("close", "sk", "Zatvoriť");
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
            foreach (LanguageInfo info in _supportedLanguages)
                Add("lang_" + info.Code, ru: info.NativeName, en: info.NativeName, tr: info.NativeName);

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
            Add("tut_place_mine_title", ru: "ПОСТАВЬ ШАХТУ", en: "PLACE MINE",  tr: "MADENİ YERLEШTİR");
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

            SeedCoreTranslations();
            PopulateMissingTranslationsFromFallbacks();
        }

        // ── Инициализация ────────────────────────────────────────────────────

        /// <summary>
        /// Определяет язык из PlayerPrefs или YG2.lang и уведомляет все подписчики.
        /// Вызывается только из LocalizationManager — единственного владельца инициализации.
        /// </summary>
        public static void Initialize()
        {
            EnsureBuildLocalizationKeys();
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
            EnsureBuildLocalizationKeys();
            if (_strings.TryGetValue(key, out var langs))
            {
                if (langs.TryGetValue(_currentLang, out string val) && !string.IsNullOrEmpty(val))
                    return val;
                string fallbackLang = GetFallbackLanguage(_currentLang);
                if (!string.IsNullOrEmpty(fallbackLang) &&
                    langs.TryGetValue(fallbackLang, out string fallbackVal) &&
                    !string.IsNullOrEmpty(fallbackVal))
                {
                    return fallbackVal;
                }
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

        public static string GetLanguageNativeName(string lang)
        {
            return TryGetLanguageInfo(lang, out LanguageInfo info)
                ? info.NativeName
                : string.IsNullOrWhiteSpace(lang) ? LangEn.ToUpperInvariant() : lang.ToUpperInvariant();
        }

        public static bool TryGetLanguageInfo(string lang, out LanguageInfo info)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                info = default;
                return false;
            }

            return _languageInfoByCode.TryGetValue(lang.Trim(), out info);
        }

        private static void EnsureBuildLocalizationKeys()
        {
            if (_buildKeysInjected)
                return;

            _buildKeysInjected = true;
            Add("btn_build", ru: "СТРОИТЬ", en: "BUILD", tr: "INSA ET");
            Add("btn_build_off", ru: "ЛОМАТЬ", en: "MINE", tr: "KAZ");
            Add("build_inventory", ru: "Строй-инвентарь", en: "Build Inventory", tr: "Insa Envanteri");
            Add("build_inventory_toggle", ru: "ИНВ", en: "INV", tr: "ENV");
            Add("build_tab_hint", ru: "Tab: {0}", en: "Tab: {0}", tr: "Tab: {0}");
            Add("build_need_block", ru: "Нужен блок: {0}", en: "Need block: {0}", tr: "Gerekli blok: {0}");
            Add("build_island_only", ru: "Строительство доступно только на твоем острове.", en: "Building is available only on your island.", tr: "Insaat sadece kendi adanda kullanilabilir.");
            Add("build_place_status", ru: "Построено: {0}", en: "Placed: {0}", tr: "Yerlestirildi: {0}");
            PopulateMissingTranslationsFromFallbacks();
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

        private static void Set(string key, string lang, string val)
        {
            if (!_strings.ContainsKey(key))
                _strings[key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _strings[key][lang] = val;
        }

        private static void PopulateMissingTranslationsFromFallbacks()
        {
            foreach (var keyEntry in _strings)
            {
                Dictionary<string, string> translations = keyEntry.Value;
                foreach (LanguageInfo info in _supportedLanguages)
                {
                    if (translations.TryGetValue(info.Code, out string existing) && !string.IsNullOrEmpty(existing))
                        continue;

                    if (translations.TryGetValue(info.FallbackLanguage, out string fallback) && !string.IsNullOrEmpty(fallback))
                    {
                        translations[info.Code] = fallback;
                        continue;
                    }

                    if (translations.TryGetValue(LangEn, out string en) && !string.IsNullOrEmpty(en))
                    {
                        translations[info.Code] = en;
                        continue;
                    }

                    if (translations.TryGetValue(LangRu, out string ru) && !string.IsNullOrEmpty(ru))
                        translations[info.Code] = ru;
                }
            }
        }

        private static void SeedCoreTranslations()
        {
            Set("settings", LangAr, "الإعدادات");
            Set("settings", LangAz, "Ayarlar");
            Set("settings", LangBe, "Налады");
            Set("settings", LangBg, "Настройки");
            Set("settings", LangCa, "Configuració");
            Set("settings", LangCs, "Nastavení");
            Set("settings", LangDe, "Einstellungen");
            Set("settings", LangEs, "Ajustes");
            Set("settings", LangFa, "تنظیمات");
            Set("settings", LangFr, "Paramètres");
            Set("settings", LangHe, "הגדרות");
            Set("settings", LangHi, "सेटिंग्स");
            Set("settings", LangHu, "Beállítások");
            Set("settings", LangHy, "Կարգավորումներ");
            Set("settings", LangId, "Pengaturan");
            Set("settings", LangIt, "Impostazioni");
            Set("settings", LangJa, "設定");
            Set("settings", LangKa, "პარამეტრები");
            Set("settings", LangKk, "Баптаулар");
            Set("settings", LangNl, "Instellingen");
            Set("settings", LangPl, "Ustawienia");
            Set("settings", LangPt, "Configurações");
            Set("settings", LangRo, "Setări");
            Set("settings", LangSk, "Nastavenia");
            Set("settings", LangSr, "Podešavanja");
            Set("settings", LangTh, "การตั้งค่า");
            Set("settings", LangTk, "Sazlamalar");
            Set("settings", LangUk, "Налаштування");
            Set("settings", LangUz, "Sozlamalar");
            Set("settings", LangVi, "Cài đặt");
            Set("settings", LangZh, "设置");

            Set("language", LangAr, "اللغة");
            Set("language", LangAz, "Dil");
            Set("language", LangBe, "Мова");
            Set("language", LangBg, "Език");
            Set("language", LangCa, "Idioma");
            Set("language", LangCs, "Jazyk");
            Set("language", LangDe, "Sprache");
            Set("language", LangEs, "Idioma");
            Set("language", LangFa, "زبان");
            Set("language", LangFr, "Langue");
            Set("language", LangHe, "שפה");
            Set("language", LangHi, "भाषा");
            Set("language", LangHu, "Nyelv");
            Set("language", LangHy, "Լեզու");
            Set("language", LangId, "Bahasa");
            Set("language", LangIt, "Lingua");
            Set("language", LangJa, "言語");
            Set("language", LangKa, "ენა");
            Set("language", LangKk, "Тіл");
            Set("language", LangNl, "Taal");
            Set("language", LangPl, "Język");
            Set("language", LangPt, "Idioma");
            Set("language", LangRo, "Limbă");
            Set("language", LangSk, "Jazyk");
            Set("language", LangSr, "Jezik");
            Set("language", LangTh, "ภาษา");
            Set("language", LangTk, "Dil");
            Set("language", LangUk, "Мова");
            Set("language", LangUz, "Til");
            Set("language", LangVi, "Ngôn ngữ");
            Set("language", LangZh, "语言");

            Set("account", LangAr, "الحساب");
            Set("account", LangAz, "Hesab");
            Set("account", LangBe, "Акаўнт");
            Set("account", LangBg, "Акаунт");
            Set("account", LangCa, "Compte");
            Set("account", LangCs, "Účet");
            Set("account", LangDe, "Konto");
            Set("account", LangEs, "Cuenta");
            Set("account", LangFa, "حساب");
            Set("account", LangFr, "Compte");
            Set("account", LangHe, "חשבון");
            Set("account", LangHi, "खाता");
            Set("account", LangHu, "Fiók");
            Set("account", LangHy, "Հաշիվ");
            Set("account", LangId, "Akun");
            Set("account", LangIt, "Account");
            Set("account", LangJa, "アカウント");
            Set("account", LangKa, "ანგარიში");
            Set("account", LangKk, "Аккаунт");
            Set("account", LangNl, "Account");
            Set("account", LangPl, "Konto");
            Set("account", LangPt, "Conta");
            Set("account", LangRo, "Cont");
            Set("account", LangSk, "Účet");
            Set("account", LangSr, "Nalog");
            Set("account", LangTh, "บัญชี");
            Set("account", LangTk, "Hasap");
            Set("account", LangUk, "Акаунт");
            Set("account", LangUz, "Hisob");
            Set("account", LangVi, "Tài khoản");
            Set("account", LangZh, "账号");

            Set("player_name_label", LangAr, "اللاعب");
            Set("player_name_label", LangAz, "Oyunçu");
            Set("player_name_label", LangBe, "Гулец");
            Set("player_name_label", LangBg, "Играч");
            Set("player_name_label", LangCa, "Jugador");
            Set("player_name_label", LangCs, "Hráč");
            Set("player_name_label", LangDe, "Spieler");
            Set("player_name_label", LangEs, "Jugador");
            Set("player_name_label", LangFa, "بازیکن");
            Set("player_name_label", LangFr, "Joueur");
            Set("player_name_label", LangHe, "שחקן");
            Set("player_name_label", LangHi, "खिलाड़ी");
            Set("player_name_label", LangHu, "Játékos");
            Set("player_name_label", LangHy, "Խաղացող");
            Set("player_name_label", LangId, "Pemain");
            Set("player_name_label", LangIt, "Giocatore");
            Set("player_name_label", LangJa, "プレイヤー");
            Set("player_name_label", LangKa, "მოთამაშე");
            Set("player_name_label", LangKk, "Ойыншы");
            Set("player_name_label", LangNl, "Speler");
            Set("player_name_label", LangPl, "Gracz");
            Set("player_name_label", LangPt, "Jogador");
            Set("player_name_label", LangRo, "Jucător");
            Set("player_name_label", LangSk, "Hráč");
            Set("player_name_label", LangSr, "Igrač");
            Set("player_name_label", LangTh, "ผู้เล่น");
            Set("player_name_label", LangTk, "Oýunçy");
            Set("player_name_label", LangUk, "Гравець");
            Set("player_name_label", LangUz, "O‘yinchi");
            Set("player_name_label", LangVi, "Người chơi");
            Set("player_name_label", LangZh, "玩家");

            Set("best_money_label", LangAr, "أفضل رصيد");
            Set("best_money_label", LangAz, "Ən yaxşı balans");
            Set("best_money_label", LangBe, "Лепшы баланс");
            Set("best_money_label", LangBg, "Най-добър баланс");
            Set("best_money_label", LangCa, "Millor saldo");
            Set("best_money_label", LangCs, "Nejlepší zůstatek");
            Set("best_money_label", LangDe, "Bestes Guthaben");
            Set("best_money_label", LangEs, "Mejor saldo");
            Set("best_money_label", LangFa, "بهترین موجودی");
            Set("best_money_label", LangFr, "Meilleur solde");
            Set("best_money_label", LangHe, "המאזן הטוב ביותר");
            Set("best_money_label", LangHi, "सर्वश्रेष्ठ बैलेंस");
            Set("best_money_label", LangHu, "Legjobb egyenleg");
            Set("best_money_label", LangHy, "Լավագույն հաշվեկշիռ");
            Set("best_money_label", LangId, "Saldo terbaik");
            Set("best_money_label", LangIt, "Saldo migliore");
            Set("best_money_label", LangJa, "最高残高");
            Set("best_money_label", LangKa, "საუკეთესო ბალანსი");
            Set("best_money_label", LangKk, "Үздік баланс");
            Set("best_money_label", LangNl, "Beste saldo");
            Set("best_money_label", LangPl, "Najlepsze saldo");
            Set("best_money_label", LangPt, "Melhor saldo");
            Set("best_money_label", LangRo, "Cel mai bun sold");
            Set("best_money_label", LangSk, "Najlepší zostatok");
            Set("best_money_label", LangSr, "Najbolji balans");
            Set("best_money_label", LangTh, "ยอดเงินสูงสุด");
            Set("best_money_label", LangTk, "Iň gowy balans");
            Set("best_money_label", LangUk, "Найкращий баланс");
            Set("best_money_label", LangUz, "Eng yaxshi balans");
            Set("best_money_label", LangVi, "Số dư tốt nhất");
            Set("best_money_label", LangZh, "最佳余额");

            Set("create_island", LangAr, "إنشاء جزيرة");
            Set("create_island", LangAz, "Ada yarat");
            Set("create_island", LangBe, "Стварыць востраў");
            Set("create_island", LangBg, "Създай остров");
            Set("create_island", LangCa, "Crear illa");
            Set("create_island", LangCs, "Vytvořit ostrov");
            Set("create_island", LangDe, "Insel erstellen");
            Set("create_island", LangEs, "Crear isla");
            Set("create_island", LangFa, "ساخت جزیره");
            Set("create_island", LangFr, "Créer l'île");
            Set("create_island", LangHe, "צור אי");
            Set("create_island", LangHi, "द्वीप बनाओ");
            Set("create_island", LangHu, "Sziget létrehozása");
            Set("create_island", LangHy, "Ստեղծել կղզի");
            Set("create_island", LangId, "Buat Pulau");
            Set("create_island", LangIt, "Crea isola");
            Set("create_island", LangJa, "島を作る");
            Set("create_island", LangKa, "კუნძულის შექმნა");
            Set("create_island", LangKk, "Арал жасау");
            Set("create_island", LangNl, "Eiland maken");
            Set("create_island", LangPl, "Stwórz wyspę");
            Set("create_island", LangPt, "Criar ilha");
            Set("create_island", LangRo, "Creează insula");
            Set("create_island", LangSk, "Vytvoriť ostrov");
            Set("create_island", LangSr, "Napravi ostrvo");
            Set("create_island", LangTh, "สร้างเกาะ");
            Set("create_island", LangTk, "Ada döret");
            Set("create_island", LangUk, "Створити острів");
            Set("create_island", LangUz, "Orol yaratish");
            Set("create_island", LangVi, "Tạo đảo");
            Set("create_island", LangZh, "创建岛屿");

            Set("mine_shop", LangAr, "متجر المناجم");
            Set("mine_shop", LangAz, "Mədən mağazası");
            Set("mine_shop", LangBe, "Крама шахт");
            Set("mine_shop", LangBg, "Магазин за мини");
            Set("mine_shop", LangCa, "Botiga de mines");
            Set("mine_shop", LangCs, "Obchod s doly");
            Set("mine_shop", LangDe, "Minenladen");
            Set("mine_shop", LangEs, "Tienda de minas");
            Set("mine_shop", LangFa, "فروشگاه معدن");
            Set("mine_shop", LangFr, "Boutique des mines");
            Set("mine_shop", LangHe, "חנות המכרות");
            Set("mine_shop", LangHi, "खान की दुकान");
            Set("mine_shop", LangHu, "Bányabolt");
            Set("mine_shop", LangHy, "Հանքերի խանութ");
            Set("mine_shop", LangId, "Toko tambang");
            Set("mine_shop", LangIt, "Negozio miniere");
            Set("mine_shop", LangJa, "鉱山ショップ");
            Set("mine_shop", LangKa, "მაღაროს მაღაზია");
            Set("mine_shop", LangKk, "Кен дүкені");
            Set("mine_shop", LangNl, "Mijnwinkel");
            Set("mine_shop", LangPl, "Sklep z kopalniami");
            Set("mine_shop", LangPt, "Loja de minas");
            Set("mine_shop", LangRo, "Magazin de mine");
            Set("mine_shop", LangSk, "Obchod s baňami");
            Set("mine_shop", LangSr, "Prodavnica rudnika");
            Set("mine_shop", LangTh, "ร้านเหมือง");
            Set("mine_shop", LangTk, "Kän dükany");
            Set("mine_shop", LangUk, "Магазин шахт");
            Set("mine_shop", LangUz, "Kon do‘koni");
            Set("mine_shop", LangVi, "Cửa hàng mỏ");
            Set("mine_shop", LangZh, "矿井商店");

            Set("pickaxe_shop", LangAr, "متجر المعاول");
            Set("pickaxe_shop", LangAz, "Külüng mağazası");
            Set("pickaxe_shop", LangBe, "Крама кіркаў");
            Set("pickaxe_shop", LangBg, "Магазин за кирки");
            Set("pickaxe_shop", LangCa, "Botiga de pics");
            Set("pickaxe_shop", LangCs, "Obchod s krumpáči");
            Set("pickaxe_shop", LangDe, "Spitzhackenladen");
            Set("pickaxe_shop", LangEs, "Tienda de picos");
            Set("pickaxe_shop", LangFa, "فروشگاه کلنگ");
            Set("pickaxe_shop", LangFr, "Boutique de pioches");
            Set("pickaxe_shop", LangHe, "חנות המכושים");
            Set("pickaxe_shop", LangHi, "कुदाल की दुकान");
            Set("pickaxe_shop", LangHu, "Csákánybolt");
            Set("pickaxe_shop", LangHy, "Քլունգների խանութ");
            Set("pickaxe_shop", LangId, "Toko beliung");
            Set("pickaxe_shop", LangIt, "Negozio picconi");
            Set("pickaxe_shop", LangJa, "ツルハシショップ");
            Set("pickaxe_shop", LangKa, "წერაქვების მაღაზია");
            Set("pickaxe_shop", LangKk, "Кирка дүкені");
            Set("pickaxe_shop", LangNl, "Pikhouwwinkel");
            Set("pickaxe_shop", LangPl, "Sklep z kilofami");
            Set("pickaxe_shop", LangPt, "Loja de picaretas");
            Set("pickaxe_shop", LangRo, "Magazin de târnăcoape");
            Set("pickaxe_shop", LangSk, "Obchod s krompáčmi");
            Set("pickaxe_shop", LangSr, "Prodavnica pijuka");
            Set("pickaxe_shop", LangTh, "ร้านพลั่ว");
            Set("pickaxe_shop", LangTk, "Pikir dükany");
            Set("pickaxe_shop", LangUk, "Магазин кирок");
            Set("pickaxe_shop", LangUz, "Cho‘kich do‘koni");
            Set("pickaxe_shop", LangVi, "Cửa hàng cuốc chim");
            Set("pickaxe_shop", LangZh, "镐子商店");

            Set("minion_shop", LangAr, "متجر العمال");
            Set("minion_shop", LangAz, "Minyon mağazası");
            Set("minion_shop", LangBe, "Крама міньёнаў");
            Set("minion_shop", LangBg, "Магазин за миньони");
            Set("minion_shop", LangCa, "Botiga de minions");
            Set("minion_shop", LangCs, "Obchod s pomocníky");
            Set("minion_shop", LangDe, "Minionladen");
            Set("minion_shop", LangEs, "Tienda de minions");
            Set("minion_shop", LangFa, "فروشگاه مینیون");
            Set("minion_shop", LangFr, "Boutique des minions");
            Set("minion_shop", LangHe, "חנות המיניונים");
            Set("minion_shop", LangHi, "मिनियन की दुकान");
            Set("minion_shop", LangHu, "Minion bolt");
            Set("minion_shop", LangHy, "Մինյոնների խանութ");
            Set("minion_shop", LangId, "Toko minion");
            Set("minion_shop", LangIt, "Negozio minion");
            Set("minion_shop", LangJa, "ミニオンショップ");
            Set("minion_shop", LangKa, "მინიონების მაღაზია");
            Set("minion_shop", LangKk, "Миньондар дүкені");
            Set("minion_shop", LangNl, "Minionwinkel");
            Set("minion_shop", LangPl, "Sklep z minionami");
            Set("minion_shop", LangPt, "Loja de minions");
            Set("minion_shop", LangRo, "Magazin de minioni");
            Set("minion_shop", LangSk, "Obchod s minionmi");
            Set("minion_shop", LangSr, "Prodavnica miniona");
            Set("minion_shop", LangTh, "ร้านมินเนียน");
            Set("minion_shop", LangTk, "Minion dükany");
            Set("minion_shop", LangUk, "Магазин міньйонів");
            Set("minion_shop", LangUz, "Minionlar do‘koni");
            Set("minion_shop", LangVi, "Cửa hàng tay sai");
            Set("minion_shop", LangZh, "随从商店");

            Set("sell_point", LangAr, "نقطة البيع");
            Set("sell_point", LangAz, "Satış nöqtəsi");
            Set("sell_point", LangBe, "Пункт продажу");
            Set("sell_point", LangBg, "Точка за продажба");
            Set("sell_point", LangCa, "Punt de venda");
            Set("sell_point", LangCs, "Prodejní místo");
            Set("sell_point", LangDe, "Verkaufspunkt");
            Set("sell_point", LangEs, "Punto de venta");
            Set("sell_point", LangFa, "نقطه فروش");
            Set("sell_point", LangFr, "Point de vente");
            Set("sell_point", LangHe, "נקודת מכירה");
            Set("sell_point", LangHi, "बिक्री बिंदु");
            Set("sell_point", LangHu, "Eladási pont");
            Set("sell_point", LangHy, "Վաճառքի կետ");
            Set("sell_point", LangId, "Titik penjualan");
            Set("sell_point", LangIt, "Punto vendita");
            Set("sell_point", LangJa, "売却ポイント");
            Set("sell_point", LangKa, "გაყიდვის წერტილი");
            Set("sell_point", LangKk, "Сату нүктесі");
            Set("sell_point", LangNl, "Verkooppunt");
            Set("sell_point", LangPl, "Punkt sprzedaży");
            Set("sell_point", LangPt, "Ponto de venda");
            Set("sell_point", LangRo, "Punct de vânzare");
            Set("sell_point", LangSk, "Predajné miesto");
            Set("sell_point", LangSr, "Mesto prodaje");
            Set("sell_point", LangTh, "จุดขาย");
            Set("sell_point", LangTk, "Satyş ýeri");
            Set("sell_point", LangUk, "Точка продажу");
            Set("sell_point", LangUz, "Sotish nuqtasi");
            Set("sell_point", LangVi, "Điểm bán");
            Set("sell_point", LangZh, "出售点");

            Set("to_island", LangAr, "إلى الجزيرة");
            Set("to_island", LangAz, "Adaya");
            Set("to_island", LangBe, "На востраў");
            Set("to_island", LangBg, "Към острова");
            Set("to_island", LangCa, "A l'illa");
            Set("to_island", LangCs, "Na ostrov");
            Set("to_island", LangDe, "Zur Insel");
            Set("to_island", LangEs, "A la isla");
            Set("to_island", LangFa, "به جزیره");
            Set("to_island", LangFr, "À l'île");
            Set("to_island", LangHe, "לאי");
            Set("to_island", LangHi, "द्वीप पर");
            Set("to_island", LangHu, "A szigetre");
            Set("to_island", LangHy, "Դեպի կղզի");
            Set("to_island", LangId, "Ke pulau");
            Set("to_island", LangIt, "All'isola");
            Set("to_island", LangJa, "島へ");
            Set("to_island", LangKa, "კუნძულზე");
            Set("to_island", LangKk, "Аралға");
            Set("to_island", LangNl, "Naar eiland");
            Set("to_island", LangPl, "Na wyspę");
            Set("to_island", LangPt, "Para a ilha");
            Set("to_island", LangRo, "Spre insulă");
            Set("to_island", LangSk, "Na ostrov");
            Set("to_island", LangSr, "Na ostrvo");
            Set("to_island", LangTh, "ไปเกาะ");
            Set("to_island", LangTk, "Ada");
            Set("to_island", LangUk, "На острів");
            Set("to_island", LangUz, "Orolga");
            Set("to_island", LangVi, "Đến đảo");
            Set("to_island", LangZh, "前往岛屿");

            Set("to_lobby", LangAr, "إلى اللوبي");
            Set("to_lobby", LangAz, "Lobbiyə");
            Set("to_lobby", LangBe, "У лобі");
            Set("to_lobby", LangBg, "Към лобито");
            Set("to_lobby", LangCa, "Al vestíbul");
            Set("to_lobby", LangCs, "Do lobby");
            Set("to_lobby", LangDe, "In die Lobby");
            Set("to_lobby", LangEs, "Al lobby");
            Set("to_lobby", LangFa, "به لابی");
            Set("to_lobby", LangFr, "Au lobby");
            Set("to_lobby", LangHe, "ללובי");
            Set("to_lobby", LangHi, "लॉबी में");
            Set("to_lobby", LangHu, "A lobbyba");
            Set("to_lobby", LangHy, "Դեպի լոբբի");
            Set("to_lobby", LangId, "Ke lobi");
            Set("to_lobby", LangIt, "Alla lobby");
            Set("to_lobby", LangJa, "ロビーへ");
            Set("to_lobby", LangKa, "ლობიში");
            Set("to_lobby", LangKk, "Лоббиге");
            Set("to_lobby", LangNl, "Naar lobby");
            Set("to_lobby", LangPl, "Do lobby");
            Set("to_lobby", LangPt, "Para lobby");
            Set("to_lobby", LangRo, "În lobby");
            Set("to_lobby", LangSk, "Do lobby");
            Set("to_lobby", LangSr, "U lobi");
            Set("to_lobby", LangTh, "ไปล็อบบี้");
            Set("to_lobby", LangTk, "Lobbi");
            Set("to_lobby", LangUk, "У лобі");
            Set("to_lobby", LangUz, "Lobbiga");
            Set("to_lobby", LangVi, "Về sảnh");
            Set("to_lobby", LangZh, "返回大厅");

            Set("btn_set_spawn", LangAr, "تعيين نقطة الظهور");
            Set("btn_set_spawn", LangAz, "Doğulma nöqtəsini təyin et");
            Set("btn_set_spawn", LangBe, "Задаць кропку спавна");
            Set("btn_set_spawn", LangBg, "Задай точка за поява");
            Set("btn_set_spawn", LangCa, "Definir punt de renaixement");
            Set("btn_set_spawn", LangCs, "Nastavit spawn");
            Set("btn_set_spawn", LangDe, "Spawnpunkt setzen");
            Set("btn_set_spawn", LangEs, "Fijar aparición");
            Set("btn_set_spawn", LangFa, "تنظیم نقطه اسپاون");
            Set("btn_set_spawn", LangFr, "Définir le point d'apparition");
            Set("btn_set_spawn", LangHe, "קבע נקודת הופעה");
            Set("btn_set_spawn", LangHi, "स्पॉन सेट करें");
            Set("btn_set_spawn", LangHu, "Respawn pont beállítása");
            Set("btn_set_spawn", LangHy, "Սահմանել սպաունը");
            Set("btn_set_spawn", LangId, "Atur titik spawn");
            Set("btn_set_spawn", LangIt, "Imposta spawn");
            Set("btn_set_spawn", LangJa, "スポーン地点を設定");
            Set("btn_set_spawn", LangKa, "სპავნის დაყენება");
            Set("btn_set_spawn", LangKk, "Пайда болу нүктесін орнату");
            Set("btn_set_spawn", LangNl, "Spawnpunt instellen");
            Set("btn_set_spawn", LangPl, "Ustaw spawn");
            Set("btn_set_spawn", LangPt, "Definir spawn");
            Set("btn_set_spawn", LangRo, "Setează punctul de respawn");
            Set("btn_set_spawn", LangSk, "Nastaviť spawn");
            Set("btn_set_spawn", LangSr, "Podesi spawn");
            Set("btn_set_spawn", LangTh, "ตั้งจุดเกิด");
            Set("btn_set_spawn", LangTk, "Dogulma nokadyny belle");
            Set("btn_set_spawn", LangUk, "Задати точку спавну");
            Set("btn_set_spawn", LangUz, "Spawn nuqtasini o‘rnatish");
            Set("btn_set_spawn", LangVi, "Đặt điểm hồi sinh");
            Set("btn_set_spawn", LangZh, "设置出生点");

            Set("auth_sign_in", LangAr, "تسجيل الدخول عبر ياندكس");
            Set("auth_sign_in", LangAz, "Yandex ilə giriş et");
            Set("auth_sign_in", LangBe, "Увайсці праз Яндэкс");
            Set("auth_sign_in", LangBg, "Вход с Yandex");
            Set("auth_sign_in", LangCa, "Inicia sessió amb Yandex");
            Set("auth_sign_in", LangCs, "Přihlásit se přes Yandex");
            Set("auth_sign_in", LangDe, "Mit Yandex anmelden");
            Set("auth_sign_in", LangEs, "Iniciar sesión con Yandex");
            Set("auth_sign_in", LangFa, "ورود با یاندکس");
            Set("auth_sign_in", LangFr, "Se connecter avec Yandex");
            Set("auth_sign_in", LangHe, "התחבר עם Yandex");
            Set("auth_sign_in", LangHi, "Yandex से साइन इन करें");
            Set("auth_sign_in", LangHu, "Bejelentkezés Yandexszel");
            Set("auth_sign_in", LangHy, "Մուտք գործել Yandex-ով");
            Set("auth_sign_in", LangId, "Masuk dengan Yandex");
            Set("auth_sign_in", LangIt, "Accedi con Yandex");
            Set("auth_sign_in", LangJa, "Yandexでログイン");
            Set("auth_sign_in", LangKa, "Yandex-ით შესვლა");
            Set("auth_sign_in", LangKk, "Yandex арқылы кіру");
            Set("auth_sign_in", LangNl, "Inloggen met Yandex");
            Set("auth_sign_in", LangPl, "Zaloguj przez Yandex");
            Set("auth_sign_in", LangPt, "Entrar com Yandex");
            Set("auth_sign_in", LangRo, "Autentificare cu Yandex");
            Set("auth_sign_in", LangSk, "Prihlásiť sa cez Yandex");
            Set("auth_sign_in", LangSr, "Prijavi se preko Yandexa");
            Set("auth_sign_in", LangTh, "เข้าสู่ระบบด้วย Yandex");
            Set("auth_sign_in", LangTk, "Yandex arkaly gir");
            Set("auth_sign_in", LangUk, "Увійти через Yandex");
            Set("auth_sign_in", LangUz, "Yandex orqali kirish");
            Set("auth_sign_in", LangVi, "Đăng nhập bằng Yandex");
            Set("auth_sign_in", LangZh, "通过 Yandex 登录");

            Set("auth_connected", LangAr, "متصل");
            Set("auth_connected", LangAz, "Qoşuldu");
            Set("auth_connected", LangBe, "Падключана");
            Set("auth_connected", LangBg, "Свързано");
            Set("auth_connected", LangCa, "Connectat");
            Set("auth_connected", LangCs, "Připojeno");
            Set("auth_connected", LangDe, "Verbunden");
            Set("auth_connected", LangEs, "Conectado");
            Set("auth_connected", LangFa, "متصل");
            Set("auth_connected", LangFr, "Connecté");
            Set("auth_connected", LangHe, "מחובר");
            Set("auth_connected", LangHi, "कनेक्टेड");
            Set("auth_connected", LangHu, "Csatlakozva");
            Set("auth_connected", LangHy, "Միացված է");
            Set("auth_connected", LangId, "Terhubung");
            Set("auth_connected", LangIt, "Connesso");
            Set("auth_connected", LangJa, "接続済み");
            Set("auth_connected", LangKa, "დაკავშირებულია");
            Set("auth_connected", LangKk, "Қосылған");
            Set("auth_connected", LangNl, "Verbonden");
            Set("auth_connected", LangPl, "Połączono");
            Set("auth_connected", LangPt, "Conectado");
            Set("auth_connected", LangRo, "Conectat");
            Set("auth_connected", LangSk, "Pripojené");
            Set("auth_connected", LangSr, "Povezano");
            Set("auth_connected", LangTh, "เชื่อมต่อแล้ว");
            Set("auth_connected", LangTk, "Baglandy");
            Set("auth_connected", LangUk, "Підключено");
            Set("auth_connected", LangUz, "Ulangan");
            Set("auth_connected", LangVi, "Đã kết nối");
            Set("auth_connected", LangZh, "已连接");

            Set("auth_guest_hint", LangAr, "سجّل الدخول عبر Yandex لمزامنة التقدم بين الأجهزة.");
            Set("auth_guest_hint", LangAz, "İrəliləyişini cihazlar arasında sinxronlaşdırmaq üçün Yandex ilə daxil ol.");
            Set("auth_guest_hint", LangBe, "Увайдзі праз Яндэкс, каб сінхранізаваць прагрэс паміж прыладамі.");
            Set("auth_guest_hint", LangBg, "Влез с Yandex, за да синхронизираш прогреса между устройствата.");
            Set("auth_guest_hint", LangCa, "Inicia sessió amb Yandex per sincronitzar el progrés entre dispositius.");
            Set("auth_guest_hint", LangCs, "Přihlas se přes Yandex pro synchronizaci postupu mezi zařízeními.");
            Set("auth_guest_hint", LangDe, "Melde dich mit Yandex an, um deinen Fortschritt zwischen Geräten zu synchronisieren.");
            Set("auth_guest_hint", LangEs, "Inicia sesión con Yandex para sincronizar tu progreso entre dispositivos.");
            Set("auth_guest_hint", LangFa, "برای همگام‌سازی پیشرفت بین دستگاه‌ها با یاندکس وارد شو.");
            Set("auth_guest_hint", LangFr, "Connecte-toi avec Yandex pour synchroniser la progression entre appareils.");
            Set("auth_guest_hint", LangHe, "התחבר עם Yandex כדי לסנכרן התקדמות בין מכשירים.");
            Set("auth_guest_hint", LangHi, "डिवाइसों के बीच प्रगति सिंक करने के लिए Yandex से साइन इन करें।");
            Set("auth_guest_hint", LangHu, "Jelentkezz be Yandexszel, hogy az előrehaladás szinkronizálható legyen az eszközök között.");
            Set("auth_guest_hint", LangHy, "Մուտք գործիր Yandex-ով՝ առաջընթացը սարքերի միջև համաժամեցնելու համար։");
            Set("auth_guest_hint", LangId, "Masuk dengan Yandex untuk menyinkronkan progres di berbagai perangkat.");
            Set("auth_guest_hint", LangIt, "Accedi con Yandex per sincronizzare i progressi tra dispositivi.");
            Set("auth_guest_hint", LangJa, "端末間で進行状況を同期するにはYandexでログインしてください。");
            Set("auth_guest_hint", LangKa, "პროგრესის მოწყობილობებს შორის სინქრონიზაციისთვის შედი Yandex-ით.");
            Set("auth_guest_hint", LangKk, "Прогресті құрылғылар арасында синхрондау үшін Yandex арқылы кір.");
            Set("auth_guest_hint", LangNl, "Log in met Yandex om je voortgang tussen apparaten te synchroniseren.");
            Set("auth_guest_hint", LangPl, "Zaloguj się przez Yandex, aby synchronizować postępy między urządzeniami.");
            Set("auth_guest_hint", LangPt, "Entre com Yandex para sincronizar o progresso entre dispositivos.");
            Set("auth_guest_hint", LangRo, "Autentifică-te cu Yandex pentru a sincroniza progresul între dispozitive.");
            Set("auth_guest_hint", LangSk, "Prihlás sa cez Yandex, aby si synchronizoval postup medzi zariadeniami.");
            Set("auth_guest_hint", LangSr, "Prijavi se preko Yandexa da sinhronizuješ napredak između uređaja.");
            Set("auth_guest_hint", LangTh, "เข้าสู่ระบบด้วย Yandex เพื่อซิงก์ความคืบหน้าระหว่างอุปกรณ์");
            Set("auth_guest_hint", LangTk, "Ösüşi enjamlaryň arasynda sinhronlamak üçin Yandex arkaly gir.");
            Set("auth_guest_hint", LangUk, "Увійди через Yandex, щоб синхронізувати прогрес між пристроями.");
            Set("auth_guest_hint", LangUz, "Jarayonni qurilmalar o‘rtasida sinxronlash uchun Yandex orqali kiring.");
            Set("auth_guest_hint", LangVi, "Đăng nhập bằng Yandex để đồng bộ tiến trình giữa các thiết bị.");
            Set("auth_guest_hint", LangZh, "通过 Yandex 登录，以便在设备之间同步进度。");

            Set("auth_connected_hint", LangAr, "يمكن حفظ التقدم ومزامنته بين الأجهزة.");
            Set("auth_connected_hint", LangAz, "İrəliləyiş saxlanıla və cihazlar arasında sinxronlaşdırıla bilər.");
            Set("auth_connected_hint", LangBe, "Прагрэс можна захоўваць і сінхранізаваць паміж прыладамі.");
            Set("auth_connected_hint", LangBg, "Прогресът може да се запазва и синхронизира между устройства.");
            Set("auth_connected_hint", LangCa, "El progrés es pot desar i sincronitzar entre dispositius.");
            Set("auth_connected_hint", LangCs, "Postup lze ukládat a synchronizovat mezi zařízeními.");
            Set("auth_connected_hint", LangDe, "Der Fortschritt kann gespeichert und zwischen Geräten synchronisiert werden.");
            Set("auth_connected_hint", LangEs, "El progreso se puede guardar y sincronizar entre dispositivos.");
            Set("auth_connected_hint", LangFa, "پیشرفت را می‌توان بین دستگاه‌ها ذخیره و همگام‌سازی کرد.");
            Set("auth_connected_hint", LangFr, "La progression peut être sauvegardée et synchronisée entre les appareils.");
            Set("auth_connected_hint", LangHe, "ניתן לשמור ולסנכרן את ההתקדמות בין מכשירים.");
            Set("auth_connected_hint", LangHi, "प्रगति सहेजी जा सकती है और डिवाइसों के बीच सिंक हो सकती है।");
            Set("auth_connected_hint", LangHu, "A haladás menthető és szinkronizálható az eszközök között.");
            Set("auth_connected_hint", LangHy, "Առաջընթացը կարելի է պահպանել և համաժամեցնել սարքերի միջև։");
            Set("auth_connected_hint", LangId, "Progres dapat disimpan dan disinkronkan di berbagai perangkat.");
            Set("auth_connected_hint", LangIt, "I progressi possono essere salvati e sincronizzati tra dispositivi.");
            Set("auth_connected_hint", LangJa, "進行状況は保存され、端末間で同期できます。");
            Set("auth_connected_hint", LangKa, "პროგრესის შენახვა და მოწყობილობებს შორის სინქრონიზაცია შესაძლებელია.");
            Set("auth_connected_hint", LangKk, "Прогресті сақтап, құрылғылар арасында синхрондауға болады.");
            Set("auth_connected_hint", LangNl, "Voortgang kan worden opgeslagen en tussen apparaten worden gesynchroniseerd.");
            Set("auth_connected_hint", LangPl, "Postęp można zapisywać i synchronizować między urządzeniami.");
            Set("auth_connected_hint", LangPt, "O progresso pode ser salvo e sincronizado entre dispositivos.");
            Set("auth_connected_hint", LangRo, "Progresul poate fi salvat și sincronizat între dispozitive.");
            Set("auth_connected_hint", LangSk, "Postup je možné ukladať a synchronizovať medzi zariadeniami.");
            Set("auth_connected_hint", LangSr, "Napredak može da se čuva i sinhronizuje između uređaja.");
            Set("auth_connected_hint", LangTh, "สามารถบันทึกและซิงก์ความคืบหน้าระหว่างอุปกรณ์ได้");
            Set("auth_connected_hint", LangTk, "Ösüş ýazdyrylyp we enjamlaryň arasynda sinhronlanyp bolýar.");
            Set("auth_connected_hint", LangUk, "Прогрес можна зберігати й синхронізувати між пристроями.");
            Set("auth_connected_hint", LangUz, "Jarayonni saqlash va qurilmalar o‘rtasida sinxronlash mumkin.");
            Set("auth_connected_hint", LangVi, "Tiến trình có thể được lưu và đồng bộ giữa các thiết bị.");
            Set("auth_connected_hint", LangZh, "进度可以在设备之间保存并同步。");

            Set("ads_bonus_desc", LangAr, "شاهد إعلانًا بمكافأة واحصل على عملات إضافية.");
            Set("ads_bonus_desc", LangAz, "Mükafatlı reklama bax və bonus sikkələr qazan.");
            Set("ads_bonus_desc", LangBe, "Паглядзі рэкламу з узнагародай і атрымай бонусныя манеты.");
            Set("ads_bonus_desc", LangBg, "Гледай реклама с награда и вземи бонус монети.");
            Set("ads_bonus_desc", LangCa, "Mira un anunci amb recompensa i aconsegueix monedes extra.");
            Set("ads_bonus_desc", LangCs, "Podívej se na odměnovou reklamu a získej bonusové mince.");
            Set("ads_bonus_desc", LangDe, "Sieh dir eine Belohnungswerbung an und erhalte Bonusmünzen.");
            Set("ads_bonus_desc", LangEs, "Mira un anuncio con recompensa y consigue monedas extra.");
            Set("ads_bonus_desc", LangFa, "یک تبلیغ جایزه‌دار ببین و سکه‌ی اضافی بگیر.");
            Set("ads_bonus_desc", LangFr, "Regarde une pub récompensée et obtiens des pièces bonus.");
            Set("ads_bonus_desc", LangHe, "צפה בפרסומת מתוגמלת וקבל מטבעות בונוס.");
            Set("ads_bonus_desc", LangHi, "रिवॉर्डेड विज्ञापन देखें और बोनस सिक्के पाएं।");
            Set("ads_bonus_desc", LangHu, "Nézz meg egy jutalmazott hirdetést, és szerezz bónusz érméket.");
            Set("ads_bonus_desc", LangHy, "Դիտիր պարգևատրվող գովազդ և ստացիր բոնուսային մետաղադրամներ։");
            Set("ads_bonus_desc", LangId, "Tonton iklan berhadiah dan dapatkan koin bonus.");
            Set("ads_bonus_desc", LangIt, "Guarda una pubblicità con ricompensa e ottieni monete bonus.");
            Set("ads_bonus_desc", LangJa, "報酬付き広告を見てボーナスコインを獲得しよう。");
            Set("ads_bonus_desc", LangKa, "უყურე ჯილდოიან რეკლამას და მიიღე ბონუს მონეტები.");
            Set("ads_bonus_desc", LangKk, "Сыйақылы жарнаманы көріп, бонустық тиын ал.");
            Set("ads_bonus_desc", LangNl, "Bekijk een rewarded advertentie en krijg bonusmunten.");
            Set("ads_bonus_desc", LangPl, "Obejrzyj reklamę z nagrodą i zdobądź bonusowe monety.");
            Set("ads_bonus_desc", LangPt, "Assista a um anúncio recompensado e ganhe moedas bônus.");
            Set("ads_bonus_desc", LangRo, "Privește o reclamă recompensată și primește monede bonus.");
            Set("ads_bonus_desc", LangSk, "Pozri si odmeňovanú reklamu a získaj bonusové mince.");
            Set("ads_bonus_desc", LangSr, "Pogledaj reklamu sa nagradom i osvoji bonus novčiće.");
            Set("ads_bonus_desc", LangTh, "ดูโฆษณาแบบมีรางวัลและรับเหรียญโบนัส");
            Set("ads_bonus_desc", LangTk, "Baýrakly reklamy gör we goşmaça teňňe al.");
            Set("ads_bonus_desc", LangUk, "Подивись рекламу з винагородою та отримай бонусні монети.");
            Set("ads_bonus_desc", LangUz, "Mukofotli reklamani ko‘ring va bonus tangalar oling.");
            Set("ads_bonus_desc", LangVi, "Xem quảng cáo thưởng và nhận xu thưởng.");
            Set("ads_bonus_desc", LangZh, "观看奖励广告并获得额外金币。");

            Set("ads_bonus_btn", LangAr, "شاهد الإعلان: +{0} عملة");
            Set("ads_bonus_btn", LangAz, "Reklama bax: +{0} sikkə");
            Set("ads_bonus_btn", LangBe, "Глядзець рэкламу: +{0} манет");
            Set("ads_bonus_btn", LangBg, "Гледай реклама: +{0} монети");
            Set("ads_bonus_btn", LangCa, "Mira l'anunci: +{0} monedes");
            Set("ads_bonus_btn", LangCs, "Sledovat reklamu: +{0} mincí");
            Set("ads_bonus_btn", LangDe, "Werbung ansehen: +{0} Münzen");
            Set("ads_bonus_btn", LangEs, "Ver anuncio: +{0} monedas");
            Set("ads_bonus_btn", LangFa, "دیدن تبلیغ: +{0} سکه");
            Set("ads_bonus_btn", LangFr, "Voir la pub : +{0} pièces");
            Set("ads_bonus_btn", LangHe, "צפה בפרסומת: +{0} מטבעות");
            Set("ads_bonus_btn", LangHi, "विज्ञापन देखें: +{0} सिक्के");
            Set("ads_bonus_btn", LangHu, "Hirdetés megtekintése: +{0} érme");
            Set("ads_bonus_btn", LangHy, "Դիտել գովազդը՝ +{0} մետաղադրամ");
            Set("ads_bonus_btn", LangId, "Tonton iklan: +{0} koin");
            Set("ads_bonus_btn", LangIt, "Guarda annuncio: +{0} monete");
            Set("ads_bonus_btn", LangJa, "広告を見る: +{0} コイン");
            Set("ads_bonus_btn", LangKa, "უყურე რეკლამას: +{0} მონეტა");
            Set("ads_bonus_btn", LangKk, "Жарнама көру: +{0} тиын");
            Set("ads_bonus_btn", LangNl, "Bekijk advertentie: +{0} munten");
            Set("ads_bonus_btn", LangPl, "Obejrzyj reklamę: +{0} monet");
            Set("ads_bonus_btn", LangPt, "Ver anúncio: +{0} moedas");
            Set("ads_bonus_btn", LangRo, "Vezi reclama: +{0} monede");
            Set("ads_bonus_btn", LangSk, "Pozrieť reklamu: +{0} mincí");
            Set("ads_bonus_btn", LangSr, "Pogledaj reklamu: +{0} novčića");
            Set("ads_bonus_btn", LangTh, "ดูโฆษณา: +{0} เหรียญ");
            Set("ads_bonus_btn", LangTk, "Reklamy gör: +{0} teňňe");
            Set("ads_bonus_btn", LangUk, "Дивитися рекламу: +{0} монет");
            Set("ads_bonus_btn", LangUz, "Reklamani ko‘rish: +{0} tanga");
            Set("ads_bonus_btn", LangVi, "Xem quảng cáo: +{0} xu");
            Set("ads_bonus_btn", LangZh, "观看广告：+{0} 金币");

            Set("guest_player", LangAr, "ضيف");
            Set("guest_player", LangAz, "Qonaq");
            Set("guest_player", LangBe, "Госць");
            Set("guest_player", LangBg, "Гост");
            Set("guest_player", LangCa, "Convidat");
            Set("guest_player", LangCs, "Host");
            Set("guest_player", LangDe, "Gast");
            Set("guest_player", LangEs, "Invitado");
            Set("guest_player", LangFa, "مهمان");
            Set("guest_player", LangFr, "Invité");
            Set("guest_player", LangHe, "אורח");
            Set("guest_player", LangHi, "अतिथि");
            Set("guest_player", LangHu, "Vendég");
            Set("guest_player", LangHy, "Հյուր");
            Set("guest_player", LangId, "Tamu");
            Set("guest_player", LangIt, "Ospite");
            Set("guest_player", LangJa, "ゲスト");
            Set("guest_player", LangKa, "სტუმარი");
            Set("guest_player", LangKk, "Қонақ");
            Set("guest_player", LangNl, "Gast");
            Set("guest_player", LangPl, "Gość");
            Set("guest_player", LangPt, "Convidado");
            Set("guest_player", LangRo, "Oaspete");
            Set("guest_player", LangSk, "Hosť");
            Set("guest_player", LangSr, "Gost");
            Set("guest_player", LangTh, "ผู้เยี่ยมชม");
            Set("guest_player", LangTk, "Myhman");
            Set("guest_player", LangUk, "Гість");
            Set("guest_player", LangUz, "Mehmon");
            Set("guest_player", LangVi, "Khách");
            Set("guest_player", LangZh, "游客");

            Set("ads_bonus_title", LangAr, "مكافأة الإعلان");
            Set("ads_bonus_title", LangAz, "Reklam bonusu");
            Set("ads_bonus_title", LangBe, "Бонус за рэкламу");
            Set("ads_bonus_title", LangBg, "Бонус от реклама");
            Set("ads_bonus_title", LangCa, "Bonus d'anunci");
            Set("ads_bonus_title", LangCs, "Bonus za reklamu");
            Set("ads_bonus_title", LangDe, "Werbebonus");
            Set("ads_bonus_title", LangEs, "Bono por anuncio");
            Set("ads_bonus_title", LangFa, "پاداش تبلیغ");
            Set("ads_bonus_title", LangFr, "Bonus pub");
            Set("ads_bonus_title", LangHe, "בונוס פרסומת");
            Set("ads_bonus_title", LangHi, "विज्ञापन बोनस");
            Set("ads_bonus_title", LangHu, "Hirdetés bónusz");
            Set("ads_bonus_title", LangHy, "Գովազդի բոնուս");
            Set("ads_bonus_title", LangId, "Bonus iklan");
            Set("ads_bonus_title", LangIt, "Bonus pubblicità");
            Set("ads_bonus_title", LangJa, "広告ボーナス");
            Set("ads_bonus_title", LangKa, "რეკლამის ბონუსი");
            Set("ads_bonus_title", LangKk, "Жарнама бонусы");
            Set("ads_bonus_title", LangNl, "Advertentiebonus");
            Set("ads_bonus_title", LangPl, "Bonus za reklamę");
            Set("ads_bonus_title", LangPt, "Bônus de anúncio");
            Set("ads_bonus_title", LangRo, "Bonus reclamă");
            Set("ads_bonus_title", LangSk, "Bonus za reklamu");
            Set("ads_bonus_title", LangSr, "Bonus za reklamu");
            Set("ads_bonus_title", LangTh, "โบนัสโฆษณา");
            Set("ads_bonus_title", LangTk, "Reklama bonusy");
            Set("ads_bonus_title", LangUk, "Бонус за рекламу");
            Set("ads_bonus_title", LangUz, "Reklama bonusi");
            Set("ads_bonus_title", LangVi, "Thưởng quảng cáo");
            Set("ads_bonus_title", LangZh, "广告奖励");

            Set("money", LangAr, "العملات");
            Set("money", LangAz, "Sikkələr");
            Set("money", LangBe, "Манеты");
            Set("money", LangBg, "Монети");
            Set("money", LangCa, "Monedes");
            Set("money", LangCs, "Mince");
            Set("money", LangDe, "Münzen");
            Set("money", LangEs, "Monedas");
            Set("money", LangFa, "سکه‌ها");
            Set("money", LangFr, "Pièces");
            Set("money", LangHe, "מטבעות");
            Set("money", LangHi, "सिक्के");
            Set("money", LangHu, "Érmék");
            Set("money", LangHy, "Մետաղադրամներ");
            Set("money", LangId, "Koin");
            Set("money", LangIt, "Monete");
            Set("money", LangJa, "コイン");
            Set("money", LangKa, "მონეტები");
            Set("money", LangKk, "Тиындар");
            Set("money", LangNl, "Munten");
            Set("money", LangPl, "Monety");
            Set("money", LangPt, "Moedas");
            Set("money", LangRo, "Monede");
            Set("money", LangSk, "Mince");
            Set("money", LangSr, "Novčići");
            Set("money", LangTh, "เหรียญ");
            Set("money", LangTk, "Teňňeler");
            Set("money", LangUk, "Монети");
            Set("money", LangUz, "Tangalar");
            Set("money", LangVi, "Xu");
            Set("money", LangZh, "金币");

            Set("sell", LangAr, "بيع");
            Set("sell", LangAz, "Sat");
            Set("sell", LangBe, "Прадаць");
            Set("sell", LangBg, "Продай");
            Set("sell", LangCa, "Vendre");
            Set("sell", LangCs, "Prodat");
            Set("sell", LangDe, "Verkaufen");
            Set("sell", LangEs, "Vender");
            Set("sell", LangFa, "فروش");
            Set("sell", LangFr, "Vendre");
            Set("sell", LangHe, "מכור");
            Set("sell", LangHi, "बेचना");
            Set("sell", LangHu, "Eladás");
            Set("sell", LangHy, "Վաճառել");
            Set("sell", LangId, "Jual");
            Set("sell", LangIt, "Vendi");
            Set("sell", LangJa, "売る");
            Set("sell", LangKa, "გაყიდვა");
            Set("sell", LangKk, "Сату");
            Set("sell", LangNl, "Verkopen");
            Set("sell", LangPl, "Sprzedaj");
            Set("sell", LangPt, "Vender");
            Set("sell", LangRo, "Vinde");
            Set("sell", LangSk, "Predať");
            Set("sell", LangSr, "Prodaj");
            Set("sell", LangTh, "ขาย");
            Set("sell", LangTk, "Sat");
            Set("sell", LangUk, "Продати");
            Set("sell", LangUz, "Sotish");
            Set("sell", LangVi, "Bán");
            Set("sell", LangZh, "出售");

            Set("buy", LangAr, "شراء");
            Set("buy", LangAz, "Al");
            Set("buy", LangBe, "Купіць");
            Set("buy", LangBg, "Купи");
            Set("buy", LangCa, "Comprar");
            Set("buy", LangCs, "Koupit");
            Set("buy", LangDe, "Kaufen");
            Set("buy", LangEs, "Comprar");
            Set("buy", LangFa, "خرید");
            Set("buy", LangFr, "Acheter");
            Set("buy", LangHe, "קנה");
            Set("buy", LangHi, "खरीदें");
            Set("buy", LangHu, "Vásárlás");
            Set("buy", LangHy, "Գնել");
            Set("buy", LangId, "Beli");
            Set("buy", LangIt, "Compra");
            Set("buy", LangJa, "購入");
            Set("buy", LangKa, "ყიდვა");
            Set("buy", LangKk, "Сатып алу");
            Set("buy", LangNl, "Kopen");
            Set("buy", LangPl, "Kup");
            Set("buy", LangPt, "Comprar");
            Set("buy", LangRo, "Cumpără");
            Set("buy", LangSk, "Kúpiť");
            Set("buy", LangSr, "Kupi");
            Set("buy", LangTh, "ซื้อ");
            Set("buy", LangTk, "Satyn al");
            Set("buy", LangUk, "Купити");
            Set("buy", LangUz, "Sotib olish");
            Set("buy", LangVi, "Mua");
            Set("buy", LangZh, "购买");

            Set("btn_mine", LangAr, "احفر");
            Set("btn_mine", LangAz, "QAZ");
            Set("btn_mine", LangBe, "КАПАЦЬ");
            Set("btn_mine", LangBg, "КОПАЙ");
            Set("btn_mine", LangCa, "MINA");
            Set("btn_mine", LangCs, "KOPAT");
            Set("btn_mine", LangDe, "ABBAU");
            Set("btn_mine", LangEs, "MINAR");
            Set("btn_mine", LangFa, "استخراج");
            Set("btn_mine", LangFr, "MINER");
            Set("btn_mine", LangHe, "כרה");
            Set("btn_mine", LangHi, "खनन");
            Set("btn_mine", LangHu, "ÁSÁS");
            Set("btn_mine", LangHy, "ՓՈՐԵԼ");
            Set("btn_mine", LangId, "TAMBANG");
            Set("btn_mine", LangIt, "SCAVA");
            Set("btn_mine", LangJa, "採掘");
            Set("btn_mine", LangKa, "მოპოვება");
            Set("btn_mine", LangKk, "ҚАЗУ");
            Set("btn_mine", LangNl, "MIJN");
            Set("btn_mine", LangPl, "KOP");
            Set("btn_mine", LangPt, "MINERAR");
            Set("btn_mine", LangRo, "MINĂ");
            Set("btn_mine", LangSk, "ŤAŽIŤ");
            Set("btn_mine", LangSr, "KOPAJ");
            Set("btn_mine", LangTh, "ขุด");
            Set("btn_mine", LangTk, "GAZ");
            Set("btn_mine", LangUk, "КОПАТИ");
            Set("btn_mine", LangUz, "QAZISH");
            Set("btn_mine", LangVi, "ĐÀO");
            Set("btn_mine", LangZh, "挖掘");

            Set("btn_sell", LangAr, "بيع");
            Set("btn_sell", LangAz, "SAT");
            Set("btn_sell", LangBe, "ПРАДАЦЬ");
            Set("btn_sell", LangBg, "ПРОДАЙ");
            Set("btn_sell", LangCa, "VEN");
            Set("btn_sell", LangCs, "PRODAT");
            Set("btn_sell", LangDe, "VERKAUF");
            Set("btn_sell", LangEs, "VENDER");
            Set("btn_sell", LangFa, "فروش");
            Set("btn_sell", LangFr, "VENDRE");
            Set("btn_sell", LangHe, "מכור");
            Set("btn_sell", LangHi, "बेचें");
            Set("btn_sell", LangHu, "ELAD");
            Set("btn_sell", LangHy, "ՎԱՃԱՌԵԼ");
            Set("btn_sell", LangId, "JUAL");
            Set("btn_sell", LangIt, "VENDI");
            Set("btn_sell", LangJa, "売却");
            Set("btn_sell", LangKa, "გაყიდვა");
            Set("btn_sell", LangKk, "САТУ");
            Set("btn_sell", LangNl, "VERKOOP");
            Set("btn_sell", LangPl, "SPRZEDAJ");
            Set("btn_sell", LangPt, "VENDER");
            Set("btn_sell", LangRo, "VINDE");
            Set("btn_sell", LangSk, "PREDAŤ");
            Set("btn_sell", LangSr, "PRODAJ");
            Set("btn_sell", LangTh, "ขาย");
            Set("btn_sell", LangTk, "SAT");
            Set("btn_sell", LangUk, "ПРОДАТИ");
            Set("btn_sell", LangUz, "SOTISH");
            Set("btn_sell", LangVi, "BÁN");
            Set("btn_sell", LangZh, "出售");

            Set("mine_shop_title", LangAr, "متجر المناجم");
            Set("mine_shop_title", LangAz, "MƏDƏN MAĞAZASI");
            Set("mine_shop_title", LangBe, "КРАМА ШАХТ");
            Set("mine_shop_title", LangBg, "МАГАЗИН ЗА МИНИ");
            Set("mine_shop_title", LangCa, "BOTIGA DE MINES");
            Set("mine_shop_title", LangCs, "OBCHOD S DOLY");
            Set("mine_shop_title", LangDe, "MINENLADEN");
            Set("mine_shop_title", LangEs, "TIENDA DE MINAS");
            Set("mine_shop_title", LangFa, "فروشگاه معدن");
            Set("mine_shop_title", LangFr, "BOUTIQUE DES MINES");
            Set("mine_shop_title", LangHe, "חנות המכרות");
            Set("mine_shop_title", LangHi, "खदान दुकान");
            Set("mine_shop_title", LangHu, "BÁNYABOLT");
            Set("mine_shop_title", LangHy, "ՀԱՆՔԻ ԽԱՆՈՒԹ");
            Set("mine_shop_title", LangId, "TOKO TAMBANG");
            Set("mine_shop_title", LangIt, "NEGOZIO MINIERE");
            Set("mine_shop_title", LangJa, "鉱山ショップ");
            Set("mine_shop_title", LangKa, "მაღაროს მაღაზია");
            Set("mine_shop_title", LangKk, "КЕН ДҮКЕНІ");
            Set("mine_shop_title", LangNl, "MIJNWINKEL");
            Set("mine_shop_title", LangPl, "SKLEP KOPALŃ");
            Set("mine_shop_title", LangPt, "LOJA DE MINAS");
            Set("mine_shop_title", LangRo, "MAGAZIN DE MINE");
            Set("mine_shop_title", LangSk, "OBCHOD S BAŇAMI");
            Set("mine_shop_title", LangSr, "PRODAVNICA RUDNIKA");
            Set("mine_shop_title", LangTh, "ร้านเหมือง");
            Set("mine_shop_title", LangTk, "KÄN DÜKANY");
            Set("mine_shop_title", LangUk, "МАГАЗИН ШАХТ");
            Set("mine_shop_title", LangUz, "KON DO‘KONI");
            Set("mine_shop_title", LangVi, "CỬA HÀNG MỎ");
            Set("mine_shop_title", LangZh, "矿井商店");

            Set("pickaxe_shop_title", LangAr, "متجر الفؤوس");
            Set("pickaxe_shop_title", LangAz, "KAZMA MAĞAZASI");
            Set("pickaxe_shop_title", LangBe, "КРАМА КІРОК");
            Set("pickaxe_shop_title", LangBg, "МАГАЗИН ЗА КИРКИ");
            Set("pickaxe_shop_title", LangCa, "BOTIGA DE PIQUETS");
            Set("pickaxe_shop_title", LangCs, "OBCHOD S KRUMPÁČI");
            Set("pickaxe_shop_title", LangDe, "SPITZHACKENLADEN");
            Set("pickaxe_shop_title", LangEs, "TIENDA DE PICOS");
            Set("pickaxe_shop_title", LangFa, "فروشگاه کلنگ");
            Set("pickaxe_shop_title", LangFr, "BOUTIQUE DE PIOCHES");
            Set("pickaxe_shop_title", LangHe, "חנות מכושים");
            Set("pickaxe_shop_title", LangHi, "कुदाल दुकान");
            Set("pickaxe_shop_title", LangHu, "CSÁKÁNYBOLT");
            Set("pickaxe_shop_title", LangHy, "ՔԻՐԿԻ ԽԱՆՈՒԹ");
            Set("pickaxe_shop_title", LangId, "TOKO PICKAXE");
            Set("pickaxe_shop_title", LangIt, "NEGOZIO PICCONI");
            Set("pickaxe_shop_title", LangJa, "ツルハシショップ");
            Set("pickaxe_shop_title", LangKa, "მწიკვის მაღაზია");
            Set("pickaxe_shop_title", LangKk, "КІРКЕ ДҮКЕНІ");
            Set("pickaxe_shop_title", LangNl, "PIKHOUWELWINKEL");
            Set("pickaxe_shop_title", LangPl, "SKLEP Z KILOFAMI");
            Set("pickaxe_shop_title", LangPt, "LOJA DE PICARETAS");
            Set("pickaxe_shop_title", LangRo, "MAGAZIN DE TÂRNĂCOAPE");
            Set("pickaxe_shop_title", LangSk, "OBCHOD S KROMPÁČMI");
            Set("pickaxe_shop_title", LangSr, "PRODAVNICA KRAMPOVA");
            Set("pickaxe_shop_title", LangTh, "ร้านพลั่ว");
            Set("pickaxe_shop_title", LangTk, "GAZMA DÜKANY");
            Set("pickaxe_shop_title", LangUk, "МАГАЗИН КИРОК");
            Set("pickaxe_shop_title", LangUz, "KETMON DO‘KONI");
            Set("pickaxe_shop_title", LangVi, "CỬA HÀNG CUỐC");
            Set("pickaxe_shop_title", LangZh, "镐子商店");

            Set("btn_jump", LangAr, "اقفز");
            Set("btn_jump", LangAz, "TULLAN");
            Set("btn_jump", LangBe, "СКОК");
            Set("btn_jump", LangBg, "СКОК");
            Set("btn_jump", LangCa, "SALTA");
            Set("btn_jump", LangCs, "SKOK");
            Set("btn_jump", LangDe, "SPRING");
            Set("btn_jump", LangEs, "SALTA");
            Set("btn_jump", LangFa, "پرش");
            Set("btn_jump", LangFr, "SAUT");
            Set("btn_jump", LangHe, "קפוץ");
            Set("btn_jump", LangHi, "कूद");
            Set("btn_jump", LangHu, "UGRÁS");
            Set("btn_jump", LangHy, "ՑԱՏԿ");
            Set("btn_jump", LangId, "LOMPAT");
            Set("btn_jump", LangIt, "SALTA");
            Set("btn_jump", LangJa, "ジャンプ");
            Set("btn_jump", LangKa, "ხტომა");
            Set("btn_jump", LangKk, "СЕКІРУ");
            Set("btn_jump", LangNl, "SPRING");
            Set("btn_jump", LangPl, "SKOK");
            Set("btn_jump", LangPt, "PULAR");
            Set("btn_jump", LangRo, "SALT");
            Set("btn_jump", LangSk, "SKOK");
            Set("btn_jump", LangSr, "SKOK");
            Set("btn_jump", LangTh, "กระโดด");
            Set("btn_jump", LangTk, "BÖK");
            Set("btn_jump", LangUk, "СТРИБОК");
            Set("btn_jump", LangUz, "SAKRASH");
            Set("btn_jump", LangVi, "NHẢY");
            Set("btn_jump", LangZh, "跳跃");

            Set("btn_run", LangAr, "اركض");
            Set("btn_run", LangAz, "QAÇ");
            Set("btn_run", LangBe, "БЕГ");
            Set("btn_run", LangBg, "БЯГ");
            Set("btn_run", LangCa, "CÓRRER");
            Set("btn_run", LangCs, "BĚH");
            Set("btn_run", LangDe, "RENNEN");
            Set("btn_run", LangEs, "CORRER");
            Set("btn_run", LangFa, "دویدن");
            Set("btn_run", LangFr, "COURIR");
            Set("btn_run", LangHe, "רוץ");
            Set("btn_run", LangHi, "दौड़");
            Set("btn_run", LangHu, "FUTÁS");
            Set("btn_run", LangHy, "ՎԱԶՔ");
            Set("btn_run", LangId, "LARI");
            Set("btn_run", LangIt, "CORRI");
            Set("btn_run", LangJa, "走る");
            Set("btn_run", LangKa, "სირბილი");
            Set("btn_run", LangKk, "ЖҮГІРУ");
            Set("btn_run", LangNl, "RENNEN");
            Set("btn_run", LangPl, "BIEG");
            Set("btn_run", LangPt, "CORRER");
            Set("btn_run", LangRo, "ALEARGĂ");
            Set("btn_run", LangSk, "BEH");
            Set("btn_run", LangSr, "TRČI");
            Set("btn_run", LangTh, "วิ่ง");
            Set("btn_run", LangTk, "YLGAMAK");
            Set("btn_run", LangUk, "БІГ");
            Set("btn_run", LangUz, "YUGUR");
            Set("btn_run", LangVi, "CHẠY");
            Set("btn_run", LangZh, "奔跑");

            Set("btn_act", LangAr, "تفاعل");
            Set("btn_act", LangAz, "ƏMƏL");
            Set("btn_act", LangBe, "ДЗЕЯН.");
            Set("btn_act", LangBg, "ДЕЙСТВ.");
            Set("btn_act", LangCa, "ACTUA");
            Set("btn_act", LangCs, "AKCE");
            Set("btn_act", LangDe, "AKTION");
            Set("btn_act", LangEs, "ACT.");
            Set("btn_act", LangFa, "عمل");
            Set("btn_act", LangFr, "ACTION");
            Set("btn_act", LangHe, "פעל");
            Set("btn_act", LangHi, "क्रिया");
            Set("btn_act", LangHu, "AKCIÓ");
            Set("btn_act", LangHy, "ԳՈՐԾ");
            Set("btn_act", LangId, "AKSI");
            Set("btn_act", LangIt, "AZIONE");
            Set("btn_act", LangJa, "操作");
            Set("btn_act", LangKa, "ქმედება");
            Set("btn_act", LangKk, "ӘРЕКЕТ");
            Set("btn_act", LangNl, "ACTIE");
            Set("btn_act", LangPl, "AKCJA");
            Set("btn_act", LangPt, "AÇÃO");
            Set("btn_act", LangRo, "ACȚIUNE");
            Set("btn_act", LangSk, "AKCIA");
            Set("btn_act", LangSr, "AKCIJA");
            Set("btn_act", LangTh, "โต้ตอบ");
            Set("btn_act", LangTk, "HEREKET");
            Set("btn_act", LangUk, "ДІЯ");
            Set("btn_act", LangUz, "HARAKAT");
            Set("btn_act", LangVi, "TƯƠNG TÁC");
            Set("btn_act", LangZh, "互动");

            Set("btn_mines", LangAr, "المناجم");
            Set("btn_mines", LangAz, "MƏDƏNLƏR");
            Set("btn_mines", LangBe, "ШАХТЫ");
            Set("btn_mines", LangBg, "МИНИ");
            Set("btn_mines", LangCa, "MINES");
            Set("btn_mines", LangCs, "DOLY");
            Set("btn_mines", LangDe, "MINEN");
            Set("btn_mines", LangEs, "MINAS");
            Set("btn_mines", LangFa, "معادن");
            Set("btn_mines", LangFr, "MINES");
            Set("btn_mines", LangHe, "מכרות");
            Set("btn_mines", LangHi, "खदानें");
            Set("btn_mines", LangHu, "BÁNYÁK");
            Set("btn_mines", LangHy, "ՀԱՆՔԵՐ");
            Set("btn_mines", LangId, "TAMBANG");
            Set("btn_mines", LangIt, "MINIERE");
            Set("btn_mines", LangJa, "鉱山");
            Set("btn_mines", LangKa, "მაღაროები");
            Set("btn_mines", LangKk, "КЕНДЕР");
            Set("btn_mines", LangNl, "MIJNEN");
            Set("btn_mines", LangPl, "KOPALNIE");
            Set("btn_mines", LangPt, "MINAS");
            Set("btn_mines", LangRo, "MINE");
            Set("btn_mines", LangSk, "BANE");
            Set("btn_mines", LangSr, "RUDNICI");
            Set("btn_mines", LangTh, "เหมือง");
            Set("btn_mines", LangTk, "KÄNLER");
            Set("btn_mines", LangUk, "ШАХТИ");
            Set("btn_mines", LangUz, "KONLAR");
            Set("btn_mines", LangVi, "MỎ");
            Set("btn_mines", LangZh, "矿井");

            Set("btn_upgrades", LangAr, "الترقيات");
            Set("btn_upgrades", LangAz, "TƏKMİLLƏŞD.");
            Set("btn_upgrades", LangBe, "ПАЛЯПШ.");
            Set("btn_upgrades", LangBg, "ПОДОБР.");
            Set("btn_upgrades", LangCa, "MILL.");
            Set("btn_upgrades", LangCs, "VYLEPŠ.");
            Set("btn_upgrades", LangDe, "UPGRADES");
            Set("btn_upgrades", LangEs, "MEJORAS");
            Set("btn_upgrades", LangFa, "ارتقاها");
            Set("btn_upgrades", LangFr, "AMÉLIOR.");
            Set("btn_upgrades", LangHe, "שדרוגים");
            Set("btn_upgrades", LangHi, "अपग्रेड");
            Set("btn_upgrades", LangHu, "FEJLESZT.");
            Set("btn_upgrades", LangHy, "ԹԱՐՄԱՑ.");
            Set("btn_upgrades", LangId, "UPGRADE");
            Set("btn_upgrades", LangIt, "UPGRADE");
            Set("btn_upgrades", LangJa, "強化");
            Set("btn_upgrades", LangKa, "გაუმჯობეს.");
            Set("btn_upgrades", LangKk, "ЖАҚСАРТ.");
            Set("btn_upgrades", LangNl, "UPGRADES");
            Set("btn_upgrades", LangPl, "ULEPSZ.");
            Set("btn_upgrades", LangPt, "MELHOR.");
            Set("btn_upgrades", LangRo, "UPGRADE");
            Set("btn_upgrades", LangSk, "VYLEPŠ.");
            Set("btn_upgrades", LangSr, "NADOGR.");
            Set("btn_upgrades", LangTh, "อัปเกรด");
            Set("btn_upgrades", LangTk, "GOWUL.");
            Set("btn_upgrades", LangUk, "ПОКРАЩ.");
            Set("btn_upgrades", LangUz, "YAXSHIL.");
            Set("btn_upgrades", LangVi, "NÂNG CẤP");
            Set("btn_upgrades", LangZh, "升级");

            Set("btn_minions", LangAr, "الأتباع");
            Set("btn_minions", LangAz, "MİNYONLAR");
            Set("btn_minions", LangBe, "МІНЬЁНЫ");
            Set("btn_minions", LangBg, "МИНЬОНИ");
            Set("btn_minions", LangCa, "ESBIRROS");
            Set("btn_minions", LangCs, "POSKOCI");
            Set("btn_minions", LangDe, "MINIONS");
            Set("btn_minions", LangEs, "MINIONS");
            Set("btn_minions", LangFa, "مینون‌ها");
            Set("btn_minions", LangFr, "MINIONS");
            Set("btn_minions", LangHe, "מיניונים");
            Set("btn_minions", LangHi, "मिनियन्स");
            Set("btn_minions", LangHu, "MINIONOK");
            Set("btn_minions", LangHy, "ՄԻՆՅՈՆՆԵՐ");
            Set("btn_minions", LangId, "MINION");
            Set("btn_minions", LangIt, "MINION");
            Set("btn_minions", LangJa, "ミニオン");
            Set("btn_minions", LangKa, "მინიონები");
            Set("btn_minions", LangKk, "МИНЬОНДАР");
            Set("btn_minions", LangNl, "MINIONS");
            Set("btn_minions", LangPl, "MINIONY");
            Set("btn_minions", LangPt, "MINIONS");
            Set("btn_minions", LangRo, "MINIONI");
            Set("btn_minions", LangSk, "MINIONI");
            Set("btn_minions", LangSr, "MINIONI");
            Set("btn_minions", LangTh, "มินเนียน");
            Set("btn_minions", LangTk, "MINIONLAR");
            Set("btn_minions", LangUk, "МІНЬЙОНИ");
            Set("btn_minions", LangUz, "MINIONLAR");
            Set("btn_minions", LangVi, "MINION");
            Set("btn_minions", LangZh, "随从");

            Set("btn_place", LangAr, "ضع");
            Set("btn_place", LangAz, "QOY");
            Set("btn_place", LangBe, "ПАСТАВІЦЬ");
            Set("btn_place", LangBg, "ПОСТАВИ");
            Set("btn_place", LangCa, "POSA");
            Set("btn_place", LangCs, "UMÍSTIT");
            Set("btn_place", LangDe, "PLATZ.");
            Set("btn_place", LangEs, "COLOCAR");
            Set("btn_place", LangFa, "قرار بده");
            Set("btn_place", LangFr, "PLACER");
            Set("btn_place", LangHe, "הצב");
            Set("btn_place", LangHi, "रखें");
            Set("btn_place", LangHu, "LERAK");
            Set("btn_place", LangHy, "ԴՆԵԼ");
            Set("btn_place", LangId, "TEMPAT");
            Set("btn_place", LangIt, "POSA");
            Set("btn_place", LangJa, "設置");
            Set("btn_place", LangKa, "დადგმა");
            Set("btn_place", LangKk, "ОРНАЛ.");
            Set("btn_place", LangNl, "PLAATS");
            Set("btn_place", LangPl, "POSTAW");
            Set("btn_place", LangPt, "COLOCAR");
            Set("btn_place", LangRo, "PLASEAZĂ");
            Set("btn_place", LangSk, "UMIEST.");
            Set("btn_place", LangSr, "POSTAVI");
            Set("btn_place", LangTh, "วาง");
            Set("btn_place", LangTk, "GOÝ");
            Set("btn_place", LangUk, "ПОСТАВ.");
            Set("btn_place", LangUz, "QO‘Y");
            Set("btn_place", LangVi, "ĐẶT");
            Set("btn_place", LangZh, "放置");

            Set("btn_del", LangAr, "حذف");
            Set("btn_del", LangAz, "SİL");
            Set("btn_del", LangBe, "ВЫДАЛ.");
            Set("btn_del", LangBg, "ИЗТР.");
            Set("btn_del", LangCa, "ESBORR.");
            Set("btn_del", LangCs, "SMAZAT");
            Set("btn_del", LangDe, "LÖSCH.");
            Set("btn_del", LangEs, "BORRAR");
            Set("btn_del", LangFa, "حذف");
            Set("btn_del", LangFr, "SUPPR.");
            Set("btn_del", LangHe, "מחק");
            Set("btn_del", LangHi, "हटाएं");
            Set("btn_del", LangHu, "TÖRÖL");
            Set("btn_del", LangHy, "ՋՆՋԵԼ");
            Set("btn_del", LangId, "HAPUS");
            Set("btn_del", LangIt, "ELIM.");
            Set("btn_del", LangJa, "削除");
            Set("btn_del", LangKa, "წაშლა");
            Set("btn_del", LangKk, "ЖОЮ");
            Set("btn_del", LangNl, "VERW.");
            Set("btn_del", LangPl, "USUŃ");
            Set("btn_del", LangPt, "APAGAR");
            Set("btn_del", LangRo, "ȘTERGE");
            Set("btn_del", LangSk, "ZMAZAŤ");
            Set("btn_del", LangSr, "OBRIŠI");
            Set("btn_del", LangTh, "ลบ");
            Set("btn_del", LangTk, "POZ");
            Set("btn_del", LangUk, "ВИДАЛ.");
            Set("btn_del", LangUz, "O‘CHIR");
            Set("btn_del", LangVi, "XÓA");
            Set("btn_del", LangZh, "删除");

            Set("btn_cancel", LangAr, "إلغاء");
            Set("btn_cancel", LangAz, "LƏĞV");
            Set("btn_cancel", LangBe, "АДМЕНА");
            Set("btn_cancel", LangBg, "ОТКАЗ");
            Set("btn_cancel", LangCa, "CANCEL.");
            Set("btn_cancel", LangCs, "ZRUŠIT");
            Set("btn_cancel", LangDe, "ABBR.");
            Set("btn_cancel", LangEs, "CANCEL.");
            Set("btn_cancel", LangFa, "لغو");
            Set("btn_cancel", LangFr, "ANNUL.");
            Set("btn_cancel", LangHe, "בטל");
            Set("btn_cancel", LangHi, "रद्द");
            Set("btn_cancel", LangHu, "MÉGSE");
            Set("btn_cancel", LangHy, "ՉԵՂ.");
            Set("btn_cancel", LangId, "BATAL");
            Set("btn_cancel", LangIt, "ANNULLA");
            Set("btn_cancel", LangJa, "取消");
            Set("btn_cancel", LangKa, "გაუქმ.");
            Set("btn_cancel", LangKk, "БАС Т.");
            Set("btn_cancel", LangNl, "ANNUL.");
            Set("btn_cancel", LangPl, "ANULUJ");
            Set("btn_cancel", LangPt, "CANCEL.");
            Set("btn_cancel", LangRo, "ANUL.");
            Set("btn_cancel", LangSk, "ZRUŠIŤ");
            Set("btn_cancel", LangSr, "OTKAŽI");
            Set("btn_cancel", LangTh, "ยกเลิก");
            Set("btn_cancel", LangTk, "ÝATYR");
            Set("btn_cancel", LangUk, "СКАС.");
            Set("btn_cancel", LangUz, "BEKOR");
            Set("btn_cancel", LangVi, "HỦY");
            Set("btn_cancel", LangZh, "取消");

            Set("block_dirt", LangAr, "تراب");
            Set("block_dirt", LangAz, "Torpaq");
            Set("block_dirt", LangBe, "Зямля");
            Set("block_dirt", LangBg, "Пръст");
            Set("block_dirt", LangCa, "Terra");
            Set("block_dirt", LangCs, "Hlína");
            Set("block_dirt", LangDe, "Erde");
            Set("block_dirt", LangEs, "Tierra");
            Set("block_dirt", LangFa, "خاک");
            Set("block_dirt", LangFr, "Terre");
            Set("block_dirt", LangHe, "עפר");
            Set("block_dirt", LangHi, "मिट्टी");
            Set("block_dirt", LangHu, "Föld");
            Set("block_dirt", LangHy, "Հող");
            Set("block_dirt", LangId, "Tanah");
            Set("block_dirt", LangIt, "Terra");
            Set("block_dirt", LangJa, "土");
            Set("block_dirt", LangKa, "მიწა");
            Set("block_dirt", LangKk, "Топырақ");
            Set("block_dirt", LangNl, "Aarde");
            Set("block_dirt", LangPl, "Ziemia");
            Set("block_dirt", LangPt, "Terra");
            Set("block_dirt", LangRo, "Pământ");
            Set("block_dirt", LangSk, "Hlina");
            Set("block_dirt", LangSr, "Zemlja");
            Set("block_dirt", LangTh, "ดิน");
            Set("block_dirt", LangTk, "Toprak");
            Set("block_dirt", LangUk, "Земля");
            Set("block_dirt", LangUz, "Tuproq");
            Set("block_dirt", LangVi, "Đất");
            Set("block_dirt", LangZh, "泥土");

            Set("block_stone", LangAr, "حجر");
            Set("block_stone", LangAz, "Daş");
            Set("block_stone", LangBe, "Камень");
            Set("block_stone", LangBg, "Камък");
            Set("block_stone", LangCa, "Pedra");
            Set("block_stone", LangCs, "Kámen");
            Set("block_stone", LangDe, "Stein");
            Set("block_stone", LangEs, "Piedra");
            Set("block_stone", LangFa, "سنگ");
            Set("block_stone", LangFr, "Pierre");
            Set("block_stone", LangHe, "אבן");
            Set("block_stone", LangHi, "पत्थर");
            Set("block_stone", LangHu, "Kő");
            Set("block_stone", LangHy, "Քար");
            Set("block_stone", LangId, "Batu");
            Set("block_stone", LangIt, "Pietra");
            Set("block_stone", LangJa, "石");
            Set("block_stone", LangKa, "ქვა");
            Set("block_stone", LangKk, "Тас");
            Set("block_stone", LangNl, "Steen");
            Set("block_stone", LangPl, "Kamień");
            Set("block_stone", LangPt, "Pedra");
            Set("block_stone", LangRo, "Piatră");
            Set("block_stone", LangSk, "Kameň");
            Set("block_stone", LangSr, "Kamen");
            Set("block_stone", LangTh, "หิน");
            Set("block_stone", LangTk, "Daş");
            Set("block_stone", LangUk, "Камінь");
            Set("block_stone", LangUz, "Tosh");
            Set("block_stone", LangVi, "Đá");
            Set("block_stone", LangZh, "石头");

            Set("block_iron", LangAr, "حديد");
            Set("block_iron", LangAz, "Dəmir");
            Set("block_iron", LangBe, "Жалеза");
            Set("block_iron", LangBg, "Желязо");
            Set("block_iron", LangCa, "Ferro");
            Set("block_iron", LangCs, "Železo");
            Set("block_iron", LangDe, "Eisen");
            Set("block_iron", LangEs, "Hierro");
            Set("block_iron", LangFa, "آهن");
            Set("block_iron", LangFr, "Fer");
            Set("block_iron", LangHe, "ברזל");
            Set("block_iron", LangHi, "लोहा");
            Set("block_iron", LangHu, "Vas");
            Set("block_iron", LangHy, "Երկաթ");
            Set("block_iron", LangId, "Besi");
            Set("block_iron", LangIt, "Ferro");
            Set("block_iron", LangJa, "鉄");
            Set("block_iron", LangKa, "რკინა");
            Set("block_iron", LangKk, "Темір");
            Set("block_iron", LangNl, "IJzer");
            Set("block_iron", LangPl, "Żelazo");
            Set("block_iron", LangPt, "Ferro");
            Set("block_iron", LangRo, "Fier");
            Set("block_iron", LangSk, "Železo");
            Set("block_iron", LangSr, "Gvožđe");
            Set("block_iron", LangTh, "เหล็ก");
            Set("block_iron", LangTk, "Demir");
            Set("block_iron", LangUk, "Залізо");
            Set("block_iron", LangUz, "Temir");
            Set("block_iron", LangVi, "Sắt");
            Set("block_iron", LangZh, "铁");

            Set("block_gold", LangAr, "ذهب");
            Set("block_gold", LangAz, "Qızıl");
            Set("block_gold", LangBe, "Золата");
            Set("block_gold", LangBg, "Злато");
            Set("block_gold", LangCa, "Or");
            Set("block_gold", LangCs, "Zlato");
            Set("block_gold", LangDe, "Gold");
            Set("block_gold", LangEs, "Oro");
            Set("block_gold", LangFa, "طلا");
            Set("block_gold", LangFr, "Or");
            Set("block_gold", LangHe, "זהב");
            Set("block_gold", LangHi, "सोना");
            Set("block_gold", LangHu, "Arany");
            Set("block_gold", LangHy, "Ոսկի");
            Set("block_gold", LangId, "Emas");
            Set("block_gold", LangIt, "Oro");
            Set("block_gold", LangJa, "金");
            Set("block_gold", LangKa, "ოქრო");
            Set("block_gold", LangKk, "Алтын");
            Set("block_gold", LangNl, "Goud");
            Set("block_gold", LangPl, "Złoto");
            Set("block_gold", LangPt, "Ouro");
            Set("block_gold", LangRo, "Aur");
            Set("block_gold", LangSk, "Zlato");
            Set("block_gold", LangSr, "Zlato");
            Set("block_gold", LangTh, "ทอง");
            Set("block_gold", LangTk, "Altyn");
            Set("block_gold", LangUk, "Золото");
            Set("block_gold", LangUz, "Oltin");
            Set("block_gold", LangVi, "Vàng");
            Set("block_gold", LangZh, "金");

            Set("btn_to_island", LangAr, "إلى الجزيرة");
            Set("btn_to_island", LangAz, "ADAYA");
            Set("btn_to_island", LangBe, "НА ВОСТРАЎ");
            Set("btn_to_island", LangBg, "КЪМ ОСТРОВА");
            Set("btn_to_island", LangCa, "A L'ILLA");
            Set("btn_to_island", LangCs, "NA OSTROV");
            Set("btn_to_island", LangDe, "ZUR INSEL");
            Set("btn_to_island", LangEs, "A LA ISLA");
            Set("btn_to_island", LangFa, "به جزیره");
            Set("btn_to_island", LangFr, "À L'ÎLE");
            Set("btn_to_island", LangHe, "לאי");
            Set("btn_to_island", LangHi, "द्वीप पर");
            Set("btn_to_island", LangHu, "SZIGETRE");
            Set("btn_to_island", LangHy, "ԴԵՊԻ ԿՂԶԻ");
            Set("btn_to_island", LangId, "KE PULAU");
            Set("btn_to_island", LangIt, "ALL'ISOLA");
            Set("btn_to_island", LangJa, "島へ");
            Set("btn_to_island", LangKa, "კუნძულზე");
            Set("btn_to_island", LangKk, "АРАЛҒА");
            Set("btn_to_island", LangNl, "NAAR EILAND");
            Set("btn_to_island", LangPl, "NA WYSPĘ");
            Set("btn_to_island", LangPt, "PARA ILHA");
            Set("btn_to_island", LangRo, "SPRE INSULĂ");
            Set("btn_to_island", LangSk, "NA OSTROV");
            Set("btn_to_island", LangSr, "NA OSTRVO");
            Set("btn_to_island", LangTh, "ไปเกาะ");
            Set("btn_to_island", LangTk, "ADA");
            Set("btn_to_island", LangUk, "НА ОСТРІВ");
            Set("btn_to_island", LangUz, "OROLGA");
            Set("btn_to_island", LangVi, "ĐẾN ĐẢO");
            Set("btn_to_island", LangZh, "前往岛屿");

            Set("btn_to_lobby", LangAr, "إلى اللوبي");
            Set("btn_to_lobby", LangAz, "LOBBİYƏ");
            Set("btn_to_lobby", LangBe, "У ЛОБІ");
            Set("btn_to_lobby", LangBg, "КЪМ ЛОБИТО");
            Set("btn_to_lobby", LangCa, "AL VESTÍBUL");
            Set("btn_to_lobby", LangCs, "DO LOBBY");
            Set("btn_to_lobby", LangDe, "IN DIE LOBBY");
            Set("btn_to_lobby", LangEs, "AL LOBBY");
            Set("btn_to_lobby", LangFa, "به لابی");
            Set("btn_to_lobby", LangFr, "AU LOBBY");
            Set("btn_to_lobby", LangHe, "ללובי");
            Set("btn_to_lobby", LangHi, "लॉबी में");
            Set("btn_to_lobby", LangHu, "LOBBIBA");
            Set("btn_to_lobby", LangHy, "ԴԵՊԻ ԼՈԲԲԻ");
            Set("btn_to_lobby", LangId, "KE LOBI");
            Set("btn_to_lobby", LangIt, "ALLA LOBBY");
            Set("btn_to_lobby", LangJa, "ロビーへ");
            Set("btn_to_lobby", LangKa, "ლობიში");
            Set("btn_to_lobby", LangKk, "ЛОББИГЕ");
            Set("btn_to_lobby", LangNl, "NAAR LOBBY");
            Set("btn_to_lobby", LangPl, "DO LOBBY");
            Set("btn_to_lobby", LangPt, "PARA LOBBY");
            Set("btn_to_lobby", LangRo, "ÎN LOBBY");
            Set("btn_to_lobby", LangSk, "DO LOBBY");
            Set("btn_to_lobby", LangSr, "U LOBI");
            Set("btn_to_lobby", LangTh, "ไปล็อบบี้");
            Set("btn_to_lobby", LangTk, "LOBBI");
            Set("btn_to_lobby", LangUk, "У ЛОБІ");
            Set("btn_to_lobby", LangUz, "LOBBIGA");
            Set("btn_to_lobby", LangVi, "VỀ SẢNH");
            Set("btn_to_lobby", LangZh, "返回大厅");

            Set("btn_buy", LangAr, "شراء");
            Set("btn_buy", LangAz, "AL");
            Set("btn_buy", LangBe, "КУПІЦЬ");
            Set("btn_buy", LangBg, "КУПИ");
            Set("btn_buy", LangCa, "COMPRAR");
            Set("btn_buy", LangCs, "KOUPIT");
            Set("btn_buy", LangDe, "KAUFEN");
            Set("btn_buy", LangEs, "COMPRAR");
            Set("btn_buy", LangFa, "خرید");
            Set("btn_buy", LangFr, "ACHETER");
            Set("btn_buy", LangHe, "קנה");
            Set("btn_buy", LangHi, "खरीदें");
            Set("btn_buy", LangHu, "VÉTEL");
            Set("btn_buy", LangHy, "ԳՆԵԼ");
            Set("btn_buy", LangId, "BELI");
            Set("btn_buy", LangIt, "COMPRA");
            Set("btn_buy", LangJa, "購入");
            Set("btn_buy", LangKa, "ყიდვა");
            Set("btn_buy", LangKk, "САТЫП АЛУ");
            Set("btn_buy", LangNl, "KOPEN");
            Set("btn_buy", LangPl, "KUP");
            Set("btn_buy", LangPt, "COMPRAR");
            Set("btn_buy", LangRo, "CUMPĂRĂ");
            Set("btn_buy", LangSk, "KÚPIŤ");
            Set("btn_buy", LangSr, "KUPI");
            Set("btn_buy", LangTh, "ซื้อ");
            Set("btn_buy", LangTk, "SATYN AL");
            Set("btn_buy", LangUk, "КУПИТИ");
            Set("btn_buy", LangUz, "SOTIB OL");
            Set("btn_buy", LangVi, "MUA");
            Set("btn_buy", LangZh, "购买");

            Set("btn_owned", LangAr, "تم الشراء");
            Set("btn_owned", LangAz, "ALINIB");
            Set("btn_owned", LangBe, "НАБЫТА");
            Set("btn_owned", LangBg, "КУПЕНО");
            Set("btn_owned", LangCa, "COMPRAT");
            Set("btn_owned", LangCs, "VLASTNÍŠ");
            Set("btn_owned", LangDe, "GEKAUFT");
            Set("btn_owned", LangEs, "COMPRADO");
            Set("btn_owned", LangFa, "خریداری شد");
            Set("btn_owned", LangFr, "ACHETÉ");
            Set("btn_owned", LangHe, "נרכש");
            Set("btn_owned", LangHi, "खरीदा गया");
            Set("btn_owned", LangHu, "MEGVAN");
            Set("btn_owned", LangHy, "ԳՆՎԱԾ");
            Set("btn_owned", LangId, "DIMILIKI");
            Set("btn_owned", LangIt, "POSSEDUTO");
            Set("btn_owned", LangJa, "購入済み");
            Set("btn_owned", LangKa, "ნაყიდია");
            Set("btn_owned", LangKk, "САТЫП АЛЫНДЫ");
            Set("btn_owned", LangNl, "IN BEZIT");
            Set("btn_owned", LangPl, "POSIADASZ");
            Set("btn_owned", LangPt, "COMPRADO");
            Set("btn_owned", LangRo, "DEȚINUT");
            Set("btn_owned", LangSk, "VLASTNÍŠ");
            Set("btn_owned", LangSr, "POSEDUJEŠ");
            Set("btn_owned", LangTh, "เป็นเจ้าของ");
            Set("btn_owned", LangTk, "BAR");
            Set("btn_owned", LangUk, "ПРИДБАНО");
            Set("btn_owned", LangUz, "SENDA BOR");
            Set("btn_owned", LangVi, "ĐÃ SỞ HỮU");
            Set("btn_owned", LangZh, "已拥有");

            Set("btn_equipped", LangAr, "مجهز");
            Set("btn_equipped", LangAz, "TƏCHİZLİ");
            Set("btn_equipped", LangBe, "ЭКІПІР.");
            Set("btn_equipped", LangBg, "ЕКИПИР.");
            Set("btn_equipped", LangCa, "EQUIPAT");
            Set("btn_equipped", LangCs, "VYBAVENO");
            Set("btn_equipped", LangDe, "AUSGER.");
            Set("btn_equipped", LangEs, "EQUIPADO");
            Set("btn_equipped", LangFa, "مجهز");
            Set("btn_equipped", LangFr, "ÉQUIPÉ");
            Set("btn_equipped", LangHe, "מצויד");
            Set("btn_equipped", LangHi, "सुसज्जित");
            Set("btn_equipped", LangHu, "FELSZER.");
            Set("btn_equipped", LangHy, "ՍԱՐՔԱՎ.");
            Set("btn_equipped", LangId, "DIPAKAI");
            Set("btn_equipped", LangIt, "EQUIP.");
            Set("btn_equipped", LangJa, "装備中");
            Set("btn_equipped", LangKa, "აღჭურვ.");
            Set("btn_equipped", LangKk, "ЖАБДЫҚ.");
            Set("btn_equipped", LangNl, "UITGER.");
            Set("btn_equipped", LangPl, "WYPOSAŻ.");
            Set("btn_equipped", LangPt, "EQUIP.");
            Set("btn_equipped", LangRo, "ECHIPAT");
            Set("btn_equipped", LangSk, "VYBAV.");
            Set("btn_equipped", LangSr, "OPREMLJ.");
            Set("btn_equipped", LangTh, "สวมใส่");
            Set("btn_equipped", LangTk, "GEÝILEN");
            Set("btn_equipped", LangUk, "ЕКІПІР.");
            Set("btn_equipped", LangUz, "JIHOZ.");
            Set("btn_equipped", LangVi, "TRANG BỊ");
            Set("btn_equipped", LangZh, "已装备");

            Set("stats_power", LangAr, "القوة");
            Set("stats_power", LangAz, "Güc");
            Set("stats_power", LangBe, "Сіла");
            Set("stats_power", LangBg, "Сила");
            Set("stats_power", LangCa, "Poder");
            Set("stats_power", LangCs, "Síla");
            Set("stats_power", LangDe, "Kraft");
            Set("stats_power", LangEs, "Poder");
            Set("stats_power", LangFa, "قدرت");
            Set("stats_power", LangFr, "Puissance");
            Set("stats_power", LangHe, "כוח");
            Set("stats_power", LangHi, "शक्ति");
            Set("stats_power", LangHu, "Erő");
            Set("stats_power", LangHy, "Ուժ");
            Set("stats_power", LangId, "Daya");
            Set("stats_power", LangIt, "Potenza");
            Set("stats_power", LangJa, "パワー");
            Set("stats_power", LangKa, "ძალა");
            Set("stats_power", LangKk, "Күш");
            Set("stats_power", LangNl, "Kracht");
            Set("stats_power", LangPl, "Moc");
            Set("stats_power", LangPt, "Poder");
            Set("stats_power", LangRo, "Putere");
            Set("stats_power", LangSk, "Sila");
            Set("stats_power", LangSr, "Snaga");
            Set("stats_power", LangTh, "พลัง");
            Set("stats_power", LangTk, "Güýç");
            Set("stats_power", LangUk, "Сила");
            Set("stats_power", LangUz, "Kuch");
            Set("stats_power", LangVi, "Sức mạnh");
            Set("stats_power", LangZh, "力量");

            Set("stats_req_lv", LangAr, "المستوى المطلوب");
            Set("stats_req_lv", LangAz, "Lazım Sv.");
            Set("stats_req_lv", LangBe, "Патр. ур.");
            Set("stats_req_lv", LangBg, "Нуж. ниво");
            Set("stats_req_lv", LangCa, "Nv requerit");
            Set("stats_req_lv", LangCs, "Potř. úr.");
            Set("stats_req_lv", LangDe, "Ben. Stufe");
            Set("stats_req_lv", LangEs, "Nv req.");
            Set("stats_req_lv", LangFa, "سطح لازم");
            Set("stats_req_lv", LangFr, "Nv requis");
            Set("stats_req_lv", LangHe, "רמה נדרשת");
            Set("stats_req_lv", LangHi, "आवश्यक स्तर");
            Set("stats_req_lv", LangHu, "Szüks. szint");
            Set("stats_req_lv", LangHy, "Պահանջ. մ.");
            Set("stats_req_lv", LangId, "Lv perlu");
            Set("stats_req_lv", LangIt, "Lv richiesto");
            Set("stats_req_lv", LangJa, "必要Lv");
            Set("stats_req_lv", LangKa, "საჭ. დონე");
            Set("stats_req_lv", LangKk, "Қаж. д.");
            Set("stats_req_lv", LangNl, "Vereist lvl");
            Set("stats_req_lv", LangPl, "Wym. poz.");
            Set("stats_req_lv", LangPt, "Nv req.");
            Set("stats_req_lv", LangRo, "Nivel nec.");
            Set("stats_req_lv", LangSk, "Potr. úr.");
            Set("stats_req_lv", LangSr, "Potr. niv.");
            Set("stats_req_lv", LangTh, "เลเวลที่ต้องใช้");
            Set("stats_req_lv", LangTk, "Gerekli Lv.");
            Set("stats_req_lv", LangUk, "Потр. рів.");
            Set("stats_req_lv", LangUz, "Kerakli Lv.");
            Set("stats_req_lv", LangVi, "Cấp cần");
            Set("stats_req_lv", LangZh, "需求等级");

            Set("stats_price", LangAr, "السعر");
            Set("stats_price", LangAz, "Qiymət");
            Set("stats_price", LangBe, "Кошт");
            Set("stats_price", LangBg, "Цена");
            Set("stats_price", LangCa, "Preu");
            Set("stats_price", LangCs, "Cena");
            Set("stats_price", LangDe, "Preis");
            Set("stats_price", LangEs, "Precio");
            Set("stats_price", LangFa, "قیمت");
            Set("stats_price", LangFr, "Prix");
            Set("stats_price", LangHe, "מחיר");
            Set("stats_price", LangHi, "कीमत");
            Set("stats_price", LangHu, "Ár");
            Set("stats_price", LangHy, "Գին");
            Set("stats_price", LangId, "Harga");
            Set("stats_price", LangIt, "Prezzo");
            Set("stats_price", LangJa, "価格");
            Set("stats_price", LangKa, "ფასი");
            Set("stats_price", LangKk, "Баға");
            Set("stats_price", LangNl, "Prijs");
            Set("stats_price", LangPl, "Cena");
            Set("stats_price", LangPt, "Preço");
            Set("stats_price", LangRo, "Preț");
            Set("stats_price", LangSk, "Cena");
            Set("stats_price", LangSr, "Cena");
            Set("stats_price", LangTh, "ราคา");
            Set("stats_price", LangTk, "Baha");
            Set("stats_price", LangUk, "Ціна");
            Set("stats_price", LangUz, "Narx");
            Set("stats_price", LangVi, "Giá");
            Set("stats_price", LangZh, "价格");

            Set("shop_balance", LangAr, "الرصيد");
            Set("shop_balance", LangAz, "Balans");
            Set("shop_balance", LangBe, "Баланс");
            Set("shop_balance", LangBg, "Баланс");
            Set("shop_balance", LangCa, "Saldo");
            Set("shop_balance", LangCs, "Zůstatek");
            Set("shop_balance", LangDe, "Guthaben");
            Set("shop_balance", LangEs, "Saldo");
            Set("shop_balance", LangFa, "موجودی");
            Set("shop_balance", LangFr, "Solde");
            Set("shop_balance", LangHe, "יתרה");
            Set("shop_balance", LangHi, "बैलेंस");
            Set("shop_balance", LangHu, "Egyenleg");
            Set("shop_balance", LangHy, "Մնացորդ");
            Set("shop_balance", LangId, "Saldo");
            Set("shop_balance", LangIt, "Saldo");
            Set("shop_balance", LangJa, "残高");
            Set("shop_balance", LangKa, "ბალანსი");
            Set("shop_balance", LangKk, "Баланс");
            Set("shop_balance", LangNl, "Saldo");
            Set("shop_balance", LangPl, "Saldo");
            Set("shop_balance", LangPt, "Saldo");
            Set("shop_balance", LangRo, "Sold");
            Set("shop_balance", LangSk, "Zostatok");
            Set("shop_balance", LangSr, "Balans");
            Set("shop_balance", LangTh, "ยอดคงเหลือ");
            Set("shop_balance", LangTk, "Balans");
            Set("shop_balance", LangUk, "Баланс");
            Set("shop_balance", LangUz, "Balans");
            Set("shop_balance", LangVi, "Số dư");
            Set("shop_balance", LangZh, "余额");

            Set("mine_bronze_name", LangAr, "منجم برونزي");
            Set("mine_bronze_name", LangAz, "Bürünc Mədən");
            Set("mine_bronze_name", LangBe, "Бронзавая шахта");
            Set("mine_bronze_name", LangBg, "Бронзова мина");
            Set("mine_bronze_name", LangCa, "Mina de bronze");
            Set("mine_bronze_name", LangCs, "Bronzový důl");
            Set("mine_bronze_name", LangDe, "Bronzemine");
            Set("mine_bronze_name", LangEs, "Mina de bronce");
            Set("mine_bronze_name", LangFa, "معدن برنزی");
            Set("mine_bronze_name", LangFr, "Mine de bronze");
            Set("mine_bronze_name", LangHe, "מכרה ברונזה");
            Set("mine_bronze_name", LangHi, "कांस्य खान");
            Set("mine_bronze_name", LangHu, "Bronzbánya");
            Set("mine_bronze_name", LangHy, "Բրոնզե հանք");
            Set("mine_bronze_name", LangId, "Tambang Perunggu");
            Set("mine_bronze_name", LangIt, "Miniera di bronzo");
            Set("mine_bronze_name", LangJa, "ブロンズ鉱山");
            Set("mine_bronze_name", LangKa, "ბრინჯაოს მაღარო");
            Set("mine_bronze_name", LangKk, "Қола кені");
            Set("mine_bronze_name", LangNl, "Bronsmijn");
            Set("mine_bronze_name", LangPl, "Kopalnia brązu");
            Set("mine_bronze_name", LangPt, "Mina de bronze");
            Set("mine_bronze_name", LangRo, "Mină de bronz");
            Set("mine_bronze_name", LangSk, "Bronzová baňa");
            Set("mine_bronze_name", LangSr, "Bronzani rudnik");
            Set("mine_bronze_name", LangTh, "เหมืองบรอนซ์");
            Set("mine_bronze_name", LangTk, "Bürünç käni");
            Set("mine_bronze_name", LangUk, "Бронзова шахта");
            Set("mine_bronze_name", LangUz, "Bronza koni");
            Set("mine_bronze_name", LangVi, "Mỏ đồng đỏ");
            Set("mine_bronze_name", LangZh, "青铜矿井");

            Set("mine_silver_name", LangAr, "منجم فضي");
            Set("mine_silver_name", LangAz, "Gümüş Mədən");
            Set("mine_silver_name", LangBe, "Сярэбраная шахта");
            Set("mine_silver_name", LangBg, "Сребърна мина");
            Set("mine_silver_name", LangCa, "Mina de plata");
            Set("mine_silver_name", LangCs, "Stříbrný důl");
            Set("mine_silver_name", LangDe, "Silbermine");
            Set("mine_silver_name", LangEs, "Mina de plata");
            Set("mine_silver_name", LangFa, "معدن نقره");
            Set("mine_silver_name", LangFr, "Mine d'argent");
            Set("mine_silver_name", LangHe, "מכרה כסף");
            Set("mine_silver_name", LangHi, "रजत खान");
            Set("mine_silver_name", LangHu, "Ezüstbánya");
            Set("mine_silver_name", LangHy, "Արծաթե հանք");
            Set("mine_silver_name", LangId, "Tambang Perak");
            Set("mine_silver_name", LangIt, "Miniera d'argento");
            Set("mine_silver_name", LangJa, "銀鉱山");
            Set("mine_silver_name", LangKa, "ვერცხლის მაღარო");
            Set("mine_silver_name", LangKk, "Күміс кені");
            Set("mine_silver_name", LangNl, "Zilvermijn");
            Set("mine_silver_name", LangPl, "Kopalnia srebra");
            Set("mine_silver_name", LangPt, "Mina de prata");
            Set("mine_silver_name", LangRo, "Mină de argint");
            Set("mine_silver_name", LangSk, "Strieborná baňa");
            Set("mine_silver_name", LangSr, "Srebrni rudnik");
            Set("mine_silver_name", LangTh, "เหมืองเงิน");
            Set("mine_silver_name", LangTk, "Kümüş käni");
            Set("mine_silver_name", LangUk, "Срібна шахта");
            Set("mine_silver_name", LangUz, "Kumush koni");
            Set("mine_silver_name", LangVi, "Mỏ bạc");
            Set("mine_silver_name", LangZh, "白银矿井");

            Set("mine_gold_name", LangAr, "منجم ذهبي");
            Set("mine_gold_name", LangAz, "Qızıl Mədən");
            Set("mine_gold_name", LangBe, "Залатая шахта");
            Set("mine_gold_name", LangBg, "Златна мина");
            Set("mine_gold_name", LangCa, "Mina d'or");
            Set("mine_gold_name", LangCs, "Zlatý důl");
            Set("mine_gold_name", LangDe, "Goldmine");
            Set("mine_gold_name", LangEs, "Mina de oro");
            Set("mine_gold_name", LangFa, "معدن طلا");
            Set("mine_gold_name", LangFr, "Mine d'or");
            Set("mine_gold_name", LangHe, "מכרה זהב");
            Set("mine_gold_name", LangHi, "स्वर्ण खान");
            Set("mine_gold_name", LangHu, "Aranybánya");
            Set("mine_gold_name", LangHy, "Ոսկու հանք");
            Set("mine_gold_name", LangId, "Tambang Emas");
            Set("mine_gold_name", LangIt, "Miniera d'oro");
            Set("mine_gold_name", LangJa, "金鉱山");
            Set("mine_gold_name", LangKa, "ოქროს მაღარო");
            Set("mine_gold_name", LangKk, "Алтын кені");
            Set("mine_gold_name", LangNl, "Goudmijn");
            Set("mine_gold_name", LangPl, "Kopalnia złota");
            Set("mine_gold_name", LangPt, "Mina de ouro");
            Set("mine_gold_name", LangRo, "Mină de aur");
            Set("mine_gold_name", LangSk, "Zlatá baňa");
            Set("mine_gold_name", LangSr, "Zlatni rudnik");
            Set("mine_gold_name", LangTh, "เหมืองทอง");
            Set("mine_gold_name", LangTk, "Altyn käni");
            Set("mine_gold_name", LangUk, "Золота шахта");
            Set("mine_gold_name", LangUz, "Oltin koni");
            Set("mine_gold_name", LangVi, "Mỏ vàng");
            Set("mine_gold_name", LangZh, "黄金矿井");

            Set("pickaxe_stone_name", LangAr, "فأس حجري");
            Set("pickaxe_stone_name", LangAz, "Daş Qazma");
            Set("pickaxe_stone_name", LangBe, "Каменная кірка");
            Set("pickaxe_stone_name", LangBg, "Каменна кирка");
            Set("pickaxe_stone_name", LangCa, "Pic de pedra");
            Set("pickaxe_stone_name", LangCs, "Kamenný krumpáč");
            Set("pickaxe_stone_name", LangDe, "Steinspitzhacke");
            Set("pickaxe_stone_name", LangEs, "Pico de piedra");
            Set("pickaxe_stone_name", LangFa, "کلنگ سنگی");
            Set("pickaxe_stone_name", LangFr, "Pioche en pierre");
            Set("pickaxe_stone_name", LangHe, "מכוש אבן");
            Set("pickaxe_stone_name", LangHi, "पत्थर की कुदाल");
            Set("pickaxe_stone_name", LangHu, "Kőcsákány");
            Set("pickaxe_stone_name", LangHy, "Քարե քլունգ");
            Set("pickaxe_stone_name", LangId, "Pickaxe Batu");
            Set("pickaxe_stone_name", LangIt, "Piccone di pietra");
            Set("pickaxe_stone_name", LangJa, "石のツルハシ");
            Set("pickaxe_stone_name", LangKa, "ქვის მწიკვი");
            Set("pickaxe_stone_name", LangKk, "Тас кірке");
            Set("pickaxe_stone_name", LangNl, "Stenen houweel");
            Set("pickaxe_stone_name", LangPl, "Kamienny kilof");
            Set("pickaxe_stone_name", LangPt, "Picareta de pedra");
            Set("pickaxe_stone_name", LangRo, "Târnăcop de piatră");
            Set("pickaxe_stone_name", LangSk, "Kamenný krompáč");
            Set("pickaxe_stone_name", LangSr, "Kameni kramp");
            Set("pickaxe_stone_name", LangTh, "พลั่วหิน");
            Set("pickaxe_stone_name", LangTk, "Daş gazma");
            Set("pickaxe_stone_name", LangUk, "Кам'яна кирка");
            Set("pickaxe_stone_name", LangUz, "Tosh ketmon");
            Set("pickaxe_stone_name", LangVi, "Cuốc đá");
            Set("pickaxe_stone_name", LangZh, "石镐");

            Set("pickaxe_iron_name", LangAr, "فأس حديدي");
            Set("pickaxe_iron_name", LangAz, "Dəmir Qazma");
            Set("pickaxe_iron_name", LangBe, "Жалезная кірка");
            Set("pickaxe_iron_name", LangBg, "Желязна кирка");
            Set("pickaxe_iron_name", LangCa, "Pic de ferro");
            Set("pickaxe_iron_name", LangCs, "Železný krumpáč");
            Set("pickaxe_iron_name", LangDe, "Eisenspitzhacke");
            Set("pickaxe_iron_name", LangEs, "Pico de hierro");
            Set("pickaxe_iron_name", LangFa, "کلنگ آهنی");
            Set("pickaxe_iron_name", LangFr, "Pioche en fer");
            Set("pickaxe_iron_name", LangHe, "מכוש ברזל");
            Set("pickaxe_iron_name", LangHi, "लोहे की कुदाल");
            Set("pickaxe_iron_name", LangHu, "Vascsákány");
            Set("pickaxe_iron_name", LangHy, "Երկաթե քլունգ");
            Set("pickaxe_iron_name", LangId, "Pickaxe Besi");
            Set("pickaxe_iron_name", LangIt, "Piccone di ferro");
            Set("pickaxe_iron_name", LangJa, "鉄のツルハシ");
            Set("pickaxe_iron_name", LangKa, "რკინის მწიკვი");
            Set("pickaxe_iron_name", LangKk, "Темір кірке");
            Set("pickaxe_iron_name", LangNl, "IJzeren houweel");
            Set("pickaxe_iron_name", LangPl, "Żelazny kilof");
            Set("pickaxe_iron_name", LangPt, "Picareta de ferro");
            Set("pickaxe_iron_name", LangRo, "Târnăcop de fier");
            Set("pickaxe_iron_name", LangSk, "Železný krompáč");
            Set("pickaxe_iron_name", LangSr, "Gvozdeni kramp");
            Set("pickaxe_iron_name", LangTh, "พลั่วเหล็ก");
            Set("pickaxe_iron_name", LangTk, "Demir gazma");
            Set("pickaxe_iron_name", LangUk, "Залізна кирка");
            Set("pickaxe_iron_name", LangUz, "Temir ketmon");
            Set("pickaxe_iron_name", LangVi, "Cuốc sắt");
            Set("pickaxe_iron_name", LangZh, "铁镐");

            Set("pickaxe_gold_name", LangAr, "فأس ذهبي");
            Set("pickaxe_gold_name", LangAz, "Qızıl Qazma");
            Set("pickaxe_gold_name", LangBe, "Залатая кірка");
            Set("pickaxe_gold_name", LangBg, "Златна кирка");
            Set("pickaxe_gold_name", LangCa, "Pic d'or");
            Set("pickaxe_gold_name", LangCs, "Zlatý krumpáč");
            Set("pickaxe_gold_name", LangDe, "Goldspitzhacke");
            Set("pickaxe_gold_name", LangEs, "Pico de oro");
            Set("pickaxe_gold_name", LangFa, "کلنگ طلایی");
            Set("pickaxe_gold_name", LangFr, "Pioche en or");
            Set("pickaxe_gold_name", LangHe, "מכוש זהב");
            Set("pickaxe_gold_name", LangHi, "सोने की कुदाल");
            Set("pickaxe_gold_name", LangHu, "Aranycsákány");
            Set("pickaxe_gold_name", LangHy, "Ոսկե քլունգ");
            Set("pickaxe_gold_name", LangId, "Pickaxe Emas");
            Set("pickaxe_gold_name", LangIt, "Piccone d'oro");
            Set("pickaxe_gold_name", LangJa, "金のツルハシ");
            Set("pickaxe_gold_name", LangKa, "ოქროს მწიკვი");
            Set("pickaxe_gold_name", LangKk, "Алтын кірке");
            Set("pickaxe_gold_name", LangNl, "Gouden houweel");
            Set("pickaxe_gold_name", LangPl, "Złoty kilof");
            Set("pickaxe_gold_name", LangPt, "Picareta de ouro");
            Set("pickaxe_gold_name", LangRo, "Târnăcop de aur");
            Set("pickaxe_gold_name", LangSk, "Zlatý krompáč");
            Set("pickaxe_gold_name", LangSr, "Zlatni kramp");
            Set("pickaxe_gold_name", LangTh, "พลั่วทอง");
            Set("pickaxe_gold_name", LangTk, "Altyn gazma");
            Set("pickaxe_gold_name", LangUk, "Золота кирка");
            Set("pickaxe_gold_name", LangUz, "Oltin ketmon");
            Set("pickaxe_gold_name", LangVi, "Cuốc vàng");
            Set("pickaxe_gold_name", LangZh, "金镐");

            Set("pickaxe_diamond_name", LangAr, "فأس ألماسي");
            Set("pickaxe_diamond_name", LangAz, "Almaz Qazma");
            Set("pickaxe_diamond_name", LangBe, "Алмазная кірка");
            Set("pickaxe_diamond_name", LangBg, "Диамантена кирка");
            Set("pickaxe_diamond_name", LangCa, "Pic de diamant");
            Set("pickaxe_diamond_name", LangCs, "Diamantový krumpáč");
            Set("pickaxe_diamond_name", LangDe, "Diamantspitzhacke");
            Set("pickaxe_diamond_name", LangEs, "Pico de diamante");
            Set("pickaxe_diamond_name", LangFa, "کلنگ الماسی");
            Set("pickaxe_diamond_name", LangFr, "Pioche en diamant");
            Set("pickaxe_diamond_name", LangHe, "מכוש יהלום");
            Set("pickaxe_diamond_name", LangHi, "हीरे की कुदाल");
            Set("pickaxe_diamond_name", LangHu, "Gyémántcsákány");
            Set("pickaxe_diamond_name", LangHy, "Ադամանդե քլունգ");
            Set("pickaxe_diamond_name", LangId, "Pickaxe Berlian");
            Set("pickaxe_diamond_name", LangIt, "Piccone di diamante");
            Set("pickaxe_diamond_name", LangJa, "ダイヤのツルハシ");
            Set("pickaxe_diamond_name", LangKa, "ბრილიანტის მწიკვი");
            Set("pickaxe_diamond_name", LangKk, "Алмаз кірке");
            Set("pickaxe_diamond_name", LangNl, "Diamanten houweel");
            Set("pickaxe_diamond_name", LangPl, "Diamentowy kilof");
            Set("pickaxe_diamond_name", LangPt, "Picareta de diamante");
            Set("pickaxe_diamond_name", LangRo, "Târnăcop de diamant");
            Set("pickaxe_diamond_name", LangSk, "Diamantový krompáč");
            Set("pickaxe_diamond_name", LangSr, "Dijamantski kramp");
            Set("pickaxe_diamond_name", LangTh, "พลั่วเพชร");
            Set("pickaxe_diamond_name", LangTk, "Göwher gazma");
            Set("pickaxe_diamond_name", LangUk, "Алмазна кирка");
            Set("pickaxe_diamond_name", LangUz, "Olmos ketmon");
            Set("pickaxe_diamond_name", LangVi, "Cuốc kim cương");
            Set("pickaxe_diamond_name", LangZh, "钻石镐");

            Set("mine_bronze_desc", LangAr, "صغيرة، معظمها تراب وحجر.");
            Set("mine_bronze_desc", LangAz, "Kiçikdir, əsasən torpaq və daşdır.");
            Set("mine_bronze_desc", LangBe, "Невялікая, пераважна зямля і камень.");
            Set("mine_bronze_desc", LangBg, "Малка, предимно пръст и камък.");
            Set("mine_bronze_desc", LangCa, "Petita, sobretot terra i pedra.");
            Set("mine_bronze_desc", LangCs, "Malý důl, hlavně hlína a kámen.");
            Set("mine_bronze_desc", LangDe, "Klein, hauptsächlich Erde und Stein.");
            Set("mine_bronze_desc", LangEs, "Pequeña, principalmente tierra y piedra.");
            Set("mine_bronze_desc", LangFa, "کوچک، بیشتر خاک و سنگ.");
            Set("mine_bronze_desc", LangFr, "Petite, surtout de la terre et de la pierre.");
            Set("mine_bronze_desc", LangHe, "קטן, בעיקר עפר ואבן.");
            Set("mine_bronze_desc", LangHi, "छोटी, ज्यादातर मिट्टी और पत्थर।");
            Set("mine_bronze_desc", LangHu, "Kicsi, főleg föld és kő.");
            Set("mine_bronze_desc", LangHy, "Փոքր, հիմնականում հող և քար:");
            Set("mine_bronze_desc", LangId, "Kecil, kebanyakan tanah dan batu.");
            Set("mine_bronze_desc", LangIt, "Piccola, soprattutto terra e pietra.");
            Set("mine_bronze_desc", LangJa, "小規模で、ほとんどが土と石。");
            Set("mine_bronze_desc", LangKa, "პატარა, ძირითადად მიწა და ქვა.");
            Set("mine_bronze_desc", LangKk, "Кішкентай, негізінен топырақ пен тас.");
            Set("mine_bronze_desc", LangNl, "Klein, vooral aarde en steen.");
            Set("mine_bronze_desc", LangPl, "Mała, głównie ziemia i kamień.");
            Set("mine_bronze_desc", LangPt, "Pequena, principalmente terra e pedra.");
            Set("mine_bronze_desc", LangRo, "Mică, în mare parte pământ și piatră.");
            Set("mine_bronze_desc", LangSk, "Malá, prevažne hlina a kameň.");
            Set("mine_bronze_desc", LangSr, "Mali rudnik, uglavnom zemlja i kamen.");
            Set("mine_bronze_desc", LangTh, "เหมืองเล็ก ส่วนใหญ่เป็นดินและหิน");
            Set("mine_bronze_desc", LangTk, "Kiçi, esasan toprak we daş.");
            Set("mine_bronze_desc", LangUk, "Невелика, переважно земля і камінь.");
            Set("mine_bronze_desc", LangUz, "Kichik, asosan tuproq va tosh.");
            Set("mine_bronze_desc", LangVi, "Mỏ nhỏ, chủ yếu là đất và đá.");
            Set("mine_bronze_desc", LangZh, "小型矿井，主要是泥土和石头。");

            Set("mine_silver_desc", LangAr, "متوسطة. يوجد الحديد وبعض الذهب في الأعماق.");
            Set("mine_silver_desc", LangAz, "Orta. Dərinlikdə dəmir və bir az qızıl var.");
            Set("mine_silver_desc", LangBe, "Сярэдняя. На глыбіні ёсць жалеза і крыху золата.");
            Set("mine_silver_desc", LangBg, "Средна. Има желязо и малко злато в дълбочина.");
            Set("mine_silver_desc", LangCa, "Mitjana. Ferro i una mica d'or en profunditat.");
            Set("mine_silver_desc", LangCs, "Střední. V hloubce železo a trochu zlata.");
            Set("mine_silver_desc", LangDe, "Mittelgroß. Eisen und etwas Gold in der Tiefe.");
            Set("mine_silver_desc", LangEs, "Mediana. Hierro y algo de oro en profundidad.");
            Set("mine_silver_desc", LangFa, "متوسط. در عمق آهن و کمی طلا دارد.");
            Set("mine_silver_desc", LangFr, "Moyenne. Du fer et un peu d'or en profondeur.");
            Set("mine_silver_desc", LangHe, "בינונית. ברזל וקצת זהב בעומק.");
            Set("mine_silver_desc", LangHi, "मध्यम। गहराई में लोहा और थोड़ा सोना।");
            Set("mine_silver_desc", LangHu, "Közepes. Mélyebben vas és egy kis arany.");
            Set("mine_silver_desc", LangHy, "Միջին. Խորքում երկաթ և մի քիչ ոսկի:");
            Set("mine_silver_desc", LangId, "Sedang. Besi dan sedikit emas di kedalaman.");
            Set("mine_silver_desc", LangIt, "Media. Ferro e un po' d'oro in profondità.");
            Set("mine_silver_desc", LangJa, "中規模。深部に鉄と少量の金。");
            Set("mine_silver_desc", LangKa, "საშუალო. სიღრმეში რკინა და ცოტა ოქროა.");
            Set("mine_silver_desc", LangKk, "Орташа. Тереңде темір мен аздап алтын бар.");
            Set("mine_silver_desc", LangNl, "Middelgroot. IJzer en wat goud op diepte.");
            Set("mine_silver_desc", LangPl, "Średnia. Żelazo i trochę złota na głębokości.");
            Set("mine_silver_desc", LangPt, "Média. Ferro e um pouco de ouro em profundidade.");
            Set("mine_silver_desc", LangRo, "Medie. Fier și puțin aur la adâncime.");
            Set("mine_silver_desc", LangSk, "Stredná. Železo a trochu zlata v hĺbke.");
            Set("mine_silver_desc", LangSr, "Srednji rudnik. Gvožđe i malo zlata u dubini.");
            Set("mine_silver_desc", LangTh, "เหมืองขนาดกลาง มีเหล็กและทองเล็กน้อยในชั้นลึก");
            Set("mine_silver_desc", LangTk, "Ortaça. Çuňlukda demir we biraz altyn.");
            Set("mine_silver_desc", LangUk, "Середня. Залізо і трохи золота на глибині.");
            Set("mine_silver_desc", LangUz, "O‘rtacha. Chuqurlikda temir va biroz oltin bor.");
            Set("mine_silver_desc", LangVi, "Mỏ vừa. Có sắt và một ít vàng ở sâu hơn.");
            Set("mine_silver_desc", LangZh, "中型矿井。深处有铁和少量黄金。");

            Set("mine_gold_desc", LangAr, "عميقة. الكثير من الحديد والذهب.");
            Set("mine_gold_desc", LangAz, "Dərin. Çoxlu dəmir və qızıl var.");
            Set("mine_gold_desc", LangBe, "Глыбокая. Шмат жалеза і золата.");
            Set("mine_gold_desc", LangBg, "Дълбока. Много желязо и злато.");
            Set("mine_gold_desc", LangCa, "Profunda. Molt ferro i or.");
            Set("mine_gold_desc", LangCs, "Hluboký důl. Spousta železa a zlata.");
            Set("mine_gold_desc", LangDe, "Tief. Viel Eisen und Gold.");
            Set("mine_gold_desc", LangEs, "Profunda. Mucho hierro y oro.");
            Set("mine_gold_desc", LangFa, "عمیق. آهن و طلای فراوان.");
            Set("mine_gold_desc", LangFr, "Profonde. Beaucoup de fer et d'or.");
            Set("mine_gold_desc", LangHe, "עמוקה. הרבה ברזל וזהב.");
            Set("mine_gold_desc", LangHi, "गहरी। बहुत सारा लोहा और सोना।");
            Set("mine_gold_desc", LangHu, "Mély. Sok vas és arany.");
            Set("mine_gold_desc", LangHy, "Խոր. Շատ երկաթ և ոսկի:");
            Set("mine_gold_desc", LangId, "Dalam. Banyak besi dan emas.");
            Set("mine_gold_desc", LangIt, "Profonda. Molto ferro e oro.");
            Set("mine_gold_desc", LangJa, "深い。鉄と金が豊富。");
            Set("mine_gold_desc", LangKa, "ღრმა. ბევრი რკინა და ოქრო.");
            Set("mine_gold_desc", LangKk, "Терең. Темір мен алтын көп.");
            Set("mine_gold_desc", LangNl, "Diep. Veel ijzer en goud.");
            Set("mine_gold_desc", LangPl, "Głęboka. Dużo żelaza i złota.");
            Set("mine_gold_desc", LangPt, "Profunda. Muito ferro e ouro.");
            Set("mine_gold_desc", LangRo, "Adâncă. Mult fier și aur.");
            Set("mine_gold_desc", LangSk, "Hlboká. Veľa železa a zlata.");
            Set("mine_gold_desc", LangSr, "Dubok rudnik. Mnogo gvožđa i zlata.");
            Set("mine_gold_desc", LangTh, "เหมืองลึก มีเหล็กและทองจำนวนมาก");
            Set("mine_gold_desc", LangTk, "Çuň. Köp demir we altyn.");
            Set("mine_gold_desc", LangUk, "Глибока. Багато заліза і золота.");
            Set("mine_gold_desc", LangUz, "Chuqur. Ko‘p temir va oltin.");
            Set("mine_gold_desc", LangVi, "Mỏ sâu. Rất nhiều sắt và vàng.");
            Set("mine_gold_desc", LangZh, "深层矿井。富含大量铁和黄金。");

            Set("pickaxe_stone_desc", LangAr, "أسرع من الافتراضية.");
            Set("pickaxe_stone_desc", LangAz, "Standartdan daha sürətlidir.");
            Set("pickaxe_stone_desc", LangBe, "Хутчэйшая за стандартную.");
            Set("pickaxe_stone_desc", LangBg, "По-бърза от стандартната.");
            Set("pickaxe_stone_desc", LangCa, "Més ràpid que l'estàndard.");
            Set("pickaxe_stone_desc", LangCs, "Rychlejší než základní.");
            Set("pickaxe_stone_desc", LangDe, "Schneller als die Standardhacke.");
            Set("pickaxe_stone_desc", LangEs, "Más rápida que la básica.");
            Set("pickaxe_stone_desc", LangFa, "از ابزار پیش‌فرض سریع‌تر است.");
            Set("pickaxe_stone_desc", LangFr, "Plus rapide que l'outil de base.");
            Set("pickaxe_stone_desc", LangHe, "מהיר יותר מהרגיל.");
            Set("pickaxe_stone_desc", LangHi, "डिफ़ॉल्ट से तेज।");
            Set("pickaxe_stone_desc", LangHu, "Gyorsabb az alapnál.");
            Set("pickaxe_stone_desc", LangHy, "Ավելի արագ է, քան սովորականը:");
            Set("pickaxe_stone_desc", LangId, "Lebih cepat dari yang standar.");
            Set("pickaxe_stone_desc", LangIt, "Più veloce di quella base.");
            Set("pickaxe_stone_desc", LangJa, "標準より速い。");
            Set("pickaxe_stone_desc", LangKa, "სტანდარტულზე სწრაფია.");
            Set("pickaxe_stone_desc", LangKk, "Қалыптысынан жылдамырақ.");
            Set("pickaxe_stone_desc", LangNl, "Sneller dan de standaard.");
            Set("pickaxe_stone_desc", LangPl, "Szybszy niż podstawowy.");
            Set("pickaxe_stone_desc", LangPt, "Mais rápida que a padrão.");
            Set("pickaxe_stone_desc", LangRo, "Mai rapid decât cea standard.");
            Set("pickaxe_stone_desc", LangSk, "Rýchlejší než základný.");
            Set("pickaxe_stone_desc", LangSr, "Brži od početnog krampa.");
            Set("pickaxe_stone_desc", LangTh, "เร็วกว่าอุปกรณ์เริ่มต้น");
            Set("pickaxe_stone_desc", LangTk, "Başlangyç gazmadan tizräk.");
            Set("pickaxe_stone_desc", LangUk, "Швидша за стандартну.");
            Set("pickaxe_stone_desc", LangUz, "Standartdan tezroq.");
            Set("pickaxe_stone_desc", LangVi, "Nhanh hơn cuốc mặc định.");
            Set("pickaxe_stone_desc", LangZh, "比默认镐更快。");

            Set("pickaxe_iron_desc", LangAr, "ترقية قوية للتعدين.");
            Set("pickaxe_iron_desc", LangAz, "Qazma üçün yaxşı təkmilləşdirmədir.");
            Set("pickaxe_iron_desc", LangBe, "Добрае паляпшэнне для здабычы.");
            Set("pickaxe_iron_desc", LangBg, "Добро подобрение за копаене.");
            Set("pickaxe_iron_desc", LangCa, "Una bona millora per minar.");
            Set("pickaxe_iron_desc", LangCs, "Solidní vylepšení pro těžbu.");
            Set("pickaxe_iron_desc", LangDe, "Solides Upgrade für den Abbau.");
            Set("pickaxe_iron_desc", LangEs, "Una mejora sólida para minar.");
            Set("pickaxe_iron_desc", LangFa, "ارتقای خوبی برای استخراج است.");
            Set("pickaxe_iron_desc", LangFr, "Une amélioration solide pour miner.");
            Set("pickaxe_iron_desc", LangHe, "שדרוג טוב לכרייה.");
            Set("pickaxe_iron_desc", LangHi, "खनन के लिए मजबूत अपग्रेड।");
            Set("pickaxe_iron_desc", LangHu, "Jó fejlesztés a bányászathoz.");
            Set("pickaxe_iron_desc", LangHy, "Լավ արդիականացում հանքագործության համար:");
            Set("pickaxe_iron_desc", LangId, "Upgrade yang bagus untuk menambang.");
            Set("pickaxe_iron_desc", LangIt, "Ottimo miglioramento per scavare.");
            Set("pickaxe_iron_desc", LangJa, "採掘に最適な堅実な強化。");
            Set("pickaxe_iron_desc", LangKa, "მყარ გაუმჯობესებას იძლევა მოპოვებისთვის.");
            Set("pickaxe_iron_desc", LangKk, "Кен қазуға жақсы жаңарту.");
            Set("pickaxe_iron_desc", LangNl, "Stevige upgrade voor mijnen.");
            Set("pickaxe_iron_desc", LangPl, "Solidne ulepszenie do kopania.");
            Set("pickaxe_iron_desc", LangPt, "Boa melhoria para mineração.");
            Set("pickaxe_iron_desc", LangRo, "Un upgrade solid pentru minat.");
            Set("pickaxe_iron_desc", LangSk, "Pevné vylepšenie na ťažbu.");
            Set("pickaxe_iron_desc", LangSr, "Solidna nadogradnja za rudarenje.");
            Set("pickaxe_iron_desc", LangTh, "อัปเกรดที่ดีสำหรับการขุด");
            Set("pickaxe_iron_desc", LangTk, "Gazmak üçin gowy gowulandyrma.");
            Set("pickaxe_iron_desc", LangUk, "Хороше покращення для видобутку.");
            Set("pickaxe_iron_desc", LangUz, "Qazish uchun yaxshi yangilanish.");
            Set("pickaxe_iron_desc", LangVi, "Nâng cấp tốt cho việc đào mỏ.");
            Set("pickaxe_iron_desc", LangZh, "适合采矿的扎实升级。");

            Set("pickaxe_gold_desc", LangAr, "سريعة جداً لكنها باهظة.");
            Set("pickaxe_gold_desc", LangAz, "Çox sürətlidir, amma bahalıdır.");
            Set("pickaxe_gold_desc", LangBe, "Вельмі хуткая, але дарагая.");
            Set("pickaxe_gold_desc", LangBg, "Много бърза, но скъпа.");
            Set("pickaxe_gold_desc", LangCa, "Molt ràpida però cara.");
            Set("pickaxe_gold_desc", LangCs, "Velmi rychlý, ale drahý.");
            Set("pickaxe_gold_desc", LangDe, "Sehr schnell, aber teuer.");
            Set("pickaxe_gold_desc", LangEs, "Muy rápida, pero cara.");
            Set("pickaxe_gold_desc", LangFa, "خیلی سریع اما گران است.");
            Set("pickaxe_gold_desc", LangFr, "Très rapide mais chère.");
            Set("pickaxe_gold_desc", LangHe, "מהירה מאוד אבל יקרה.");
            Set("pickaxe_gold_desc", LangHi, "बहुत तेज, लेकिन महंगी।");
            Set("pickaxe_gold_desc", LangHu, "Nagyon gyors, de drága.");
            Set("pickaxe_gold_desc", LangHy, "Շատ արագ է, բայց թանկ:");
            Set("pickaxe_gold_desc", LangId, "Sangat cepat, tapi mahal.");
            Set("pickaxe_gold_desc", LangIt, "Molto veloce, ma costosa.");
            Set("pickaxe_gold_desc", LangJa, "非常に速いが高価。");
            Set("pickaxe_gold_desc", LangKa, "ძალიან სწრაფია, მაგრამ ძვირია.");
            Set("pickaxe_gold_desc", LangKk, "Өте жылдам, бірақ қымбат.");
            Set("pickaxe_gold_desc", LangNl, "Erg snel, maar duur.");
            Set("pickaxe_gold_desc", LangPl, "Bardzo szybki, ale drogi.");
            Set("pickaxe_gold_desc", LangPt, "Muito rápida, mas cara.");
            Set("pickaxe_gold_desc", LangRo, "Foarte rapid, dar scump.");
            Set("pickaxe_gold_desc", LangSk, "Veľmi rýchly, ale drahý.");
            Set("pickaxe_gold_desc", LangSr, "Veoma brz, ali skup.");
            Set("pickaxe_gold_desc", LangTh, "เร็วมาก แต่ราคาแพง");
            Set("pickaxe_gold_desc", LangTk, "Örän çalt, ýöne gymmat.");
            Set("pickaxe_gold_desc", LangUk, "Дуже швидка, але дорога.");
            Set("pickaxe_gold_desc", LangUz, "Juda tez, lekin qimmat.");
            Set("pickaxe_gold_desc", LangVi, "Rất nhanh nhưng đắt.");
            Set("pickaxe_gold_desc", LangZh, "非常快，但价格昂贵。");

            Set("pickaxe_diamond_desc", LangAr, "فأس من الفئة العليا.");
            Set("pickaxe_diamond_desc", LangAz, "Ən yüksək səviyyəli qazmadır.");
            Set("pickaxe_diamond_desc", LangBe, "Кірка найвышэйшага ўзроўню.");
            Set("pickaxe_diamond_desc", LangBg, "Кирка от най-висок клас.");
            Set("pickaxe_diamond_desc", LangCa, "Pic de nivell superior.");
            Set("pickaxe_diamond_desc", LangCs, "Krumpáč nejvyšší úrovně.");
            Set("pickaxe_diamond_desc", LangDe, "Spitzhacke der Spitzenklasse.");
            Set("pickaxe_diamond_desc", LangEs, "Pico de primera categoría.");
            Set("pickaxe_diamond_desc", LangFa, "کلنگ درجه‌یک.");
            Set("pickaxe_diamond_desc", LangFr, "Pioche de tout premier rang.");
            Set("pickaxe_diamond_desc", LangHe, "מכוש ברמה הגבוהה ביותר.");
            Set("pickaxe_diamond_desc", LangHi, "उच्च स्तर की कुदाल।");
            Set("pickaxe_diamond_desc", LangHu, "Csúcskategóriás csákány.");
            Set("pickaxe_diamond_desc", LangHy, "Բարձրագույն դասի քլունգ:");
            Set("pickaxe_diamond_desc", LangId, "Pickaxe tingkat tertinggi.");
            Set("pickaxe_diamond_desc", LangIt, "Piccone di altissimo livello.");
            Set("pickaxe_diamond_desc", LangJa, "最上級のツルハシ。");
            Set("pickaxe_diamond_desc", LangKa, "უმაღლესი დონის მწიკვი.");
            Set("pickaxe_diamond_desc", LangKk, "Ең жоғарғы деңгейлі кірке.");
            Set("pickaxe_diamond_desc", LangNl, "Houweel van topniveau.");
            Set("pickaxe_diamond_desc", LangPl, "Kilof najwyższego poziomu.");
            Set("pickaxe_diamond_desc", LangPt, "Picareta de nível máximo.");
            Set("pickaxe_diamond_desc", LangRo, "Târnăcop de top.");
            Set("pickaxe_diamond_desc", LangSk, "Krompáč najvyššej úrovne.");
            Set("pickaxe_diamond_desc", LangSr, "Kramp najvišeg nivoa.");
            Set("pickaxe_diamond_desc", LangTh, "พลั่วระดับสูงสุด");
            Set("pickaxe_diamond_desc", LangTk, "Iň ýokary derejeli gazma.");
            Set("pickaxe_diamond_desc", LangUk, "Кирка найвищого рівня.");
            Set("pickaxe_diamond_desc", LangUz, "Eng yuqori darajadagi ketmon.");
            Set("pickaxe_diamond_desc", LangVi, "Cuốc cấp cao nhất.");
            Set("pickaxe_diamond_desc", LangZh, "顶级镐子。");

            Set("balance_header", LangAr, "الرصيد: ${0}  |  [B]/[X] إغلاق");
            Set("balance_header", LangAz, "Balans: ${0}  |  [B]/[X] bağla");
            Set("balance_header", LangBe, "Баланс: ${0}  |  [B]/[X] закрыць");
            Set("balance_header", LangBg, "Баланс: ${0}  |  [B]/[X] затвори");
            Set("balance_header", LangCa, "Saldo: ${0}  |  [B]/[X] tanca");
            Set("balance_header", LangCs, "Zůstatek: ${0}  |  [B]/[X] zavřít");
            Set("balance_header", LangDe, "Guthaben: ${0}  |  [B]/[X] schließen");
            Set("balance_header", LangEs, "Saldo: ${0}  |  [B]/[X] cerrar");
            Set("balance_header", LangFa, "موجودی: ${0}  |  [B]/[X] بستن");
            Set("balance_header", LangFr, "Solde: ${0}  |  [B]/[X] fermer");
            Set("balance_header", LangHe, "יתרה: ${0}  |  [B]/[X] סגור");
            Set("balance_header", LangHi, "बैलेंस: ${0}  |  [B]/[X] बंद");
            Set("balance_header", LangHu, "Egyenleg: ${0}  |  [B]/[X] bezár");
            Set("balance_header", LangHy, "Մնացորդ: ${0}  |  [B]/[X] փակել");
            Set("balance_header", LangId, "Saldo: ${0}  |  [B]/[X] tutup");
            Set("balance_header", LangIt, "Saldo: ${0}  |  [B]/[X] chiudi");
            Set("balance_header", LangJa, "残高: ${0}  |  [B]/[X] 閉じる");
            Set("balance_header", LangKa, "ბალანსი: ${0}  |  [B]/[X] დახურვა");
            Set("balance_header", LangKk, "Баланс: ${0}  |  [B]/[X] жабу");
            Set("balance_header", LangNl, "Saldo: ${0}  |  [B]/[X] sluiten");
            Set("balance_header", LangPl, "Saldo: ${0}  |  [B]/[X] zamknij");
            Set("balance_header", LangPt, "Saldo: ${0}  |  [B]/[X] fechar");
            Set("balance_header", LangRo, "Sold: ${0}  |  [B]/[X] închide");
            Set("balance_header", LangSk, "Zostatok: ${0}  |  [B]/[X] zatvoriť");
            Set("balance_header", LangSr, "Balans: ${0}  |  [B]/[X] zatvori");
            Set("balance_header", LangTh, "ยอดเงิน: ${0}  |  [B]/[X] ปิด");
            Set("balance_header", LangTk, "Balans: ${0}  |  [B]/[X] ýap");
            Set("balance_header", LangUk, "Баланс: ${0}  |  [B]/[X] закрити");
            Set("balance_header", LangUz, "Balans: ${0}  |  [B]/[X] yopish");
            Set("balance_header", LangVi, "Số dư: ${0}  |  [B]/[X] đóng");
            Set("balance_header", LangZh, "余额: ${0}  |  [B]/[X] 关闭");

            Set("upgrade_str_title", LangAr, "القوة");
            Set("upgrade_str_title", LangAz, "GÜC");
            Set("upgrade_str_title", LangBe, "СІЛА");
            Set("upgrade_str_title", LangBg, "СИЛА");
            Set("upgrade_str_title", LangCa, "FORÇA");
            Set("upgrade_str_title", LangCs, "SÍLA");
            Set("upgrade_str_title", LangDe, "KRAFT");
            Set("upgrade_str_title", LangEs, "FUERZA");
            Set("upgrade_str_title", LangFa, "قدرت");
            Set("upgrade_str_title", LangFr, "FORCE");
            Set("upgrade_str_title", LangHe, "כוח");
            Set("upgrade_str_title", LangHi, "शक्ति");
            Set("upgrade_str_title", LangHu, "ERŐ");
            Set("upgrade_str_title", LangHy, "ՈՒԺ");
            Set("upgrade_str_title", LangId, "DAYA");
            Set("upgrade_str_title", LangIt, "FORZA");
            Set("upgrade_str_title", LangJa, "パワー");
            Set("upgrade_str_title", LangKa, "ძალა");
            Set("upgrade_str_title", LangKk, "КҮШ");
            Set("upgrade_str_title", LangNl, "KRACHT");
            Set("upgrade_str_title", LangPl, "MOC");
            Set("upgrade_str_title", LangPt, "FORÇA");
            Set("upgrade_str_title", LangRo, "PUTERE");
            Set("upgrade_str_title", LangSk, "SILA");
            Set("upgrade_str_title", LangSr, "SNAGA");
            Set("upgrade_str_title", LangTh, "พลัง");
            Set("upgrade_str_title", LangTk, "GÜÝÇ");
            Set("upgrade_str_title", LangUk, "СИЛА");
            Set("upgrade_str_title", LangUz, "KUCH");
            Set("upgrade_str_title", LangVi, "SỨC MẠNH");
            Set("upgrade_str_title", LangZh, "力量");

            Set("upgrade_bp_title", LangAr, "الحقيبة");
            Set("upgrade_bp_title", LangAz, "ÇANTA");
            Set("upgrade_bp_title", LangBe, "РУКЗАК");
            Set("upgrade_bp_title", LangBg, "РАНИЦА");
            Set("upgrade_bp_title", LangCa, "MOTXILLA");
            Set("upgrade_bp_title", LangCs, "BATOH");
            Set("upgrade_bp_title", LangDe, "RUCKSACK");
            Set("upgrade_bp_title", LangEs, "MOCHILA");
            Set("upgrade_bp_title", LangFa, "کوله‌پشتی");
            Set("upgrade_bp_title", LangFr, "SAC À DOS");
            Set("upgrade_bp_title", LangHe, "תרמיל");
            Set("upgrade_bp_title", LangHi, "बैग");
            Set("upgrade_bp_title", LangHu, "HÁTIZSÁK");
            Set("upgrade_bp_title", LangHy, "ՊԱՅՈՒՍԱԿ");
            Set("upgrade_bp_title", LangId, "RANSEL");
            Set("upgrade_bp_title", LangIt, "ZAINO");
            Set("upgrade_bp_title", LangJa, "バックパック");
            Set("upgrade_bp_title", LangKa, "ზურგჩანთა");
            Set("upgrade_bp_title", LangKk, "РЮКЗАК");
            Set("upgrade_bp_title", LangNl, "RUGZAK");
            Set("upgrade_bp_title", LangPl, "PLECAK");
            Set("upgrade_bp_title", LangPt, "MOCHILA");
            Set("upgrade_bp_title", LangRo, "RUCSAC");
            Set("upgrade_bp_title", LangSk, "BATOH");
            Set("upgrade_bp_title", LangSr, "RANAC");
            Set("upgrade_bp_title", LangTh, "กระเป๋า");
            Set("upgrade_bp_title", LangTk, "SUMKA");
            Set("upgrade_bp_title", LangUk, "РЮКЗАК");
            Set("upgrade_bp_title", LangUz, "RYUKZAK");
            Set("upgrade_bp_title", LangVi, "BA LÔ");
            Set("upgrade_bp_title", LangZh, "背包");

            Set("upgrade_str_subtitle", LangAr, "ضرر الكتل");
            Set("upgrade_str_subtitle", LangAz, "Blok zərəri");
            Set("upgrade_str_subtitle", LangBe, "Урон па блоках");
            Set("upgrade_str_subtitle", LangBg, "Щети по блокове");
            Set("upgrade_str_subtitle", LangCa, "Dany als blocs");
            Set("upgrade_str_subtitle", LangCs, "Poškození bloků");
            Set("upgrade_str_subtitle", LangDe, "Blockschaden");
            Set("upgrade_str_subtitle", LangEs, "Daño a bloques");
            Set("upgrade_str_subtitle", LangFa, "آسیب به بلوک‌ها");
            Set("upgrade_str_subtitle", LangFr, "Dégâts aux blocs");
            Set("upgrade_str_subtitle", LangHe, "נזק לבלוקים");
            Set("upgrade_str_subtitle", LangHi, "ब्लॉक क्षति");
            Set("upgrade_str_subtitle", LangHu, "Blokksebzés");
            Set("upgrade_str_subtitle", LangHy, "Բլոկների վնաս");
            Set("upgrade_str_subtitle", LangId, "Damage blok");
            Set("upgrade_str_subtitle", LangIt, "Danno ai blocchi");
            Set("upgrade_str_subtitle", LangJa, "ブロックダメージ");
            Set("upgrade_str_subtitle", LangKa, "ბლოკის დაზიანება");
            Set("upgrade_str_subtitle", LangKk, "Блок зақымы");
            Set("upgrade_str_subtitle", LangNl, "Blokschade");
            Set("upgrade_str_subtitle", LangPl, "Obrażenia bloków");
            Set("upgrade_str_subtitle", LangPt, "Dano aos blocos");
            Set("upgrade_str_subtitle", LangRo, "Daune blocuri");
            Set("upgrade_str_subtitle", LangSk, "Poškodenie blokov");
            Set("upgrade_str_subtitle", LangSr, "Šteta blokovima");
            Set("upgrade_str_subtitle", LangTh, "ความเสียหายต่อบล็อก");
            Set("upgrade_str_subtitle", LangTk, "Blok zeperi");
            Set("upgrade_str_subtitle", LangUk, "Шкода по блоках");
            Set("upgrade_str_subtitle", LangUz, "Blok zarari");
            Set("upgrade_str_subtitle", LangVi, "Sát thương khối");
            Set("upgrade_str_subtitle", LangZh, "方块伤害");

            Set("upgrade_bp_subtitle", LangAr, "سعة الخام");
            Set("upgrade_bp_subtitle", LangAz, "Filiz tutumu");
            Set("upgrade_bp_subtitle", LangBe, "Ліміт руды");
            Set("upgrade_bp_subtitle", LangBg, "Капацитет за руда");
            Set("upgrade_bp_subtitle", LangCa, "Capacitat de mineral");
            Set("upgrade_bp_subtitle", LangCs, "Kapacita rudy");
            Set("upgrade_bp_subtitle", LangDe, "Erzkapazität");
            Set("upgrade_bp_subtitle", LangEs, "Capacidad de mineral");
            Set("upgrade_bp_subtitle", LangFa, "ظرفیت سنگ معدن");
            Set("upgrade_bp_subtitle", LangFr, "Capacité de minerai");
            Set("upgrade_bp_subtitle", LangHe, "קיבולת עפרה");
            Set("upgrade_bp_subtitle", LangHi, "अयस्क क्षमता");
            Set("upgrade_bp_subtitle", LangHu, "Érckapacitás");
            Set("upgrade_bp_subtitle", LangHy, "Հանքաքարի տարողություն");
            Set("upgrade_bp_subtitle", LangId, "Kapasitas bijih");
            Set("upgrade_bp_subtitle", LangIt, "Capacità minerale");
            Set("upgrade_bp_subtitle", LangJa, "鉱石容量");
            Set("upgrade_bp_subtitle", LangKa, "მადნის ტევადობა");
            Set("upgrade_bp_subtitle", LangKk, "Кен сыйымдылығы");
            Set("upgrade_bp_subtitle", LangNl, "Ertscapaciteit");
            Set("upgrade_bp_subtitle", LangPl, "Pojemność rudy");
            Set("upgrade_bp_subtitle", LangPt, "Capacidade de minério");
            Set("upgrade_bp_subtitle", LangRo, "Capacitate minereu");
            Set("upgrade_bp_subtitle", LangSk, "Kapacita rudy");
            Set("upgrade_bp_subtitle", LangSr, "Kapacitet rude");
            Set("upgrade_bp_subtitle", LangTh, "ความจุแร่");
            Set("upgrade_bp_subtitle", LangTk, "Magdan sygymy");
            Set("upgrade_bp_subtitle", LangUk, "Місткість руди");
            Set("upgrade_bp_subtitle", LangUz, "Ruda sig‘imi");
            Set("upgrade_bp_subtitle", LangVi, "Sức chứa quặng");
            Set("upgrade_bp_subtitle", LangZh, "矿石容量");

            Set("upgrade_btn_format", LangAr, "ترقية: ${0}");
            Set("upgrade_btn_format", LangAz, "TƏKMİL ET: ${0}");
            Set("upgrade_btn_format", LangBe, "ПАЛЕПШЫЦЬ: ${0}");
            Set("upgrade_btn_format", LangBg, "ПОДОБРИ: ${0}");
            Set("upgrade_btn_format", LangCa, "MILLORA: ${0}");
            Set("upgrade_btn_format", LangCs, "VYLEPŠIT: ${0}");
            Set("upgrade_btn_format", LangDe, "UPGRADE: ${0}");
            Set("upgrade_btn_format", LangEs, "MEJORAR: ${0}");
            Set("upgrade_btn_format", LangFa, "ارتقا: ${0}");
            Set("upgrade_btn_format", LangFr, "AMÉLIORER: ${0}");
            Set("upgrade_btn_format", LangHe, "שדרג: ${0}");
            Set("upgrade_btn_format", LangHi, "अपग्रेड: ${0}");
            Set("upgrade_btn_format", LangHu, "FEJLESZTÉS: ${0}");
            Set("upgrade_btn_format", LangHy, "ԹԱՐՄԱՑՈՒՄ: ${0}");
            Set("upgrade_btn_format", LangId, "UPGRADE: ${0}");
            Set("upgrade_btn_format", LangIt, "MIGLIORA: ${0}");
            Set("upgrade_btn_format", LangJa, "強化: ${0}");
            Set("upgrade_btn_format", LangKa, "გაუმჯობესება: ${0}");
            Set("upgrade_btn_format", LangKk, "ЖАҚСАРТУ: ${0}");
            Set("upgrade_btn_format", LangNl, "UPGRADE: ${0}");
            Set("upgrade_btn_format", LangPl, "ULEPSZ: ${0}");
            Set("upgrade_btn_format", LangPt, "MELHORAR: ${0}");
            Set("upgrade_btn_format", LangRo, "UPGRADE: ${0}");
            Set("upgrade_btn_format", LangSk, "VYLEPŠIŤ: ${0}");
            Set("upgrade_btn_format", LangSr, "NADOGRADI: ${0}");
            Set("upgrade_btn_format", LangTh, "อัปเกรด: ${0}");
            Set("upgrade_btn_format", LangTk, "GOWULAŞDYR: ${0}");
            Set("upgrade_btn_format", LangUk, "ПОКРАЩИТИ: ${0}");
            Set("upgrade_btn_format", LangUz, "YAXSHILASH: ${0}");
            Set("upgrade_btn_format", LangVi, "NÂNG CẤP: ${0}");
            Set("upgrade_btn_format", LangZh, "升级: ${0}");

            Set("mining_level_label", LangAr, "مستوى التعدين");
            Set("mining_level_label", LangAz, "Qazma səviyyəsi");
            Set("mining_level_label", LangBe, "Узровень капання");
            Set("mining_level_label", LangBg, "Ниво на копаене");
            Set("mining_level_label", LangCa, "Nivell de mineria");
            Set("mining_level_label", LangCs, "Úroveň těžby");
            Set("mining_level_label", LangDe, "Abbaustufe");
            Set("mining_level_label", LangEs, "Nivel de minería");
            Set("mining_level_label", LangFa, "سطح معدن‌کاری");
            Set("mining_level_label", LangFr, "Niveau de minage");
            Set("mining_level_label", LangHe, "רמת כרייה");
            Set("mining_level_label", LangHi, "खनन स्तर");
            Set("mining_level_label", LangHu, "Bányász szint");
            Set("mining_level_label", LangHy, "Հանքափորման մակարդակ");
            Set("mining_level_label", LangId, "Level menambang");
            Set("mining_level_label", LangIt, "Livello di scavo");
            Set("mining_level_label", LangJa, "採掘レベル");
            Set("mining_level_label", LangKa, "მოპოვების დონე");
            Set("mining_level_label", LangKk, "Қазу деңгейі");
            Set("mining_level_label", LangNl, "Mijnniveau");
            Set("mining_level_label", LangPl, "Poziom kopania");
            Set("mining_level_label", LangPt, "Nível de mineração");
            Set("mining_level_label", LangRo, "Nivel de minerit");
            Set("mining_level_label", LangSk, "Úroveň ťažby");
            Set("mining_level_label", LangSr, "Nivo rudarenja");
            Set("mining_level_label", LangTh, "ระดับการขุด");
            Set("mining_level_label", LangTk, "Gazuw derejesi");
            Set("mining_level_label", LangUk, "Рівень копання");
            Set("mining_level_label", LangUz, "Qazish darajasi");
            Set("mining_level_label", LangVi, "Cấp khai thác");
            Set("mining_level_label", LangZh, "采矿等级");

            Set("lv_short", LangAr, "م.");
            Set("lv_short", LangAz, "Sv.");
            Set("lv_short", LangBe, "Узр.");
            Set("lv_short", LangBg, "Нв.");
            Set("lv_short", LangCa, "Nv.");
            Set("lv_short", LangCs, "Úr.");
            Set("lv_short", LangDe, "St.");
            Set("lv_short", LangEs, "Nv.");
            Set("lv_short", LangFa, "سط.");
            Set("lv_short", LangFr, "Nv.");
            Set("lv_short", LangHe, "רמ.");
            Set("lv_short", LangHi, "स्तर");
            Set("lv_short", LangHu, "Sz.");
            Set("lv_short", LangHy, "Մ.");
            Set("lv_short", LangId, "Lv.");
            Set("lv_short", LangIt, "Lv.");
            Set("lv_short", LangJa, "Lv.");
            Set("lv_short", LangKa, "დ.");
            Set("lv_short", LangKk, "Дең.");
            Set("lv_short", LangNl, "Lv.");
            Set("lv_short", LangPl, "Poz.");
            Set("lv_short", LangPt, "Nv.");
            Set("lv_short", LangRo, "Nv.");
            Set("lv_short", LangSk, "Úr.");
            Set("lv_short", LangSr, "Niv.");
            Set("lv_short", LangTh, "เลเวล");
            Set("lv_short", LangTk, "Lv.");
            Set("lv_short", LangUk, "Рів.");
            Set("lv_short", LangUz, "Lv.");
            Set("lv_short", LangVi, "Lv.");
            Set("lv_short", LangZh, "级");

            Set("xp_short", LangAr, "XP");
            Set("xp_short", LangAz, "XP");
            Set("xp_short", LangBe, "Воп.");
            Set("xp_short", LangBg, "XP");
            Set("xp_short", LangCa, "XP");
            Set("xp_short", LangCs, "XP");
            Set("xp_short", LangDe, "EP");
            Set("xp_short", LangEs, "XP");
            Set("xp_short", LangFa, "XP");
            Set("xp_short", LangFr, "XP");
            Set("xp_short", LangHe, "XP");
            Set("xp_short", LangHi, "XP");
            Set("xp_short", LangHu, "XP");
            Set("xp_short", LangHy, "XP");
            Set("xp_short", LangId, "XP");
            Set("xp_short", LangIt, "XP");
            Set("xp_short", LangJa, "XP");
            Set("xp_short", LangKa, "XP");
            Set("xp_short", LangKk, "XP");
            Set("xp_short", LangNl, "XP");
            Set("xp_short", LangPl, "XP");
            Set("xp_short", LangPt, "XP");
            Set("xp_short", LangRo, "XP");
            Set("xp_short", LangSk, "XP");
            Set("xp_short", LangSr, "XP");
            Set("xp_short", LangTh, "XP");
            Set("xp_short", LangTk, "XP");
            Set("xp_short", LangUk, "Досв.");
            Set("xp_short", LangUz, "XP");
            Set("xp_short", LangVi, "XP");
            Set("xp_short", LangZh, "经验");

            Set("mine_depth_format", LangAr, "🕳 العمق: {0}-{1} طبقات");
            Set("mine_depth_format", LangAz, "🕳 Dərinlik: {0}-{1} qat");
            Set("mine_depth_format", LangBe, "🕳 Глыбіня: {0}-{1} слаёў");
            Set("mine_depth_format", LangBg, "🕳 Дълбочина: {0}-{1} слоя");
            Set("mine_depth_format", LangCa, "🕳 Profunditat: {0}-{1} capes");
            Set("mine_depth_format", LangCs, "🕳 Hloubka: {0}-{1} vrstev");
            Set("mine_depth_format", LangDe, "🕳 Tiefe: {0}-{1} Schichten");
            Set("mine_depth_format", LangEs, "🕳 Profundidad: {0}-{1} capas");
            Set("mine_depth_format", LangFa, "🕳 عمق: {0}-{1} لایه");
            Set("mine_depth_format", LangFr, "🕳 Profondeur: {0}-{1} couches");
            Set("mine_depth_format", LangHe, "🕳 עומק: {0}-{1} שכבות");
            Set("mine_depth_format", LangHi, "🕳 गहराई: {0}-{1} परतें");
            Set("mine_depth_format", LangHu, "🕳 Mélység: {0}-{1} réteg");
            Set("mine_depth_format", LangHy, "🕳 Խորություն՝ {0}-{1} շերտ");
            Set("mine_depth_format", LangId, "🕳 Kedalaman: {0}-{1} lapisan");
            Set("mine_depth_format", LangIt, "🕳 Profondità: {0}-{1} strati");
            Set("mine_depth_format", LangJa, "🕳 深さ: {0}-{1} 層");
            Set("mine_depth_format", LangKa, "🕳 სიღრმე: {0}-{1} ფენა");
            Set("mine_depth_format", LangKk, "🕳 Тереңдік: {0}-{1} қабат");
            Set("mine_depth_format", LangNl, "🕳 Diepte: {0}-{1} lagen");
            Set("mine_depth_format", LangPl, "🕳 Głębokość: {0}-{1} warstw");
            Set("mine_depth_format", LangPt, "🕳 Profundidade: {0}-{1} camadas");
            Set("mine_depth_format", LangRo, "🕳 Adâncime: {0}-{1} straturi");
            Set("mine_depth_format", LangSk, "🕳 Hĺbka: {0}-{1} vrstiev");
            Set("mine_depth_format", LangSr, "🕳 Dubina: {0}-{1} slojeva");
            Set("mine_depth_format", LangTh, "🕳 ความลึก: {0}-{1} ชั้น");
            Set("mine_depth_format", LangTk, "🕳 Çuňluk: {0}-{1} gat");
            Set("mine_depth_format", LangUk, "🕳 Глибина: {0}-{1} шарів");
            Set("mine_depth_format", LangUz, "🕳 Chuqurlik: {0}-{1} qatlam");
            Set("mine_depth_format", LangVi, "🕳 Độ sâu: {0}-{1} lớp");
            Set("mine_depth_format", LangZh, "🕳 深度: {0}-{1} 层");

            Set("balance_bar_format", LangAr, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangAz, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangBe, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangBg, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangCa, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangCs, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangDe, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangEs, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangFa, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangFr, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangHe, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangHi, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangHu, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangHy, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangId, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangIt, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangJa, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangKa, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangKk, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangNl, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangPl, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangPt, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangRo, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangSk, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangSr, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangTh, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangTk, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangUk, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangUz, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangVi, "${0}  |  {1} {2} ({3} {4})");
            Set("balance_bar_format", LangZh, "${0}  |  {1} {2} ({3} {4})");

            Set("mining_level_format", LangAr, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangAz, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangBe, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangBg, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangCa, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangCs, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangDe, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangEs, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangFa, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangFr, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangHe, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangHi, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangHu, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangHy, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangId, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangIt, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangJa, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangKa, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangKk, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangNl, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangPl, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangPt, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangRo, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangSk, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangSr, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangTh, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangTk, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangUk, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangUz, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangVi, "{0}: {1} ({2} {3})");
            Set("mining_level_format", LangZh, "{0}: {1} ({2} {3})");

            Set("upgrade_str_stats", LangAr, "المكافأة الحالية:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangAz, "Cari bonus:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangBe, "Бягучы бонус:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangBg, "Текущ бонус:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangCa, "Bonificació actual:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangCs, "Aktuální bonus:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangDe, "Aktueller Bonus:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangEs, "Bono actual:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangFa, "پاداش فعلی:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangFr, "Bonus actuel :\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangHe, "בונוס נוכחי:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangHi, "वर्तमान बोनस:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangHu, "Jelenlegi bónusz:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangHy, "Ընթացիկ բոնուս՝\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangId, "Bonus saat ini:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangIt, "Bonus attuale:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangJa, "現在のボーナス:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangKa, "მიმდინარე ბონუსი:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangKk, "Ағымдағы бонус:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangNl, "Huidige bonus:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangPl, "Aktualny bonus:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangPt, "Bônus atual:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangRo, "Bonus curent:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangSk, "Aktuálny bonus:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangSr, "Trenutni bonus:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangTh, "โบนัสปัจจุบัน:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangTk, "Häzirki bonus:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangUk, "Поточний бонус:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangUz, "Joriy bonus:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangVi, "Thưởng hiện tại:\n<color=#FFD700>+{0}</color>");
            Set("upgrade_str_stats", LangZh, "当前加成：\n<color=#FFD700>+{0}</color>");

            Set("upgrade_bp_stats", LangAr, "السعة الحالية:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangAz, "Cari tutum:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangBe, "Бягучая ёмістасць:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangBg, "Текущ капацитет:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangCa, "Capacitat actual:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangCs, "Aktuální kapacita:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangDe, "Aktuelle Kapazität:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangEs, "Capacidad actual:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangFa, "ظرفیت فعلی:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangFr, "Capacité actuelle :\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangHe, "קיבולת נוכחית:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangHi, "वर्तमान क्षमता:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangHu, "Jelenlegi kapacitás:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangHy, "Ընթացիկ տարողություն՝\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangId, "Kapasitas saat ini:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangIt, "Capacità attuale:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangJa, "現在の容量:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangKa, "მიმდინარე ტევადობა:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangKk, "Ағымдағы сыйымдылық:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangNl, "Huidige capaciteit:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangPl, "Aktualna pojemność:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangPt, "Capacidade atual:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangRo, "Capacitate curentă:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangSk, "Aktuálna kapacita:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangSr, "Trenutni kapacitet:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangTh, "ความจุปัจจุบัน:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangTk, "Häzirki sygym:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangUk, "Поточна місткість:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangUz, "Joriy sig‘im:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangVi, "Sức chứa hiện tại:\n<color=#00EAFF>{0}</color>");
            Set("upgrade_bp_stats", LangZh, "当前容量：\n<color=#00EAFF>{0}</color>");

            Set("tut_controls_title", LangAr, "التحكم");
            Set("tut_controls_title", LangAz, "İdarəetmə");
            Set("tut_controls_title", LangBe, "КІРАВАННЕ");
            Set("tut_controls_title", LangBg, "УПРАВЛЕНИЕ");
            Set("tut_controls_title", LangCa, "CONTROLS");
            Set("tut_controls_title", LangCs, "OVLÁDÁNÍ");
            Set("tut_controls_title", LangDe, "STEUERUNG");
            Set("tut_controls_title", LangEs, "CONTROLES");
            Set("tut_controls_title", LangFa, "کنترل‌ها");
            Set("tut_controls_title", LangFr, "COMMANDES");
            Set("tut_controls_title", LangHe, "שליטה");
            Set("tut_controls_title", LangHi, "नियंत्रण");
            Set("tut_controls_title", LangHu, "IRÁNYÍTÁS");
            Set("tut_controls_title", LangHy, "ԿԱՌԱՎԱՐՈՒՄ");
            Set("tut_controls_title", LangId, "KONTROL");
            Set("tut_controls_title", LangIt, "CONTROLLI");
            Set("tut_controls_title", LangJa, "操作");
            Set("tut_controls_title", LangKa, "მართვა");
            Set("tut_controls_title", LangKk, "БАСҚАРУ");
            Set("tut_controls_title", LangNl, "BESTURING");
            Set("tut_controls_title", LangPl, "STEROWANIE");
            Set("tut_controls_title", LangPt, "CONTROLES");
            Set("tut_controls_title", LangRo, "CONTROALE");
            Set("tut_controls_title", LangSk, "OVLÁDANIE");
            Set("tut_controls_title", LangSr, "KONTROLE");
            Set("tut_controls_title", LangTh, "การควบคุม");
            Set("tut_controls_title", LangTk, "DOLANDYRYŞ");
            Set("tut_controls_title", LangUk, "КЕРУВАННЯ");
            Set("tut_controls_title", LangUz, "BOSHQARUV");
            Set("tut_controls_title", LangVi, "ĐIỀU KHIỂN");
            Set("tut_controls_title", LangZh, "操作方式");

            Set("tut_controls_pc", LangAr, "الحركة: W A S D\nالقفز: Space\nالحفر: زر الفأرة الأيسر\n\nاضغط أي مفتاح أو انقر للبدء.");
            Set("tut_controls_pc", LangAz, "Hərəkət: W A S D\nTullan: Space\nQaz: Sol siçan düyməsi\n\nBaşlamaq üçün istənilən düyməyə bas və ya kliklə.");
            Set("tut_controls_pc", LangBe, "Рух: W A S D\nСкачок: Прабел\nКапаць: Левая кнопка мышы\n\nНацісні любую клавішу або клікні, каб пачаць.");
            Set("tut_controls_pc", LangBg, "Движение: W A S D\nСкок: Space\nКопаене: Ляв бутон на мишката\n\nНатисни клавиш или кликни, за да започнеш.");
            Set("tut_controls_pc", LangCa, "Moviment: W A S D\nSalt: Space\nExcavar: Botó esquerre del ratolí\n\nPrem qualsevol tecla o fes clic per començar.");
            Set("tut_controls_pc", LangCs, "Pohyb: W A S D\nSkok: Mezerník\nKopání: Levé tlačítko myši\n\nPro začátek stiskni libovolnou klávesu nebo klikni.");
            Set("tut_controls_pc", LangDe, "Bewegung: W A S D\nSpringen: Leertaste\nGraben: Linke Maustaste\n\nDrücke eine Taste oder klicke, um zu starten.");
            Set("tut_controls_pc", LangEs, "Movimiento: W A S D\nSalto: Espacio\nCavar: Botón izquierdo del ratón\n\nPulsa cualquier tecla o haz clic para empezar.");
            Set("tut_controls_pc", LangFa, "حرکت: W A S D\nپرش: Space\nکندن: دکمه چپ ماوس\n\nبرای شروع یک کلید را بزن یا کلیک کن.");
            Set("tut_controls_pc", LangFr, "Déplacement : W A S D\nSaut : Espace\nCreuser : bouton gauche de la souris\n\nAppuyez sur une touche ou cliquez pour commencer.");
            Set("tut_controls_pc", LangHe, "תנועה: W A S D\nקפיצה: Space\nחפירה: לחצן עכבר שמאלי\n\nלחץ על מקש כלשהו או לחץ כדי להתחיל.");
            Set("tut_controls_pc", LangHi, "चलना: W A S D\nकूदना: Space\nखोदना: बायाँ माउस बटन\n\nशुरू करने के लिए कोई भी कुंजी दबाएँ या क्लिक करें।");
            Set("tut_controls_pc", LangHu, "Mozgás: W A S D\nUgrás: Szóköz\nÁsás: Bal egérgomb\n\nNyomj meg egy gombot vagy kattints a kezdéshez.");
            Set("tut_controls_pc", LangHy, "Շարժում՝ W A S D\nՑատկ՝ Space\nՓորել՝ մկնիկի ձախ կոճակ\n\nՍկսելու համար սեղմիր որևէ կոճակ կամ քլիք արա։");
            Set("tut_controls_pc", LangId, "Gerak: W A S D\nLompat: Space\nGali: Tombol mouse kiri\n\nTekan tombol apa pun atau klik untuk mulai.");
            Set("tut_controls_pc", LangIt, "Movimento: W A S D\nSalto: Spazio\nScava: Tasto sinistro del mouse\n\nPremi un tasto o clicca per iniziare.");
            Set("tut_controls_pc", LangJa, "移動: W A S D\nジャンプ: Space\n掘る: 左クリック\n\n何かキーを押すかクリックして開始。");
            Set("tut_controls_pc", LangKa, "მოძრაობა: W A S D\nხტომა: Space\nთხრა: მაუსის მარცხენა ღილაკი\n\nდასაწყებად დააჭირე ნებისმიერ ღილაკს ან დააკლიკე.");
            Set("tut_controls_pc", LangKk, "Қозғалыс: W A S D\nСекіру: Space\nҚазу: Тінтуірдің сол жақ батырмасы\n\nБастау үшін кез келген пернені бас немесе шертіңіз.");
            Set("tut_controls_pc", LangNl, "Beweging: W A S D\nSpringen: Spatie\nGraven: Linkermuisknop\n\nDruk op een toets of klik om te starten.");
            Set("tut_controls_pc", LangPl, "Ruch: W A S D\nSkok: Spacja\nKopanie: Lewy przycisk myszy\n\nNaciśnij dowolny klawisz lub kliknij, aby zacząć.");
            Set("tut_controls_pc", LangPt, "Movimento: W A S D\nPular: Espaço\nCavar: Botão esquerdo do mouse\n\nPressione qualquer tecla ou clique para começar.");
            Set("tut_controls_pc", LangRo, "Mișcare: W A S D\nSari: Space\nSapă: Butonul stâng al mouse-ului\n\nApasă orice tastă sau fă clic pentru a începe.");
            Set("tut_controls_pc", LangSk, "Pohyb: W A S D\nSkok: Medzerník\nKopanie: Ľavé tlačidlo myši\n\nStlač ľubovoľný kláves alebo klikni pre začatie.");
            Set("tut_controls_pc", LangSr, "Kretanje: W A S D\nSkok: Space\nKopanje: Levi klik miša\n\nPritisni bilo koji taster ili klikni za početak.");
            Set("tut_controls_pc", LangTh, "เคลื่อนที่: W A S D\nกระโดด: Space\nขุด: เมาส์ซ้าย\n\nกดปุ่มใดก็ได้หรือคลิกเพื่อเริ่ม");
            Set("tut_controls_pc", LangTk, "Hereket: W A S D\nBök: Space\nGaz: Syçanyň çep düwmesi\n\nBaşlamak üçin islendik düwmä bas ýa-da basyp gör.");
            Set("tut_controls_pc", LangUk, "Рух: W A S D\nСтрибок: Пробіл\nКопати: Ліва кнопка миші\n\nНатисни будь-яку клавішу або клікни, щоб почати.");
            Set("tut_controls_pc", LangUz, "Harakat: W A S D\nSakrash: Space\nQazish: Sichqonchaning chap tugmasi\n\nBoshlash uchun istalgan tugmani bosing yoki bosing.");
            Set("tut_controls_pc", LangVi, "Di chuyển: W A S D\nNhảy: Space\nĐào: Chuột trái\n\nNhấn phím bất kỳ hoặc nhấp để bắt đầu.");
            Set("tut_controls_pc", LangZh, "移动：W A S D\n跳跃：Space\n挖掘：鼠标左键\n\n按任意键或点击开始。");

            Set("tut_controls_mob", LangAr, "هذا عصا الحركة.\nحرّكها لتسير إلى الأمام.");
            Set("tut_controls_mob", LangAz, "Bu hərəkət joystickidir.\nİrəli getmək üçün onu hərəkət etdir.");
            Set("tut_controls_mob", LangBe, "Гэта джойсцік руху.\nРухай яго, каб ісці наперад.");
            Set("tut_controls_mob", LangBg, "Това е джойстикът за движение.\nПремести го, за да вървиш напред.");
            Set("tut_controls_mob", LangCa, "Aquest és el joystick de moviment.\nMou-lo per avançar.");
            Set("tut_controls_mob", LangCs, "Tohle je pohybový joystick.\nPosuň ho, abys šel dopředu.");
            Set("tut_controls_mob", LangDe, "Das ist der Bewegungs-Joystick.\nBewege ihn, um vorwärts zu laufen.");
            Set("tut_controls_mob", LangEs, "Este es el joystick de movimiento.\nMuévelo para avanzar.");
            Set("tut_controls_mob", LangFa, "این جوی‌استیک حرکت است.\nآن را حرکت بده تا جلو بروی.");
            Set("tut_controls_mob", LangFr, "Voici le joystick de déplacement.\nDéplace-le pour avancer.");
            Set("tut_controls_mob", LangHe, "זהו ג'ויסטיק התנועה.\nהזז אותו כדי ללכת קדימה.");
            Set("tut_controls_mob", LangHi, "यह चलने वाला जॉयस्टिक है।\nआगे बढ़ने के लिए इसे हिलाएँ।");
            Set("tut_controls_mob", LangHu, "Ez a mozgás joystick.\nMozgasd, hogy előre menj.");
            Set("tut_controls_mob", LangHy, "Սա շարժման ջոյսթիկն է։\nՇարժիր այն առաջ գնալու համար։");
            Set("tut_controls_mob", LangId, "Ini joystick gerakan.\nGerakkan untuk berjalan maju.");
            Set("tut_controls_mob", LangIt, "Questo è il joystick di movimento.\nMuovilo per andare avanti.");
            Set("tut_controls_mob", LangJa, "これは移動用ジョイスティックです。\n前に進むには動かしてください。");
            Set("tut_controls_mob", LangKa, "ეს მოძრაობის ჯოისტიკია.\nწინ წასასვლელად გაამოძრავე.");
            Set("tut_controls_mob", LangKk, "Бұл қозғалыс джойстигі.\nАлға жүру үшін оны жылжыт.");
            Set("tut_controls_mob", LangNl, "Dit is de bewegingsjoystick.\nBeweeg hem om vooruit te lopen.");
            Set("tut_controls_mob", LangPl, "To jest joystick ruchu.\nPrzesuń go, aby iść do przodu.");
            Set("tut_controls_mob", LangPt, "Este é o joystick de movimento.\nMova-o para andar para frente.");
            Set("tut_controls_mob", LangRo, "Acesta este joystickul de mișcare.\nMișcă-l pentru a merge înainte.");
            Set("tut_controls_mob", LangSk, "Toto je pohybový joystick.\nPosuň ho, aby si išiel dopredu.");
            Set("tut_controls_mob", LangSr, "Ovo je džojstik za kretanje.\nPomeraj ga da ideš napred.");
            Set("tut_controls_mob", LangTh, "นี่คือจอยสติ๊กสำหรับเคลื่อนที่\nเลื่อนเพื่อเดินไปข้างหน้า");
            Set("tut_controls_mob", LangTk, "Bu hereket joýstigi.\nÖňe gitmek üçin ony süýşür.");
            Set("tut_controls_mob", LangUk, "Це джойстик руху.\nРухай його, щоб іти вперед.");
            Set("tut_controls_mob", LangUz, "Bu harakat joystigi.\nOldinga yurish uchun uni siljiting.");
            Set("tut_controls_mob", LangVi, "Đây là cần điều khiển di chuyển.\nDi chuyển nó để đi về phía trước.");
            Set("tut_controls_mob", LangZh, "这是移动摇杆。\n拖动它即可向前移动。");

            Set("tut_buttons_title", LangAr, "الأزرار");
            Set("tut_buttons_title", LangAz, "Düymələr");
            Set("tut_buttons_title", LangBe, "КНОПКІ");
            Set("tut_buttons_title", LangBg, "БУТОНИ");
            Set("tut_buttons_title", LangCa, "BOTONS");
            Set("tut_buttons_title", LangCs, "TLAČÍTKA");
            Set("tut_buttons_title", LangDe, "TASTEN");
            Set("tut_buttons_title", LangEs, "BOTONES");
            Set("tut_buttons_title", LangFa, "دکمه‌ها");
            Set("tut_buttons_title", LangFr, "BOUTONS");
            Set("tut_buttons_title", LangHe, "כפתורים");
            Set("tut_buttons_title", LangHi, "बटन");
            Set("tut_buttons_title", LangHu, "GOMBOK");
            Set("tut_buttons_title", LangHy, "ԿՈՃԱԿՆԵՐ");
            Set("tut_buttons_title", LangId, "TOMBOL");
            Set("tut_buttons_title", LangIt, "PULSANTI");
            Set("tut_buttons_title", LangJa, "ボタン");
            Set("tut_buttons_title", LangKa, "ღილაკები");
            Set("tut_buttons_title", LangKk, "БАТЫРМАЛАР");
            Set("tut_buttons_title", LangNl, "KNOPPEN");
            Set("tut_buttons_title", LangPl, "PRZYCISKI");
            Set("tut_buttons_title", LangPt, "BOTÕES");
            Set("tut_buttons_title", LangRo, "BUTOANE");
            Set("tut_buttons_title", LangSk, "TLAČIDLÁ");
            Set("tut_buttons_title", LangSr, "DUGMAD");
            Set("tut_buttons_title", LangTh, "ปุ่ม");
            Set("tut_buttons_title", LangTk, "DÜWMELER");
            Set("tut_buttons_title", LangUk, "КНОПКИ");
            Set("tut_buttons_title", LangUz, "TUGMALAR");
            Set("tut_buttons_title", LangVi, "NÚT");
            Set("tut_buttons_title", LangZh, "按钮");

            Set("tut_buttons_body", LangAr, "حفر — حفر كتلة\nقفز — قفز\nفعل — تفاعل\nركض — اندفاع\nالعمال — قائمة العمال\n\nاضغط على الشاشة للمتابعة.");
            Set("tut_buttons_body", LangAz, "QAZ — bloku qaz\nTULLAN — tullan\nHƏRƏKƏT — qarşılıqlı əlaqə\nQAÇ — sürətlən\nMİNYONLAR — minyonlar menyusu\n\nDavam etmək üçün ekrana toxun.");
            Set("tut_buttons_body", LangBe, "КАПАЦЬ — здабыць блок\nСКОК — скачок\nДЗЕЯННЕ — узаемадзеянне\nБЕГ — паскарэнне\nМІНЬЁНЫ — меню міньёнаў\n\nНацісні на экран, каб працягнуць.");
            Set("tut_buttons_body", LangBg, "КОПАЙ — изкопай блок\nСКОК — скок\nДЕЙСТВИЕ — взаимодействие\nБЯГ — спринт\nМИНЬОНИ — меню на миньоните\n\nДокосни екрана, за да продължиш.");
            Set("tut_buttons_body", LangCa, "MINA — excava un bloc\nSALT — salta\nACCIÓ — interactua\nCÓRRER — esprinta\nMINIONS — menú de minions\n\nToca la pantalla per continuar.");
            Set("tut_buttons_body", LangCs, "KOPAT — vytěž blok\nSKOK — skoč\nAKCE — interakce\nBĚH — sprint\nPOMOCNÍCI — menu pomocníků\n\nKlepni na obrazovku pro pokračování.");
            Set("tut_buttons_body", LangDe, "ABBAU — Block abbauen\nSPRUNG — springen\nAKTION — interagieren\nRENNEN — sprinten\nMINIONS — Minions-Menü\n\nTippe auf den Bildschirm, um fortzufahren.");
            Set("tut_buttons_body", LangEs, "MINAR — romper bloque\nSALTAR — salto\nACTUAR — interactuar\nCORRER — sprint\nMINIONS — menú de minions\n\nToca la pantalla para continuar.");
            Set("tut_buttons_body", LangFa, "استخراج — یک بلوک را بکن\nپرش — بپر\nعمل — تعامل\nدویدن — سرعت گرفتن\nمینیون‌ها — منوی مینیون‌ها\n\nبرای ادامه روی صفحه بزن.");
            Set("tut_buttons_body", LangFr, "MINER — creuser un bloc\nSAUTER — sauter\nACTION — interagir\nCOURIR — sprinter\nMINIONS — menu des minions\n\nTouchez l'écran pour continuer.");
            Set("tut_buttons_body", LangHe, "כרייה — חפור בלוק\nקפיצה — קפוץ\nפעולה — אינטראקציה\nריצה — ספרינט\nמיניונים — תפריט מיניונים\n\nהקש על המסך כדי להמשיך.");
            Set("tut_buttons_body", LangHi, "खोदें — ब्लॉक तोड़ें\nकूदें — कूदना\nक्रिया — इंटरैक्ट करें\nदौड़ — स्प्रिंट\nमिनियन — मिनियन मेनू\n\nजारी रखने के लिए स्क्रीन पर टैप करें।");
            Set("tut_buttons_body", LangHu, "BÁNYÁSZAT — blokk kiásása\nUGRÁS — ugrás\nAKCIÓ — interakció\nFUTÁS — sprint\nMINIONOK — minion menü\n\nÉrintsd meg a képernyőt a folytatáshoz.");
            Set("tut_buttons_body", LangHy, "ՓՈՐԵԼ — կոտրել բլոկը\nՑԱՏԿ — ցատկել\nԳՈՐԾՈՂՈՒԹՅՈՒՆ — փոխազդել\nՎԱԶՔ — արագ վազք\nՄԻՆՅՈՆՆԵՐ — մինյոնների ընտրացանկ\n\nՇարունակելու համար հպիր էկրանին։");
            Set("tut_buttons_body", LangId, "TAMBANG — gali blok\nLOMPAT — melompat\nAKSI — berinteraksi\nLARI — sprint\nMINION — menu minion\n\nKetuk layar untuk lanjut.");
            Set("tut_buttons_body", LangIt, "SCAVA — estrai un blocco\nSALTA — salto\nAZIONE — interagisci\nCORRI — sprint\nMINIONI — menu minion\n\nTocca lo schermo per continuare.");
            Set("tut_buttons_body", LangJa, "採掘 — ブロックを掘る\nジャンプ — ジャンプ\nアクション — 調べる\n走る — ダッシュ\nミニオン — ミニオンメニュー\n\n続けるには画面をタップ。");
            Set("tut_buttons_body", LangKa, "თხრა — ბლოკის მოპოვება\nხტომა — ახტომა\nქმედება — ურთიერთქმედება\nრბენა — სპრინტი\nმინიონები — მინიონების მენიუ\n\nგასაგრძელებლად შეეხე ეკრანს.");
            Set("tut_buttons_body", LangKk, "ҚАЗУ — блокты қазу\nСЕКІРУ — секіру\nӘРЕКЕТ — әрекеттесу\nЖҮГІРУ — жеделдеу\nМИНЬОНДАР — миньон мәзірі\n\nЖалғастыру үшін экранды бас.");
            Set("tut_buttons_body", LangNl, "MIJN — graaf blok\nSPRING — springen\nACTIE — interactie\nREN — sprint\nMINIONS — minions-menu\n\nTik op het scherm om verder te gaan.");
            Set("tut_buttons_body", LangPl, "KOP — wydobądź blok\nSKOK — skok\nAKCJA — interakcja\nBIEG — sprint\nMINIONY — menu minionów\n\nDotknij ekranu, aby kontynuować.");
            Set("tut_buttons_body", LangPt, "MINERAR — cavar bloco\nPULAR — pular\nAÇÃO — interagir\nCORRER — sprint\nMINIONS — menu dos minions\n\nToque na tela para continuar.");
            Set("tut_buttons_body", LangRo, "MINEAZĂ — sparge blocul\nSARI — salt\nACȚIUNE — interacțiune\nALERGARE — sprint\nMINIONI — meniu minioni\n\nAtinge ecranul pentru a continua.");
            Set("tut_buttons_body", LangSk, "ŤAŽIŤ — vyťažiť blok\nSKOK — skočiť\nAKCIA — interagovať\nBEH — šprint\nMINIONI — menu minionov\n\nŤukni na obrazovku pre pokračovanie.");
            Set("tut_buttons_body", LangSr, "KOPAJ — iskopaj blok\nSKOK — skoči\nAKCIJA — interakcija\nTRČI — sprint\nMINIONI — meni miniona\n\nDodirni ekran za nastavak.");
            Set("tut_buttons_body", LangTh, "ขุด — ขุดบล็อก\nกระโดด — กระโดด\nแอ็กชัน — โต้ตอบ\nวิ่ง — เร่งความเร็ว\nมินเนียน — เมนูมินเนียน\n\nแตะหน้าจอเพื่อเล่นต่อ");
            Set("tut_buttons_body", LangTk, "GAZ — bloky gaz\nBÖK — bök\nHEREKET — özara täsir et\nYLGA — çaltlaş\nMINIONLAR — minion menýusy\n\nDowam etmek üçin ekrana bas.");
            Set("tut_buttons_body", LangUk, "КОПАТИ — добути блок\nСТРИБОК — стрибок\nДІЯ — взаємодія\nБІГ — спринт\nМІНЬЙОНИ — меню міньйонів\n\nТоркнися екрана, щоб продовжити.");
            Set("tut_buttons_body", LangUz, "QAZISH — blok qazish\nSAKRASH — sakrash\nHARAKAT — o‘zaro ta’sir\nYUGURISH — sprint\nMINIONLAR — minionlar menyusi\n\nDavom etish uchun ekranga teging.");
            Set("tut_buttons_body", LangVi, "ĐÀO — đào khối\nNHẢY — nhảy\nHÀNH ĐỘNG — tương tác\nCHẠY — tăng tốc\nTAY SAI — menu tay sai\n\nChạm màn hình để tiếp tục.");
            Set("tut_buttons_body", LangZh, "挖掘 — 挖方块\n跳跃 — 跳跃\n动作 — 互动\n奔跑 — 冲刺\n随从 — 随从菜单\n\n点击屏幕继续。");

            Set("tut_island_title", LangAr, "جزيرتك");
            Set("tut_island_title", LangAz, "Sənin adan");
            Set("tut_island_title", LangBe, "ТВОЙ ВОСТРАЎ");
            Set("tut_island_title", LangBg, "ТВОЯТ ОСТРОВ");
            Set("tut_island_title", LangCa, "LA TEVA ILLA");
            Set("tut_island_title", LangCs, "TVŮJ OSTROV");
            Set("tut_island_title", LangDe, "DEINE INSEL");
            Set("tut_island_title", LangEs, "TU ISLA");
            Set("tut_island_title", LangFa, "جزیره تو");
            Set("tut_island_title", LangFr, "TON ÎLE");
            Set("tut_island_title", LangHe, "האי שלך");
            Set("tut_island_title", LangHi, "तुम्हारा द्वीप");
            Set("tut_island_title", LangHu, "A SAJÁT SZIGETED");
            Set("tut_island_title", LangHy, "ՔՈ ԿՂԶԻՆ");
            Set("tut_island_title", LangId, "PULAUMU");
            Set("tut_island_title", LangIt, "LA TUA ISOLA");
            Set("tut_island_title", LangJa, "自分の島");
            Set("tut_island_title", LangKa, "შენი კუნძული");
            Set("tut_island_title", LangKk, "СЕНІҢ АРАЛЫҢ");
            Set("tut_island_title", LangNl, "JOUW EILAND");
            Set("tut_island_title", LangPl, "TWOJA WYSPA");
            Set("tut_island_title", LangPt, "SUA ILHA");
            Set("tut_island_title", LangRo, "INSULA TA");
            Set("tut_island_title", LangSk, "TVÔJ OSTROV");
            Set("tut_island_title", LangSr, "TVOJE OSTRVO");
            Set("tut_island_title", LangTh, "เกาะของคุณ");
            Set("tut_island_title", LangTk, "SENIŇ ADAŇ");
            Set("tut_island_title", LangUk, "ТВІЙ ОСТРІВ");
            Set("tut_island_title", LangUz, "SENING OROLING");
            Set("tut_island_title", LangVi, "HÒN ĐẢO CỦA BẠN");
            Set("tut_island_title", LangZh, "你的岛屿");

            Set("tut_island_body", LangAr, "هذه جزيرتك يا عامل المنجم!\nاستكشفها. وعندما تصبح جاهزًا —\nارجع إلى الردهة.");
            Set("tut_island_body", LangAz, "Budur sənin adan, madənçi!\nOnu araşdır. Hazır olanda —\nlobbiyə qayıt.");
            Set("tut_island_body", LangBe, "Вось твой востраў, шахцёр!\nДаследуй яго. Калі будзеш гатовы —\nвяртайся ў лобі.");
            Set("tut_island_body", LangBg, "Това е твоят остров, миньоре!\nРазгледай го. Когато си готов —\nвърни се в лобито.");
            Set("tut_island_body", LangCa, "Aquí tens la teva illa, miner!\nExplora-la. Quan estiguis preparat —\ntorna al vestíbul.");
            Set("tut_island_body", LangCs, "Tady je tvůj ostrov, horníku!\nProzkoumej ho. Až budeš připraven —\nvrať se do lobby.");
            Set("tut_island_body", LangDe, "Hier ist deine Insel, Bergmann!\nErkunde sie. Wenn du bereit bist —\ngeh zurück in die Lobby.");
            Set("tut_island_body", LangEs, "Aquí está tu isla, minero.\nExplórala. Cuando estés listo —\nvuelve al lobby.");
            Set("tut_island_body", LangFa, "این هم جزیره تو، معدن‌چی!\nآن را بگرد. وقتی آماده شدی —\nبه لابی برگرد.");
            Set("tut_island_body", LangFr, "Voici ton île, mineur !\nExplore-la. Quand tu seras prêt —\nretourne au lobby.");
            Set("tut_island_body", LangHe, "זה האי שלך, כורה!\nחקור אותו. כשתהיה מוכן —\nחזור ללובי.");
            Set("tut_island_body", LangHi, "यह रहा तुम्हारा द्वीप, खनिक!\nइसे घूमें। जब तैयार हो जाओ —\nलॉबी में वापस जाओ।");
            Set("tut_island_body", LangHu, "Itt a szigeted, bányász!\nFedezd fel. Ha készen állsz —\nmenj vissza a lobbyba.");
            Set("tut_island_body", LangHy, "Ահա քո կղզին, հանքափոր։\nՈւսումնասիրիր այն։ Երբ պատրաստ լինես՝\nվերադարձիր լոբբի։");
            Set("tut_island_body", LangId, "Ini pulaumu, penambang!\nJelajahi dulu. Saat sudah siap —\nkembali ke lobi.");
            Set("tut_island_body", LangIt, "Ecco la tua isola, minatore!\nEsplorala. Quando sei pronto —\ntorna alla lobby.");
            Set("tut_island_body", LangJa, "ここが君の島だ、採掘者よ！\n探索してみよう。準備ができたら —\nロビーに戻ろう。");
            Set("tut_island_body", LangKa, "ეს შენი კუნძულია, მაღაროელო!\nგამოიკვლიე. როცა მზად იქნები —\nდაბრუნდი ლობიში.");
            Set("tut_island_body", LangKk, "Міне, сенің аралың, кенші!\nОны зертте. Дайын болғанда —\nлоббиге қайт.");
            Set("tut_island_body", LangNl, "Hier is jouw eiland, mijnwerker!\nVerken het. Als je klaar bent —\nga terug naar de lobby.");
            Set("tut_island_body", LangPl, "To jest twoja wyspa, górniku!\nZbadaj ją. Gdy będziesz gotowy —\nwróć do lobby.");
            Set("tut_island_body", LangPt, "Aqui está sua ilha, minerador!\nExplore-a. Quando estiver pronto —\nvolte ao lobby.");
            Set("tut_island_body", LangRo, "Iată insula ta, minerule!\nExploreaz-o. Când ești gata —\nîntoarce-te în lobby.");
            Set("tut_island_body", LangSk, "Toto je tvoj ostrov, baník!\nPreskúmaj ho. Keď budeš pripravený —\nvráť sa do lobby.");
            Set("tut_island_body", LangSr, "Ovo je tvoje ostrvo, rudaru!\nIstraži ga. Kada budeš spreman —\nvrati se u lobi.");
            Set("tut_island_body", LangTh, "นี่คือเกาะของคุณ นักขุด!\nสำรวจมันก่อน แล้วเมื่อพร้อม —\nกลับไปที่ล็อบบี้");
            Set("tut_island_body", LangTk, "Ine, seniň adaň, magdançy!\nOny öwren. Taýýar bolanyňda —\nlobbä dolan.");
            Set("tut_island_body", LangUk, "Ось твій острів, шахтарю!\nДосліди його. Коли будеш готовий —\nповертайся до лобі.");
            Set("tut_island_body", LangUz, "Mana oroling, konchi!\nUni o‘rgan. Tayyor bo‘lgach —\nlobbiga qayt.");
            Set("tut_island_body", LangVi, "Đây là hòn đảo của bạn, thợ mỏ!\nHãy khám phá nó. Khi sẵn sàng —\nquay lại sảnh.");
            Set("tut_island_body", LangZh, "这就是你的岛屿，矿工！\n先探索一下。准备好了以后 —\n返回大厅。");

            Set("tut_buy_mine_title", LangAr, "أول منجم");
            Set("tut_buy_mine_title", LangAz, "İlk mədən");
            Set("tut_buy_mine_title", LangBe, "ПЕРШАЯ ШАХТА");
            Set("tut_buy_mine_title", LangBg, "ПЪРВА МИНА");
            Set("tut_buy_mine_title", LangCa, "PRIMERA MINA");
            Set("tut_buy_mine_title", LangCs, "PRVNÍ DŮL");
            Set("tut_buy_mine_title", LangDe, "ERSTE MINE");
            Set("tut_buy_mine_title", LangEs, "PRIMERA MINA");
            Set("tut_buy_mine_title", LangFa, "اولین معدن");
            Set("tut_buy_mine_title", LangFr, "PREMIÈRE MINE");
            Set("tut_buy_mine_title", LangHe, "המכרה הראשון");
            Set("tut_buy_mine_title", LangHi, "पहली खान");
            Set("tut_buy_mine_title", LangHu, "ELSŐ BÁNYA");
            Set("tut_buy_mine_title", LangHy, "ԱՌԱՋԻՆ ՀԱՆՔԸ");
            Set("tut_buy_mine_title", LangId, "TAMBANG PERTAMA");
            Set("tut_buy_mine_title", LangIt, "PRIMA MINIERA");
            Set("tut_buy_mine_title", LangJa, "最初の鉱山");
            Set("tut_buy_mine_title", LangKa, "პირველი მაღარო");
            Set("tut_buy_mine_title", LangKk, "АЛҒАШҚЫ КЕН");
            Set("tut_buy_mine_title", LangNl, "EERSTE MIJN");
            Set("tut_buy_mine_title", LangPl, "PIERWSZA KOPALNIA");
            Set("tut_buy_mine_title", LangPt, "PRIMEIRA MINA");
            Set("tut_buy_mine_title", LangRo, "PRIMA MINĂ");
            Set("tut_buy_mine_title", LangSk, "PRVÁ BAŇA");
            Set("tut_buy_mine_title", LangSr, "PRVI RUDNIK");
            Set("tut_buy_mine_title", LangTh, "เหมืองแรก");
            Set("tut_buy_mine_title", LangTk, "ILKINJI KÄN");
            Set("tut_buy_mine_title", LangUk, "ПЕРША ШАХТА");
            Set("tut_buy_mine_title", LangUz, "BIRINCHI KON");
            Set("tut_buy_mine_title", LangVi, "MỎ ĐẦU TIÊN");
            Set("tut_buy_mine_title", LangZh, "第一座矿井");

            Set("tut_buy_mine_body", LangAr, "للبداية، اشتر أول منجم لك.\nيشير الشعاع إلى متجر المناجم.");
            Set("tut_buy_mine_body", LangAz, "Başlamaq üçün ilk mədənini al.\nŞüa səni mədən mağazasına aparır.");
            Set("tut_buy_mine_body", LangBe, "Для пачатку купі сваю першую шахту.\nПрамень паказвае на краму шахт.");
            Set("tut_buy_mine_body", LangBg, "За начало купи първата си мина.\nЛъчът сочи към магазина за мини.");
            Set("tut_buy_mine_body", LangCa, "Per començar, compra la teva primera mina.\nEl feix apunta a la botiga de mines.");
            Set("tut_buy_mine_body", LangCs, "Na začátek si kup svůj první důl.\nPaprsek ukazuje na obchod s doly.");
            Set("tut_buy_mine_body", LangDe, "Kaufe zuerst deine erste Mine.\nDer Lichtstrahl zeigt auf den Minenladen.");
            Set("tut_buy_mine_body", LangEs, "Para empezar, compra tu primera mina.\nEl rayo apunta a la tienda de minas.");
            Set("tut_buy_mine_body", LangFa, "برای شروع، اولین معدن خود را بخر.\nپرتو نور فروشگاه معدن را نشان می‌دهد.");
            Set("tut_buy_mine_body", LangFr, "Pour commencer, achète ta première mine.\nLe faisceau indique la boutique des mines.");
            Set("tut_buy_mine_body", LangHe, "כדי להתחיל, קנה את המכרה הראשון שלך.\nהקרן מצביעה על חנות המכרות.");
            Set("tut_buy_mine_body", LangHi, "शुरू करने के लिए अपनी पहली खान खरीदो।\nकिरण खान की दुकान की ओर इशारा करती है।");
            Set("tut_buy_mine_body", LangHu, "Kezdésként vedd meg az első bányádat.\nA fénysugár a bányaboltot mutatja.");
            Set("tut_buy_mine_body", LangHy, "Սկսելու համար գնիր քո առաջին հանքը։\nԼույսի շողը ցույց է տալիս հանքերի խանութը։");
            Set("tut_buy_mine_body", LangId, "Untuk memulai, beli tambang pertamamu.\nSorot cahaya menunjuk ke toko tambang.");
            Set("tut_buy_mine_body", LangIt, "Per iniziare, compra la tua prima miniera.\nIl raggio indica il negozio delle miniere.");
            Set("tut_buy_mine_body", LangJa, "まずは最初の鉱山を購入しよう。\n光の柱が鉱山ショップを示している。");
            Set("tut_buy_mine_body", LangKa, "დასაწყისისთვის იყიდე შენი პირველი მაღარო.\nსხივი მაღაროს მაღაზიას გაჩვენებს.");
            Set("tut_buy_mine_body", LangKk, "Бастау үшін алғашқы кенеңді сатып ал.\nСәуле кен дүкенін көрсетіп тұр.");
            Set("tut_buy_mine_body", LangNl, "Koop om te beginnen je eerste mijn.\nDe lichtstraal wijst naar de mijnwinkel.");
            Set("tut_buy_mine_body", LangPl, "Na początek kup swoją pierwszą kopalnię.\nPromień wskazuje sklep z kopalniami.");
            Set("tut_buy_mine_body", LangPt, "Para começar, compre sua primeira mina.\nO feixe aponta para a loja de minas.");
            Set("tut_buy_mine_body", LangRo, "Pentru început, cumpără prima ta mină.\nRaza arată spre magazinul de mine.");
            Set("tut_buy_mine_body", LangSk, "Na začiatok si kúp svoju prvú baňu.\nLúč ukazuje na obchod s baňami.");
            Set("tut_buy_mine_body", LangSr, "Za početak kupi svoj prvi rudnik.\nZrak pokazuje ka prodavnici rudnika.");
            Set("tut_buy_mine_body", LangTh, "เริ่มต้นด้วยการซื้อเหมืองแรกของคุณ\nลำแสงจะชี้ไปที่ร้านขายเหมือง");
            Set("tut_buy_mine_body", LangTk, "Başlamak üçin ilkinji käniňi satyn al.\nŞöhle kän dükanyny görkezýär.");
            Set("tut_buy_mine_body", LangUk, "Для початку купи свою першу шахту.\nПромінь вказує на магазин шахт.");
            Set("tut_buy_mine_body", LangUz, "Boshlash uchun birinchi koningizni sotib oling.\nNur kon do‘konini ko‘rsatmoqda.");
            Set("tut_buy_mine_body", LangVi, "Để bắt đầu, hãy mua mỏ đầu tiên của bạn.\nTia sáng đang chỉ đến cửa hàng mỏ.");
            Set("tut_buy_mine_body", LangZh, "先购买你的第一座矿井。\n光柱会指向矿井商店。");

            Set("tut_place_mine_title", LangAr, "ضع المنجم");
            Set("tut_place_mine_title", LangAz, "Mədəni yerləşdir");
            Set("tut_place_mine_title", LangBe, "ПАСТАЎ ШАХТУ");
            Set("tut_place_mine_title", LangBg, "ПОСТАВИ МИНАТА");
            Set("tut_place_mine_title", LangCa, "COL·LOCA LA MINA");
            Set("tut_place_mine_title", LangCs, "UMÍSTI DŮL");
            Set("tut_place_mine_title", LangDe, "MINE PLATZIEREN");
            Set("tut_place_mine_title", LangEs, "COLOCAR MINA");
            Set("tut_place_mine_title", LangFa, "معدن را قرار بده");
            Set("tut_place_mine_title", LangFr, "PLACER LA MINE");
            Set("tut_place_mine_title", LangHe, "הצב את המכרה");
            Set("tut_place_mine_title", LangHi, "खान लगाओ");
            Set("tut_place_mine_title", LangHu, "HELYEZD LE A BÁNYÁT");
            Set("tut_place_mine_title", LangHy, "ՏԵՂԱԴՐԻՐ ՀԱՆՔԸ");
            Set("tut_place_mine_title", LangId, "TEMPATKAN TAMBANG");
            Set("tut_place_mine_title", LangIt, "POSIZIONA MINIERA");
            Set("tut_place_mine_title", LangJa, "鉱山を設置");
            Set("tut_place_mine_title", LangKa, "დააყენე მაღარო");
            Set("tut_place_mine_title", LangKk, "КЕНДІ ОРНАТ");
            Set("tut_place_mine_title", LangNl, "PLAATS MIJN");
            Set("tut_place_mine_title", LangPl, "POSTAW KOPALNIĘ");
            Set("tut_place_mine_title", LangPt, "COLOCAR MINA");
            Set("tut_place_mine_title", LangRo, "PLASEAZĂ MINA");
            Set("tut_place_mine_title", LangSk, "UMIESTNI BAŇU");
            Set("tut_place_mine_title", LangSr, "POSTAVI RUDNIK");
            Set("tut_place_mine_title", LangTh, "วางเหมือง");
            Set("tut_place_mine_title", LangTk, "KÄNI GOÝ");
            Set("tut_place_mine_title", LangUk, "ПОСТАВ ШАХТУ");
            Set("tut_place_mine_title", LangUz, "KONNI JOYLASHTIR");
            Set("tut_place_mine_title", LangVi, "ĐẶT MỎ");
            Set("tut_place_mine_title", LangZh, "放置矿井");

            Set("tut_place_mine_body", LangAr, "ممتاز! عد إلى جزيرتك\nواضع المنجم الذي اشتريته.");
            Set("tut_place_mine_body", LangAz, "Əla! Adana qayıt\nvə aldığın mədəni yerləşdir.");
            Set("tut_place_mine_body", LangBe, "Выдатна! Вярніся на востраў\nі пастаў купленую шахту.");
            Set("tut_place_mine_body", LangBg, "Чудесно! Върни се на острова\nи постави купената мина.");
            Set("tut_place_mine_body", LangCa, "Perfecte! Torna a la teva illa\ni col·loca la mina comprada.");
            Set("tut_place_mine_body", LangCs, "Skvělé! Vrať se na ostrov\na umísti koupený důl.");
            Set("tut_place_mine_body", LangDe, "Super! Geh zurück auf deine Insel\nund platziere die gekaufte Mine.");
            Set("tut_place_mine_body", LangEs, "¡Genial! Vuelve a tu isla\ny coloca la mina comprada.");
            Set("tut_place_mine_body", LangFa, "عالی! به جزیره‌ات برگرد\nو معدن خریداری‌شده را قرار بده.");
            Set("tut_place_mine_body", LangFr, "Parfait ! Retourne sur ton île\net place la mine achetée.");
            Set("tut_place_mine_body", LangHe, "מעולה! חזור לאי שלך\nוהצב את המכרה שקנית.");
            Set("tut_place_mine_body", LangHi, "बहुत बढ़िया! अपने द्वीप पर लौटो\nऔर खरीदी हुई खान लगाओ।");
            Set("tut_place_mine_body", LangHu, "Nagyszerű! Térj vissza a szigetedre\nés helyezd le a megvett bányát.");
            Set("tut_place_mine_body", LangHy, "Հիանալի։ Վերադարձիր քո կղզի\nև տեղադրիր գնված հանքը։");
            Set("tut_place_mine_body", LangId, "Bagus! Kembali ke pulaumu\ndan tempatkan tambang yang dibeli.");
            Set("tut_place_mine_body", LangIt, "Ottimo! Torna sulla tua isola\ne posiziona la miniera acquistata.");
            Set("tut_place_mine_body", LangJa, "いいぞ！ 島に戻って\n購入した鉱山を設置しよう。");
            Set("tut_place_mine_body", LangKa, "შესანიშნავია! დაბრუნდი კუნძულზე\nდა დადგი ნაყიდი მაღარო.");
            Set("tut_place_mine_body", LangKk, "Тамаша! Аралыңа қайт\nжәне сатып алған кенеңді орнат.");
            Set("tut_place_mine_body", LangNl, "Mooi! Ga terug naar je eiland\nen plaats de gekochte mijn.");
            Set("tut_place_mine_body", LangPl, "Świetnie! Wróć na swoją wyspę\ni postaw kupioną kopalnię.");
            Set("tut_place_mine_body", LangPt, "Ótimo! Volte para sua ilha\ne coloque a mina comprada.");
            Set("tut_place_mine_body", LangRo, "Excelent! Întoarce-te pe insulă\nși plasează mina cumpărată.");
            Set("tut_place_mine_body", LangSk, "Skvelé! Vráť sa na svoj ostrov\na umiestni kúpenú baňu.");
            Set("tut_place_mine_body", LangSr, "Odlično! Vrati se na svoje ostrvo\ni postavi kupljeni rudnik.");
            Set("tut_place_mine_body", LangTh, "เยี่ยมมาก! กลับไปที่เกาะของคุณ\nแล้ววางเหมืองที่ซื้อมา");
            Set("tut_place_mine_body", LangTk, "Gowy! Adaňa dolan\nwe satyn alan käniňi goý.");
            Set("tut_place_mine_body", LangUk, "Чудово! Повернися на свій острів\nі постав куплену шахту.");
            Set("tut_place_mine_body", LangUz, "Ajoyib! Orolingizga qayting\nva sotib olingan konni joylashtiring.");
            Set("tut_place_mine_body", LangVi, "Tuyệt! Hãy quay lại đảo của bạn\nvà đặt mỏ vừa mua.");
            Set("tut_place_mine_body", LangZh, "很好！回到你的岛上\n放置刚刚购买的矿井。");

            Set("tut_mining_title", LangAr, "التعدين");
            Set("tut_mining_title", LangAz, "Qazma");
            Set("tut_mining_title", LangBe, "ДОБЫЧА");
            Set("tut_mining_title", LangBg, "КОПАЕНЕ");
            Set("tut_mining_title", LangCa, "MINERIA");
            Set("tut_mining_title", LangCs, "TĚŽBA");
            Set("tut_mining_title", LangDe, "ABBAU");
            Set("tut_mining_title", LangEs, "MINERÍA");
            Set("tut_mining_title", LangFa, "استخراج");
            Set("tut_mining_title", LangFr, "MINAGE");
            Set("tut_mining_title", LangHe, "כרייה");
            Set("tut_mining_title", LangHi, "खनन");
            Set("tut_mining_title", LangHu, "BÁNYÁSZAT");
            Set("tut_mining_title", LangHy, "ՀԱՆՔԱՓՈՐՈՒՄ");
            Set("tut_mining_title", LangId, "MENAMBANG");
            Set("tut_mining_title", LangIt, "ESTRAZIONE");
            Set("tut_mining_title", LangJa, "採掘");
            Set("tut_mining_title", LangKa, "მოპოვება");
            Set("tut_mining_title", LangKk, "ӨНДІРУ");
            Set("tut_mining_title", LangNl, "MIJNBOUW");
            Set("tut_mining_title", LangPl, "KOPANIE");
            Set("tut_mining_title", LangPt, "MINERAÇÃO");
            Set("tut_mining_title", LangRo, "MINERIT");
            Set("tut_mining_title", LangSk, "ŤAŽBA");
            Set("tut_mining_title", LangSr, "RUDARENJE");
            Set("tut_mining_title", LangTh, "การขุด");
            Set("tut_mining_title", LangTk, "GAZUW");
            Set("tut_mining_title", LangUk, "ВИДОБУТОК");
            Set("tut_mining_title", LangUz, "QAZISH");
            Set("tut_mining_title", LangVi, "KHAI THÁC");
            Set("tut_mining_title", LangZh, "采矿");

            Set("tut_mining_mob", LangAr, "اذهب إلى المنجم واضغط على الكتلة التي تريد كسرها.\nاضغط مطولًا على الكتلة أو اضغط زر الحفر.");
            Set("tut_mining_mob", LangAz, "Mədənə get və qırmaq istədiyin bloka toxun.\nBloku basılı saxla və ya QAZ düyməsini bas.");
            Set("tut_mining_mob", LangBe, "Ідзі ў шахту і націсні на блок, які хочаш зламаць.\nУтрымлівай экран на блоку або націсні кнопку КАПАЦЬ.");
            Set("tut_mining_mob", LangBg, "Отиди в мината и докосни блока, който искаш да счупиш.\nЗадръж върху блока или натисни бутона КОПАЙ.");
            Set("tut_mining_mob", LangCa, "Ves a la mina i toca el bloc que vols trencar.\nMantén premut sobre el bloc o prem el botó MINA.");
            Set("tut_mining_mob", LangCs, "Jdi do dolu a klepni na blok, který chceš rozbít.\nPodrž na bloku nebo stiskni tlačítko KOPAT.");
            Set("tut_mining_mob", LangDe, "Gehe zur Mine und tippe auf den Block, den du abbauen willst.\nHalte auf dem Block gedrückt oder drücke die ABBAU-Taste.");
            Set("tut_mining_mob", LangEs, "Ve a la mina y toca el bloque que quieres romper.\nMantén pulsado el bloque o pulsa el botón MINAR.");
            Set("tut_mining_mob", LangFa, "به معدن برو و روی بلوکی که می‌خواهی بشکنی بزن.\nروی بلوک نگه دار یا دکمه استخراج را بزن.");
            Set("tut_mining_mob", LangFr, "Va dans la mine et touche le bloc que tu veux casser.\nMaintiens sur le bloc ou appuie sur le bouton MINER.");
            Set("tut_mining_mob", LangHe, "לך למכרה והקש על הבלוק שברצונך לשבור.\nהחזק על הבלוק או לחץ על כפתור הכרייה.");
            Set("tut_mining_mob", LangHi, "खदान में जाओ और जिस ब्लॉक को तोड़ना है उस पर टैप करो।\nब्लॉक पर दबाए रखो या खनन बटन दबाओ।");
            Set("tut_mining_mob", LangHu, "Menj a bányába, és érintsd meg a blokkot, amit szét akarsz törni.\nTartsd rajta az ujjad, vagy nyomd meg a BÁNYÁSZAT gombot.");
            Set("tut_mining_mob", LangHy, "Գնա հանք և հպիր այն բլոկին, որը ուզում ես կոտրել։\nՊահիր սեղմած բլոկի վրա կամ սեղմիր ՓՈՐԵԼ կոճակը։");
            Set("tut_mining_mob", LangId, "Pergi ke tambang dan ketuk blok yang ingin kamu pecahkan.\nTahan pada blok atau tekan tombol TAMBANG.");
            Set("tut_mining_mob", LangIt, "Vai alla miniera e tocca il blocco che vuoi rompere.\nTieni premuto sul blocco o premi il pulsante SCAVA.");
            Set("tut_mining_mob", LangJa, "鉱山に行き、壊したいブロックをタップしよう。\nそのまま押し続けるか、採掘ボタンを押す。");
            Set("tut_mining_mob", LangKa, "წადი მაღაროში და შეეხე ბლოკს, რომლის გატეხვაც გინდა.\nხელი დაადე ბლოკს ან დააჭირე თხრის ღილაკს.");
            Set("tut_mining_mob", LangKk, "Кенге барып, сындырғың келетін блокты бас.\nБлокты басып ұстап тұр немесе ҚАЗУ батырмасын бас.");
            Set("tut_mining_mob", LangNl, "Ga naar de mijn en tik op het blok dat je wilt breken.\nHoud het blok vast of druk op de MIJN-knop.");
            Set("tut_mining_mob", LangPl, "Idź do kopalni i dotknij bloku, który chcesz rozbić.\nPrzytrzymaj blok albo naciśnij przycisk KOP.");
            Set("tut_mining_mob", LangPt, "Vá até a mina e toque no bloco que deseja quebrar.\nSegure no bloco ou pressione o botão MINERAR.");
            Set("tut_mining_mob", LangRo, "Mergi la mină și atinge blocul pe care vrei să-l spargi.\nȚine apăsat pe bloc sau apasă butonul MINEAZĂ.");
            Set("tut_mining_mob", LangSk, "Choď do bane a ťukni na blok, ktorý chceš rozbiť.\nPodrž na bloku alebo stlač tlačidlo ŤAŽIŤ.");
            Set("tut_mining_mob", LangSr, "Idi u rudnik i dodirni blok koji želiš da razbiješ.\nDrži na bloku ili pritisni dugme KOPAJ.");
            Set("tut_mining_mob", LangTh, "ไปที่เหมืองแล้วแตะบล็อกที่คุณต้องการทุบ\nกดค้างบนบล็อกหรือกดปุ่มขุด");
            Set("tut_mining_mob", LangTk, "Käne git we döwmek isleýän blogyňa bas.\nBloga basyp sakla ýa-da GAZ düwmesini bas.");
            Set("tut_mining_mob", LangUk, "Іди в шахту і торкнись блока, який хочеш зламати.\nЗатисни блок або натисни кнопку КОПАТИ.");
            Set("tut_mining_mob", LangUz, "Konga boring va sindirmoqchi bo‘lgan blokni bosing.\nBlokni bosib turing yoki QAZISH tugmasini bosing.");
            Set("tut_mining_mob", LangVi, "Đi vào mỏ và chạm vào khối bạn muốn phá.\nGiữ trên khối hoặc nhấn nút ĐÀO.");
            Set("tut_mining_mob", LangZh, "进入矿井并点击你想打碎的方块。\n按住方块或按下“挖掘”按钮。");

            Set("tut_mining_pc", LangAr, "اذهب إلى منجمك.\nانقر بزر الفأرة الأيسر على الكتل للحفر.\nاملأ حقيبتك حتى النهاية!");
            Set("tut_mining_pc", LangAz, "Öz mədəninə get.\nQazmaq üçün bloklara sol klik et.\nÇantanı ağzınadək doldur!");
            Set("tut_mining_pc", LangBe, "Ідзі ў сваю шахту.\nНаціскай ЛКМ па блоках, каб капаць.\nЗапоўні заплечнік да краёў!");
            Set("tut_mining_pc", LangBg, "Отиди в своята мина.\nЩракай с левия бутон по блоковете, за да копаеш.\nНапълни раницата догоре!");
            Set("tut_mining_pc", LangCa, "Ves a la teva mina.\nFes clic esquerre als blocs per excavar.\nOmple la motxilla fins dalt!");
            Set("tut_mining_pc", LangCs, "Jdi do svého dolu.\nKlikáním levým tlačítkem těž bloky.\nNaplň batoh až po okraj!");
            Set("tut_mining_pc", LangDe, "Gehe zu deiner Mine.\nKlicke mit der linken Maustaste auf Blöcke, um abzubauen.\nFülle deinen Rucksack bis zum Rand!");
            Set("tut_mining_pc", LangEs, "Ve a tu mina.\nHaz clic izquierdo en los bloques para cavar.\n¡Llena tu mochila hasta arriba!");
            Set("tut_mining_pc", LangFa, "به معدن خود برو.\nبرای کندن روی بلوک‌ها کلیک چپ کن.\nکوله‌پشتی را تا آخر پر کن!");
            Set("tut_mining_pc", LangFr, "Va dans ta mine.\nClique avec le bouton gauche sur les blocs pour miner.\nRemplis ton sac à dos à ras bord !");
            Set("tut_mining_pc", LangHe, "לך למכרה שלך.\nלחץ שמאלי על הבלוקים כדי לכרות.\nמלא את התרמיל עד הסוף!");
            Set("tut_mining_pc", LangHi, "अपनी खान में जाओ।\nब्लॉकों पर बायाँ क्लिक करके खोदो।\nअपना बैग पूरा भर दो!");
            Set("tut_mining_pc", LangHu, "Menj a saját bányádba.\nKattints bal gombbal a blokkokra az ásáshoz.\nTöltsd tele a hátizsákodat!");
            Set("tut_mining_pc", LangHy, "Գնա քո հանքը։\nԲլոկների վրա ձախ քլիք արա փորելու համար։\nԼրացրու ուսապարկդ մինչև վերջ։");
            Set("tut_mining_pc", LangId, "Pergi ke tambangmu.\nKlik kiri pada blok untuk menambang.\nPenuhi ranselmu sampai penuh!");
            Set("tut_mining_pc", LangIt, "Vai alla tua miniera.\nFai clic sinistro sui blocchi per scavare.\nRiempi lo zaino fino all'orlo!");
            Set("tut_mining_pc", LangJa, "自分の鉱山へ行こう。\nブロックを左クリックして掘る。\nリュックをいっぱいにしよう！");
            Set("tut_mining_pc", LangKa, "წადი საკუთარ მაღაროში.\nბლოკებზე მარცხენა კლიკით თხარე.\nზურგჩანთა ბოლომდე აავსე!");
            Set("tut_mining_pc", LangKk, "Өз кенеңе бар.\nҚазу үшін блоктарға тінтуірдің сол жағымен бас.\nРюкзагыңды лық толтыр!");
            Set("tut_mining_pc", LangNl, "Ga naar je mijn.\nKlik met de linkermuisknop op blokken om te graven.\nVul je rugzak tot de rand!");
            Set("tut_mining_pc", LangPl, "Idź do swojej kopalni.\nKlikaj lewym przyciskiem na bloki, aby kopać.\nNapełnij plecak po brzegi!");
            Set("tut_mining_pc", LangPt, "Vá para sua mina.\nClique com o botão esquerdo nos blocos para cavar.\nEncha sua mochila até a borda!");
            Set("tut_mining_pc", LangRo, "Mergi în mina ta.\nDă clic stânga pe blocuri pentru a săpa.\nUmple-ți rucsacul până la refuz!");
            Set("tut_mining_pc", LangSk, "Choď do svojej bane.\nKlikaj ľavým tlačidlom na bloky, aby si ťažil.\nNaplň batoh až po okraj!");
            Set("tut_mining_pc", LangSr, "Idi u svoj rudnik.\nLevim klikom na blokove kopaj.\nNapunI ranac do vrha!");
            Set("tut_mining_pc", LangTh, "ไปที่เหมืองของคุณ\nคลิกซ้ายที่บล็อกเพื่อขุด\nเติมกระเป๋าให้เต็ม!");
            Set("tut_mining_pc", LangTk, "Öz käniňe git.\nGazmak üçin bloga syçanyň çep düwmesi bilen bas.\nSumkaňy doly doldur!");
            Set("tut_mining_pc", LangUk, "Іди у свою шахту.\nНатискай ЛКМ по блоках, щоб копати.\nНаповни рюкзак вщерть!");
            Set("tut_mining_pc", LangUz, "O‘z koningizga boring.\nQazish uchun bloklarga chap tugma bilan bosing.\nRyukzakni to‘ldirib tashlang!");
            Set("tut_mining_pc", LangVi, "Đi tới mỏ của bạn.\nNhấp chuột trái vào các khối để đào.\nLấp đầy ba lô của bạn!");
            Set("tut_mining_pc", LangZh, "前往你的矿井。\n用鼠标左键点击方块进行挖掘。\n把背包装满吧！");

            Set("tut_backpack_title", LangAr, "الحقيبة ممتلئة!");
            Set("tut_backpack_title", LangAz, "Çanta doludur!");
            Set("tut_backpack_title", LangBe, "ЗАПЛЕЧНІК ПОВЕН!");
            Set("tut_backpack_title", LangBg, "РАНИЦАТА Е ПЪЛНА!");
            Set("tut_backpack_title", LangCa, "MOTXILLA PLENA!");
            Set("tut_backpack_title", LangCs, "BATOH PLNÝ!");
            Set("tut_backpack_title", LangDe, "RUCKSACK VOLL!");
            Set("tut_backpack_title", LangEs, "¡MOCHILA LLENA!");
            Set("tut_backpack_title", LangFa, "کوله‌پشتی پر است!");
            Set("tut_backpack_title", LangFr, "SAC À DOS PLEIN !");
            Set("tut_backpack_title", LangHe, "התרמיל מלא!");
            Set("tut_backpack_title", LangHi, "बैग भर गया!");
            Set("tut_backpack_title", LangHu, "MEGTELT A HÁTIZSÁK!");
            Set("tut_backpack_title", LangHy, "ՈՒՍԱՊԱՐԿԸ ԼԻՆ Է!");
            Set("tut_backpack_title", LangId, "RANSEL PENUH!");
            Set("tut_backpack_title", LangIt, "ZAINO PIENO!");
            Set("tut_backpack_title", LangJa, "リュックがいっぱい！");
            Set("tut_backpack_title", LangKa, "ზურგჩანთა სავსეა!");
            Set("tut_backpack_title", LangKk, "РЮКЗАК ТОЛДЫ!");
            Set("tut_backpack_title", LangNl, "RUGZAK VOL!");
            Set("tut_backpack_title", LangPl, "PLECAK PEŁNY!");
            Set("tut_backpack_title", LangPt, "MOCHILA CHEIA!");
            Set("tut_backpack_title", LangRo, "RUCSACUL E PLIN!");
            Set("tut_backpack_title", LangSk, "BATOH JE PLNÝ!");
            Set("tut_backpack_title", LangSr, "RANAC JE PUN!");
            Set("tut_backpack_title", LangTh, "กระเป๋าเต็ม!");
            Set("tut_backpack_title", LangTk, "SUMKA DOLDY!");
            Set("tut_backpack_title", LangUk, "РЮКЗАК ПОВНИЙ!");
            Set("tut_backpack_title", LangUz, "RYUKZAK TO‘LDI!");
            Set("tut_backpack_title", LangVi, "BA LÔ ĐẦY!");
            Set("tut_backpack_title", LangZh, "背包已满！");

            Set("tut_backpack_body", LangAr, "عمل رائع! حان وقت التفريغ.\nارجع إلى الردهة — سيُظهر الشعاع نقطة بيع الخام.");
            Set("tut_backpack_body", LangAz, "Əla iş! Boşaltma vaxtıdır.\nLobbiyə qayıt — şüa filiz satış nöqtəsini göstərəcək.");
            Set("tut_backpack_body", LangBe, "Выдатна! Час разгрузіцца.\nВярніся ў лобі — прамень пакажа кропку продажу руды.");
            Set("tut_backpack_body", LangBg, "Страхотна! Време е да разтовариш.\nВърни се в лобито — лъчът ще покаже мястото за продажба на руда.");
            Set("tut_backpack_body", LangCa, "Molt bé! És hora de descarregar.\nTorna al vestíbul — el feix mostrarà el punt de venda del mineral.");
            Set("tut_backpack_body", LangCs, "Skvělá práce! Je čas vyložit náklad.\nVrať se do lobby — paprsek ukáže místo prodeje rudy.");
            Set("tut_backpack_body", LangDe, "Gute Arbeit! Zeit zum Ausladen.\nGeh zurück in die Lobby — der Strahl zeigt den Erzverkaufspunkt.");
            Set("tut_backpack_body", LangEs, "¡Buen trabajo! Hora de descargar.\nVuelve al lobby — el rayo mostrará el punto de venta de mineral.");
            Set("tut_backpack_body", LangFa, "آفرین! وقت خالی کردن بار است.\nبه لابی برگرد — پرتو محل فروش سنگ معدن را نشان می‌دهد.");
            Set("tut_backpack_body", LangFr, "Bien joué ! Il est temps de décharger.\nRetourne au lobby — le faisceau indiquera le point de vente du minerai.");
            Set("tut_backpack_body", LangHe, "עבודה טובה! הגיע הזמן לפרוק.\nחזור ללובי — הקרן תראה את נקודת מכירת העפרה.");
            Set("tut_backpack_body", LangHi, "शानदार! अब खाली करने का समय है।\nलॉबी में लौटो — किरण अयस्क बेचने की जगह दिखाएगी।");
            Set("tut_backpack_body", LangHu, "Szép munka! Ideje kipakolni.\nMenj vissza a lobbyba — a fénysugár megmutatja az érceladási pontot.");
            Set("tut_backpack_body", LangHy, "Գերազանց։ Ժամանակն է բեռնաթափվելու։\nՎերադարձիր լոբբի — լույսի շողը ցույց կտա հանքաքարի վաճառքի տեղը։");
            Set("tut_backpack_body", LangId, "Kerja bagus! Saatnya mengosongkan.\nKembali ke lobi — sorot cahaya akan menunjukkan titik penjualan bijih.");
            Set("tut_backpack_body", LangIt, "Ottimo lavoro! È ora di scaricare.\nTorna alla lobby — il raggio mostrerà il punto vendita del minerale.");
            Set("tut_backpack_body", LangJa, "よくやった！ 売りに行こう。\nロビーに戻ると、光の柱が鉱石売却地点を示す。");
            Set("tut_backpack_body", LangKa, "ყოჩაღ! დროა გადმოტვირთო.\nდაბრუნდი ლობიში — სხივი მადნის გაყიდვის წერტილს გაჩვენებს.");
            Set("tut_backpack_body", LangKk, "Керемет! Енді босататын уақыт.\nЛоббиге қайт — сәуле кен сату орнын көрсетеді.");
            Set("tut_backpack_body", LangNl, "Goed gedaan! Tijd om te lossen.\nGa terug naar de lobby — de lichtstraal toont het verkooppunt voor erts.");
            Set("tut_backpack_body", LangPl, "Świetna robota! Czas się rozładować.\nWróć do lobby — promień pokaże punkt sprzedaży rudy.");
            Set("tut_backpack_body", LangPt, "Bom trabalho! Hora de descarregar.\nVolte ao lobby — o feixe mostrará o ponto de venda do minério.");
            Set("tut_backpack_body", LangRo, "Treabă bună! E timpul să descarci.\nÎntoarce-te în lobby — raza va arăta punctul de vânzare a minereului.");
            Set("tut_backpack_body", LangSk, "Výborne! Je čas vyložiť náklad.\nVráť sa do lobby — lúč ukáže miesto predaja rudy.");
            Set("tut_backpack_body", LangSr, "Sjajno! Vreme je da istovariš.\nVrati se u lobi — zrak će pokazati mesto prodaje rude.");
            Set("tut_backpack_body", LangTh, "ยอดเยี่ยม! ถึงเวลาขนของออกแล้ว\nกลับไปที่ล็อบบี้ — ลำแสงจะชี้จุดขายแร่");
            Set("tut_backpack_body", LangTk, "Gaty gowy! Indi boşatmagyň wagty.\nLobbä dolan — şöhle magdan satylýan ýeri görkezer.");
            Set("tut_backpack_body", LangUk, "Чудова робота! Час розвантажитись.\nПовернися до лобі — промінь покаже точку продажу руди.");
            Set("tut_backpack_body", LangUz, "Zo‘r ish! Endi tushirish vaqti.\nLobbiga qayting — nur ruda sotish nuqtasini ko‘rsatadi.");
            Set("tut_backpack_body", LangVi, "Làm tốt lắm! Đến lúc dỡ hàng rồi.\nQuay lại sảnh — tia sáng sẽ chỉ điểm bán quặng.");
            Set("tut_backpack_body", LangZh, "干得好！该去卸货了。\n返回大厅 — 光柱会指向矿石出售点。");

            Set("tut_sell_title", LangAr, "بِع الخام");
            Set("tut_sell_title", LangAz, "Filizi sat");
            Set("tut_sell_title", LangBe, "ПРАДАЙ РУДУ");
            Set("tut_sell_title", LangBg, "ПРОДАЙ РУДАТА");
            Set("tut_sell_title", LangCa, "VEN MINERAL");
            Set("tut_sell_title", LangCs, "PRODEJ RUDU");
            Set("tut_sell_title", LangDe, "ERZ VERKAUFEN");
            Set("tut_sell_title", LangEs, "VENDE EL MINERAL");
            Set("tut_sell_title", LangFa, "سنگ معدن را بفروش");
            Set("tut_sell_title", LangFr, "VENDRE LE MINERAI");
            Set("tut_sell_title", LangHe, "מכור עפרה");
            Set("tut_sell_title", LangHi, "अयस्क बेचो");
            Set("tut_sell_title", LangHu, "ADD EL AZ ÉRCET");
            Set("tut_sell_title", LangHy, "ՎԱՃԱՌԻՐ ՀԱՆՔԱՔԱՐԸ");
            Set("tut_sell_title", LangId, "JUAL BIJIH");
            Set("tut_sell_title", LangIt, "VENDI IL MINERALE");
            Set("tut_sell_title", LangJa, "鉱石を売る");
            Set("tut_sell_title", LangKa, "გაყიდე მადანი");
            Set("tut_sell_title", LangKk, "КЕНДІ САТ");
            Set("tut_sell_title", LangNl, "VERKOOP ERTS");
            Set("tut_sell_title", LangPl, "SPRZEDAJ RUDĘ");
            Set("tut_sell_title", LangPt, "VENDER MINÉRIO");
            Set("tut_sell_title", LangRo, "VINDE MINEREUL");
            Set("tut_sell_title", LangSk, "PREDAJ RUDU");
            Set("tut_sell_title", LangSr, "PRODAJ RUDU");
            Set("tut_sell_title", LangTh, "ขายแร่");
            Set("tut_sell_title", LangTk, "MAGDANY SAT");
            Set("tut_sell_title", LangUk, "ПРОДАЙ РУДУ");
            Set("tut_sell_title", LangUz, "RUDANI SOT");
            Set("tut_sell_title", LangVi, "BÁN QUẶNG");
            Set("tut_sell_title", LangZh, "出售矿石");

            Set("tut_sell_body", LangAr, "اذهب إلى نقطة البيع وسلّم محتويات حقيبتك.\nيشير الشعاع إلى الطريق.");
            Set("tut_sell_body", LangAz, "Satış nöqtəsinə get və çantandakıları təhvil ver.\nŞüa yolu göstərir.");
            Set("tut_sell_body", LangBe, "Ідзі да кропкі продажу і здай змесціва заплечніка.\nПрамень паказвае шлях.");
            Set("tut_sell_body", LangBg, "Отиди до точката за продажба и предай съдържанието на раницата.\nЛъчът показва пътя.");
            Set("tut_sell_body", LangCa, "Ves al punt de venda i entrega el contingut de la motxilla.\nEl feix t'indica el camí.");
            Set("tut_sell_body", LangCs, "Jdi na prodejní místo a odevzdej obsah batohu.\nPaprsek ukazuje cestu.");
            Set("tut_sell_body", LangDe, "Gehe zum Verkaufspunkt und gib den Inhalt deines Rucksacks ab.\nDer Strahl zeigt dir den Weg.");
            Set("tut_sell_body", LangEs, "Ve al punto de venta y entrega el contenido de tu mochila.\nEl rayo marca el camino.");
            Set("tut_sell_body", LangFa, "به نقطه فروش برو و محتویات کوله‌پشتی را تحویل بده.\nپرتو مسیر را نشان می‌دهد.");
            Set("tut_sell_body", LangFr, "Va au point de vente et remets le contenu de ton sac.\nLe faisceau indique le chemin.");
            Set("tut_sell_body", LangHe, "לך לנקודת המכירה ומסור את תכולת התרמיל.\nהקרן מראה את הדרך.");
            Set("tut_sell_body", LangHi, "बिक्री बिंदु पर जाओ और बैग की सामग्री जमा करो।\nकिरण रास्ता दिखाएगी।");
            Set("tut_sell_body", LangHu, "Menj az eladási ponthoz, és add le a hátizsák tartalmát.\nA fénysugár mutatja az utat.");
            Set("tut_sell_body", LangHy, "Գնա վաճառքի կետ և հանձնիր ուսապարկի պարունակությունը։\nԼույսի շողը ցույց է տալիս ճանապարհը։");
            Set("tut_sell_body", LangId, "Pergi ke titik penjualan dan serahkan isi ranselmu.\nSorot cahaya menunjukkan jalannya.");
            Set("tut_sell_body", LangIt, "Vai al punto vendita e consegna il contenuto dello zaino.\nIl raggio indica la strada.");
            Set("tut_sell_body", LangJa, "売却ポイントへ行き、リュックの中身を渡そう。\n光の柱が道を示す。");
            Set("tut_sell_body", LangKa, "მიდი გაყიდვის წერტილთან და ჩააბარე ზურგჩანთის შიგთავსი.\nსხივი გზას გაჩვენებს.");
            Set("tut_sell_body", LangKk, "Сату нүктесіне бар да, рюкзактағы заттарды өткіз.\nСәуле жолды көрсетеді.");
            Set("tut_sell_body", LangNl, "Ga naar het verkooppunt en lever de inhoud van je rugzak in.\nDe lichtstraal wijst de weg.");
            Set("tut_sell_body", LangPl, "Idź do punktu sprzedaży i oddaj zawartość plecaka.\nPromień wskazuje drogę.");
            Set("tut_sell_body", LangPt, "Vá ao ponto de venda e entregue o conteúdo da mochila.\nO feixe mostra o caminho.");
            Set("tut_sell_body", LangRo, "Mergi la punctul de vânzare și predă conținutul rucsacului.\nRaza arată drumul.");
            Set("tut_sell_body", LangSk, "Choď na predajné miesto a odovzdaj obsah batohu.\nLúč ukazuje cestu.");
            Set("tut_sell_body", LangSr, "Idi do mesta prodaje i predaj sadržaj rancA.\nZrak pokazuje put.");
            Set("tut_sell_body", LangTh, "ไปยังจุดขายและส่งมอบของในกระเป๋า\nลำแสงจะชี้ทางให้");
            Set("tut_sell_body", LangTk, "Satyş ýerine git we sumkaňdaky zatlary tabşyr.\nŞöhle ýoly görkezýär.");
            Set("tut_sell_body", LangUk, "Іди до точки продажу і здай вміст рюкзака.\nПромінь покаже шлях.");
            Set("tut_sell_body", LangUz, "Sotish nuqtasiga boring va ryukzakdagi narsalarni topshiring.\nNur yo‘lni ko‘rsatadi.");
            Set("tut_sell_body", LangVi, "Đi tới điểm bán và giao nộp đồ trong ba lô.\nTia sáng sẽ chỉ đường.");
            Set("tut_sell_body", LangZh, "前往出售点并交出背包里的内容。\n光柱会指引道路。");

            Set("tut_upgrade_title", LangAr, "طوّر المعدات");
            Set("tut_upgrade_title", LangAz, "Avadanlığı təkmilləşdir");
            Set("tut_upgrade_title", LangBe, "ПАЛЕПШЫ СНАРАЖЭННЕ");
            Set("tut_upgrade_title", LangBg, "ПОДОБРИ ЕКИПИРОВКАТА");
            Set("tut_upgrade_title", LangCa, "MILLORA L'EQUIP");
            Set("tut_upgrade_title", LangCs, "VYLEPŠI VYBAVENÍ");
            Set("tut_upgrade_title", LangDe, "AUSRÜSTUNG VERBESSERN");
            Set("tut_upgrade_title", LangEs, "MEJORA EL EQUIPO");
            Set("tut_upgrade_title", LangFa, "تجهیزات را ارتقا بده");
            Set("tut_upgrade_title", LangFr, "AMÉLIORE L'ÉQUIPEMENT");
            Set("tut_upgrade_title", LangHe, "שדרג ציוד");
            Set("tut_upgrade_title", LangHi, "उपकरण उन्नत करो");
            Set("tut_upgrade_title", LangHu, "FEJLESZD A FELSZERELÉST");
            Set("tut_upgrade_title", LangHy, "ԲԱՐԵԼԱՎԻՐ ՍԱՐՔԱՎՈՐՈՒՄԸ");
            Set("tut_upgrade_title", LangId, "TINGKATKAN PERALATAN");
            Set("tut_upgrade_title", LangIt, "MIGLIORA L'EQUIPAGGIAMENTO");
            Set("tut_upgrade_title", LangJa, "装備を強化");
            Set("tut_upgrade_title", LangKa, "გააუმჯობესე აღჭურვილობა");
            Set("tut_upgrade_title", LangKk, "ЖАБДЫҚТЫ ЖАҚСАРТ");
            Set("tut_upgrade_title", LangNl, "UPGRADE UITRUSTING");
            Set("tut_upgrade_title", LangPl, "ULEPSZ SPRZĘT");
            Set("tut_upgrade_title", LangPt, "MELHORE O EQUIPAMENTO");
            Set("tut_upgrade_title", LangRo, "ÎMBUNĂTĂȚEȘTE ECHIPAMENTUL");
            Set("tut_upgrade_title", LangSk, "VYLEPŠI VÝBAVU");
            Set("tut_upgrade_title", LangSr, "NADOGRADI OPREMU");
            Set("tut_upgrade_title", LangTh, "อัปเกรดอุปกรณ์");
            Set("tut_upgrade_title", LangTk, "ENJAMY GOWULAŞDYR");
            Set("tut_upgrade_title", LangUk, "ПОКРАЩ СПОРЯДЖЕННЯ");
            Set("tut_upgrade_title", LangUz, "JIHOZNI YAXSHILA");
            Set("tut_upgrade_title", LangVi, "NÂNG CẤP TRANG BỊ");
            Set("tut_upgrade_title", LangZh, "升级装备");

            Set("tut_upgrade_body", LangAr, "هل تريد الحفر أسرع والوصول إلى خامات نادرة؟\nاذهب إلى متجر الفؤوس وطوّر معداتك.");
            Set("tut_upgrade_body", LangAz, "Daha sürətli qazmaq və nadir filizlərə çatmaq istəyirsən?\nBaltalar mağazasına get və avadanlığını təkmilləşdir.");
            Set("tut_upgrade_body", LangBe, "Хочаш капаць хутчэй і дабрацца да рэдкай руды?\nІдзі ў краму кіркаў і палепшы сваё снаражэнне.");
            Set("tut_upgrade_body", LangBg, "Искаш да копаеш по-бързо и да стигнеш до редки руди?\nОтиди в магазина за кирки и подобри екипировката си.");
            Set("tut_upgrade_body", LangCa, "Vols excavar més ràpid i arribar a minerals rars?\nVes a la botiga de pics i millora l'equip.");
            Set("tut_upgrade_body", LangCs, "Chceš kopat rychleji a dostat se ke vzácným rudám?\nJdi do obchodu s krumpáči a vylepši výbavu.");
            Set("tut_upgrade_body", LangDe, "Willst du schneller abbauen und seltene Erze erreichen?\nGeh zum Spitzhackenladen und verbessere deine Ausrüstung.");
            Set("tut_upgrade_body", LangEs, "¿Quieres cavar más rápido y llegar a minerales raros?\nVe a la tienda de picos y mejora tu equipo.");
            Set("tut_upgrade_body", LangFa, "می‌خواهی سریع‌تر حفاری کنی و به سنگ‌های کمیاب برسی؟\nبه فروشگاه کلنگ برو و تجهیزاتت را ارتقا بده.");
            Set("tut_upgrade_body", LangFr, "Tu veux creuser plus vite et atteindre des minerais rares ?\nVa à la boutique de pioches et améliore ton équipement.");
            Set("tut_upgrade_body", LangHe, "רוצה לכרות מהר יותר ולהגיע לעפרות נדירות?\nלך לחנות המכושים ושדרג את הציוד שלך.");
            Set("tut_upgrade_body", LangHi, "क्या तुम तेज़ी से खोदना और दुर्लभ अयस्क तक पहुँचना चाहते हो?\nकुदाल की दुकान पर जाओ और अपना उपकरण उन्नत करो।");
            Set("tut_upgrade_body", LangHu, "Gyorsabban akarsz ásni és ritka ércekhez eljutni?\nMenj a csákányboltba, és fejleszd a felszerelésed.");
            Set("tut_upgrade_body", LangHy, "Ուզո՞ւմ ես ավելի արագ փորել և հասնել հազվագյուտ հանքաքարերի։\nԳնա քլունգների խանութ և բարելավիր սարքավորումը։");
            Set("tut_upgrade_body", LangId, "Ingin menambang lebih cepat dan mencapai bijih langka?\nPergi ke toko beliung dan tingkatkan perlengkapanmu.");
            Set("tut_upgrade_body", LangIt, "Vuoi scavare più in fretta e raggiungere minerali rari?\nVai al negozio di picconi e migliora il tuo equipaggiamento.");
            Set("tut_upgrade_body", LangJa, "もっと速く掘ってレア鉱石に届きたい？\nツルハシショップへ行って装備を強化しよう。");
            Set("tut_upgrade_body", LangKa, "გინდა უფრო სწრაფად თხრა და იშვიათ მადნებამდე მიღწევა?\nწადი წერაქვების მაღაზიაში და გააუმჯობესე აღჭურვილობა.");
            Set("tut_upgrade_body", LangKk, "Тезірек қазып, сирек кендерге жеткің келе ме?\nКирка дүкеніне барып, жабдығыңды жақсарт.");
            Set("tut_upgrade_body", LangNl, "Wil je sneller graven en zeldzame ertsen bereiken?\nGa naar de pikhouwwinkel en upgrade je uitrusting.");
            Set("tut_upgrade_body", LangPl, "Chcesz kopać szybciej i docierać do rzadkich rud?\nIdź do sklepu z kilofami i ulepsz swój sprzęt.");
            Set("tut_upgrade_body", LangPt, "Quer cavar mais rápido e alcançar minérios raros?\nVá à loja de picaretas e melhore seu equipamento.");
            Set("tut_upgrade_body", LangRo, "Vrei să sapi mai repede și să ajungi la minereuri rare?\nMergi la magazinul de târnăcoape și îmbunătățește-ți echipamentul.");
            Set("tut_upgrade_body", LangSk, "Chceš kopať rýchlejšie a dostať sa k vzácnym rudám?\nChoď do obchodu s krompáčmi a vylepši si výbavu.");
            Set("tut_upgrade_body", LangSr, "Želiš da kopaš brže i dođeš do retkih ruda?\nIdi u prodavnicu pijuka i nadogradi opremu.");
            Set("tut_upgrade_body", LangTh, "อยากขุดให้เร็วขึ้นและเข้าถึงแร่หายากไหม?\nไปที่ร้านพลั่วแล้วอัปเกรดอุปกรณ์ของคุณ");
            Set("tut_upgrade_body", LangTk, "Has çalt gazmak we seýrek magdanlara ýetmek isleýärsiňmi?\nPikir dükanyna git we enjamlaryňy gowulaşdyr.");
            Set("tut_upgrade_body", LangUk, "Хочеш копати швидше й дістатися до рідкісної руди?\nІди в магазин кирок і покращ своє спорядження.");
            Set("tut_upgrade_body", LangUz, "Tezroq qazishni va noyob rudalarga yetishni xohlaysanmi?\nCho‘kich do‘koniga bor va jihozingni yaxshila.");
            Set("tut_upgrade_body", LangVi, "Muốn đào nhanh hơn và chạm tới quặng hiếm?\nHãy tới cửa hàng cuốc chim và nâng cấp trang bị.");
            Set("tut_upgrade_body", LangZh, "想挖得更快并接触稀有矿石吗？\n前往镐子商店升级你的装备。");

            Set("tut_minion_title", LangAr, "أتمتة المنجم");
            Set("tut_minion_title", LangAz, "Mədəni avtomatlaşdır");
            Set("tut_minion_title", LangBe, "АЎТАМАТЫЗУЙ ШАХТУ");
            Set("tut_minion_title", LangBg, "АВТОМАТИЗИРАЙ МИНАТА");
            Set("tut_minion_title", LangCa, "AUTOMATITZA LA MINA");
            Set("tut_minion_title", LangCs, "AUTOMATIZUJ DŮL");
            Set("tut_minion_title", LangDe, "MINE AUTOMATISIEREN");
            Set("tut_minion_title", LangEs, "AUTOMATIZA LA MINA");
            Set("tut_minion_title", LangFa, "معدن را خودکار کن");
            Set("tut_minion_title", LangFr, "AUTOMATISE LA MINE");
            Set("tut_minion_title", LangHe, "הפוך את המכרה לאוטומטי");
            Set("tut_minion_title", LangHi, "खान को स्वचालित करो");
            Set("tut_minion_title", LangHu, "AUTOMATIZÁLD A BÁNYÁT");
            Set("tut_minion_title", LangHy, "ԱՎՏՈՄԱՏԱՑՐՈՒ ՀԱՆՔԸ");
            Set("tut_minion_title", LangId, "OTOMATISKAN TAMBANG");
            Set("tut_minion_title", LangIt, "AUTOMATIZZA LA MINIERA");
            Set("tut_minion_title", LangJa, "鉱山を自動化");
            Set("tut_minion_title", LangKa, "მაღაროს ავტომატიზაცია");
            Set("tut_minion_title", LangKk, "КЕНДІ АВТОМАТТАНДЫР");
            Set("tut_minion_title", LangNl, "AUTOMATISEER DE MIJN");
            Set("tut_minion_title", LangPl, "ZAUTOMATYZUJ KOPALNIĘ");
            Set("tut_minion_title", LangPt, "AUTOMATIZE A MINA");
            Set("tut_minion_title", LangRo, "AUTOMATIZEAZĂ MINA");
            Set("tut_minion_title", LangSk, "AUTOMATIZUJ BAŇU");
            Set("tut_minion_title", LangSr, "AUTOMATIZUJ RUDNIK");
            Set("tut_minion_title", LangTh, "ทำเหมืองอัตโนมัติ");
            Set("tut_minion_title", LangTk, "KÄNI AWTOMATLAŞDYR");
            Set("tut_minion_title", LangUk, "АВТОМАТИЗУЙ ШАХТУ");
            Set("tut_minion_title", LangUz, "KONNI AVTOMATLASHTIR");
            Set("tut_minion_title", LangVi, "TỰ ĐỘNG HÓA MỎ");
            Set("tut_minion_title", LangZh, "自动化矿井");

            Set("tut_minion_body", LangAr, "إذا أردت أتمتة التعدين —\nتعال إلى هنا واستأجر أول عامل مينيون!\n\nاضغط أي مفتاح / المس للإغلاق.");
            Set("tut_minion_body", LangAz, "Qazmanı avtomatlaşdırmaq istəyirsənsə —\nbura gəl və ilk minyon işçini işə götür!\n\nBağlamaq üçün düymə bas / toxun.");
            Set("tut_minion_body", LangBe, "Калі хочаш аўтаматызаваць здабычу —\nпрыйдзі сюды і наймі свайго першага рабочага-міньёна!\n\nНацісні клавішу / тапні, каб закрыць.");
            Set("tut_minion_body", LangBg, "Ако искаш да автоматизираш добива —\nела тук и наеми първия си работник миньон!\n\nНатисни бутон / докосни, за да затвориш.");
            Set("tut_minion_body", LangCa, "Si vols automatitzar la mineria —\nvingues aquí i contracta el teu primer treballador minion!\n\nPrem una tecla / toca per tancar.");
            Set("tut_minion_body", LangCs, "Jestli chceš automatizovat těžbu —\npřijď sem a najmi si prvního pomocníka!\n\nStiskni klávesu / klepni pro zavření.");
            Set("tut_minion_body", LangDe, "Wenn du den Abbau automatisieren willst —\nkomm hierher und stelle deinen ersten Minion-Arbeiter ein!\n\nDrücke eine Taste / tippe zum Schließen.");
            Set("tut_minion_body", LangEs, "Si quieres automatizar la minería —\nven aquí y contrata a tu primer minion trabajador.\n\nPulsa una tecla / toca para cerrar.");
            Set("tut_minion_body", LangFa, "اگر می‌خواهی استخراج را خودکار کنی —\nبه اینجا بیا و اولین مینیون کارگرت را استخدام کن!\n\nبرای بستن کلیدی بزن / لمس کن.");
            Set("tut_minion_body", LangFr, "Si tu veux automatiser l'extraction —\nviens ici et engage ton premier ouvrier minion !\n\nAppuie sur une touche / touche pour fermer.");
            Set("tut_minion_body", LangHe, "אם אתה רוצה להפוך את הכרייה לאוטומטית —\nבוא לכאן ושכור את המיניון הראשון שלך!\n\nלחץ על מקש / הקש כדי לסגור.");
            Set("tut_minion_body", LangHi, "अगर तुम खनन को स्वचालित करना चाहते हो —\nयहाँ आओ और अपना पहला मिनियन कर्मचारी रखो!\n\nबंद करने के लिए कोई कुंजी दबाओ / टैप करो।");
            Set("tut_minion_body", LangHu, "Ha automatizálni akarod a bányászatot —\ngyere ide, és fogadd fel az első minion munkásodat!\n\nNyomj meg egy gombot / érintsd meg a bezáráshoz.");
            Set("tut_minion_body", LangHy, "Եթե ուզում ես ավտոմատացնել հանքափորումը —\nարի այստեղ և վարձիր քո առաջին մինյոն աշխատակցին։\n\nՓակելու համար սեղմիր կոճակ / հպիր։");
            Set("tut_minion_body", LangId, "Kalau ingin menambang otomatis —\ndatang ke sini dan rekrut pekerja minion pertamamu!\n\nTekan tombol / ketuk untuk menutup.");
            Set("tut_minion_body", LangIt, "Se vuoi automatizzare l'estrazione —\nvieni qui e assumi il tuo primo minion lavoratore!\n\nPremi un tasto / tocca per chiudere.");
            Set("tut_minion_body", LangJa, "採掘を自動化したいなら —\nここへ来て最初のミニオン作業員を雇おう！\n\n何かキーを押す / タップで閉じる。");
            Set("tut_minion_body", LangKa, "თუ გინდა მოპოვება ავტომატური გახადო —\nმოდი აქ და დაიქირავე შენი პირველი მინიონ-მუშა!\n\nდასახურად დააჭირე ღილაკს / შეეხე.");
            Set("tut_minion_body", LangKk, "Егер өндірісті автоматтандырғың келсе —\nосында кел де, алғашқы миньон-жұмысшыңды жалда!\n\nЖабу үшін батырма бас / түрт.");
            Set("tut_minion_body", LangNl, "Als je het delven wilt automatiseren —\nkom hierheen en huur je eerste minion-werker in!\n\nDruk op een toets / tik om te sluiten.");
            Set("tut_minion_body", LangPl, "Jeśli chcesz zautomatyzować wydobycie —\nprzyjdź tutaj i zatrudnij swojego pierwszego miniona!\n\nNaciśnij klawisz / dotknij, aby zamknąć.");
            Set("tut_minion_body", LangPt, "Se quiser automatizar a mineração —\nvenha aqui e contrate seu primeiro trabalhador minion!\n\nPressione uma tecla / toque para fechar.");
            Set("tut_minion_body", LangRo, "Dacă vrei să automatizezi mineritul —\nvin-o aici și angajează primul tău minion muncitor!\n\nApasă o tastă / atinge pentru a închide.");
            Set("tut_minion_body", LangSk, "Ak chceš automatizovať ťažbu —\npríď sem a najmi si svojho prvého miniona!\n\nStlač kláves / ťukni pre zavretie.");
            Set("tut_minion_body", LangSr, "Ako želiš da automatizuješ rudarenje —\ndođi ovde i zaposli svog prvog miniona!\n\nPritisni taster / dodirni za zatvaranje.");
            Set("tut_minion_body", LangTh, "ถ้าคุณอยากให้การขุดเป็นอัตโนมัติ —\nมาที่นี่แล้วจ้างมินเนียนคนแรกของคุณ!\n\nกดปุ่มหรือแตะเพื่อปิด");
            Set("tut_minion_body", LangTk, "Gazmagy awtomatlaşdyrmak isleseň —\nbu ýere gel we ilkinji minion işgäriňi işe al!\n\nÝapmak üçin düwme bas / ekrana deg.");
            Set("tut_minion_body", LangUk, "Якщо хочеш автоматизувати видобуток —\nприйди сюди й найми свого першого міньйона!\n\nНатисни клавішу / торкнись, щоб закрити.");
            Set("tut_minion_body", LangUz, "Agar qazishni avtomatlashtirmoqchi bo‘lsang —\nbu yerga kel va birinchi minion ishchingni yolla!\n\nYopish uchun tugma bos / tegin.");
            Set("tut_minion_body", LangVi, "Nếu muốn tự động hóa việc khai thác —\nhãy đến đây và thuê tay sai đầu tiên của bạn!\n\nNhấn phím / chạm để đóng.");
            Set("tut_minion_body", LangZh, "如果你想自动化采矿 —\n来到这里并雇佣你的第一个随从工人！\n\n按键 / 点击即可关闭。");

            Set("tut_tap_hint", LangAr, "المس الشاشة للمتابعة ›");
            Set("tut_tap_hint", LangAz, "Davam etmək üçün ekrana toxun ›");
            Set("tut_tap_hint", LangBe, "Націсні на экран, каб працягнуць ›");
            Set("tut_tap_hint", LangBg, "Докосни екрана, за да продължиш ›");
            Set("tut_tap_hint", LangCa, "Toca la pantalla per continuar ›");
            Set("tut_tap_hint", LangCs, "Klepni na obrazovku pro pokračování ›");
            Set("tut_tap_hint", LangDe, "Tippe auf den Bildschirm, um fortzufahren ›");
            Set("tut_tap_hint", LangEs, "Toca la pantalla para continuar ›");
            Set("tut_tap_hint", LangFa, "برای ادامه روی صفحه بزن ›");
            Set("tut_tap_hint", LangFr, "Touchez l'écran pour continuer ›");
            Set("tut_tap_hint", LangHe, "הקש על המסך כדי להמשיך ›");
            Set("tut_tap_hint", LangHi, "जारी रखने के लिए स्क्रीन पर टैप करें ›");
            Set("tut_tap_hint", LangHu, "Érintsd meg a képernyőt a folytatáshoz ›");
            Set("tut_tap_hint", LangHy, "Հպիր էկրանին՝ շարունակելու համար ›");
            Set("tut_tap_hint", LangId, "Ketuk layar untuk lanjut ›");
            Set("tut_tap_hint", LangIt, "Tocca lo schermo per continuare ›");
            Set("tut_tap_hint", LangJa, "続けるには画面をタップ ›");
            Set("tut_tap_hint", LangKa, "გასაგრძელებლად შეეხე ეკრანს ›");
            Set("tut_tap_hint", LangKk, "Жалғастыру үшін экранды бас ›");
            Set("tut_tap_hint", LangNl, "Tik op het scherm om verder te gaan ›");
            Set("tut_tap_hint", LangPl, "Dotknij ekranu, aby kontynuować ›");
            Set("tut_tap_hint", LangPt, "Toque na tela para continuar ›");
            Set("tut_tap_hint", LangRo, "Atinge ecranul pentru a continua ›");
            Set("tut_tap_hint", LangSk, "Ťukni na obrazovku pre pokračovanie ›");
            Set("tut_tap_hint", LangSr, "Dodirni ekran za nastavak ›");
            Set("tut_tap_hint", LangTh, "แตะหน้าจอเพื่อเล่นต่อ ›");
            Set("tut_tap_hint", LangTk, "Dowam etmek üçin ekrana bas ›");
            Set("tut_tap_hint", LangUk, "Торкнися екрана, щоб продовжити ›");
            Set("tut_tap_hint", LangUz, "Davom etish uchun ekranga teging ›");
            Set("tut_tap_hint", LangVi, "Chạm màn hình để tiếp tục ›");
            Set("tut_tap_hint", LangZh, "点击屏幕继续 ›");

            Set("tut_create_island_title", LangAr, "أنشئ الجزيرة");
            Set("tut_create_island_title", LangAz, "Ada yarat");
            Set("tut_create_island_title", LangBe, "СТВАРЫ ВОСТРАЎ");
            Set("tut_create_island_title", LangBg, "СЪЗДАЙ ОСТРОВ");
            Set("tut_create_island_title", LangCa, "CREA L'ILLA");
            Set("tut_create_island_title", LangCs, "VYTVOŘ OSTROV");
            Set("tut_create_island_title", LangDe, "INSEL ERSTELLEN");
            Set("tut_create_island_title", LangEs, "CREAR ISLA");
            Set("tut_create_island_title", LangFa, "جزیره بساز");
            Set("tut_create_island_title", LangFr, "CRÉER L'ÎLE");
            Set("tut_create_island_title", LangHe, "צור אי");
            Set("tut_create_island_title", LangHi, "द्वीप बनाओ");
            Set("tut_create_island_title", LangHu, "SZIGET LÉTREHOZÁSA");
            Set("tut_create_island_title", LangHy, "ՍՏԵՂԾԵԼ ԿՂԶԻ");
            Set("tut_create_island_title", LangId, "BUAT PULAU");
            Set("tut_create_island_title", LangIt, "CREA ISOLA");
            Set("tut_create_island_title", LangJa, "島を作る");
            Set("tut_create_island_title", LangKa, "შექმენი კუნძული");
            Set("tut_create_island_title", LangKk, "АРАЛ ЖАСА");
            Set("tut_create_island_title", LangNl, "MAAK EILAND");
            Set("tut_create_island_title", LangPl, "STWÓRZ WYSPĘ");
            Set("tut_create_island_title", LangPt, "CRIAR ILHA");
            Set("tut_create_island_title", LangRo, "CREEAZĂ INSULA");
            Set("tut_create_island_title", LangSk, "VYTVOR OSTROV");
            Set("tut_create_island_title", LangSr, "NAPRAVI OSTRVO");
            Set("tut_create_island_title", LangTh, "สร้างเกาะ");
            Set("tut_create_island_title", LangTk, "ADA DÖRET");
            Set("tut_create_island_title", LangUk, "СТВОРИ ОСТРІВ");
            Set("tut_create_island_title", LangUz, "OROL YARAT");
            Set("tut_create_island_title", LangVi, "TẠO ĐẢO");
            Set("tut_create_island_title", LangZh, "创建岛屿");

            Set("tut_create_island_body", LangAr, "اضغط زر إنشاء الجزيرة.\nحتى ذلك الوقت، ستكون كل الإجراءات الأخرى محظورة.");
            Set("tut_create_island_body", LangAz, "Ada yarat düyməsini bas.\nO vaxta qədər digər bütün hərəkətlər bağlıdır.");
            Set("tut_create_island_body", LangBe, "Націсні кнопку Стварыць Востраў.\nДа гэтага ўсе астатнія дзеянні заблакаваныя.");
            Set("tut_create_island_body", LangBg, "Натисни бутона Създай остров.\nДотогава всички други действия са блокирани.");
            Set("tut_create_island_body", LangCa, "Prem el botó Crea l'illa.\nFins llavors, totes les altres accions estaran bloquejades.");
            Set("tut_create_island_body", LangCs, "Stiskni tlačítko Vytvořit ostrov.\nDo té doby jsou všechny ostatní akce zablokované.");
            Set("tut_create_island_body", LangDe, "Drücke die Schaltfläche Insel erstellen.\nBis dahin sind alle anderen Aktionen blockiert.");
            Set("tut_create_island_body", LangEs, "Pulsa el botón Crear isla.\nHasta entonces, todas las demás acciones están bloqueadas.");
            Set("tut_create_island_body", LangFa, "دکمه ساخت جزیره را بزن.\nتا آن زمان همه کارهای دیگر قفل هستند.");
            Set("tut_create_island_body", LangFr, "Appuie sur le bouton Créer l'île.\nJusque-là, toutes les autres actions sont bloquées.");
            Set("tut_create_island_body", LangHe, "לחץ על הכפתור צור אי.\nעד אז כל שאר הפעולות חסומות.");
            Set("tut_create_island_body", LangHi, "द्वीप बनाओ बटन दबाओ।\nतब तक बाकी सभी क्रियाएँ बंद रहेंगी।");
            Set("tut_create_island_body", LangHu, "Nyomd meg a Sziget létrehozása gombot.\nAddig minden más művelet blokkolva van.");
            Set("tut_create_island_body", LangHy, "Սեղմիր Ստեղծել կղզի կոճակը։\nՄինչ այդ բոլոր մյուս գործողությունները փակված են։");
            Set("tut_create_island_body", LangId, "Tekan tombol Buat Pulau.\nSampai saat itu, semua tindakan lain terkunci.");
            Set("tut_create_island_body", LangIt, "Premi il pulsante Crea isola.\nFino ad allora, tutte le altre azioni sono bloccate.");
            Set("tut_create_island_body", LangJa, "島を作るボタンを押そう。\nそれまでは他の行動はすべてロックされている。");
            Set("tut_create_island_body", LangKa, "დააჭირე ღილაკს შექმენი კუნძული.\nმანამდე ყველა სხვა მოქმედება დაბლოკილია.");
            Set("tut_create_island_body", LangKk, "Арал жасау батырмасын бас.\nОған дейін басқа әрекеттердің бәрі бұғатталған.");
            Set("tut_create_island_body", LangNl, "Druk op de knop Eiland maken.\nTot die tijd zijn alle andere acties geblokkeerd.");
            Set("tut_create_island_body", LangPl, "Naciśnij przycisk Stwórz wyspę.\nDo tego czasu wszystkie inne działania są zablokowane.");
            Set("tut_create_island_body", LangPt, "Pressione o botão Criar Ilha.\nAté lá, todas as outras ações estarão bloqueadas.");
            Set("tut_create_island_body", LangRo, "Apasă butonul Creează insula.\nPână atunci, toate celelalte acțiuni sunt blocate.");
            Set("tut_create_island_body", LangSk, "Stlač tlačidlo Vytvor ostrov.\nDovtedy sú všetky ostatné akcie zablokované.");
            Set("tut_create_island_body", LangSr, "Pritisni dugme Napravi ostrvo.\nDo tada su sve ostale radnje blokirane.");
            Set("tut_create_island_body", LangTh, "กดปุ่มสร้างเกาะ\nจนกว่าจะทำเช่นนั้น การกระทำอื่นทั้งหมดจะถูกล็อก");
            Set("tut_create_island_body", LangTk, "Ada döret düwmesine bas.\nŞo wagta çenli beýleki ähli hereketler gulpludyr.");
            Set("tut_create_island_body", LangUk, "Натисни кнопку Створити Острів.\nДо цього всі інші дії заблоковані.");
            Set("tut_create_island_body", LangUz, "Orol yaratish tugmasini bosing.\nUngacha boshqa barcha harakatlar bloklangan bo‘ladi.");
            Set("tut_create_island_body", LangVi, "Nhấn nút Tạo Đảo.\nCho đến lúc đó, mọi hành động khác đều bị khóa.");
            Set("tut_create_island_body", LangZh, "按下“创建岛屿”按钮。\n在那之前，其他所有操作都会被锁定。");

            Set("zone_tap_open", LangAr, "اضغط <color=#FFD700><b>[{0}]</b></color> لفتح {1}");
            Set("zone_tap_open", LangAz, "<color=#FFD700><b>[{0}]</b></color> toxun, {1} aç");
            Set("zone_tap_open", LangBe, "Націсні <color=#FFD700><b>[{0}]</b></color>, каб адкрыць {1}");
            Set("zone_tap_open", LangBg, "Докосни <color=#FFD700><b>[{0}]</b></color>, за да отвориш {1}");
            Set("zone_tap_open", LangCa, "Toca <color=#FFD700><b>[{0}]</b></color> per obrir {1}");
            Set("zone_tap_open", LangCs, "Klepni <color=#FFD700><b>[{0}]</b></color> pro otevření {1}");
            Set("zone_tap_open", LangDe, "Tippe <color=#FFD700><b>[{0}]</b></color>, um {1} zu öffnen");
            Set("zone_tap_open", LangEs, "Toca <color=#FFD700><b>[{0}]</b></color> para abrir {1}");
            Set("zone_tap_open", LangFa, "برای باز کردن {1} روی <color=#FFD700><b>[{0}]</b></color> بزن");
            Set("zone_tap_open", LangFr, "Touchez <color=#FFD700><b>[{0}]</b></color> pour ouvrir {1}");
            Set("zone_tap_open", LangHe, "הקש <color=#FFD700><b>[{0}]</b></color> כדי לפתוח את {1}");
            Set("zone_tap_open", LangHi, "{1} खोलने के लिए <color=#FFD700><b>[{0}]</b></color> टैप करें");
            Set("zone_tap_open", LangHu, "Érintsd meg: <color=#FFD700><b>[{0}]</b></color> a(z) {1} megnyitásához");
            Set("zone_tap_open", LangHy, "Հպիր <color=#FFD700><b>[{0}]</b></color>, որպեսզի բացես {1}");
            Set("zone_tap_open", LangId, "Ketuk <color=#FFD700><b>[{0}]</b></color> untuk membuka {1}");
            Set("zone_tap_open", LangIt, "Tocca <color=#FFD700><b>[{0}]</b></color> per aprire {1}");
            Set("zone_tap_open", LangJa, "{1} を開くには <color=#FFD700><b>[{0}]</b></color> をタップ");
            Set("zone_tap_open", LangKa, "{1}-ის გასახსნელად შეეხე <color=#FFD700><b>[{0}]</b></color>");
            Set("zone_tap_open", LangKk, "{1} ашу үшін <color=#FFD700><b>[{0}]</b></color> бас");
            Set("zone_tap_open", LangNl, "Tik <color=#FFD700><b>[{0}]</b></color> om {1} te openen");
            Set("zone_tap_open", LangPl, "Dotknij <color=#FFD700><b>[{0}]</b></color>, aby otworzyć {1}");
            Set("zone_tap_open", LangPt, "Toque em <color=#FFD700><b>[{0}]</b></color> para abrir {1}");
            Set("zone_tap_open", LangRo, "Atinge <color=#FFD700><b>[{0}]</b></color> pentru a deschide {1}");
            Set("zone_tap_open", LangSk, "Ťukni na <color=#FFD700><b>[{0}]</b></color>, aby si otvoril {1}");
            Set("zone_tap_open", LangSr, "Dodirni <color=#FFD700><b>[{0}]</b></color> da otvoriš {1}");
            Set("zone_tap_open", LangTh, "แตะ <color=#FFD700><b>[{0}]</b></color> เพื่อเปิด {1}");
            Set("zone_tap_open", LangTk, "{1} açmak üçin <color=#FFD700><b>[{0}]</b></color> bas");
            Set("zone_tap_open", LangUk, "Торкнись <color=#FFD700><b>[{0}]</b></color>, щоб відкрити {1}");
            Set("zone_tap_open", LangUz, "{1} ni ochish uchun <color=#FFD700><b>[{0}]</b></color> ni bosing");
            Set("zone_tap_open", LangVi, "Chạm <color=#FFD700><b>[{0}]</b></color> để mở {1}");
            Set("zone_tap_open", LangZh, "点击 <color=#FFD700><b>[{0}]</b></color> 以打开 {1}");

            Set("zone_press_open", LangAr, "اضغط <color=#FFD700><b>[{0}]</b></color> لفتح {1}");
            Set("zone_press_open", LangAz, "{1} açmaq üçün <color=#FFD700><b>[{0}]</b></color> bas");
            Set("zone_press_open", LangBe, "Націсні <color=#FFD700><b>[{0}]</b></color>, каб адкрыць {1}");
            Set("zone_press_open", LangBg, "Натисни <color=#FFD700><b>[{0}]</b></color>, за да отвориш {1}");
            Set("zone_press_open", LangCa, "Prem <color=#FFD700><b>[{0}]</b></color> per obrir {1}");
            Set("zone_press_open", LangCs, "Stiskni <color=#FFD700><b>[{0}]</b></color> pro otevření {1}");
            Set("zone_press_open", LangDe, "Drücke <color=#FFD700><b>[{0}]</b></color>, um {1} zu öffnen");
            Set("zone_press_open", LangEs, "Pulsa <color=#FFD700><b>[{0}]</b></color> para abrir {1}");
            Set("zone_press_open", LangFa, "برای باز کردن {1} کلید <color=#FFD700><b>[{0}]</b></color> را بزن");
            Set("zone_press_open", LangFr, "Appuyez sur <color=#FFD700><b>[{0}]</b></color> pour ouvrir {1}");
            Set("zone_press_open", LangHe, "לחץ על <color=#FFD700><b>[{0}]</b></color> כדי לפתוח את {1}");
            Set("zone_press_open", LangHi, "{1} खोलने के लिए <color=#FFD700><b>[{0}]</b></color> दबाएँ");
            Set("zone_press_open", LangHu, "Nyomd meg: <color=#FFD700><b>[{0}]</b></color> a(z) {1} megnyitásához");
            Set("zone_press_open", LangHy, "Սեղմիր <color=#FFD700><b>[{0}]</b></color>, որպեսզի բացես {1}");
            Set("zone_press_open", LangId, "Tekan <color=#FFD700><b>[{0}]</b></color> untuk membuka {1}");
            Set("zone_press_open", LangIt, "Premi <color=#FFD700><b>[{0}]</b></color> per aprire {1}");
            Set("zone_press_open", LangJa, "{1} を開くには <color=#FFD700><b>[{0}]</b></color> を押す");
            Set("zone_press_open", LangKa, "{1}-ის გასახსნელად დააჭირე <color=#FFD700><b>[{0}]</b></color>");
            Set("zone_press_open", LangKk, "{1} ашу үшін <color=#FFD700><b>[{0}]</b></color> бас");
            Set("zone_press_open", LangNl, "Druk op <color=#FFD700><b>[{0}]</b></color> om {1} te openen");
            Set("zone_press_open", LangPl, "Naciśnij <color=#FFD700><b>[{0}]</b></color>, aby otworzyć {1}");
            Set("zone_press_open", LangPt, "Pressione <color=#FFD700><b>[{0}]</b></color> para abrir {1}");
            Set("zone_press_open", LangRo, "Apasă <color=#FFD700><b>[{0}]</b></color> pentru a deschide {1}");
            Set("zone_press_open", LangSk, "Stlač <color=#FFD700><b>[{0}]</b></color>, aby si otvoril {1}");
            Set("zone_press_open", LangSr, "Pritisni <color=#FFD700><b>[{0}]</b></color> da otvoriš {1}");
            Set("zone_press_open", LangTh, "กด <color=#FFD700><b>[{0}]</b></color> เพื่อเปิด {1}");
            Set("zone_press_open", LangTk, "{1} açmak üçin <color=#FFD700><b>[{0}]</b></color> bas");
            Set("zone_press_open", LangUk, "Натисни <color=#FFD700><b>[{0}]</b></color>, щоб відкрити {1}");
            Set("zone_press_open", LangUz, "{1} ni ochish uchun <color=#FFD700><b>[{0}]</b></color> ni bosing");
            Set("zone_press_open", LangVi, "Nhấn <color=#FFD700><b>[{0}]</b></color> để mở {1}");
            Set("zone_press_open", LangZh, "按下 <color=#FFD700><b>[{0}]</b></color> 以打开 {1}");

            Set("zone_tap_sell", LangAr, "اضغط <color=#FFD700><b>[{0}]</b></color> لبيع الموارد");
            Set("zone_tap_sell", LangAz, "Resursları satmaq üçün <color=#FFD700><b>[{0}]</b></color> toxun");
            Set("zone_tap_sell", LangBe, "Націсні <color=#FFD700><b>[{0}]</b></color>, каб прадаць рэсурсы");
            Set("zone_tap_sell", LangBg, "Докосни <color=#FFD700><b>[{0}]</b></color>, за да продадеш ресурсите");
            Set("zone_tap_sell", LangCa, "Toca <color=#FFD700><b>[{0}]</b></color> per vendre recursos");
            Set("zone_tap_sell", LangCs, "Klepni <color=#FFD700><b>[{0}]</b></color> pro prodej surovin");
            Set("zone_tap_sell", LangDe, "Tippe <color=#FFD700><b>[{0}]</b></color>, um Ressourcen zu verkaufen");
            Set("zone_tap_sell", LangEs, "Toca <color=#FFD700><b>[{0}]</b></color> para vender recursos");
            Set("zone_tap_sell", LangFa, "برای فروش منابع روی <color=#FFD700><b>[{0}]</b></color> بزن");
            Set("zone_tap_sell", LangFr, "Touchez <color=#FFD700><b>[{0}]</b></color> pour vendre les ressources");
            Set("zone_tap_sell", LangHe, "הקש <color=#FFD700><b>[{0}]</b></color> כדי למכור משאבים");
            Set("zone_tap_sell", LangHi, "संसाधन बेचने के लिए <color=#FFD700><b>[{0}]</b></color> टैप करें");
            Set("zone_tap_sell", LangHu, "Érintsd meg: <color=#FFD700><b>[{0}]</b></color> az erőforrások eladásához");
            Set("zone_tap_sell", LangHy, "Հպիր <color=#FFD700><b>[{0}]</b></color>, որպեսզի վաճառես ռեսուրսները");
            Set("zone_tap_sell", LangId, "Ketuk <color=#FFD700><b>[{0}]</b></color> untuk menjual sumber daya");
            Set("zone_tap_sell", LangIt, "Tocca <color=#FFD700><b>[{0}]</b></color> per vendere risorse");
            Set("zone_tap_sell", LangJa, "資源を売るには <color=#FFD700><b>[{0}]</b></color> をタップ");
            Set("zone_tap_sell", LangKa, "რესურსების გასაყიდად შეეხე <color=#FFD700><b>[{0}]</b></color>");
            Set("zone_tap_sell", LangKk, "Ресурстарды сату үшін <color=#FFD700><b>[{0}]</b></color> бас");
            Set("zone_tap_sell", LangNl, "Tik <color=#FFD700><b>[{0}]</b></color> om grondstoffen te verkopen");
            Set("zone_tap_sell", LangPl, "Dotknij <color=#FFD700><b>[{0}]</b></color>, aby sprzedać zasoby");
            Set("zone_tap_sell", LangPt, "Toque em <color=#FFD700><b>[{0}]</b></color> para vender recursos");
            Set("zone_tap_sell", LangRo, "Atinge <color=#FFD700><b>[{0}]</b></color> pentru a vinde resurse");
            Set("zone_tap_sell", LangSk, "Ťukni na <color=#FFD700><b>[{0}]</b></color> pre predaj zdrojov");
            Set("zone_tap_sell", LangSr, "Dodirni <color=#FFD700><b>[{0}]</b></color> da prodaš resurse");
            Set("zone_tap_sell", LangTh, "แตะ <color=#FFD700><b>[{0}]</b></color> เพื่อขายทรัพยากร");
            Set("zone_tap_sell", LangTk, "Resurslary satmak üçin <color=#FFD700><b>[{0}]</b></color> bas");
            Set("zone_tap_sell", LangUk, "Торкнись <color=#FFD700><b>[{0}]</b></color>, щоб продати ресурси");
            Set("zone_tap_sell", LangUz, "Resurslarni sotish uchun <color=#FFD700><b>[{0}]</b></color> ni bosing");
            Set("zone_tap_sell", LangVi, "Chạm <color=#FFD700><b>[{0}]</b></color> để bán tài nguyên");
            Set("zone_tap_sell", LangZh, "点击 <color=#FFD700><b>[{0}]</b></color> 以出售资源");

            Set("zone_press_sell", LangAr, "اضغط <color=#FFD700><b>[{0}]</b></color> لبيع الموارد");
            Set("zone_press_sell", LangAz, "Resursları satmaq üçün <color=#FFD700><b>[{0}]</b></color> bas");
            Set("zone_press_sell", LangBe, "Націсні <color=#FFD700><b>[{0}]</b></color>, каб прадаць рэсурсы");
            Set("zone_press_sell", LangBg, "Натисни <color=#FFD700><b>[{0}]</b></color>, за да продадеш ресурсите");
            Set("zone_press_sell", LangCa, "Prem <color=#FFD700><b>[{0}]</b></color> per vendre recursos");
            Set("zone_press_sell", LangCs, "Stiskni <color=#FFD700><b>[{0}]</b></color> pro prodej surovin");
            Set("zone_press_sell", LangDe, "Drücke <color=#FFD700><b>[{0}]</b></color>, um Ressourcen zu verkaufen");
            Set("zone_press_sell", LangEs, "Pulsa <color=#FFD700><b>[{0}]</b></color> para vender recursos");
            Set("zone_press_sell", LangFa, "برای فروش منابع کلید <color=#FFD700><b>[{0}]</b></color> را بزن");
            Set("zone_press_sell", LangFr, "Appuyez sur <color=#FFD700><b>[{0}]</b></color> pour vendre les ressources");
            Set("zone_press_sell", LangHe, "לחץ על <color=#FFD700><b>[{0}]</b></color> כדי למכור משאבים");
            Set("zone_press_sell", LangHi, "संसाधन बेचने के लिए <color=#FFD700><b>[{0}]</b></color> दबाएँ");
            Set("zone_press_sell", LangHu, "Nyomd meg: <color=#FFD700><b>[{0}]</b></color> az erőforrások eladásához");
            Set("zone_press_sell", LangHy, "Սեղմիր <color=#FFD700><b>[{0}]</b></color>, որպեսզի վաճառես ռեսուրսները");
            Set("zone_press_sell", LangId, "Tekan <color=#FFD700><b>[{0}]</b></color> untuk menjual sumber daya");
            Set("zone_press_sell", LangIt, "Premi <color=#FFD700><b>[{0}]</b></color> per vendere risorse");
            Set("zone_press_sell", LangJa, "資源を売るには <color=#FFD700><b>[{0}]</b></color> を押す");
            Set("zone_press_sell", LangKa, "რესურსების გასაყიდად დააჭირე <color=#FFD700><b>[{0}]</b></color>");
            Set("zone_press_sell", LangKk, "Ресурстарды сату үшін <color=#FFD700><b>[{0}]</b></color> бас");
            Set("zone_press_sell", LangNl, "Druk op <color=#FFD700><b>[{0}]</b></color> om grondstoffen te verkopen");
            Set("zone_press_sell", LangPl, "Naciśnij <color=#FFD700><b>[{0}]</b></color>, aby sprzedać zasoby");
            Set("zone_press_sell", LangPt, "Pressione <color=#FFD700><b>[{0}]</b></color> para vender recursos");
            Set("zone_press_sell", LangRo, "Apasă <color=#FFD700><b>[{0}]</b></color> pentru a vinde resurse");
            Set("zone_press_sell", LangSk, "Stlač <color=#FFD700><b>[{0}]</b></color> pre predaj zdrojov");
            Set("zone_press_sell", LangSr, "Pritisni <color=#FFD700><b>[{0}]</b></color> da prodaš resurse");
            Set("zone_press_sell", LangTh, "กด <color=#FFD700><b>[{0}]</b></color> เพื่อขายทรัพยากร");
            Set("zone_press_sell", LangTk, "Resurslary satmak üçin <color=#FFD700><b>[{0}]</b></color> bas");
            Set("zone_press_sell", LangUk, "Натисни <color=#FFD700><b>[{0}]</b></color>, щоб продати ресурси");
            Set("zone_press_sell", LangUz, "Resurslarni sotish uchun <color=#FFD700><b>[{0}]</b></color> ni bosing");
            Set("zone_press_sell", LangVi, "Nhấn <color=#FFD700><b>[{0}]</b></color> để bán tài nguyên");
            Set("zone_press_sell", LangZh, "按下 <color=#FFD700><b>[{0}]</b></color> 以出售资源");

            Set("status_mine_bought", LangAr, "<color=yellow>تم شراء المنجم.</color> اذهب إلى الجزيرة لوضعه.");
            Set("status_mine_bought", LangAz, "<color=yellow>Mədən alındı.</color> Yerləşdirmək üçün adaya get.");
            Set("status_mine_bought", LangBe, "<color=yellow>Шахта набыта.</color> Ідзі на востраў, каб паставіць яе.");
            Set("status_mine_bought", LangBg, "<color=yellow>Мината е купена.</color> Отиди на острова, за да я поставиш.");
            Set("status_mine_bought", LangCa, "<color=yellow>Mina comprada.</color> Ves a l'illa per col·locar-la.");
            Set("status_mine_bought", LangCs, "<color=yellow>Důl zakoupen.</color> Jdi na ostrov a umísti ho.");
            Set("status_mine_bought", LangDe, "<color=yellow>Mine gekauft.</color> Gehe zur Insel, um sie zu platzieren.");
            Set("status_mine_bought", LangEs, "<color=yellow>Mina comprada.</color> Ve a la isla para colocarla.");
            Set("status_mine_bought", LangFa, "<color=yellow>معدن خریداری شد.</color> برای قرار دادن آن به جزیره برو.");
            Set("status_mine_bought", LangFr, "<color=yellow>Mine achetée.</color> Va sur l'île pour la placer.");
            Set("status_mine_bought", LangHe, "<color=yellow>המכרה נרכש.</color> לך לאי כדי להציב אותו.");
            Set("status_mine_bought", LangHi, "<color=yellow>खदान खरीदी गई।</color> इसे लगाने के लिए द्वीप पर जाएँ।");
            Set("status_mine_bought", LangHu, "<color=yellow>A bánya megvéve.</color> Menj a szigetre, hogy lerakd.");
            Set("status_mine_bought", LangHy, "<color=yellow>Հանքը գնված է.</color> Գնա կղզի՝ այն տեղադրելու համար:");
            Set("status_mine_bought", LangId, "<color=yellow>Tambang dibeli.</color> Pergi ke pulau untuk menaruhnya.");
            Set("status_mine_bought", LangIt, "<color=yellow>Miniera acquistata.</color> Vai sull'isola per posizionarla.");
            Set("status_mine_bought", LangJa, "<color=yellow>鉱山を購入しました。</color> 島へ行って設置してください。");
            Set("status_mine_bought", LangKa, "<color=yellow>მაღარო ნაყიდია.</color> კუნძულზე წადი მის დასადგმელად.");
            Set("status_mine_bought", LangKk, "<color=yellow>Кен сатып алынды.</color> Оны орналастыру үшін аралға бар.");
            Set("status_mine_bought", LangNl, "<color=yellow>Mijn gekocht.</color> Ga naar het eiland om hem te plaatsen.");
            Set("status_mine_bought", LangPl, "<color=yellow>Kopalnia kupiona.</color> Idź na wyspę, aby ją postawić.");
            Set("status_mine_bought", LangPt, "<color=yellow>Mina comprada.</color> Vá para a ilha para colocá-la.");
            Set("status_mine_bought", LangRo, "<color=yellow>Mina a fost cumpărată.</color> Mergi pe insulă ca s-o plasezi.");
            Set("status_mine_bought", LangSk, "<color=yellow>Baňa kúpená.</color> Choď na ostrov a umiestni ju.");
            Set("status_mine_bought", LangSr, "<color=yellow>Rudnik je kupljen.</color> Idi na ostrvo da ga postaviš.");
            Set("status_mine_bought", LangTh, "<color=yellow>ซื้อเหมืองแล้ว</color> ไปที่เกาะเพื่อวางมัน");
            Set("status_mine_bought", LangTk, "<color=yellow>Kän satyn alyndy.</color> Ony goýmak üçin ada git.");
            Set("status_mine_bought", LangUk, "<color=yellow>Шахту придбано.</color> Іди на острів, щоб її поставити.");
            Set("status_mine_bought", LangUz, "<color=yellow>Kon sotib olindi.</color> Uni joylashtirish uchun orolga boring.");
            Set("status_mine_bought", LangVi, "<color=yellow>Đã mua mỏ.</color> Hãy đến đảo để đặt nó.");
            Set("status_mine_bought", LangZh, "<color=yellow>矿井已购买。</color> 前往岛屿放置它。");

            Set("status_placement", LangAr, "<color=yellow>وضع التثبيت.</color> اختر مكانًا واضغط وضع.");
            Set("status_placement", LangAz, "<color=yellow>Yerləşdirmə rejimi.</color> Yer seç və QOY bas.");
            Set("status_placement", LangBe, "<color=yellow>Рэжым размяшчэння.</color> Абяры месца і націсні ПАСТАВІЦЬ.");
            Set("status_placement", LangBg, "<color=yellow>Режим на поставяне.</color> Избери място и натисни ПОСТАВИ.");
            Set("status_placement", LangCa, "<color=yellow>Mode de col·locació.</color> Tria un lloc i prem POSA.");
            Set("status_placement", LangCs, "<color=yellow>Režim umístění.</color> Vyber místo a stiskni UMÍSTIT.");
            Set("status_placement", LangDe, "<color=yellow>Platzierungsmodus.</color> Wähle einen Ort und drücke PLATZ.");
            Set("status_placement", LangEs, "<color=yellow>Modo de colocación.</color> Elige un lugar y pulsa COLOCAR.");
            Set("status_placement", LangFa, "<color=yellow>حالت قراردهی.</color> یک مکان انتخاب کن و «قرار بده» را بزن.");
            Set("status_placement", LangFr, "<color=yellow>Mode placement.</color> Choisis un endroit et appuie sur PLACER.");
            Set("status_placement", LangHe, "<color=yellow>מצב הצבה.</color> בחר מקום ולחץ הצב.");
            Set("status_placement", LangHi, "<color=yellow>प्लेसमेंट मोड।</color> स्थान चुनें और रखें दबाएँ।");
            Set("status_placement", LangHu, "<color=yellow>Elhelyezési mód.</color> Válassz helyet és nyomd meg a LERAK gombot.");
            Set("status_placement", LangHy, "<color=yellow>Տեղադրման ռեժիմ.</color> Ընտրիր տեղ և սեղմիր ԴՆԵԼ:");
            Set("status_placement", LangId, "<color=yellow>Mode penempatan.</color> Pilih posisi dan tekan TEMPAT.");
            Set("status_placement", LangIt, "<color=yellow>Modalità posizionamento.</color> Scegli un posto e premi POSA.");
            Set("status_placement", LangJa, "<color=yellow>設置モード。</color> 場所を選んで設置を押してください。");
            Set("status_placement", LangKa, "<color=yellow>დადგმის რეჟიმი.</color> აირჩიე ადგილი და დააჭირე დადგმას.");
            Set("status_placement", LangKk, "<color=yellow>Орналастыру режимі.</color> Орын таңдап, ОРНАЛ. бас.");
            Set("status_placement", LangNl, "<color=yellow>Plaatsingsmodus.</color> Kies een plek en druk op PLAATS.");
            Set("status_placement", LangPl, "<color=yellow>Tryb stawiania.</color> Wybierz miejsce i naciśnij POSTAW.");
            Set("status_placement", LangPt, "<color=yellow>Modo de posicionamento.</color> Escolha um local e pressione COLOCAR.");
            Set("status_placement", LangRo, "<color=yellow>Mod de plasare.</color> Alege un loc și apasă PLASEAZĂ.");
            Set("status_placement", LangSk, "<color=yellow>Režim umiestnenia.</color> Vyber miesto a stlač UMIEST.");
            Set("status_placement", LangSr, "<color=yellow>Režim postavljanja.</color> Izaberi mesto i pritisni POSTAVI.");
            Set("status_placement", LangTh, "<color=yellow>โหมดวาง.</color> เลือกตำแหน่งแล้วกด วาง");
            Set("status_placement", LangTk, "<color=yellow>Goýmak režimi.</color> Ýer saýla we GOÝ bas.");
            Set("status_placement", LangUk, "<color=yellow>Режим розміщення.</color> Обери місце і натисни ПОСТАВ.");
            Set("status_placement", LangUz, "<color=yellow>Joylashtirish rejimi.</color> Joy tanlang va QO‘Y ni bosing.");
            Set("status_placement", LangVi, "<color=yellow>Chế độ đặt.</color> Chọn vị trí và nhấn ĐẶT.");
            Set("status_placement", LangZh, "<color=yellow>放置模式。</color> 选择位置并按下放置。");

            Set("status_not_enough_money_detail", LangAr, "لا يوجد مال كافٍ. المطلوب {0}، لديك {1}.");
            Set("status_not_enough_money_detail", LangAz, "Pul çatmır. Lazım: {0}, səndə: {1}.");
            Set("status_not_enough_money_detail", LangBe, "Не хапае грошай. Трэба {0}, у цябе {1}.");
            Set("status_not_enough_money_detail", LangBg, "Няма достатъчно пари. Трябват {0}, имаш {1}.");
            Set("status_not_enough_money_detail", LangCa, "No hi ha prou diners. Calen {0}, tens {1}.");
            Set("status_not_enough_money_detail", LangCs, "Nedostatek peněz. Potřeba {0}, máš {1}.");
            Set("status_not_enough_money_detail", LangDe, "Nicht genug Geld. Benötigt {0}, du hast {1}.");
            Set("status_not_enough_money_detail", LangEs, "No hay suficiente dinero. Necesitas {0}, tienes {1}.");
            Set("status_not_enough_money_detail", LangFa, "پول کافی نیست. لازم: {0}، موجودی: {1}.");
            Set("status_not_enough_money_detail", LangFr, "Pas assez d'argent. Il faut {0}, tu as {1}.");
            Set("status_not_enough_money_detail", LangHe, "אין מספיק כסף. צריך {0}, יש לך {1}.");
            Set("status_not_enough_money_detail", LangHi, "पर्याप्त पैसे नहीं हैं। चाहिए {0}, आपके पास {1} है।");
            Set("status_not_enough_money_detail", LangHu, "Nincs elég pénz. Kell {0}, neked {1} van.");
            Set("status_not_enough_money_detail", LangHy, "Փողը չի հերիքում։ Պետք է {0}, քեզ մոտ {1} է:");
            Set("status_not_enough_money_detail", LangId, "Uang tidak cukup. Butuh {0}, kamu punya {1}.");
            Set("status_not_enough_money_detail", LangIt, "Denaro insufficiente. Servono {0}, hai {1}.");
            Set("status_not_enough_money_detail", LangJa, "お金が足りません。必要: {0}、所持: {1}。");
            Set("status_not_enough_money_detail", LangKa, "ფული არ გყოფნის. საჭიროა {0}, შენ გაქვს {1}.");
            Set("status_not_enough_money_detail", LangKk, "Ақша жеткіліксіз. Керегі {0}, сенде {1}.");
            Set("status_not_enough_money_detail", LangNl, "Niet genoeg geld. Nodig: {0}, je hebt {1}.");
            Set("status_not_enough_money_detail", LangPl, "Za mało pieniędzy. Potrzeba {0}, masz {1}.");
            Set("status_not_enough_money_detail", LangPt, "Dinheiro insuficiente. Precisa de {0}, você tem {1}.");
            Set("status_not_enough_money_detail", LangRo, "Nu ai destui bani. Ai nevoie de {0}, ai {1}.");
            Set("status_not_enough_money_detail", LangSk, "Nedostatok peňazí. Treba {0}, máš {1}.");
            Set("status_not_enough_money_detail", LangSr, "Nema dovoljno novca. Potrebno {0}, imaš {1}.");
            Set("status_not_enough_money_detail", LangTh, "เงินไม่พอ ต้องใช้ {0}, คุณมี {1}");
            Set("status_not_enough_money_detail", LangTk, "Pul ýetmeýär. Gerek {0}, sende {1} bar.");
            Set("status_not_enough_money_detail", LangUk, "Не вистачає грошей. Потрібно {0}, у тебе {1}.");
            Set("status_not_enough_money_detail", LangUz, "Pul yetarli emas. Kerak {0}, sizda {1} bor.");
            Set("status_not_enough_money_detail", LangVi, "Không đủ tiền. Cần {0}, bạn có {1}.");
            Set("status_not_enough_money_detail", LangZh, "金钱不足。需要 {0}，你拥有 {1}。");

            Set("status_place_mine_hint", LangAr, "انقر بالزر الأيسر لوضع {0}. اضغط Esc للإلغاء.");
            Set("status_place_mine_hint", LangAz, "{0} yerləşdirmək üçün sol klik et. Ləğv üçün Esc.");
            Set("status_place_mine_hint", LangBe, "ЛКМ каб паставіць {0}. Esc каб адмяніць.");
            Set("status_place_mine_hint", LangBg, "Ляв клик за поставяне на {0}. Esc за отказ.");
            Set("status_place_mine_hint", LangCa, "Clic esquerre per col·locar {0}. Esc per cancel·lar.");
            Set("status_place_mine_hint", LangCs, "Levým klikem umístíš {0}. Esc zruší.");
            Set("status_place_mine_hint", LangDe, "Linksklick, um {0} zu platzieren. Esc zum Abbrechen.");
            Set("status_place_mine_hint", LangEs, "Clic izquierdo para colocar {0}. Esc para cancelar.");
            Set("status_place_mine_hint", LangFa, "برای قرار دادن {0} کلیک چپ کن. Esc برای لغو.");
            Set("status_place_mine_hint", LangFr, "Clic gauche pour placer {0}. Esc pour annuler.");
            Set("status_place_mine_hint", LangHe, "לחיצה שמאלית כדי להציב את {0}. Esc לביטול.");
            Set("status_place_mine_hint", LangHi, "{0} रखने के लिए बायाँ क्लिक करें। रद्द करने के लिए Esc।");
            Set("status_place_mine_hint", LangHu, "Bal kattintás a(z) {0} lerakásához. Esc a megszakításhoz.");
            Set("status_place_mine_hint", LangHy, "Ձախ սեղմում՝ {0} դնելու համար։ Esc՝ չեղարկելու համար:");
            Set("status_place_mine_hint", LangId, "Klik kiri untuk menaruh {0}. Esc untuk batal.");
            Set("status_place_mine_hint", LangIt, "Clic sinistro per posizionare {0}. Esc per annullare.");
            Set("status_place_mine_hint", LangJa, "{0} を設置するには左クリック。Escでキャンセル。");
            Set("status_place_mine_hint", LangKa, "{0}-ის დასადგმელად მარცხენა დაკლიკება. Esc გასაუქმებლად.");
            Set("status_place_mine_hint", LangKk, "{0} орналастыру үшін сол жақ батырманы бас. Болдырмау үшін Esc.");
            Set("status_place_mine_hint", LangNl, "Linksklik om {0} te plaatsen. Esc om te annuleren.");
            Set("status_place_mine_hint", LangPl, "Lewy klik, aby postawić {0}. Esc, aby anulować.");
            Set("status_place_mine_hint", LangPt, "Clique esquerdo para colocar {0}. Esc para cancelar.");
            Set("status_place_mine_hint", LangRo, "Click stânga pentru a plasa {0}. Esc pentru anulare.");
            Set("status_place_mine_hint", LangSk, "Ľavým klikom umiestniš {0}. Esc zruší.");
            Set("status_place_mine_hint", LangSr, "Levi klik da postaviš {0}. Esc za otkaz.");
            Set("status_place_mine_hint", LangTh, "คลิกซ้ายเพื่อวาง {0} กด Esc เพื่อยกเลิก");
            Set("status_place_mine_hint", LangTk, "{0} goýmak üçin çep bas. Ýatyrmak üçin Esc.");
            Set("status_place_mine_hint", LangUk, "ЛКМ щоб поставити {0}. Esc щоб скасувати.");
            Set("status_place_mine_hint", LangUz, "{0} ni joylashtirish uchun chap tugmani bosing. Bekor qilish uchun Esc.");
            Set("status_place_mine_hint", LangVi, "Nhấp chuột trái để đặt {0}. Esc để hủy.");
            Set("status_place_mine_hint", LangZh, "左键放置 {0}。按 Esc 取消。");

            Set("status_mine_placed", LangAr, "تم وضع المنجم {0}. العمق: {1}.");
            Set("status_mine_placed", LangAz, "{0} mədəni yerləşdirildi. Dərinlik: {1}.");
            Set("status_mine_placed", LangBe, "Шахта {0} усталявана. Глыбіня: {1}.");
            Set("status_mine_placed", LangBg, "Мината {0} е поставена. Дълбочина: {1}.");
            Set("status_mine_placed", LangCa, "S'ha col·locat la mina {0}. Profunditat: {1}.");
            Set("status_mine_placed", LangCs, "Důl {0} umístěn. Hloubka: {1}.");
            Set("status_mine_placed", LangDe, "Mine {0} platziert. Tiefe: {1}.");
            Set("status_mine_placed", LangEs, "Mina {0} colocada. Profundidad: {1}.");
            Set("status_mine_placed", LangFa, "معدن {0} قرار داده شد. عمق: {1}.");
            Set("status_mine_placed", LangFr, "Mine {0} placée. Profondeur: {1}.");
            Set("status_mine_placed", LangHe, "המכרה {0} הוצב. עומק: {1}.");
            Set("status_mine_placed", LangHi, "खदान {0} रखी गई। गहराई: {1}।");
            Set("status_mine_placed", LangHu, "A(z) {0} bánya lerakva. Mélység: {1}.");
            Set("status_mine_placed", LangHy, "{0} հանքը տեղադրված է։ Խորություն՝ {1}:");
            Set("status_mine_placed", LangId, "Tambang {0} ditempatkan. Kedalaman: {1}.");
            Set("status_mine_placed", LangIt, "Miniera {0} posizionata. Profondità: {1}.");
            Set("status_mine_placed", LangJa, "鉱山 {0} を設置しました。深さ: {1}。");
            Set("status_mine_placed", LangKa, "მაღარო {0} დადგმულია. სიღრმე: {1}.");
            Set("status_mine_placed", LangKk, "{0} кені орнатылды. Тереңдік: {1}.");
            Set("status_mine_placed", LangNl, "Mijn {0} geplaatst. Diepte: {1}.");
            Set("status_mine_placed", LangPl, "Kopalnia {0} ustawiona. Głębokość: {1}.");
            Set("status_mine_placed", LangPt, "Mina {0} colocada. Profundidade: {1}.");
            Set("status_mine_placed", LangRo, "Mina {0} a fost plasată. Adâncime: {1}.");
            Set("status_mine_placed", LangSk, "Baňa {0} umiestnená. Hĺbka: {1}.");
            Set("status_mine_placed", LangSr, "Rudnik {0} je postavljen. Dubina: {1}.");
            Set("status_mine_placed", LangTh, "วางเหมือง {0} แล้ว ความลึก: {1}");
            Set("status_mine_placed", LangTk, "{0} käni goýuldy. Çuňluk: {1}.");
            Set("status_mine_placed", LangUk, "Шахту {0} встановлено. Глибина: {1}.");
            Set("status_mine_placed", LangUz, "{0} koni joylashtirildi. Chuqurlik: {1}.");
            Set("status_mine_placed", LangVi, "Đã đặt mỏ {0}. Độ sâu: {1}.");
            Set("status_mine_placed", LangZh, "矿井 {0} 已放置。深度：{1}。");

            Set("status_mine_sold", LangAr, "تم بيع المنجم مقابل {0}.");
            Set("status_mine_sold", LangAz, "Mədən {0} qiymətinə satıldı.");
            Set("status_mine_sold", LangBe, "Шахта прададзена за {0}.");
            Set("status_mine_sold", LangBg, "Мината е продадена за {0}.");
            Set("status_mine_sold", LangCa, "Mina venuda per {0}.");
            Set("status_mine_sold", LangCs, "Důl prodán za {0}.");
            Set("status_mine_sold", LangDe, "Mine für {0} verkauft.");
            Set("status_mine_sold", LangEs, "Mina vendida por {0}.");
            Set("status_mine_sold", LangFa, "معدن به قیمت {0} فروخته شد.");
            Set("status_mine_sold", LangFr, "Mine vendue pour {0}.");
            Set("status_mine_sold", LangHe, "המכרה נמכר ב-{0}.");
            Set("status_mine_sold", LangHi, "खदान {0} में बेची गई।");
            Set("status_mine_sold", LangHu, "A bánya eladva {0} összegért.");
            Set("status_mine_sold", LangHy, "Հանքը վաճառվել է {0}-ով:");
            Set("status_mine_sold", LangId, "Tambang dijual seharga {0}.");
            Set("status_mine_sold", LangIt, "Miniera venduta per {0}.");
            Set("status_mine_sold", LangJa, "鉱山を {0} で売却しました。");
            Set("status_mine_sold", LangKa, "მაღარო გაიყიდა {0}-ად.");
            Set("status_mine_sold", LangKk, "Кен {0} бағаға сатылды.");
            Set("status_mine_sold", LangNl, "Mijn verkocht voor {0}.");
            Set("status_mine_sold", LangPl, "Kopalnia sprzedana za {0}.");
            Set("status_mine_sold", LangPt, "Mina vendida por {0}.");
            Set("status_mine_sold", LangRo, "Mina a fost vândută pentru {0}.");
            Set("status_mine_sold", LangSk, "Baňa predaná za {0}.");
            Set("status_mine_sold", LangSr, "Rudnik je prodat za {0}.");
            Set("status_mine_sold", LangTh, "ขายเหมืองได้ {0}");
            Set("status_mine_sold", LangTk, "Kän {0} üçin satyldy.");
            Set("status_mine_sold", LangUk, "Шахту продано за {0}.");
            Set("status_mine_sold", LangUz, "Kon {0} ga sotildi.");
            Set("status_mine_sold", LangVi, "Đã bán mỏ với giá {0}.");
            Set("status_mine_sold", LangZh, "矿井已以 {0} 售出。");

            Set("status_placement_cancelled", LangAr, "تم إلغاء الوضع. تمت إعادة المال.");
            Set("status_placement_cancelled", LangAz, "Yerləşdirmə ləğv edildi. Pul qaytarıldı.");
            Set("status_placement_cancelled", LangBe, "Размяшчэнне скасавана. Грошы вернуты.");
            Set("status_placement_cancelled", LangBg, "Поставянето е отменено. Парите са върнати.");
            Set("status_placement_cancelled", LangCa, "Col·locació cancel·lada. S'han retornat els diners.");
            Set("status_placement_cancelled", LangCs, "Umístění zrušeno. Peníze vráceny.");
            Set("status_placement_cancelled", LangDe, "Platzierung abgebrochen. Geld zurückerstattet.");
            Set("status_placement_cancelled", LangEs, "Colocación cancelada. Dinero devuelto.");
            Set("status_placement_cancelled", LangFa, "قراردهی لغو شد. پول بازگردانده شد.");
            Set("status_placement_cancelled", LangFr, "Placement annulé. Argent remboursé.");
            Set("status_placement_cancelled", LangHe, "ההצבה בוטלה. הכסף הוחזר.");
            Set("status_placement_cancelled", LangHi, "प्लेसमेंट रद्द। पैसा वापस कर दिया गया।");
            Set("status_placement_cancelled", LangHu, "Elhelyezés megszakítva. A pénz visszajár.");
            Set("status_placement_cancelled", LangHy, "Տեղադրումը չեղարկվեց։ Գումարը վերադարձվեց:");
            Set("status_placement_cancelled", LangId, "Penempatan dibatalkan. Uang dikembalikan.");
            Set("status_placement_cancelled", LangIt, "Posizionamento annullato. Denaro restituito.");
            Set("status_placement_cancelled", LangJa, "設置をキャンセルしました。お金は返却されました。");
            Set("status_placement_cancelled", LangKa, "დადგმა გაუქმდა. ფული დაბრუნდა.");
            Set("status_placement_cancelled", LangKk, "Орналастыру тоқтатылды. Ақша қайтарылды.");
            Set("status_placement_cancelled", LangNl, "Plaatsing geannuleerd. Geld teruggegeven.");
            Set("status_placement_cancelled", LangPl, "Anulowano stawianie. Pieniądze zwrócone.");
            Set("status_placement_cancelled", LangPt, "Posicionamento cancelado. Dinheiro devolvido.");
            Set("status_placement_cancelled", LangRo, "Plasarea a fost anulată. Banii au fost returnați.");
            Set("status_placement_cancelled", LangSk, "Umiestnenie zrušené. Peniaze vrátené.");
            Set("status_placement_cancelled", LangSr, "Postavljanje je otkazano. Novac je vraćen.");
            Set("status_placement_cancelled", LangTh, "ยกเลิกการวางแล้ว คืนเงินแล้ว");
            Set("status_placement_cancelled", LangTk, "Goýmak ýatyryldy. Pul gaýtaryldy.");
            Set("status_placement_cancelled", LangUk, "Розміщення скасовано. Гроші повернено.");
            Set("status_placement_cancelled", LangUz, "Joylashtirish bekor qilindi. Pul qaytarildi.");
            Set("status_placement_cancelled", LangVi, "Đã hủy đặt. Tiền đã được hoàn lại.");
            Set("status_placement_cancelled", LangZh, "放置已取消。金钱已返还。");

            Set("status_spawn_saved", LangAr, "تم حفظ نقطة الظهور على الجزيرة.");
            Set("status_spawn_saved", LangAz, "Spawn nöqtəsi adada saxlanıldı.");
            Set("status_spawn_saved", LangBe, "Кропка спавна захавана на востраве.");
            Set("status_spawn_saved", LangBg, "Точката за поява е запазена на острова.");
            Set("status_spawn_saved", LangCa, "S'ha desat el punt de renaixement a l'illa.");
            Set("status_spawn_saved", LangCs, "Bod zrození uložen na ostrově.");
            Set("status_spawn_saved", LangDe, "Spawnpunkt auf der Insel gespeichert.");
            Set("status_spawn_saved", LangEs, "Punto de aparición guardado en la isla.");
            Set("status_spawn_saved", LangFa, "نقطه اسپاون روی جزیره ذخیره شد.");
            Set("status_spawn_saved", LangFr, "Point d'apparition sauvegardé sur l'île.");
            Set("status_spawn_saved", LangHe, "נקודת ההופעה נשמרה על האי.");
            Set("status_spawn_saved", LangHi, "स्पॉन बिंदु द्वीप पर सहेजा गया।");
            Set("status_spawn_saved", LangHu, "A respawn pont elmentve a szigeten.");
            Set("status_spawn_saved", LangHy, "Սփաունի կետը պահպանվել է կղզում:");
            Set("status_spawn_saved", LangId, "Titik spawn disimpan di pulau.");
            Set("status_spawn_saved", LangIt, "Punto di spawn salvato sull'isola.");
            Set("status_spawn_saved", LangJa, "島にスポーン地点を保存しました。");
            Set("status_spawn_saved", LangKa, "სპავნის წერტილი კუნძულზე შეინახა.");
            Set("status_spawn_saved", LangKk, "Пайда болу нүктесі аралда сақталды.");
            Set("status_spawn_saved", LangNl, "Spawnpunt opgeslagen op het eiland.");
            Set("status_spawn_saved", LangPl, "Punkt odrodzenia zapisany na wyspie.");
            Set("status_spawn_saved", LangPt, "Ponto de spawn salvo na ilha.");
            Set("status_spawn_saved", LangRo, "Punctul de respawn a fost salvat pe insulă.");
            Set("status_spawn_saved", LangSk, "Bod zrodu uložený na ostrove.");
            Set("status_spawn_saved", LangSr, "Tačka spawna je sačuvana na ostrvu.");
            Set("status_spawn_saved", LangTh, "บันทึกจุดเกิดบนเกาะแล้ว");
            Set("status_spawn_saved", LangTk, "Dogulma nokady adada ýazdyryldy.");
            Set("status_spawn_saved", LangUk, "Точку спавну збережено на острові.");
            Set("status_spawn_saved", LangUz, "Spawn nuqtasi orolda saqlandi.");
            Set("status_spawn_saved", LangVi, "Đã lưu điểm hồi sinh trên đảo.");
            Set("status_spawn_saved", LangZh, "出生点已保存在岛上。");

            Set("status_spawn_save_failed", LangAr, "لا يمكن حفظ نقطة الظهور هنا. قف على أرض الجزيرة الصلبة.");
            Set("status_spawn_save_failed", LangAz, "Spawn burada saxlanmır. Adanın möhkəm yerində dur.");
            Set("status_spawn_save_failed", LangBe, "Нельга захаваць спавн тут. Стань на цвёрдую зямлю вострава.");
            Set("status_spawn_save_failed", LangBg, "Тук не може да се запази точка за поява. Застани на твърда земя на острова.");
            Set("status_spawn_save_failed", LangCa, "No es pot desar el punt de renaixement aquí. Posa't sobre terra ferma de l'illa.");
            Set("status_spawn_save_failed", LangCs, "Nelze zde uložit spawn. Postav se na pevnou zem ostrova.");
            Set("status_spawn_save_failed", LangDe, "Spawn kann hier nicht gespeichert werden. Stelle dich auf festen Inselboden.");
            Set("status_spawn_save_failed", LangEs, "No se puede guardar el punto de aparición aquí. Ponte sobre suelo firme de la isla.");
            Set("status_spawn_save_failed", LangFa, "نمی‌توان نقطه اسپاون را اینجا ذخیره کرد. روی زمین محکم جزیره بایست.");
            Set("status_spawn_save_failed", LangFr, "Impossible de sauvegarder le spawn ici. Tiens-toi sur un sol solide de l'île.");
            Set("status_spawn_save_failed", LangHe, "אי אפשר לשמור כאן נקודת הופעה. עמוד על אדמה מוצקה באי.");
            Set("status_spawn_save_failed", LangHi, "यहाँ स्पॉन सहेजा नहीं जा सकता। द्वीप की ठोस जमीन पर खड़े हों।");
            Set("status_spawn_save_failed", LangHu, "Itt nem lehet menteni a respawn pontot. Állj a sziget szilárd talajára.");
            Set("status_spawn_save_failed", LangHy, "Այստեղ չի կարելի պահել սփաունը։ Կանգնիր կղզու ամուր հողի վրա:");
            Set("status_spawn_save_failed", LangId, "Tidak bisa menyimpan spawn di sini. Berdirilah di tanah pulau yang padat.");
            Set("status_spawn_save_failed", LangIt, "Non puoi salvare qui il punto di spawn. Mettiti su terreno solido dell'isola.");
            Set("status_spawn_save_failed", LangJa, "ここではスポーン地点を保存できません。島の固い地面に立ってください。");
            Set("status_spawn_save_failed", LangKa, "აქ სპავნის შენახვა არ შეიძლება. დადექი კუნძულის მყარ მიწაზე.");
            Set("status_spawn_save_failed", LangKk, "Мұнда спавнды сақтау мүмкін емес. Аралдың қатты жеріне тұр.");
            Set("status_spawn_save_failed", LangNl, "Spawnpunt kan hier niet worden opgeslagen. Ga op stevige eilandgrond staan.");
            Set("status_spawn_save_failed", LangPl, "Nie można tu zapisać punktu odrodzenia. Stań na twardym gruncie wyspy.");
            Set("status_spawn_save_failed", LangPt, "Não é possível salvar o spawn aqui. Fique sobre o solo firme da ilha.");
            Set("status_spawn_save_failed", LangRo, "Nu poți salva punctul de respawn aici. Stai pe un teren solid al insulei.");
            Set("status_spawn_save_failed", LangSk, "Tu sa nedá uložiť spawn. Postav sa na pevnú zem ostrova.");
            Set("status_spawn_save_failed", LangSr, "Ovde nije moguće sačuvati spawn. Stani na čvrsto tlo ostrva.");
            Set("status_spawn_save_failed", LangTh, "ไม่สามารถบันทึกจุดเกิดที่นี่ได้ ยืนบนพื้นแข็งของเกาะ");
            Set("status_spawn_save_failed", LangTk, "Bu ýerde dogulma nokadyny ýazdyryp bolmaýar. Adanyň gaty ýerinde dur.");
            Set("status_spawn_save_failed", LangUk, "Тут не можна зберегти спавн. Стань на тверду землю острова.");
            Set("status_spawn_save_failed", LangUz, "Bu yerda spawn saqlab bo‘lmaydi. Orolning qattiq yerida turing.");
            Set("status_spawn_save_failed", LangVi, "Không thể lưu điểm hồi sinh ở đây. Hãy đứng trên nền đất chắc của đảo.");
            Set("status_spawn_save_failed", LangZh, "这里无法保存出生点。请站在岛上的坚实地面上。");
        }

        private static bool IsSupported(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return false;
            lang = lang.ToLowerInvariant().Trim();
            return _languageInfoByCode.ContainsKey(lang);
        }

        private static string GetFallbackLanguage(string lang)
        {
            return TryGetLanguageInfo(lang, out LanguageInfo info) ? info.FallbackLanguage : LangEn;
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
