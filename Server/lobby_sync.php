<?php
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Headers: Content-Type');
header('Access-Control-Allow-Methods: POST, OPTIONS');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['ok' => false, 'error' => 'method_not_allowed'], JSON_UNESCAPED_UNICODE);
    exit;
}

$raw = file_get_contents('php://input');
$req = json_decode($raw ?? '', true);
if (!is_array($req)) {
    echo json_encode(['ok' => false, 'error' => 'bad_json'], JSON_UNESCAPED_UNICODE);
    exit;
}

$action = (string)($req['action'] ?? '');
if ($action !== 'sync') {
    echo json_encode(['ok' => false, 'error' => 'unsupported_action'], JSON_UNESCAPED_UNICODE);
    exit;
}

$roomId = preg_replace('/[^a-zA-Z0-9_\-]/', '', (string)($req['roomId'] ?? 'global_lobby'));
if ($roomId === '') {
    $roomId = 'global_lobby';
}

$clientId = trim((string)($req['clientId'] ?? ''));
if ($clientId === '') {
    echo json_encode(['ok' => false, 'error' => 'client_id_required'], JSON_UNESCAPED_UNICODE);
    exit;
}

$clientName = trim((string)($req['clientName'] ?? 'Player'));
if ($clientName === '') {
    $clientName = 'Player';
}

$sinceSeq = (int)($req['sinceSeq'] ?? 0);
$requestSnapshot = !empty($req['requestSnapshot']);
$opsIn = is_array($req['ops'] ?? null) ? $req['ops'] : [];
$state = is_array($req['state'] ?? null) ? $req['state'] : null;

$storePath = __DIR__ . '/lobby_sync_store.json';
$maxOpsStored = 50000;
$maxOpsPerRequest = 1024;
$maxPlayerIdleSeconds = 20;

$fp = fopen($storePath, 'c+');
if ($fp === false) {
    echo json_encode(['ok' => false, 'error' => 'store_open_failed'], JSON_UNESCAPED_UNICODE);
    exit;
}

flock($fp, LOCK_EX);
$existingRaw = stream_get_contents($fp);
$store = json_decode($existingRaw ?: '', true);
if (!is_array($store)) {
    $store = ['rooms' => []];
}
if (!isset($store['rooms']) || !is_array($store['rooms'])) {
    $store['rooms'] = [];
}
if (!isset($store['rooms'][$roomId]) || !is_array($store['rooms'][$roomId])) {
    $store['rooms'][$roomId] = [
        'seq' => 0,
        'ops' => [],
        'opIndex' => [],
        'snapshot' => [],
        'players' => [],
    ];
}

$room =& $store['rooms'][$roomId];
if (!isset($room['seq'])) $room['seq'] = 0;
if (!isset($room['ops']) || !is_array($room['ops'])) $room['ops'] = [];
if (!isset($room['opIndex']) || !is_array($room['opIndex'])) $room['opIndex'] = [];
if (!isset($room['snapshot']) || !is_array($room['snapshot'])) $room['snapshot'] = [];
if (!isset($room['players']) || !is_array($room['players'])) $room['players'] = [];

$now = time();

$opsCount = 0;
foreach ($opsIn as $op) {
    if (!is_array($op)) continue;
    if ($opsCount >= $maxOpsPerRequest) break;
    $opsCount++;

    $kind = (string)($op['kind'] ?? '');
    if ($kind !== 'place' && $kind !== 'remove') continue;

    $opId = trim((string)($op['opId'] ?? ''));
    if ($opId === '') {
        $opId = uniqid($clientId . '_', true);
    }
    if (isset($room['opIndex'][$opId])) {
        continue;
    }

    $x = (int)($op['x'] ?? 0);
    $y = (int)($op['y'] ?? 0);
    $z = (int)($op['z'] ?? 0);
    $blockType = (int)($op['blockType'] ?? 0);
    $t = (int)($op['t'] ?? 0);

    $room['seq']++;
    $record = [
        'seq' => $room['seq'],
        'opId' => $opId,
        'from' => $clientId,
        'kind' => $kind,
        'x' => $x,
        'y' => $y,
        'z' => $z,
        'blockType' => $kind === 'place' ? max(1, $blockType) : 0,
        't' => $t > 0 ? $t : (int)round(microtime(true) * 1000),
    ];

    $room['ops'][] = $record;
    $room['opIndex'][$opId] = $record['seq'];

    $key = $x . ':' . $y . ':' . $z;
    if ($kind === 'place') {
        $room['snapshot'][$key] = [
            'x' => $x,
            'y' => $y,
            'z' => $z,
            'blockType' => max(1, $blockType),
        ];
    } else {
        $room['snapshot'][$key] = [
            'x' => $x,
            'y' => $y,
            'z' => $z,
            'blockType' => 0,
        ];
    }
}

if (count($room['ops']) > $maxOpsStored) {
    $room['ops'] = array_slice($room['ops'], -$maxOpsStored);
    $room['opIndex'] = [];
    foreach ($room['ops'] as $existingOp) {
        $id = (string)($existingOp['opId'] ?? '');
        if ($id !== '') {
            $room['opIndex'][$id] = (int)($existingOp['seq'] ?? 0);
        }
    }
}

if (is_array($state)) {
    $room['players'][$clientId] = [
        'clientId' => $clientId,
        'name' => $clientName,
        'x' => (float)($state['x'] ?? 0),
        'y' => (float)($state['y'] ?? 0),
        'z' => (float)($state['z'] ?? 0),
        'ry' => (float)($state['ry'] ?? 0),
        'inLobby' => !empty($state['inLobby']),
        't' => (int)($state['t'] ?? (int)round(microtime(true) * 1000)),
        'seenAt' => $now,
    ];
}

foreach ($room['players'] as $pid => $player) {
    $seenAt = (int)($player['seenAt'] ?? 0);
    if ($now - $seenAt > $maxPlayerIdleSeconds) {
        unset($room['players'][$pid]);
    }
}

$opsOut = [];
foreach ($room['ops'] as $op) {
    if ((int)($op['seq'] ?? 0) > $sinceSeq) {
        $opsOut[] = $op;
    }
}

$playersOut = [];
foreach ($room['players'] as $player) {
    $playersOut[] = [
        'clientId' => (string)($player['clientId'] ?? ''),
        'name' => (string)($player['name'] ?? 'Player'),
        'x' => (float)($player['x'] ?? 0),
        'y' => (float)($player['y'] ?? 0),
        'z' => (float)($player['z'] ?? 0),
        'ry' => (float)($player['ry'] ?? 0),
        'inLobby' => !empty($player['inLobby']),
        't' => (int)($player['t'] ?? 0),
    ];
}

$snapshotOut = [];
if ($requestSnapshot) {
    foreach ($room['snapshot'] as $cell) {
        $snapshotOut[] = [
            'x' => (int)($cell['x'] ?? 0),
            'y' => (int)($cell['y'] ?? 0),
            'z' => (int)($cell['z'] ?? 0),
            'blockType' => (int)($cell['blockType'] ?? 0),
        ];
    }
}

ftruncate($fp, 0);
rewind($fp);
fwrite($fp, json_encode($store, JSON_UNESCAPED_UNICODE));
fflush($fp);
flock($fp, LOCK_UN);
fclose($fp);

$resp = [
    'ok' => true,
    'seq' => (int)$room['seq'],
    'ops' => $opsOut,
    'snapshot' => $snapshotOut,
    'players' => $playersOut,
];

echo json_encode($resp, JSON_UNESCAPED_UNICODE);
