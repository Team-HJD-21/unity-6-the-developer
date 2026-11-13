# 아키텍처 문서

최종 갱신일 2026-11-14
상태 · 문서 탐색용 목차

## 공통 아키텍처와 규칙

- [architecture-rebuild-notes](00_foundations/architecture-rebuild-notes.md)
- [asset-layout-and-migration](00_foundations/asset-layout-and-migration.md)
- [naming-and-architecture-conventions](00_foundations/naming-and-architecture-conventions.md)
- [system-rearchitecture-charter](00_foundations/system-rearchitecture-charter.md)

## Core와 난수

- [Core API 구현 현황과 남은 연결](01_core/core-implementation-status.md)
- [Unity 기본 오디오·GameAudioManager 사용법](01_core/game-audio-api.md)
- [core-api-usage](01_core/core-api-usage.md)
- [core-feature-integration-guide](01_core/core-feature-integration-guide.md)
- [core-lifecycle-and-api-guide](01_core/core-lifecycle-and-api-guide.md)
- [core-presence-report-api](01_core/core-presence-report-api.md)
- [core-random-api](01_core/core-random-api.md)
- [core-sprint-region-and-request-api](01_core/core-sprint-region-and-request-api.md)
- [정적 지역 확보·해금·스폰 제한](02_match/static-region-capture.md)
- [core-structure-review-2026-11-05](01_core/core-structure-review-2026-11-05.md)
- [random-weighted-selection](01_core/random-weighted-selection.md)

## 매치

- [match-environment-api](02_match/match-environment-api.md)
- [match-field-pause-api](02_match/match-field-pause-api.md)
- [match-management-responsibilities](02_match/match-management-responsibilities.md)
- [match-time-api](02_match/match-time-api.md)

## 씬 전환과 우주선 이동

- [로컬 CameraManager: 컷씬 시점·블렌드·복귀 사용법](03_scene-flow/camera-manager-api.md)
- [scene-flow-api](03_scene-flow/scene-flow-api.md)
- [screen-management-api](03_scene-flow/screen-management-api.md)
- [spaceship-travel-api](03_scene-flow/spaceship-travel-api.md)

## 네트워크와 Steam

- [network-match-api](04_networking/network-match-api.md)
- [steam-api](04_networking/steam-api.md)
- [steam-integration-design](04_networking/steam-integration-design.md)

## 체크포인트

- [checkpoint-execution-api](05_checkpoints/checkpoint-execution-api.md)
- [checkpoint-storage-api](05_checkpoints/checkpoint-storage-api.md)

## 플레이어와 HUD

- [player-class-responsibilities](06_player/player-class-responsibilities.md)
- [player-combat-api](06_player/player-combat-api.md)
- [player-dash-api](06_player/player-dash-api.md)
- [player-device-collision-and-depth](06_player/player-device-collision-and-depth.md)
- [player-motion-api](06_player/player-motion-api.md)
- [player-primary-input-api](06_player/player-primary-input-api.md)
- [player-skill-hud-api](06_player/player-skill-hud-api.md)
- [player-skill-review](06_player/player-skill-review.md)
- [player-ui-api](06_player/player-ui-api.md)
- [player-weapon-api](06_player/player-weapon-api.md)
- [player-weapon-appearance-api](06_player/player-weapon-appearance-api.md)

## 터렛

- [api-reference](07_turrets/api-reference.md)
- [index](07_turrets/index.md)
- [initialization](07_turrets/initialization.md)
- [quick-start](07_turrets/quick-start.md)
- [snapshots](07_turrets/snapshots.md)
- [testing](07_turrets/testing.md)
- [turret-owner-time-initialization](07_turrets/turret-owner-time-initialization.md)
- [turret-system-guide](07_turrets/turret-system-guide.md)

## Enemy와 Encounter

- [encounter-planning-guide](08_encounter/encounter-planning-guide.md)
- [encounter-spawn-origin-api](08_encounter/encounter-spawn-origin-api.md)
- [enemy-system-guide](08_encounter/enemy-system-guide.md)
- [spatial-spawn-distribution](08_encounter/spatial-spawn-distribution.md)
- [stage1-enemy-content-guide](08_encounter/stage1-enemy-content-guide.md)

## 월드 시간과 Weather

- [day-night-api](09_world/day-night-api.md)
- [weather-debug-hud](09_world/weather-debug-hud.md)
- [weather-system-guide](09_world/weather-system-guide.md)
