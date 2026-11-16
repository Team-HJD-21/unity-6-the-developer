# 한글 픽셀 폰트 설정

Unity의 TMP 컴포넌트에서 영문과 한글을 함께 표시할 때 `Assets/Art/Fonts`의 Gravity SDF를 사용한다. 한글 글리프는 기본 폰트의 Fallback Font Asset Table을 통해 찾는다.

| TMP 기본 폰트 | 한글 폴백 |
| --- | --- |
| `GravityBold8 SDF` | `Mulmaru SDF` |
| `GravityRegular5 SDF` | `ThinMulmaru SDF` |

두 Gravity SDF와 물마루 폴백은 Static이다. 게임 UI의 한글과 기호는 기존 TTF에서 미리 구운 아틀라스를 사용한다. 폰트가 다르게 보이면 TMP 컴포넌트의 기본 Font Asset과 Fallback Font Asset Table을 확인한다.

- [GravityBold8 설정](../../Assets/Art/Fonts/GravityBold8%20SDF.asset)
- [GravityRegular5 설정](../../Assets/Art/Fonts/GravityRegular5%20SDF.asset)
공용 UI의 [CombatHudBodySDF](../../Assets/Art/UI/HudV6/CombatHudBodySDF.asset)와 TMP 기본 영문 폰트의 LiberationSans SDF 폴백도 Static이다. 네 에셋을 다시 구울 때는 현재 글자와 새 UI 문구의 글자를 합쳐 기존 원본 폰트로 생성하고, 에셋의 아틀라스·글리프 테이블을 함께 저장한다. 기존 Font Asset의 참조와 Fallback Font Asset Table을 유지한다.

- 물마루 원본과 OFL은 [폰트 에셋 폴더](../../Assets/Art/Fonts/)에 함께 있다.
