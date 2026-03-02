mergeInto(LibraryManager.library, {
    YandexMP_IsAvailable: function () {
        return (typeof ysdk !== 'undefined' && ysdk !== null && ysdk.multiplayer && ysdk.multiplayer.sessions) ? 1 : 0;
    },

    YandexMP_Init: function (gameObjectNamePtr, initMethodPtr, txMethodPtr, finishMethodPtr, configJsonPtr) {
        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        var initMethod = UTF8ToString(initMethodPtr);
        var txMethod = UTF8ToString(txMethodPtr);
        var finishMethod = UTF8ToString(finishMethodPtr);
        var configJson = UTF8ToString(configJsonPtr);

        function safeSend(method, payload) {
            try {
                SendMessage(gameObjectName, method, payload || '');
            } catch (e) {
                console.warn('[YandexMP] SendMessage failed', method, e);
            }
        }

        function tryStartGameplay() {
            try {
                if (ysdk && ysdk.features && ysdk.features.GameplayAPI && ysdk.features.GameplayAPI.start) {
                    ysdk.features.GameplayAPI.start();
                }
            } catch (e) {
                console.warn('[YandexMP] GameplayAPI.start failed', e);
            }
        }

        if (typeof ysdk === 'undefined' || ysdk === null || !ysdk.multiplayer || !ysdk.multiplayer.sessions) {
            safeSend(initMethod, JSON.stringify({ ok: false, reason: 'multiplayer_unavailable' }));
            return;
        }

        var options = {};
        try {
            options = configJson ? JSON.parse(configJson) : {};
        } catch (e) {
            options = {};
        }

        if (!window.__yandexMpBoundHandlers) {
            window.__yandexMpBoundHandlers = true;

            ysdk.on('multiplayer-sessions-transaction', function (evt) {
                if (!evt) {
                    safeSend(txMethod, '');
                    return;
                }

                var txs = [];
                if (Array.isArray(evt.transactions)) {
                    for (var i = 0; i < evt.transactions.length; i++) {
                        var t = evt.transactions[i] || {};
                        txs.push({
                            id: t.id || '',
                            time: typeof t.time === 'number' ? t.time : 0,
                            payloadJson: JSON.stringify(typeof t.payload === 'undefined' ? null : t.payload)
                        });
                    }
                }

                safeSend(txMethod, JSON.stringify({
                    opponentId: evt.opponentId || '',
                    transactions: txs
                }));
            });

            ysdk.on('multiplayer-sessions-finish', function (opponentId) {
                safeSend(finishMethod, JSON.stringify({ opponentId: opponentId || '' }));
            });
        }

        ysdk.multiplayer.sessions.init(options)
            .then(function (opponents) {
                tryStartGameplay();
                var count = Array.isArray(opponents) ? opponents.length : 0;
                safeSend(initMethod, JSON.stringify({ ok: true, opponentsCount: count }));
            })
            .catch(function (err) {
                safeSend(initMethod, JSON.stringify({ ok: false, reason: String(err || 'init_failed') }));
            });
    },

    YandexMP_Commit: function (payloadJsonPtr) {
        var payloadJson = UTF8ToString(payloadJsonPtr);
        if (typeof ysdk === 'undefined' || ysdk === null || !ysdk.multiplayer || !ysdk.multiplayer.sessions)
            return;

        try {
            var payload = payloadJson ? JSON.parse(payloadJson) : {};
            tryStartGameplay();
            ysdk.multiplayer.sessions.commit(payload);
        } catch (e) {
            console.warn('[YandexMP] commit parse error', e);
        }
    },

    YandexMP_Push: function (metaJsonPtr) {
        var metaJson = UTF8ToString(metaJsonPtr);
        if (typeof ysdk === 'undefined' || ysdk === null || !ysdk.multiplayer || !ysdk.multiplayer.sessions)
            return;

        try {
            var meta = metaJson ? JSON.parse(metaJson) : {};
            ysdk.multiplayer.sessions.push(meta);
        } catch (e) {
            console.warn('[YandexMP] push parse error', e);
        }
    }
});
