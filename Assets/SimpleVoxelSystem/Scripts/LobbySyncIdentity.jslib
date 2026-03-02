mergeInto(LibraryManager.library, {
    LobbySync_RequestIdentity: function (gameObjectNamePtr, callbackMethodPtr) {
        var gameObjectName = UTF8ToString(gameObjectNamePtr);
        var callbackMethod = UTF8ToString(callbackMethodPtr);

        function send(payload) {
            try {
                SendMessage(gameObjectName, callbackMethod, JSON.stringify(payload || {}));
            } catch (e) {
                console.warn('[LobbySyncIdentity] SendMessage failed', e);
            }
        }

        function fallback() {
            try {
                var id = localStorage.getItem('svs_sync_guest_id');
                if (!id) {
                    id = 'guest_' + Math.random().toString(36).slice(2) + Date.now().toString(36);
                    localStorage.setItem('svs_sync_guest_id', id);
                }
                send({ playerId: id, playerName: 'Player' });
            } catch (e) {
                send({ playerId: 'guest_' + Date.now().toString(36), playerName: 'Player' });
            }
        }

        if (typeof ysdk === 'undefined' || ysdk === null || typeof ysdk.getPlayer !== 'function') {
            fallback();
            return;
        }

        ysdk.getPlayer({ scopes: false }).then(function (player) {
            if (!player) {
                fallback();
                return;
            }

            var playerId = '';
            var playerName = '';

            try {
                if (typeof player.getUniqueID === 'function') {
                    playerId = String(player.getUniqueID() || '');
                } else if (typeof player.uniqueID !== 'undefined') {
                    playerId = String(player.uniqueID || '');
                } else if (typeof player.getID === 'function') {
                    playerId = String(player.getID() || '');
                }
            } catch (e1) {}

            try {
                if (typeof player.getName === 'function')
                    playerName = String(player.getName() || '');
                else if (typeof player.name !== 'undefined')
                    playerName = String(player.name || '');
            } catch (e2) {}

            if (!playerId) {
                fallback();
                return;
            }

            send({
                playerId: playerId,
                playerName: playerName || 'Player'
            });
        }).catch(function () {
            fallback();
        });
    }
});
