using System.Collections;
using UnityEngine;

/// <summary>
/// Monster 클래스를 수정하지 않고 외부에서 이동 속도를 깎아주는 무종속 디버프 컴포넌트
/// </summary>
public class MonsterSlowDebuff : MonoBehaviour
{
    private Monster targetMonster;
    private float originalSpeed;
    private Coroutine slowRoutine;

    /// <summary>
    /// 감속 적용 (slowRatio: 0.5f이면 50% 감속, duration: 지속시간)
    /// </summary>
    public void ApplySlow(Monster monster, float slowRatio, float duration)
    {
        if (monster == null) return;
        targetMonster = monster;

        // 처음 붙은 경우 원래 속도를 기록
        if (slowRoutine == null)
        {
            originalSpeed = monster.moveSpeed;
        }
        else
        {
            // 이미 돌고 있다면 타이머 갱신을 위해 기존 코루틴 중단
            StopCoroutine(slowRoutine);
        }

        slowRoutine = StartCoroutine(SlowRoutine(slowRatio, duration));
    }

    private IEnumerator SlowRoutine(float slowRatio, float duration)
    {
        // 몬스터의 이동속도 감속 적용
        targetMonster.moveSpeed = originalSpeed * Mathf.Clamp01(1f - slowRatio);

        // 시각 효과: 몬스터 스프라이트를 푸른색(얼음)으로 틴트
        SpriteRenderer sr = targetMonster.GetComponent<SpriteRenderer>();
        Color originalColor = Color.white;
        if (sr != null)
        {
            originalColor = sr.color;
            sr.color = new Color(0.4f, 0.8f, 1f, 1f); // 하늘색 틴트
        }

        yield return new WaitForSeconds(duration);

        // 복원 처리 (몬스터가 아직 살아있을 때만)
        if (targetMonster != null && !targetMonster.isDead)
        {
            targetMonster.moveSpeed = originalSpeed;
        }

        if (sr != null)
        {
            sr.color = originalColor;
        }

        Destroy(this);
    }

    private void OnDisable()
    {
        // 오브젝트 풀링 등으로 갑자기 비활성화되거나 사망 시 속도 원복 방어
        if (targetMonster != null && originalSpeed > 0f)
        {
            targetMonster.moveSpeed = originalSpeed;
        }
    }
}