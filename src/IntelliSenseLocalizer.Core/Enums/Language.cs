using System.ComponentModel;

namespace IntelliSenseLocalizer;

public enum Language
{
    /// <summary>
    /// Bahasa Indonesia
    /// </summary>
    [Description("id-id")]
    id_ID,

    /// <summary>
    /// Bahasa Melayu
    /// </summary>
    [Description("ms-my")]
    ms_MY,

    /// <summary>
    /// Bosanski
    /// </summary>
    [Description("bs-latn-ba")]
    bs_LATN_BA,

    /// <summary>
    /// Català
    /// </summary>
    [Description("ca-es")]
    ca_ES,

    /// <summary>
    /// Čeština
    /// </summary>
    [Description("cs-cz")]
    cs_CZ,

    /// <summary>
    /// Dansk
    /// </summary>
    [Description("da-dk")]
    da_DK,

    /// <summary>
    /// Deutsch (Österreich)
    /// </summary>
    [Description("de-at")]
    de_AT,

    /// <summary>
    /// Deutsch (Schweiz)
    /// </summary>
    [Description("de-ch")]
    de_CH,

    /// <summary>
    /// Deutsch
    /// </summary>
    [Description("de-de")]
    de_DE,

    /// <summary>
    /// Eesti
    /// </summary>
    [Description("et-ee")]
    et_EE,

    /// <summary>
    /// English (Australia)
    /// </summary>
    [Description("en-au")]
    en_AU,

    /// <summary>
    /// English (Canada)
    /// </summary>
    [Description("en-ca")]
    en_CA,

    /// <summary>
    /// English (India)
    /// </summary>
    [Description("en-in")]
    en_IN,

    /// <summary>
    /// English (Ireland)
    /// </summary>
    [Description("en-ie")]
    en_IE,

    /// <summary>
    /// English (Malaysia)
    /// </summary>
    [Description("en-my")]
    en_MY,

    /// <summary>
    /// English (New Zealand)
    /// </summary>
    [Description("en-nz")]
    en_NZ,

    /// <summary>
    /// English (Singapore)
    /// </summary>
    [Description("en-sg")]
    en_SG,

    /// <summary>
    /// English (South Africa)
    /// </summary>
    [Description("en-za")]
    en_ZA,

    /// <summary>
    /// English (United Kingdom)
    /// </summary>
    [Description("en-gb")]
    en_GB,

    /// <summary>
    /// English (United States)
    /// </summary>
    [Description("en-us")]
    en_US,

    /// <summary>
    /// Español (México)
    /// </summary>
    [Description("es-mx")]
    es_MX,

    /// <summary>
    /// Español
    /// </summary>
    [Description("es-es")]
    es_ES,

    /// <summary>
    /// Euskara
    /// </summary>
    [Description("eu-es")]
    eu_ES,

    /// <summary>
    /// Filipino
    /// </summary>
    [Description("fil-ph")]
    fil_PH,

    /// <summary>
    /// Français (Belgique)
    /// </summary>
    [Description("fr-be")]
    fr_BE,

    /// <summary>
    /// Français (Canada)
    /// </summary>
    [Description("fr-ca")]
    fr_CA,

    /// <summary>
    /// Français (Suisse)
    /// </summary>
    [Description("fr-ch")]
    fr_CH,

    /// <summary>
    /// Français
    /// </summary>
    [Description("fr-fr")]
    fr_FR,

    /// <summary>
    /// Gaeilge
    /// </summary>
    [Description("ga-ie")]
    ga_IE,

    /// <summary>
    /// Galego
    /// </summary>
    [Description("gl-es")]
    gl_ES,

    /// <summary>
    /// ქართული
    /// </summary>
    [Description("ka-ge")]
    ka_GE,

    /// <summary>
    /// Hrvatski
    /// </summary>
    [Description("hr-hr")]
    hr_HR,

    /// <summary>
    /// Íslenska
    /// </summary>
    [Description("is-is")]
    is_IS,

    /// <summary>
    /// Italiano (Svizzera)
    /// </summary>
    [Description("it-ch")]
    it_CH,

    /// <summary>
    /// Italiano
    /// </summary>
    [Description("it-it")]
    it_IT,

    /// <summary>
    /// Latviešu
    /// </summary>
    [Description("lv-lv")]
    lv_LV,

    /// <summary>
    /// Lëtzebuergesch
    /// </summary>
    [Description("lb-lu")]
    lb_LU,

    /// <summary>
    /// Lietuvių
    /// </summary>
    [Description("lt-lt")]
    lt_LT,

    /// <summary>
    /// Magyar
    /// </summary>
    [Description("hu-hu")]
    hu_HU,

    /// <summary>
    /// Malti
    /// </summary>
    [Description("mt-mt")]
    mt_MT,

    /// <summary>
    /// Nederlands (België)
    /// </summary>
    [Description("nl-be")]
    nl_BE,

    /// <summary>
    /// Nederlands
    /// </summary>
    [Description("nl-nl")]
    nl_NL,

    /// <summary>
    /// Norsk Bokmål
    /// </summary>
    [Description("nb-no")]
    nb_NO,

    /// <summary>
    /// Polski
    /// </summary>
    [Description("pl-pl")]
    pl_PL,

    /// <summary>
    /// Português (Brasil)
    /// </summary>
    [Description("pt-br")]
    pt_BR,

    /// <summary>
    /// Português (Portugal)
    /// </summary>
    [Description("pt-pt")]
    pt_PT,

    /// <summary>
    /// Română
    /// </summary>
    [Description("ro-ro")]
    ro_RO,

    /// <summary>
    /// Slovenčina
    /// </summary>
    [Description("sk-sk")]
    sk_SK,

    /// <summary>
    /// Slovenski
    /// </summary>
    [Description("sl-si")]
    sl_SI,

    /// <summary>
    /// Srbija - Srpski
    /// </summary>
    [Description("sr-latn-rs")]
    sr_LATN_RS,

    /// <summary>
    /// Suomi
    /// </summary>
    [Description("fi-fi")]
    fi_FI,

    /// <summary>
    /// Svenska
    /// </summary>
    [Description("sv-se")]
    sv_SE,

    /// <summary>
    /// TiếngViệt
    /// </summary>
    [Description("vi-vn")]
    vi_VN,

    /// <summary>
    /// Türkçe
    /// </summary>
    [Description("tr-tr")]
    tr_TR,

    /// <summary>
    /// Ελληνικά
    /// </summary>
    [Description("el-gr")]
    el_GR,

    /// <summary>
    /// Български
    /// </summary>
    [Description("bg-bg")]
    bg_BG,

    /// <summary>
    /// қазақ тілі
    /// </summary>
    [Description("kk-kz")]
    kk_KZ,

    /// <summary>
    /// Русский
    /// </summary>
    [Description("ru-ru")]
    ru_RU,

    /// <summary>
    /// Српски
    /// </summary>
    [Description("sr-cyrl-rs")]
    sr_CYRL_RS,

    /// <summary>
    /// Українська
    /// </summary>
    [Description("uk-ua")]
    uk_UA,

    /// <summary>
    /// עברית
    /// </summary>
    [Description("he-il")]
    he_IL,

    /// <summary>
    /// العربية
    /// </summary>
    [Description("ar-sa")]
    ar_SA,

    /// <summary>
    /// हिंदी
    /// </summary>
    [Description("hi-in")]
    hi_IN,

    /// <summary>
    /// ไทย
    /// </summary>
    [Description("th-th")]
    th_TH,

    /// <summary>
    /// 한국어
    /// </summary>
    [Description("ko-kr")]
    ko_KR,

    /// <summary>
    /// 中文 (简体)
    /// </summary>
    [Description("zh-cn")]
    zh_CN,

    /// <summary>
    /// 中文 (繁體)
    /// </summary>
    [Description("zh-tw")]
    zh_TW,

    /// <summary>
    /// 中文 (繁體 香港特別行政區)
    /// </summary>
    [Description("zh-hk")]
    zh_HK,

    /// <summary>
    /// 日本語
    /// </summary>
    [Description("ja-jp")]
    ja_JP,
}
