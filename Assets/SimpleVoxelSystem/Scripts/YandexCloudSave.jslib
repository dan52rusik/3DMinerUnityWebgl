mergeInto(LibraryManager.library, {
    YandexCloud_IsReady: function () {
        return (typeof ysdk !== 'undefined' && ysdk !== null && typeof ysdk.getPlayer === 'function') ? 1 : 0;
    },

    YandexCloud_Load: function (gameObjectNamePtr, callbackMethodPtr) {
        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        var callbackMethod = UTF8ToString(callbackMethodPtr);

        function sendResult(text) {
            try {
                SendMessage(gameObjectName, callbackMethod, text || '');
            } catch (e) {
                console.warn('[YandexCloud] SendMessage failed:', e);
            }
        }

        if (typeof ysdk === 'undefined' || ysdk === null || typeof ysdk.getPlayer !== 'function') {
            sendResult('');
            return;
        }

        ysdk.getPlayer({ scopes: true }).then(function (player) {
            if (!player || typeof player.getData !== 'function') {
                sendResult('');
                return;
            }

            player.getData(["svs_progress_v1"]).then(function (data) {
                if (data && typeof data.svs_progress_v1 === 'string')
                    sendResult(data.svs_progress_v1);
                else
                    sendResult('');
            }).catch(function () {
                sendResult('');
            });
        }).catch(function () {
            sendResult('');
        });
    },

    YandexCloud_Save: function (jsonPtr) {
        var json = UTF8ToString(jsonPtr);

        if (typeof ysdk === 'undefined' || ysdk === null || typeof ysdk.getPlayer !== 'function') {
            try {
                localStorage.setItem('svs_progress_v1', json);
            } catch (e) {}
            return;
        }

        ysdk.getPlayer({ scopes: true }).then(function (player) {
            if (!player || typeof player.setData !== 'function') {
                try {
                    localStorage.setItem('svs_progress_v1', json);
                } catch (e) {}
                return;
            }

            var payload = { svs_progress_v1: json };
            player.setData(payload, true).catch(function () {
                try {
                    localStorage.setItem('svs_progress_v1', json);
                } catch (e) {}
            });
        }).catch(function () {
            try {
                localStorage.setItem('svs_progress_v1', json);
            } catch (e) {}
        });
    }
});
