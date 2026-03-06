mergeInto(LibraryManager.library,
{
    Init_js: function(configJsonPtr) {
        var configJson = UTF8ToString(configJsonPtr);
        if (ysdk == null || !ysdk.multiplayer || !ysdk.multiplayer.sessions) {
            YG2Instance('OnSessionsLoaded', JSON.stringify({ sessions: [] }));
            return;
        }

        try {
            var config = JSON.parse(configJson);
            config.isEventBased = false; // Manual mode
            var promise = ysdk.multiplayer.sessions.init(config);
            promise.then(function(sessions) {
                var json = JSON.stringify({ sessions: sessions });
                YG2Instance('OnSessionsLoaded', json);
            }).catch(function(e) {
                console.error('Multiplayer Init Error:', e);
                YG2Instance('OnSessionsLoaded', JSON.stringify({ sessions: [] }));
            });
        } catch (e) {
            console.error('Multiplayer Parse Error:', e);
            YG2Instance('OnSessionsLoaded', JSON.stringify({ sessions: [] }));
        }
    },

    Commit_js: function(payloadJsonPtr) {
        var payloadJson = UTF8ToString(payloadJsonPtr);
        if (ysdk && ysdk.multiplayer && ysdk.multiplayer.sessions) {
            try {
                ysdk.multiplayer.sessions.commit(JSON.parse(payloadJson));
            } catch (e) {
                console.error('Commit Error:', e);
            }
        }
    },

    Push_js: function(metaJsonPtr) {
        var metaJson = UTF8ToString(metaJsonPtr);
        if (ysdk && ysdk.multiplayer && ysdk.multiplayer.sessions) {
            try {
                ysdk.multiplayer.sessions.push(JSON.parse(metaJson));
            } catch (e) {
                console.error('Push Error:', e);
            }
        }
    }
});