# 한글 픽셀 폰트 설정

Unity의 TMP 컴포넌트에서 영문과 한글을 함께 표시할 때 `Assets/Art/Fonts`의 Gravity SDF를 사용한다. 한글 글리프는 기본 폰트의 Fallback Font Asset Table을 통해 찾는다.

| TMP 기본 폰트 | 한글 폴백 |
| --- | --- |
| `GravityBold8 SDF` | `Mulmaru SDF` |
| `GravityRegular5 SDF` | `ThinMulmaru SDF` |

두 Gravity SDF는 Static이며 물마루 SDF는 Dynamic이다. 화면에서 처음 사용한 한글은 연결된 TTF에서 동적 아틀라스로 추가된다. Unity에서 한글 UI를 열면 SDF 캐시가 직렬화되어 변경 목록에 나타날 수 있다. 폰트가 바뀐 것처럼 보이면 TMP 컴포넌트의 기본 Font Asset과 Fallback Font Asset Table을 먼저 확인하고, 글리프·아틀라스 저장 차이와 구분해 검토한다.

- [GravityBold8 설정](../../Assets/Art/Fonts/GravityBold8%20SDF.asset)
- [GravityRegular5 설정](../../Assets/Art/Fonts/GravityRegular5%20SDF.asset)
공용 전투 HUD의 [CombatHudBodySDF](../../Assets/Art/UI/HudV6/CombatHudBodySDF.asset)도 Dynamic이다. 소스 폰트·폴백 참조가 그대로여도 TMP가 글리프 간격 보정 정보를 저장하면 Glyph Pair Adjustment Records가 바뀔 수 있다. 물마루 폴백의 캐시를 비운 경우에는 한글 문구를 다시 표시할 때 글리프와 아틀라스가 채워진다.

- 물마루 원본과 OFL은 [폰트 에셋 폴더](../../Assets/Art/Fonts/)에 함께 있다.
