// CU 체력의 읽기 전용 Port입니다. 생존 원본이나 피해 규칙을 Core에 복제하지 않습니다.
namespace TeamHJD.Game.Contracts
{
    public interface IControlUnitVitals
    {
        int CurrentHealth { get; }
        int MaxHealth { get; }
    }
}
