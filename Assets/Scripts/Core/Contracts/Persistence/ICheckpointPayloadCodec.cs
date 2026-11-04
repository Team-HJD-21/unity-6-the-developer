// Feature가 자신의 schema를 명시적으로 기록/해석하는 제한된 바이너리 payload 경계입니다.
using System;
using System.IO;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface ICheckpointPayloadCodec
    {
        /// <summary>메인 스레드의 Feature 캡처가 호출합니다. writer는 callback 안에서만 사용합니다.</summary>
        CheckpointSection Write(string key, int version, CheckpointSectionKind kind, Action<BinaryWriter> write);
        /// <summary>복원 준비 단계에서 schema 버전·범위·후행 자료를 확인하며 읽습니다. 실제 게임 상태는 변경하지 않습니다.</summary>
        T Read<T>(CheckpointSection section, int expectedVersion, Func<BinaryReader, T> read);
        void WriteText(BinaryWriter writer, string text, int maximumBytes = 1024);
        string ReadText(BinaryReader reader, int maximumBytes = 1024);
        int ReadCount(BinaryReader reader, int maximum);
    }
}
