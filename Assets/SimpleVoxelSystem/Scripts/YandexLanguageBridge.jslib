mergeInto(LibraryManager.library, {
    SVS_GetYandexSdkLanguage: function () {
        var lang = '';

        try {
            if (typeof ysdk !== 'undefined' && ysdk !== null) {
                if (ysdk.environment) {
                    if (ysdk.environment.i18n) {
                        lang = ysdk.environment.i18n.lang || ysdk.environment.i18n.language || '';
                    }

                    if (!lang) {
                        lang = ysdk.environment.language || ysdk.environment.lang || '';
                    }
                }
            }
        } catch (e) {}

        if (!lang) {
            try {
                if (typeof navigator !== 'undefined') {
                    lang = navigator.language || navigator.userLanguage || '';
                }
            } catch (e2) {}
        }

        lang = String(lang || '').toLowerCase();

        var bufferSize = lengthBytesUTF8(lang) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(lang, buffer, bufferSize);
        return buffer;
    }
});
