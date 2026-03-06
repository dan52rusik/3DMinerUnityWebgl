using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleVoxelSystem
{
    /// <summary>
    /// FIX #7: Единое хранилище идентификации игрока для всех систем.
    /// LobbyRealtimeSync и YandexAsyncMultiplayerManager раньше хранили ID
    /// под разными ключами — у одного игрока было два разных guest ID.
    /// Теперь все системы читают из одного места.
    /// </summary>
    public static class PlayerIdentity
    {
        private const string IdPrefKey   = "svs_player_id";
        private const string NamePrefKey = "svs_player_name";

        public static event Action OnIdentityChanged;

        private static string _playerId;
        private static string _playerName;
        private static readonly HashSet<string> _knownLocalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string PlayerId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_playerId))
                    Load();
                return _playerId;
            }
        }

        public static string PlayerName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_playerName))
                    Load();
                return _playerName;
            }
        }

        public static bool IsGuest => string.IsNullOrWhiteSpace(_playerId) ||
                                      _playerId.StartsWith("guest_", StringComparison.OrdinalIgnoreCase);

        public static bool IsLocalId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return false;

            string normalized = playerId.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            if (!string.IsNullOrWhiteSpace(_playerId) &&
                string.Equals(_playerId, normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            return _knownLocalIds.Contains(normalized);
        }

        /// <summary>
        /// Вызывается когда SDK вернул реальные данные игрока (например, из Yandex).
        /// Обновляет хранилище и уведомляет подписчиков.
        /// </summary>
        public static void UpdateFromSdk(string playerId, string playerName)
        {
            bool changed = false;
            string previousId = _playerId;

            if (!string.IsNullOrWhiteSpace(playerId) && playerId.Trim() != _playerId)
            {
                _playerId = playerId.Trim();
                TrackLocalId(_playerId);
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(playerName) && playerName.Trim() != _playerName)
            {
                _playerName = playerName.Trim();
                changed = true;
            }

            if (changed)
            {
                TrackLocalId(previousId);
                Save();
                OnIdentityChanged?.Invoke();
            }
        }

        private static void Load()
        {
            // Читаем из унифицированного ключа
            _playerId   = PlayerPrefs.GetString(IdPrefKey, string.Empty);
            _playerName = PlayerPrefs.GetString(NamePrefKey, "Player");

            // Миграция: если старый ключ LobbyRealtimeSync существует — переносим
            if (string.IsNullOrWhiteSpace(_playerId))
            {
                string legacyLobby = PlayerPrefs.GetString("svs_sync_client_id", string.Empty);
                string legacyAsync = PlayerPrefs.GetString("svs_async_player_id", string.Empty);
                _playerId = !string.IsNullOrWhiteSpace(legacyLobby) ? legacyLobby : legacyAsync;
            }

            if (string.IsNullOrWhiteSpace(_playerName) || _playerName == "Player")
            {
                string legacyLobby = PlayerPrefs.GetString("svs_sync_client_name", string.Empty);
                string legacyAsync = PlayerPrefs.GetString("svs_async_player_name", string.Empty);
                if (!string.IsNullOrWhiteSpace(legacyLobby)) _playerName = legacyLobby;
                else if (!string.IsNullOrWhiteSpace(legacyAsync)) _playerName = legacyAsync;
            }

            // Генерируем гостевой ID если нет ни одного
            if (string.IsNullOrWhiteSpace(_playerId))
            {
                _playerId   = "guest_" + Guid.NewGuid().ToString("N");
                _playerName = "Player";
                TrackLocalId(_playerId);
                Save();
            }
            else
            {
                TrackLocalId(_playerId);
            }
        }

        private static void TrackLocalId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            _knownLocalIds.Add(playerId.Trim());
        }

        private static void Save()
        {
            PlayerPrefs.SetString(IdPrefKey,   _playerId);
            PlayerPrefs.SetString(NamePrefKey, _playerName);
            PlayerPrefs.Save();
        }
    }
}
