using System.Collections.Generic;

/// <summary>UI 필수 참조 검사에서 누락된 Inspector 필드 이름을 수집합니다.</summary>
internal static class StageUIReferenceValidation
{
    public static string Missing(params (string Name, bool IsAssigned)[] fields)
    {
        var missing = new List<string>();
        foreach (var field in fields)
            if (!field.IsAssigned) missing.Add(field.Name);
        return string.Join(", ", missing);
    }
}
