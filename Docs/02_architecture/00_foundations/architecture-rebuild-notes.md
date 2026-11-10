# 레거시 아키텍처 이식 기록 — 보관 안내

[이전 제안 원문](../../99_archive/2026-11-05/architecture/architecture-rebuild-notes.md)은 과거 이식 목표다. 현행 상태 원본·수명 책임은 [Match 책임](../02_match/match-management-responsibilities.md), 호출은 [Core API 지도](../01_core/core-lifecycle-and-api-guide.md)를 따른다. Feature HP 원본을 별도 Core writer로 복제하지 않는다.
